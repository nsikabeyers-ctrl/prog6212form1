using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organisers capture finish times and positions for the people who took part.</summary>
[RoleRequired(Roles.Organiser)]
public class ManageResultsController : BaseController
{
    // hh:mm:ss, so 48:32 can never be read as 48 hours.
    private static readonly Regex TimePattern = new(@"^\d{1,2}:[0-5]\d:[0-5]\d$");

    private readonly RaceDayApiClient _api;

    public ManageResultsController(RaceDayApiClient api)
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

        var model = new ManageResultsViewModel
        {
            Event = ev.Data,
            HasPassed = ev.Data.EventDate <= DateTime.UtcNow
        };

        var enrolments = await _api.GetEventEnrolmentsAsync(eventId);
        var results = await _api.GetEventResultsAsync(eventId);

        if (!enrolments.Success)
        {
            Fail(enrolments.Error ?? "The entries could not be loaded.");
            return View(model);
        }

        var resultsByEnrolment = results.Success
            ? results.Data!.ToDictionary(r => r.EnrolmentId)
            : new Dictionary<int, ResultDto>();

        // Cancelled entries did not run, so they get no result row.
        model.Rows = enrolments.Data!
            .Where(e => e.Status != EnrolmentStatus.Cancelled)
            .OrderBy(e => resultsByEnrolment.TryGetValue(e.EnrolmentId, out var r) ? r.FinishPosition : int.MaxValue)
            .ThenBy(e => e.ParticipantName)
            .Select(e => new ResultRow
            {
                Enrolment = e,
                Result = resultsByEnrolment.GetValueOrDefault(e.EnrolmentId)
            })
            .ToList();

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Save(int eventId, int enrolmentId, int? resultId, string? finishTime, int? finishPosition)
    {
        if (string.IsNullOrWhiteSpace(finishTime) || !TimePattern.IsMatch(finishTime.Trim()))
        {
            Fail("Write the finish time as hh:mm:ss, for example 00:48:32.");
            return RedirectToAction(nameof(Index), new { eventId });
        }

        if (finishPosition is null or < 1)
        {
            Fail("The finishing position must be 1 or higher.");
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var time = TimeSpan.Parse(finishTime.Trim(), CultureInfo.InvariantCulture);

        var result = resultId is int existing
            ? await _api.UpdateResultAsync(existing, time, finishPosition.Value)
            : await _api.CaptureResultAsync(enrolmentId, time, finishPosition.Value);

        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success($"Result saved for {result.Data!.ParticipantName}.");
        }
        else
        {
            Fail(result.ValidationMessages.FirstOrDefault() ?? result.Error ?? "The result could not be saved.");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}
