using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Result> ResultsWithDetails() =>
        _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en.Participant)
            .Include(r => r.Enrolment).ThenInclude(en => en.Event)
            .Include(r => r.Enrolment).ThenInclude(en => en.Category);

    private Task<int> CountFinishers(int eventId) =>
        _db.Results.CountAsync(r => r.Enrolment.EventId == eventId);

    /// <summary>Captures the finish time and position for one enrolment.</summary>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentId == enrolmentId);
        if (enrolment is null) return Error(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.Event.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only capture results for events you created.");
        }

        if (enrolment.Status == EnrolmentStatus.Cancelled)
        {
            return Error(StatusCodes.Status400BadRequest, "Enrolment cancelled", "A cancelled enrolment cannot have a result.");
        }

        if (request.FinishTime!.Value <= TimeSpan.Zero)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid finish time", "Finish time must be more than zero.");
        }

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
        {
            return Error(StatusCodes.Status409Conflict, "Result already captured", "Use PUT /api/results/{id} to correct it.");
        }

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime.Value,
            FinishPosition = request.FinishPosition!.Value,
            RecordedByOrganiserId = CurrentUserId
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await ResultsWithDetails().AsNoTracking().FirstAsync(r => r.ResultId == result.ResultId);
        return Created($"/api/events/{enrolment.EventId}/results", saved.ToResponse(await CountFinishers(enrolment.EventId)));
    }

    /// <summary>Corrects a result that was captured earlier.</summary>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, ResultRequest request)
    {
        var result = await ResultsWithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Error(StatusCodes.Status404NotFound, "Result not found");
        if (result.Enrolment.Event.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only change results for events you created.");
        }

        if (request.FinishTime!.Value <= TimeSpan.Zero)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid finish time", "Finish time must be more than zero.");
        }

        result.FinishTime = request.FinishTime.Value;
        result.FinishPosition = request.FinishPosition!.Value;
        result.RecordedByOrganiserId = CurrentUserId;
        result.RecordedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(result.ToResponse(await CountFinishers(result.Enrolment.EventId)));
    }

    /// <summary>Returns the logged-in Participant's race history, newest first.</summary>
    [HttpGet("api/results/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var results = await ResultsWithDetails()
            .AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == CurrentUserId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .ToListAsync();

        var eventIds = results.Select(r => r.Enrolment.EventId).Distinct().ToList();
        var totals = await _db.Results
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return Ok(results.Select(r => r.ToResponse(totals.GetValueOrDefault(r.Enrolment.EventId))).ToList());
    }

    /// <summary>Lists all results for an event in finishing order.</summary>
    [HttpGet("api/events/{eventId:int}/results")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return Error(StatusCodes.Status404NotFound, "Event not found");
        }

        var results = await ResultsWithDetails()
            .AsNoTracking()
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.FinishPosition)
            .ToListAsync();

        return Ok(results.Select(r => r.ToResponse(results.Count)).ToList());
    }
}
