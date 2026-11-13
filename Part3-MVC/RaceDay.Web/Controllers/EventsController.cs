using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>The Participant home page and event pages.</summary>
[RoleRequired(Roles.Participant)]
public class EventsController : BaseController
{
    private readonly RaceDayApiClient _api;

    public EventsController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? type, string? search, DateTime? from, DateTime? to)
    {
        var events = await _api.GetEventsAsync(type, search, from, to);
        if (SessionExpired(events) is { } expired) return expired;

        var model = new EventListViewModel { Type = type, Search = search, From = from, To = to };

        if (events.Success)
        {
            model.Events = events.Data!;
        }
        else
        {
            Fail(events.Error ?? "The events could not be loaded.");
        }

        // Used to mark the events this Participant has already entered.
        var mine = await _api.GetMyEnrolmentsAsync();
        if (mine.Success)
        {
            model.EnteredEventIds = mine.Data!.Select(e => e.EventId).ToHashSet();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (SessionExpired(ev) is { } expired) return expired;
        if (ev.IsNotFound) return NotFound();
        if (!ev.Success)
        {
            Fail(ev.Error ?? "That event could not be loaded.");
            return RedirectToAction(nameof(Index));
        }

        var model = new EventDetailsViewModel
        {
            Event = ev.Data!,
            HasPassed = ev.Data!.EventDate <= DateTime.UtcNow
        };

        var mine = await _api.GetMyEnrolmentsAsync();
        if (mine.Success)
        {
            model.MyEnrolment = mine.Data!.FirstOrDefault(e => e.EventId == id);
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        if (categoryId <= 0)
        {
            Fail("Choose a category before entering the event.");
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await _api.EnrolAsync(eventId, categoryId);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success("You are entered. The organiser will confirm your entry.");
        }
        else
        {
            Fail(result.ValidationMessages.FirstOrDefault() ?? result.Error ?? "The entry did not go through.");
        }

        return RedirectToAction(nameof(Details), new { id = eventId });
    }
}
