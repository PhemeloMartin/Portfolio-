using System.Text.Json;
using System.Text.RegularExpressions;

namespace Portfolio.Web.Content;

/// <summary>
/// Everything on the public site that isn't managed from the admin area.
/// Loaded once from Content/portfolio.json at startup and validated, so a broken
/// content file stops the app from starting instead of failing on a live page.
/// </summary>
public sealed class PortfolioContent
{
    public required Profile Profile { get; init; }
    public required List<Project> Projects { get; init; }
    public required List<Experience> Experience { get; init; }
    public required List<SkillGroup> Skills { get; init; }
    public required List<Education> Education { get; init; }
    public required List<Award> Awards { get; init; }
    public required ContactDetails Contact { get; init; }
    public List<string> Community { get; init; } = [];

    public Project? FindProject(string slug) =>
        Projects.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
}

public sealed class Profile
{
    public required string Name { get; init; }
    public required string ShortName { get; init; }
    public required string Kicker { get; init; }
    public required string Lede { get; init; }
    public required string MetaDescription { get; init; }
    public List<string> Areas { get; init; } = [];
    public List<Fact> Facts { get; init; } = [];
    public required string PhotoAlt { get; init; }
    public string? PhotoCaption { get; init; }
}

public sealed class Fact
{
    public required string Label { get; init; }
    public required string Text { get; init; }
    public string? Href { get; init; }
}

public sealed class Project
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string Year { get; init; }
    public required string Kind { get; init; }
    public List<string> Tags { get; init; } = [];
    public bool IsLive { get; init; }

    /// <summary>"media" (screenshot beside intro), "split" (photo beside text) or "row" (compact list row).</summary>
    public string Layout { get; init; } = "row";

    public required string Summary { get; init; }
    public List<string> Stack { get; init; } = [];
    public List<ProjectLink> Links { get; init; } = [];
    public List<Highlight> Highlights { get; init; } = [];
    public ProjectImage? Image { get; init; }
    public List<CaseStudySection> CaseStudy { get; init; } = [];

    public string Meta => string.Join(" · ", new[] { Year, Kind }.Concat(Tags));
}

public sealed class ProjectLink
{
    public required string Label { get; init; }
    public required string Url { get; init; }
}

public sealed class Highlight
{
    public required string Title { get; init; }
    public required string Text { get; init; }
}

public sealed class ProjectImage
{
    public required string Src { get; init; }
    public required string Alt { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Caption { get; init; }
    public string? BrowserUrl { get; init; }
}

public sealed class CaseStudySection
{
    public required string Heading { get; init; }
    public List<string> Paragraphs { get; init; } = [];
    public List<string> Bullets { get; init; } = [];
}

public sealed class Experience
{
    public required string Period { get; init; }
    public required string Role { get; init; }
    public required string Organisation { get; init; }
    public string? Summary { get; init; }
    public List<string> Bullets { get; init; } = [];
}

public sealed class SkillGroup
{
    public required string Name { get; init; }
    public List<string> Items { get; init; } = [];
}

public sealed class Education
{
    public required string Period { get; init; }
    public required string Title { get; init; }
    public string? Institution { get; init; }
    public string? Description { get; init; }
}

public sealed class Award
{
    public required string Year { get; init; }
    public required string Text { get; init; }
    public string? Emphasis { get; init; }
}

public sealed class ContactDetails
{
    public required string Lead { get; init; }
    public required string Email { get; init; }
    public required string LinkedInUrl { get; init; }
    public required string LinkedInLabel { get; init; }
    public required string GitHubUrl { get; init; }
    public required string GitHubLabel { get; init; }
}

public static partial class PortfolioContentLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    public static PortfolioContent Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The site content file is missing.", path);

        PortfolioContent content;
        try
        {
            content = JsonSerializer.Deserialize<PortfolioContent>(File.ReadAllText(path), Options)
                      ?? throw new InvalidDataException("The site content file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The site content file is not valid JSON: {ex.Message}", ex);
        }

        Validate(content);
        return content;
    }

    private static void Validate(PortfolioContent content)
    {
        var problems = new List<string>();

        foreach (var p in content.Projects)
        {
            if (!SlugPattern().IsMatch(p.Slug))
                problems.Add($"Project '{p.Title}' has an invalid slug '{p.Slug}' (use lowercase letters, digits and hyphens).");
            if (p.Layout is not ("media" or "split" or "row"))
                problems.Add($"Project '{p.Slug}' has an unknown layout '{p.Layout}'.");
            if (p.Layout != "row" && p.Image is null)
                problems.Add($"Project '{p.Slug}' uses the '{p.Layout}' layout but has no image.");
            foreach (var link in p.Links)
                if (!IsSafeUrl(link.Url))
                    problems.Add($"Project '{p.Slug}' has an unsafe link '{link.Url}'.");
        }

        var duplicateSlugs = content.Projects.GroupBy(p => p.Slug, StringComparer.OrdinalIgnoreCase)
                                             .Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var slug in duplicateSlugs)
            problems.Add($"More than one project uses the slug '{slug}'.");

        if (!IsSafeUrl(content.Contact.LinkedInUrl) || !IsSafeUrl(content.Contact.GitHubUrl))
            problems.Add("Contact links must be https:// URLs.");

        if (problems.Count > 0)
            throw new InvalidDataException("The site content file has problems:\n- " + string.Join("\n- ", problems));
    }

    // Only https:// links or site-relative paths, so content can never inject javascript: URLs.
    private static bool IsSafeUrl(string url) =>
        url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)
        || Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}
