using System.ComponentModel.DataAnnotations;
using Portfolio.Web.Data;

namespace Portfolio.Web.Areas.Admin.Models;

public sealed class LoginModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorModel
{
    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
    [RegularExpression(@"^\s*\d{3}[\s-]?\d{3}\s*$", ErrorMessage = "The code is 6 digits.")]
    public string? Code { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class RecoveryCodeModel
{
    [Required(ErrorMessage = "Enter one of your recovery codes.")]
    [StringLength(20)]
    public string? Code { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class SetupAuthenticatorModel
{
    public string SharedKey { get; set; } = "";
    public string QrCodeDataUri { get; set; } = "";

    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
    [RegularExpression(@"^\s*\d{3}[\s-]?\d{3}\s*$", ErrorMessage = "The code is 6 digits.")]
    public string? Code { get; set; }
}

public sealed class DashboardModel
{
    public int UnreadMessages { get; init; }
    public int TotalMessages { get; init; }
    public int Certificates { get; init; }
    public int PublishedCertificates { get; init; }
    public bool HasCustomPhoto { get; init; }
    public int RetentionDays { get; init; }
}

public sealed class MessageListModel
{
    public required IReadOnlyList<ContactMessage> Messages { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int RetentionDays { get; init; }
}

public sealed class CertificateFormModel
{
    public int? Id { get; set; }

    [StringLength(20)]
    [Display(Name = "Course code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Enter the certificate title.")]
    [StringLength(200)]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Enter who issued it.")]
    [StringLength(120)]
    public string? Issuer { get; set; }

    [StringLength(80)]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Enter the date it was issued.")]
    [Display(Name = "Issued on")]
    public DateOnly? IssuedOn { get; set; }

    [StringLength(80)]
    [Display(Name = "Credential ID")]
    public string? CredentialId { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "Enter a full link starting with https://")]
    [Display(Name = "Verification link")]
    public string? CredentialUrl { get; set; }

    [Range(0, 999)]
    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Show on the site")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "Certificate PDF")]
    public IFormFile? File { get; set; }

    [Display(Name = "Remove the uploaded PDF")]
    public bool RemoveFile { get; set; }

    // Shown on the edit page, not posted back.
    public string? StaticPath { get; set; }
    public string? UploadedFileName { get; set; }
}

public sealed class PhotoModel
{
    public bool HasCustomPhoto { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
