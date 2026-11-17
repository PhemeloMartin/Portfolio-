using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Web.Content;

namespace Portfolio.Web.Controllers;

/// <summary>sitemap.xml and robots.txt, built from the content file so they never go stale.</summary>
public sealed class SeoController(PortfolioContent content, IConfiguration config) : Controller
{
    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Sitemap()
    {
        var baseUrl = BaseUrl();
        var paths = new List<string> { "/" };
        paths.AddRange(content.Projects.Where(p => p.CaseStudy.Count > 0).Select(p => $"/projects/{p.Slug}"));
        paths.Add("/privacy");

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset", paths.Select(p => new XElement(ns + "url", new XElement(ns + "loc", baseUrl + p)))));

        return Content(doc.Declaration + "\n" + doc.Root, "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Robots() =>
        Content($"User-agent: *\nDisallow: /admin\nSitemap: {BaseUrl()}/sitemap.xml\n", "text/plain", Encoding.UTF8);

    private string BaseUrl() =>
        (string.IsNullOrWhiteSpace(config["Site:BaseUrl"]) ? $"{Request.Scheme}://{Request.Host}" : config["Site:BaseUrl"]!)
        .TrimEnd('/');
}
