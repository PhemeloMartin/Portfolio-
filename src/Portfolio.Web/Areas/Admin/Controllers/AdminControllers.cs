using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Web.Areas.Admin.Models;
using Portfolio.Web.Data;
using Portfolio.Web.Infrastructure;
using Portfolio.Web.Services;

namespace Portfolio.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public abstract class AdminControllerBase : Controller
{
    protected void Flash(string message) => TempData["Flash"] = message;
    protected void FlashError(string message) => TempData["FlashError"] = message;
}

[Route("admin")]
public sealed class DashboardController(AppDbContext db, ProfilePhotoService photos, IConfiguration config) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new DashboardModel
    {
        UnreadMessages = await db.ContactMessages.CountAsync(m => !m.IsRead, ct),
        TotalMessages = await db.ContactMessages.CountAsync(ct),
        Certificates = await db.Certificates.CountAsync(ct),
        PublishedCertificates = await db.Certificates.CountAsync(c => c.IsPublished, ct),
        HasCustomPhoto = await photos.GetAsync(ct) is not null,
        RetentionDays = config.GetValue("Privacy:MessageRetentionDays", 180),
    });
}

[Route("admin/messages")]
public sealed class MessagesController(AppDbContext db, IConfiguration config) : AdminControllerBase
{
    private const int PageSize = 25;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var total = await db.ContactMessages.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var messages = await db.ContactMessages.AsNoTracking()
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(ct);

        return View(new MessageListModel
        {
            Messages = messages, Page = page, TotalPages = totalPages,
            RetentionDays = config.GetValue("Privacy:MessageRetentionDays", 180),
        });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return NotFound();

        if (!message.IsRead)
        {
            message.IsRead = true;
            await db.SaveChangesAsync(ct);
        }
        return View(message);
    }

    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var deleted = await db.ContactMessages.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
        if (deleted > 0) Flash("Message deleted.");
        return RedirectToAction(nameof(Index));
    }
}

[Route("admin/certificates")]
public sealed class CertificatesController(
    AppDbContext db, CertificateService certificates, ILogger<CertificatesController> logger) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await db.Certificates.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenByDescending(c => c.IssuedOn)
            .Select(c => new Certificate
            {
                Id = c.Id, Code = c.Code, Title = c.Title, Issuer = c.Issuer, IssuedOn = c.IssuedOn,
                IsPublished = c.IsPublished, SortOrder = c.SortOrder, StaticPath = c.StaticPath,
                FileName = c.FileName, CredentialUrl = c.CredentialUrl,
            })
            .ToListAsync(ct);
        return View(list);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var nextOrder = (await db.Certificates.MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 1;
        return View("Edit", new CertificateFormModel { SortOrder = nextOrder });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return NotFound();

        return View(new CertificateFormModel
        {
            Id = c.Id, Code = c.Code, Title = c.Title, Issuer = c.Issuer, Category = c.Category,
            IssuedOn = c.IssuedOn, CredentialId = c.CredentialId, CredentialUrl = c.CredentialUrl,
            SortOrder = c.SortOrder, IsPublished = c.IsPublished, StaticPath = c.StaticPath, UploadedFileName = c.FileName,
        });
    }

    [HttpPost("save")]
    [RequestSizeLimit(UploadLimits.RequestMaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestMaxBytes)]
    public async Task<IActionResult> Save(CertificateFormModel form, CancellationToken ct)
    {
        Certificate? entity = null;
        if (form.Id is int id)
        {
            entity = await db.Certificates.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (entity is null)
                return NotFound();
            form.StaticPath = entity.StaticPath;
            form.UploadedFileName = entity.FileName;
        }

        if (!string.IsNullOrWhiteSpace(form.CredentialUrl) &&
            !(Uri.TryCreate(form.CredentialUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
            ModelState.AddModelError(nameof(form.CredentialUrl), "Enter a full link starting with https://");

        byte[]? pdf = null;
        if (form.File is { Length: > 0 })
        {
            try
            {
                pdf = await UploadReader.ReadAsync(form.File, UploadLimits.PdfMaxBytes, ct);
                PdfValidator.Validate(pdf);
            }
            catch (InvalidUploadException ex)
            {
                ModelState.AddModelError(nameof(form.File), ex.Message);
            }
        }

        var willHaveFile = pdf is not null
            || (entity?.FileData is not null && !form.RemoveFile)
            || !string.IsNullOrEmpty(entity?.StaticPath);
        if (!willHaveFile && string.IsNullOrWhiteSpace(form.CredentialUrl))
            ModelState.AddModelError(nameof(form.File), "Upload the certificate PDF or add a verification link.");

        if (!ModelState.IsValid)
            return View("Edit", form);

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new Certificate { Title = form.Title!, Issuer = form.Issuer!, CreatedAtUtc = now };
            db.Certificates.Add(entity);
        }

        entity.Code = Blank(form.Code);
        entity.Title = form.Title!.Trim();
        entity.Issuer = form.Issuer!.Trim();
        entity.Category = Blank(form.Category);
        entity.IssuedOn = form.IssuedOn!.Value;
        entity.CredentialId = Blank(form.CredentialId);
        entity.CredentialUrl = Blank(form.CredentialUrl);
        entity.SortOrder = form.SortOrder;
        entity.IsPublished = form.IsPublished;
        entity.UpdatedAtUtc = now;

        if (pdf is not null)
        {
            entity.FileData = pdf;
            entity.FileName = SafeFileName(form.File!.FileName);
        }
        else if (form.RemoveFile)
        {
            entity.FileData = null;
            entity.FileName = null;
        }

        await db.SaveChangesAsync(ct);
        certificates.Invalidate();
        logger.LogInformation("Certificate {CertificateId} saved", entity.Id);
        Flash($"Saved \"{entity.Title}\".");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.Certificates.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        if (deleted > 0)
        {
            certificates.Invalidate();
            Flash("Certificate deleted.");
        }
        return RedirectToAction(nameof(Index));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Keep only safe characters so the name can't break the download header or a path.
    private static string SafeFileName(string name)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        var safe = new string(baseName.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_').Take(80).ToArray());
        return (string.IsNullOrEmpty(safe) ? "certificate" : safe) + ".pdf";
    }
}

[Route("admin/photo")]
public sealed class PhotoController(ProfilePhotoService photos, ILogger<PhotoController> logger) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var photo = await photos.GetAsync(ct);
        return View(new PhotoModel
        {
            HasCustomPhoto = photo is not null,
            UpdatedAtUtc = photo?.UpdatedAtUtc,
            Width = photo?.Width ?? 0,
            Height = photo?.Height ?? 0,
        });
    }

    [HttpPost("")]
    [RequestSizeLimit(UploadLimits.RequestMaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestMaxBytes)]
    public async Task<IActionResult> Upload(IFormFile? photo, CancellationToken ct)
    {
        try
        {
            var bytes = await UploadReader.ReadAsync(photo, UploadLimits.ImageMaxBytes, ct);
            var processed = ImageProcessor.ToSafeJpeg(bytes);
            await photos.SaveAsync(processed, ct);
            logger.LogInformation("Profile photo updated ({Width}x{Height})", processed.Width, processed.Height);
            Flash("Photo updated. Location and camera details were removed.");
        }
        catch (InvalidUploadException ex)
        {
            FlashError(ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await photos.ResetAsync(ct);
        Flash("Back to the original graduation photo.");
        return RedirectToAction(nameof(Index));
    }
}
