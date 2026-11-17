using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Web.Content;
using Portfolio.Web.Data;
using Portfolio.Web.Infrastructure;
using Portfolio.Web.Services;
using Serilog;
using Serilog.Events;

// Log startup problems even before configuration is loaded.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var isDev = builder.Environment.IsDevelopment();

    // ---- Logging -------------------------------------------------------------------------------
    builder.Services.AddSerilog((services, log) => log
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.WebHost.ConfigureKestrel(o =>
    {
        o.AddServerHeader = false;
        o.Limits.MaxRequestBodySize = UploadLimits.RequestMaxBytes;
    });

    // ---- Content and data ----------------------------------------------------------------------
    builder.Services.AddSingleton(PortfolioContentLoader.Load(
        Path.Combine(builder.Environment.ContentRootPath, "Content", "portfolio.json")));

    var connectionString = DatabaseConfig.BuildConnectionString(builder.Configuration.GetConnectionString("Portfolio"));
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(15)));

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<CertificateService>();
    builder.Services.AddScoped<ProfilePhotoService>();
    builder.Services.AddHostedService<MessageRetentionService>();

    // Keys that protect cookies and anti-forgery tokens are kept in the database, so sign-ins and
    // forms keep working across restarts and deploys (containers have no persistent disk).
    builder.Services.AddDataProtection()
        .SetApplicationName("Portfolio")
        .PersistKeysToDbContext<AppDbContext>();

    // ---- Admin sign-in -------------------------------------------------------------------------
    builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.AllowedForNewUsers = true;
            o.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.Cookie.Name = isDev ? "pm-admin" : "__Host-pm-admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.LoginPath = "/admin/account/login";
        o.LogoutPath = "/admin/account/logout";
        // Signed in with a password but no second factor yet: send them to set up the authenticator.
        o.AccessDeniedPath = "/admin/account/setup-authenticator";
    });

    // Admin pages need a full two-factor sign-in ("amr" = "mfa"), not just a password.
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AdminPolicy.Name, p => p.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));

    // ---- Web -----------------------------------------------------------------------------------
    builder.Services.AddRouting(o => o.LowercaseUrls = true);

    // Output characters like "·", "–" and "ë" as-is instead of as &#x..; codes. HTML-special
    // characters (< > & " ') are still always encoded, so this doesn't weaken XSS protection.
    builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
        o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
    builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

    builder.Services.AddAntiforgery(o =>
    {
        o.Cookie.Name = isDev ? "pm-af" : "__Host-pm-af";
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.FormFieldName = "__rvt";
    });

    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(RateLimits.Contact, ctx => RateLimitPartition.GetFixedWindowLimiter(RateLimits.ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15) }));
        o.AddPolicy(RateLimits.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(RateLimits.ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) }));
    });

    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(365);
        o.IncludeSubDomains = true;
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

    // The host (Render) terminates HTTPS at its proxy and forwards plain HTTP to the app. Read the
    // proxy's X-Forwarded-* headers so the app sees the visitor's real IP (for rate limiting) and
    // knows the request was HTTPS (for HSTS and canonical links). The proxy's addresses aren't fixed,
    // so any proxy is trusted; the container is only reachable through it. Render appends to
    // X-Forwarded-For and documents the first entry as the client, so the whole chain is read.
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = null;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });

    // ---- Pipeline ------------------------------------------------------------------------------
    var app = builder.Build();

    await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

    app.UseForwardedHeaders();
    app.UseSecurityHeaders(isDev);

    if (!isDev)
    {
        // Visitors see a friendly page with a reference code; the details go to the logs only.
        app.UseExceptionHandler("/error");
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseStatusCodePagesWithReExecute("/error/{0}");

    app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
        ex is not null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
        : ctx.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : LogEventLevel.Information);

    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    // Liveness (is the app up?) and readiness (can it reach the database?) for the host to poll.
    app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

    app.MapAreaControllerRoute("admin", "Admin", "admin/{controller=Dashboard}/{action=Index}/{id?}")
        .WithStaticAssets();
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The site failed to start");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Make the implicit Program class visible to the integration tests.
public partial class Program;
