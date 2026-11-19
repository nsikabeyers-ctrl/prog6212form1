using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleRequired(Roles.Participant)]
public class MyEnrolmentsController : BaseController
{
    private readonly RaceDayApiClient _api;

    public MyEnrolmentsController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            Fail(result.Error ?? "Your events could not be loaded.");
            return View(new List<EnrolmentDto>());
        }

        return View(result.Data!.OrderBy(e => e.EventDate).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var result = await _api.WithdrawAsync(id);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success("You have been withdrawn from that event.");
        }
        else
        {
            Fail(result.Error ?? "You could not be withdrawn.");
        }

        return RedirectToAction(nameof(Index));
    }
}
