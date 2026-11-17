namespace Portfolio.Web.Data;

/// <summary>
/// The certificates the site starts with. Used to seed the database (via a migration, so deleting one
/// in the admin area doesn't bring it back) and as a fallback list if the database is unreachable.
/// </summary>
public static class SeedData
{
    private static readonly DateTime Seeded = new(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Certificate[] Certificates =
    [
        new()
        {
            Id = 1, Code = "LFS101", Title = "Introduction to Linux", Issuer = "The Linux Foundation", Category = "Linux",
            IssuedOn = new DateOnly(2026, 7, 19), CredentialId = "LF-2q3z5p809t",
            StaticPath = "/certificates/lfs101-introduction-to-linux.pdf",
            SortOrder = 1, IsPublished = true, CreatedAtUtc = Seeded, UpdatedAtUtc = Seeded,
        },
        new()
        {
            Id = 2, Code = "LFS158", Title = "Introduction to Kubernetes", Issuer = "The Linux Foundation", Category = "Cloud & containers",
            IssuedOn = new DateOnly(2026, 7, 19), CredentialId = "LF-mhek1gi7nf",
            StaticPath = "/certificates/lfs158-introduction-to-kubernetes.pdf",
            SortOrder = 2, IsPublished = true, CreatedAtUtc = Seeded, UpdatedAtUtc = Seeded,
        },
        new()
        {
            Id = 3, Code = "LFS250", Title = "Kubernetes and Cloud Native Essentials", Issuer = "The Linux Foundation", Category = "Cloud & containers",
            IssuedOn = new DateOnly(2026, 7, 19), CredentialId = "LF-x65a0c6kg3",
            StaticPath = "/certificates/lfs250-kubernetes-cloud-native-essentials.pdf",
            SortOrder = 3, IsPublished = true, CreatedAtUtc = Seeded, UpdatedAtUtc = Seeded,
        },
    ];
}
