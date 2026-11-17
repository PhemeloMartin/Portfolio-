using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

public sealed record CertificateView(
    int Id, string? Code, string Title, string Issuer, string? Category,
    DateOnly IssuedOn, string? CredentialId, string? Url);

/// <summary>
/// Reads published certificates for the public site. Results are cached briefly; if the database is
/// unreachable the last good list (or the built-in seed list) is shown instead of an error.
/// </summary>
public sealed class CertificateService(AppDbContext db, IMemoryCache cache, ILogger<CertificateService> logger)
{
    private const string CacheKey = "certificates:published";
    private static volatile IReadOnlyList<CertificateView>? _lastKnownGood;

    public async Task<IReadOnlyList<CertificateView>> GetPublishedAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<CertificateView>? cached) && cached is not null)
            return cached;

        try
        {
            var rows = await db.Certificates.AsNoTracking()
                .Where(c => c.IsPublished)
                .OrderBy(c => c.SortOrder).ThenByDescending(c => c.IssuedOn)
                .Select(c => new
                {
                    c.Id, c.Code, c.Title, c.Issuer, c.Category, c.IssuedOn, c.CredentialId,
                    c.StaticPath, c.CredentialUrl, HasUpload = c.FileData != null,
                })
                .ToListAsync(ct);

            IReadOnlyList<CertificateView> views = rows
                .Select(c => new CertificateView(c.Id, c.Code, c.Title, c.Issuer, c.Category, c.IssuedOn, c.CredentialId,
                    c.HasUpload ? $"/media/certificates/{c.Id}" : c.StaticPath ?? c.CredentialUrl))
                .ToList();

            cache.Set(CacheKey, views, TimeSpan.FromMinutes(5));
            _lastKnownGood = views;
            return views;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load certificates from the database; showing the fallback list.");
            var fallback = _lastKnownGood ?? SeedData.Certificates
                .Where(c => c.IsPublished)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CertificateView(c.Id, c.Code, c.Title, c.Issuer, c.Category, c.IssuedOn, c.CredentialId, c.StaticPath))
                .ToList();

            // Don't retry a down database on every request; try again in a minute.
            cache.Set(CacheKey, fallback, TimeSpan.FromMinutes(1));
            return fallback;
        }
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
