using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Portfolio.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<SiteImage> SiteImages => Set<SiteImage>();

    /// <summary>Keys that protect cookies and anti-forgery tokens, shared across restarts and deploys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ContactMessage>(e =>
        {
            e.ToTable("contact_messages");
            e.Property(m => m.Name).HasMaxLength(ContactLimits.NameMax).IsRequired();
            e.Property(m => m.Email).HasMaxLength(ContactLimits.EmailMax).IsRequired();
            e.Property(m => m.Body).HasMaxLength(ContactLimits.MessageMax).IsRequired();
            e.HasIndex(m => m.CreatedAtUtc);
        });

        builder.Entity<Certificate>(e =>
        {
            e.ToTable("certificates", t =>
            {
                // A certificate links to either a shipped PDF or an uploaded one, never neither.
                t.HasCheckConstraint("ck_certificates_has_file",
                    "\"StaticPath\" IS NOT NULL OR \"FileData\" IS NOT NULL OR \"CredentialUrl\" IS NOT NULL");
            });
            // Seeded rows use ids 1-3, so new certificates start numbering at 100 to avoid colliding with them.
            e.Property(c => c.Id).HasIdentityOptions(startValue: 100);
            e.Property(c => c.Code).HasMaxLength(20);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Issuer).HasMaxLength(120).IsRequired();
            e.Property(c => c.Category).HasMaxLength(80);
            e.Property(c => c.CredentialId).HasMaxLength(80);
            e.Property(c => c.CredentialUrl).HasMaxLength(500);
            e.Property(c => c.StaticPath).HasMaxLength(200);
            e.Property(c => c.FileName).HasMaxLength(200);
            e.HasIndex(c => new { c.IsPublished, c.SortOrder });
            e.HasData(SeedData.Certificates);
        });

        builder.Entity<SiteImage>(e =>
        {
            e.ToTable("site_images");
            e.HasKey(i => i.Key);
            e.Property(i => i.Key).HasMaxLength(50);
            e.Property(i => i.ContentType).HasMaxLength(50).IsRequired();
        });
    }
}

public static class ContactLimits
{
    public const int NameMax = 100;
    public const int EmailMax = 254;
    public const int MessageMin = 10;
    public const int MessageMax = 2000;
}
