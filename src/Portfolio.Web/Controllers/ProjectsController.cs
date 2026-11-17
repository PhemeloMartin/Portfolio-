using Microsoft.AspNetCore.Mvc;
using Portfolio.Web.Content;
using Portfolio.Web.Models;

namespace Portfolio.Web.Controllers;

[Route("projects")]
public sealed class ProjectsController(PortfolioContent content) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectPermanent("/#work");

    [HttpGet("{slug}")]
    public IActionResult Details(string slug)
    {
        var project = content.FindProject(slug);
        if (project is null || project.CaseStudy.Count == 0)
            return NotFound();

        var withCaseStudies = content.Projects.Where(p => p.CaseStudy.Count > 0).ToList();
        var i = withCaseStudies.IndexOf(project);

        return View(new ProjectViewModel
        {
            Project = project,
            Previous = i > 0 ? withCaseStudies[i - 1] : null,
            Next = i < withCaseStudies.Count - 1 ? withCaseStudies[i + 1] : null,
        });
    }
}
