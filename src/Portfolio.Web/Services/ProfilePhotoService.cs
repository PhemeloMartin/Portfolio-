using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

/// <summary>The profile photo uploaded in the admin area, cached in memory. Null means "use the built-in photo".</summary>
public sealed class ProfilePhotoService(AppDbContext db, IMemoryCache cache, ILogger<ProfilePhotoService> logger)
{
    private const string CacheKey = "profile-photo";
    private sealed record Cached(SiteImage? Photo);

    public async Task<SiteImage?> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out Cached? cached) && cached is not null)
            return cached.Photo;

        try
        {
            var photo = await db.SiteImages.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Key == SiteImage.ProfilePhotoKey, ct);
            cache.Set(CacheKey, new Cached(photo), TimeSpan.FromMinutes(10));
            return photo;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load the profile photo; using the built-in one");
            cache.Set(CacheKey, new Cached(null), TimeSpan.FromMinutes(1));
            return null;
        }
    }

    public async Task SaveAsync(ProcessedImage image, CancellationToken ct = default)
    {
        var existing = await db.SiteImages.FirstOrDefaultAsync(i => i.Key == SiteImage.ProfilePhotoKey, ct);
        if (existing is null)
        {
            db.SiteImages.Add(new SiteImage
            {
                Key = SiteImage.ProfilePhotoKey, ContentType = image.ContentType, Data = image.Data,
                Width = image.Width, Height = image.Height, UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.ContentType = image.ContentType;
            existing.Data = image.Data;
            existing.Width = image.Width;
            existing.Height = image.Height;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.SiteImages.Where(i => i.Key == SiteImage.ProfilePhotoKey).ExecuteDeleteAsync(ct);
        cache.Remove(CacheKey);
    }
}
