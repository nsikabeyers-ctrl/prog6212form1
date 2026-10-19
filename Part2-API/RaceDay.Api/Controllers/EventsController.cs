using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Event> EventsWithDetails() =>
        _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .Include(e => e.Enrolments);

    /// <summary>Lists upcoming events. Past events are left out unless includePast is true.</summary>
    /// <param name="type">Filter by Run, Walk or Cycle.</param>
    /// <param name="search">Matches part of the event name or location.</param>
    /// <param name="from">Only events on or after this date.</param>
    /// <param name="to">Only events on or before this date.</param>
    /// <param name="includePast">Also return events that have already happened.</param>
    [HttpGet]
    [SessionAuthorize]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] EventType? type,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includePast = false)
    {
        var query = EventsWithDetails().AsNoTracking();

        if (!includePast) query = query.Where(e => e.EventDate >= DateTime.UtcNow);
        if (type is not null) query = query.Where(e => e.EventType == type);
        if (from is not null) query = query.Where(e => e.EventDate >= from);
        if (to is not null) query = query.Where(e => e.EventDate <= to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.Location.Contains(term));
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()).ToList());
    }

    /// <summary>Lists every event created by the logged-in Organiser, past and upcoming.</summary>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEvents()
    {
        var events = await EventsWithDetails()
            .AsNoTracking()
            .Where(e => e.OrganiserId == CurrentUserId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(e => e.ToResponse()).ToList());
    }

    /// <summary>Returns one event with its categories.</summary>
    [HttpGet("{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await EventsWithDetails().AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");

        return Ok(ev.ToResponse());
    }

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent(EventRequest request)
    {
        if (request.EventDate!.Value <= DateTime.UtcNow)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid event date", "The event date must be in the future.");
        }

        var organiser = await _db.Organisers.FindAsync(CurrentUserId);
        if (organiser is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

        var ev = new Event
        {
            OrganiserId = organiser.OrganiserId,
            Organiser = organiser,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm!.Value,
            EventType = request.EventType!.Value
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ev.ToResponse());
    }

    /// <summary>Updates an event. Only the Organiser who created it can do this.</summary>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, EventRequest request)
    {
        var ev = await EventsWithDetails().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only change events you created.");
        }

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm!.Value;
        ev.EventType = request.EventType!.Value;

        await _db.SaveChangesAsync();
        return Ok(ev.ToResponse());
    }

    /// <summary>Deletes an event and its categories. Blocked if anyone has enrolled.</summary>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only delete events you created.");
        }

        if (await _db.Enrolments.AnyAsync(en => en.EventId == id))
        {
            return Error(StatusCodes.Status409Conflict, "Event has enrolments",
                "An event with enrolments cannot be deleted.");
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
