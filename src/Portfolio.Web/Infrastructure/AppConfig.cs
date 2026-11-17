using Npgsql;

namespace Portfolio.Web.Infrastructure;

public static class AdminPolicy
{
    public const string Name = "Admin";
}

public static class RateLimits
{
    public const string Contact = "contact";
    public const string Login = "login";

    /// <summary>Partition by client IP. Behind a proxy this relies on forwarded headers being enabled.</summary>
    public static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public static class DatabaseConfig
{
    private const string LocalDefault = "Host=localhost;Database=portfolio_dev;Username=postgres";

    /// <summary>
    /// Accepts either a keyword connection string or a postgres:// URL (as Neon and Render give you),
    /// and keeps the connect timeout short so a database outage doesn't stall page loads.
    /// </summary>
    public static string BuildConnectionString(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? LocalDefault : configured.Trim();

        var csb = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? FromUrl(value)
            : new NpgsqlConnectionStringBuilder(value);

        if (!value.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            csb.Timeout = 5;

        return csb.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require,
        };
        return csb;
    }
}
