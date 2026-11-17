using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Web.Content;
using Portfolio.Web.Data;
using Portfolio.Web.Infrastructure;
using Portfolio.Web.Models;

namespace Portfolio.Web.Controllers;

[Route("contact")]
public sealed class ContactController(
    PortfolioContent content,
    AppDbContext db,
    IAntiforgery antiforgery,
    ILogger<ContactController> logger) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(ContactFormViewModel.Create(HttpContext, antiforgery, logger, content));

    [HttpPost("")]
    [EnableRateLimiting(RateLimits.Contact)]
    public async Task<IActionResult> Send(ContactFormModel form, CancellationToken ct)
    {
        // Spam trap filled in: pretend it worked so bots learn nothing, but store nothing.
        if (!string.IsNullOrEmpty(form.Website))
        {
            logger.LogInformation("Contact form spam trap triggered; message discarded");
            return RedirectToAction(nameof(Sent));
        }

        // The server always re-validates; the browser checks are only for quick feedback.
        form.Normalise();
        ModelState.Clear();
        if (!TryValidateModel(form))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Index), ContactFormViewModel.Create(HttpContext, antiforgery, logger, content, form));
        }

        try
        {
            db.ContactMessages.Add(new ContactMessage
            {
                Name = form.Name!,
                Email = form.Email!,
                Body = form.Message!,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not save a contact message");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            var error = $"Sorry, your message couldn't be sent right now. Please try again later, or email me directly at {content.Contact.Email}.";
            return View(nameof(Index), ContactFormViewModel.Create(HttpContext, antiforgery, logger, content, form, error));
        }

        // Log that a message arrived, never what it says or who sent it.
        logger.LogInformation("Contact message received");
        return RedirectToAction(nameof(Sent));
    }

    [HttpGet("sent")]
    public IActionResult Sent() => View(content);
}
