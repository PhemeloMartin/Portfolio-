namespace Portfolio.Web.Infrastructure;

public static class Formatting
{
    // South Africa is UTC+2 all year (no daylight saving), so a fixed offset is exact and needs no time zone data.
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    public static string ToSast(this DateTime utc) => $"{utc.Add(Sast):d MMM yyyy, HH:mm}";

    public static string Preview(this string text, int length = 90)
    {
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= length ? flat : flat[..length].TrimEnd() + "…";
    }
}
