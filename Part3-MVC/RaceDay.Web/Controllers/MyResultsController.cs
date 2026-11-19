using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleRequired(Roles.Participant)]
public class MyResultsController : BaseController
{
    private readonly RaceDayApiClient _api;

    public MyResultsController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyResultsAsync();
        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            Fail(result.Error ?? "Your results could not be loaded.");
            return View(new List<ResultDto>());
        }

        // Newest race first.
        return View(result.Data!.OrderByDescending(r => r.EventDate).ToList());
    }
}
