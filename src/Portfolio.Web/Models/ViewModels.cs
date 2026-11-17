using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Portfolio.Web.Content;
using Portfolio.Web.Data;
using Portfolio.Web.Services;

namespace Portfolio.Web.Models;

public sealed class ContactFormModel
{
    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(ContactLimits.NameMax, ErrorMessage = "Keep your name under 100 characters.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter an email address like name@example.com.")]
    [StringLength(ContactLimits.EmailMax, ErrorMessage = "That email address is too long.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter a message.")]
    [StringLength(ContactLimits.MessageMax, MinimumLength = ContactLimits.MessageMin,
        ErrorMessage = "Your message needs between 10 and 2,000 characters.")]
    public string? Message { get; set; }

    /// <summary>Spam trap: hidden from people, but bots tend to fill it in.</summary>
    public string? Website { get; set; }

    public void Normalise()
    {
        Name = Clean(Name, multiline: false);
        Email = Clean(Email, multiline: false);
        Message = Clean(Message, multiline: true);
    }

    // Trim and drop control characters (keeping line breaks in the message).
    private static string? Clean(string? value, bool multiline)
    {
        if (value is null) return null;
        var chars = value.Where(c => !char.IsControl(c) || (multiline && (c == '\n' || c == '\r' || c == '\t')));
        return new string(chars.ToArray()).Trim();
    }
}

/// <summary>Everything the contact form partial needs, including a safely generated anti-forgery token.</summary>
public sealed class ContactFormViewModel
{
    public required ContactFormModel Form { get; init; }
    public required string Email { get; init; }
    public string? TokenFieldName { get; init; }
    public string? Token { get; init; }
    public string? FormError { get; init; }

    /// <summary>False if the security tokens couldn't be created (e.g. keys unavailable); the email is shown instead.</summary>
    public bool IsAvailable => Token is not null;

    public static ContactFormViewModel Create(HttpContext http, IAntiforgery antiforgery, ILogger logger,
        PortfolioContent content, ContactFormModel? form = null, string? formError = null)
    {
        string? field = null, token = null;
        try
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            (field, token) = (tokens.FormFieldName, tokens.RequestToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Anti-forgery tokens are unavailable; showing the email address instead of the contact form");
        }

        return new ContactFormViewModel
        {
            Form = form ?? new ContactFormModel(),
            Email = content.Contact.Email,
            TokenFieldName = field,
            Token = token,
            FormError = formError,
        };
    }
}

public sealed class HomeViewModel
{
    public required PortfolioContent Content { get; init; }
    public required IReadOnlyList<CertificateView> Certificates { get; init; }
    public required ContactFormViewModel ContactForm { get; init; }
}

public sealed class ProjectViewModel
{
    public required Project Project { get; init; }
    public Project? Previous { get; init; }
    public Project? Next { get; init; }
}

public sealed class ErrorViewModel
{
    public int StatusCode { get; init; }
    public string? Reference { get; init; }
}
