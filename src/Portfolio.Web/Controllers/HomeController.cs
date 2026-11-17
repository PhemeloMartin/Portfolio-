using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Web.Content;
using Portfolio.Web.Models;
using Portfolio.Web.Services;

namespace Portfolio.Web.Controllers;

public sealed class HomeController(
    PortfolioContent content,
    CertificateService certificates,
    IAntiforgery antiforgery,
    ILogger<HomeController> logger) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new HomeViewModel
    {
        Content = content,
        Certificates = await certificates.GetPublishedAsync(ct),
        ContactForm = ContactFormViewModel.Create(HttpContext, antiforgery, logger, content),
    });

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View(content);
}
