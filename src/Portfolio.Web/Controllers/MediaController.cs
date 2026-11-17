using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Portfolio.Web.Data;
using Portfolio.Web.Services;

namespace Portfolio.Web.Controllers;

/// <summary>Serves files managed from the admin area (profile photo, uploaded certificate PDFs).</summary>
[Route("media")]
public sealed class MediaController(
    ProfilePhotoService photos,
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<MediaController> logger) : Controller
{
    [HttpGet("profile-photo")]
    public async Task<IActionResult> ProfilePhoto(CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=300";

        var photo = await photos.GetAsync(ct);
        if (photo is not null)
        {
            var etag = new EntityTagHeaderValue($"\"{photo.UpdatedAtUtc.Ticks:x}\"");
            return File(photo.Data, photo.ContentType, photo.UpdatedAtUtc, etag);
        }

        // No uploaded photo (or the database is down): use the one that ships with the site.
        return PhysicalFile(Path.Combine(env.WebRootPath, "img", "graduation.jpg"), "image/jpeg");
    }

    [HttpGet("certificates/{id:int}")]
    public async Task<IActionResult> Certificate(int id, CancellationToken ct)
    {
        try
        {
            var cert = await db.Certificates.AsNoTracking()
                .Where(c => c.Id == id && c.IsPublished && c.FileData != null)
                .Select(c => new { c.FileData, c.FileName, c.UpdatedAtUtc })
                .FirstOrDefaultAsync(ct);

            if (cert is null)
                return NotFound();

            var disposition = new ContentDispositionHeaderValue("inline");
            disposition.SetHttpFileName(cert.FileName ?? $"certificate-{id}.pdf");
            Response.Headers.ContentDisposition = disposition.ToString();
            Response.Headers.CacheControl = "public, max-age=3600";

            var etag = new EntityTagHeaderValue($"\"{cert.UpdatedAtUtc.Ticks:x}\"");
            return File(cert.FileData!, "application/pdf", cert.UpdatedAtUtc, etag);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load certificate {CertificateId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
