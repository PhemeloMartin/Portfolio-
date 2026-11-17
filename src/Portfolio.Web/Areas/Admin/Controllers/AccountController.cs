using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Web.Areas.Admin.Models;
using Portfolio.Web.Infrastructure;
using QRCoder;

namespace Portfolio.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin sign-in. There is no public registration: the single admin account is created from configuration.
/// Signing in always needs a password and a code from an authenticator app.
/// </summary>
[Area("Admin")]
[Route("admin/account")]
public sealed class AccountController(
    SignInManager<IdentityUser> signIn,
    UserManager<IdentityUser> users,
    ILogger<AccountController> logger) : Controller
{
    private const string Issuer = "Phemelo Mashome Portfolio";
    private const string BadCredentials = "That email and password don't match.";

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.HasClaim("amr", "mfa"))
            return RedirectToDashboard(returnUrl);
        return View(new LoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await users.FindByEmailAsync(model.Email!);
        if (user is null)
        {
            logger.LogWarning("Failed admin sign-in (unknown email)");
            ModelState.AddModelError("", BadCredentials);
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(user, model.Password!, isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
            return RedirectToAction(nameof(LoginWith2fa), new { returnUrl = model.ReturnUrl });

        if (result.Succeeded)
            return RedirectToAction(nameof(SetupAuthenticator)); // Password accepted, but 2FA isn't set up yet.

        if (result.IsLockedOut)
        {
            logger.LogWarning("Admin account locked after repeated failed sign-ins");
            ModelState.AddModelError("", "Too many failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        logger.LogWarning("Failed admin sign-in (wrong password)");
        ModelState.AddModelError("", BadCredentials);
        return View(model);
    }

    [HttpGet("login-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl = null)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));
        return View(new TwoFactorModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login-2fa")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> LoginWith2fa(TwoFactorModel model)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid)
            return View(model);

        var result = await signIn.TwoFactorAuthenticatorSignInAsync(Digits(model.Code), isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            logger.LogInformation("Admin signed in with two-factor authentication");
            return RedirectToDashboard(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Admin account locked after repeated failed two-factor codes");
            ModelState.AddModelError("", "Too many failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        logger.LogWarning("Failed two-factor code");
        ModelState.AddModelError(nameof(model.Code), "That code isn't right. Check your authenticator app and try again.");
        return View(model);
    }

    [HttpGet("login-recovery")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithRecoveryCode(string? returnUrl = null)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));
        return View(new RecoveryCodeModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login-recovery")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> LoginWithRecoveryCode(RecoveryCodeModel model)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid)
            return View(model);

        var result = await signIn.TwoFactorRecoveryCodeSignInAsync(model.Code!.Replace(" ", ""));
        if (result.Succeeded)
        {
            logger.LogWarning("Admin signed in with a recovery code");
            return RedirectToDashboard(model.ReturnUrl);
        }

        logger.LogWarning("Failed recovery code sign-in");
        ModelState.AddModelError(nameof(model.Code), result.IsLockedOut
            ? "Too many failed attempts. Try again in 15 minutes."
            : "That recovery code isn't valid or has already been used.");
        return View(model);
    }

    [HttpGet("setup-authenticator")]
    [Authorize]
    public async Task<IActionResult> SetupAuthenticator()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return await SignOutToLogin();

        // Already set up: a password-only session must sign in again with the second factor.
        if (await users.GetTwoFactorEnabledAsync(user))
            return await SignOutToLogin();

        return View(await BuildSetupModelAsync(user));
    }

    [HttpPost("setup-authenticator")]
    [Authorize]
    public async Task<IActionResult> SetupAuthenticator(SetupAuthenticatorModel model)
    {
        var user = await users.GetUserAsync(User);
        if (user is null || await users.GetTwoFactorEnabledAsync(user))
            return await SignOutToLogin();

        if (!ModelState.IsValid)
            return View(await BuildSetupModelAsync(user));

        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, Digits(model.Code));
        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), "That code isn't right. Make sure your phone's time is correct and try the newest code.");
            return View(await BuildSetupModelAsync(user));
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray() ?? [];
        await signIn.SignOutAsync();
        logger.LogInformation("Admin enabled two-factor authentication");

        return View("RecoveryCodes", recoveryCodes);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return Redirect("/");
    }

    private async Task<SetupAuthenticatorModel> BuildSetupModelAsync(IdentityUser user)
    {
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        var label = Uri.EscapeDataString($"{Issuer}:{user.Email}");
        var uri = $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(6);

        return new SetupAuthenticatorModel
        {
            SharedKey = FormatKey(key!),
            QrCodeDataUri = "data:image/png;base64," + Convert.ToBase64String(png),
        };
    }

    private async Task<IActionResult> SignOutToLogin()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToDashboard(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Dashboard", new { area = "Admin" });

    private static string Digits(string? code) => new((code ?? "").Where(char.IsDigit).ToArray());

    private static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
}
