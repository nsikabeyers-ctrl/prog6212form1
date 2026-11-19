using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Controllers;

public class HomeController : BaseController
{
    /// <summary>
    /// Visitors see the landing page. A logged-in Participant lands on the upcoming events
    /// and a logged-in Organiser lands on their dashboard.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        if (Me is { } user) return HomeFor(user);

        return View();
    }

    // The error pages are shown after a POST too, so they must skip the anti-forgery check.
    [IgnoreAntiforgeryToken]
    public IActionResult Error() => View();

    [IgnoreAntiforgeryToken]
    public IActionResult StatusPage(int code = 500)
    {
        Response.StatusCode = code;
        return View(code);
    }
}
