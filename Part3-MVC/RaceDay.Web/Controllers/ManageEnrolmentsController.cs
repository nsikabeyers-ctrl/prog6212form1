using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organisers see who entered their event and confirm or cancel each entry.</summary>
[RoleRequired(Roles.Organiser)]
public class ManageEnrolmentsController : BaseController
{
    private readonly RaceDayApiClient _api;

    public ManageEnrolmentsController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (SessionExpired(ev) is { } expired) return expired;
        if (ev.IsNotFound) return NotFound();

        if (!ev.Success)
        {
            Fail(ev.Error ?? "That event could not be loaded.");
            return RedirectToAction("Index", "ManageEvents");
        }

        if (ev.Data!.OrganiserId != Me!.UserId) return RedirectToAction("AccessDenied", "Account");

        var model = new ManageEnrolmentsViewModel { Event = ev.Data };

        var enrolments = await _api.GetEventEnrolmentsAsync(eventId);
        if (enrolments.Success)
        {
            model.Enrolments = enrolments.Data!
                .OrderBy(e => e.Status == EnrolmentStatus.Cancelled)
                .ThenBy(e => e.ParticipantName)
                .ToList();
        }
        else
        {
            Fail(enrolments.Error ?? "The entries could not be loaded.");
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, int eventId, EnrolmentStatus status)
    {
        var result = await _api.SetEnrolmentStatusAsync(id, status);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success($"{result.Data!.ParticipantName} is now {status}.");
        }
        else
        {
            Fail(result.Error ?? "The status could not be changed.");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}
