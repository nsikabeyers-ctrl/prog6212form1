using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class BaseController : Controller
{
    /// <summary>The logged-in user from the session, or null.</summary>
    protected CurrentUser? Me => HttpContext.Session.GetCurrentUser();

    protected void Success(string message) => TempData["Success"] = message;

    protected void Fail(string message) => TempData["Error"] = message;

    /// <summary>
    /// When the API says the session has ended (401), clear ours and send the user to log in.
    /// Returns null when the session is fine.
    /// </summary>
    protected IActionResult? SessionExpired(ApiResult result)
    {
        if (!result.IsUnauthorized) return null;

        HttpContext.Session.Clear();
        Fail("Your session has ended. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Copies the API's complaints onto the form so the user sees them next to the fields.</summary>
    protected void AddApiErrors(ApiResult result)
    {
        if (result.ValidationMessages.Count > 0)
        {
            foreach (var message in result.ValidationMessages)
            {
                ModelState.AddModelError(string.Empty, message);
            }
            return;
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "That did not work. Please try again.");
    }

    protected IActionResult HomeFor(CurrentUser user) =>
        user.IsOrganiser
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "Events");
}
