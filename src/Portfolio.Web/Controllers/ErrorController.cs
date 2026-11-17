using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Web.Models;

namespace Portfolio.Web.Controllers;

/// <summary>
/// Friendly error pages. Visitors get a short explanation and a reference code; the exception
/// details are only written to the logs (tagged with the same request id), never shown on screen.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class ErrorController : Controller
{
    [Route("error")]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", new ErrorViewModel { StatusCode = 500, Reference = HttpContext.TraceIdentifier });
    }

    [Route("error/{code:int}")]
    public IActionResult Status(int code)
    {
        if (code is < 400 or > 599)
            code = StatusCodes.Status404NotFound;

        Response.StatusCode = code;
        return code switch
        {
            404 => View("NotFound"),
            429 => View("TooManyRequests"),
            _ => View("Error", new ErrorViewModel { StatusCode = code, Reference = HttpContext.TraceIdentifier }),
        };
    }
}
