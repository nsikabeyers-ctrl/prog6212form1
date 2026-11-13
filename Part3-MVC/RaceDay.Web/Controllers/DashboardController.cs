using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleRequired(Roles.Organiser)]
public class DashboardController : BaseController
{
    private readonly RaceDayApiClient _api;

    public DashboardController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyEventsAsync();
        if (SessionExpired(result) is { } expired) return expired;

        var model = new DashboardViewModel();
        if (result.Success)
        {
            model.Events = result.Data!.OrderBy(e => e.EventDate).ToList();
        }
        else
        {
            Fail(result.Error ?? "Your events could not be loaded.");
        }

        return View(model);
    }
}
