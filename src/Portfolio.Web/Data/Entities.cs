namespace Portfolio.Web.Data;

/// <summary>A message sent through the contact form. Only what's needed to reply is stored (POPIA).</summary>
public class ContactMessage
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Body { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>A certificate shown on the site. Seeded ones point to a static PDF; uploaded ones store the PDF.</summary>
public class Certificate
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public required string Title { get; set; }
    public required string Issuer { get; set; }
    public string? Category { get; set; }
    public DateOnly IssuedOn { get; set; }
    public string? CredentialId { get; set; }
    public string? CredentialUrl { get; set; }

    /// <summary>Site-relative path to a PDF shipped with the site (used by the seeded certificates).</summary>
    public string? StaticPath { get; set; }

    /// <summary>PDF uploaded through the admin area. Validated before it is stored.</summary>
    public byte[]? FileData { get; set; }
    public string? FileName { get; set; }

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>An image managed from the admin area, such as the profile photo. Stored re-encoded, without metadata.</summary>
public class SiteImage
{
    public const string ProfilePhotoKey = "profile-photo";

    public required string Key { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Data { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
