using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Enrolment> EnrolmentsWithDetails() =>
        _db.Enrolments
            .Include(en => en.Participant)
            .Include(en => en.Event)
            .Include(en => en.Category);

    /// <summary>Enters the logged-in Participant into an event under the chosen category.</summary>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, EnrolRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");

        if (ev.EventDate <= DateTime.UtcNow)
        {
            return Error(StatusCodes.Status400BadRequest, "Entries closed", "This event has already taken place.");
        }

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null || category.EventId != eventId)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid category", "That category does not belong to this event.");
        }

        var participant = await _db.Participants.FindAsync(CurrentUserId);
        if (participant is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

        if (await _db.Enrolments.AnyAsync(en => en.EventId == eventId && en.ParticipantId == participant.ParticipantId))
        {
            return Error(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this event.");
        }

        // Age check only when the category has an age range and we know the participant's date of birth.
        if (participant.DateOfBirth is DateOnly dob && (category.MinAge is not null || category.MaxAge is not null))
        {
            var eventDay = DateOnly.FromDateTime(ev.EventDate);
            var age = eventDay.Year - dob.Year;
            if (eventDay < dob.AddYears(age)) age--;

            var tooYoung = category.MinAge is int min && age < min;
            var tooOld = category.MaxAge is int max && age > max;
            if (tooYoung || tooOld)
            {
                return Error(StatusCodes.Status400BadRequest, "Outside age range",
                    $"You will be {age} on race day and this category does not allow that age.");
            }
        }

        var enrolment = new Enrolment
        {
            ParticipantId = participant.ParticipantId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            Status = EnrolmentStatus.Pending
        };

        try
        {
            _db.Enrolments.Add(enrolment);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two requests at once can both pass the check above. The unique index catches the second one.
            return Error(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this event.");
        }

        var saved = await EnrolmentsWithDetails().AsNoTracking().FirstAsync(en => en.EnrolmentId == enrolment.EnrolmentId);
        return Created("/api/enrolments/mine", saved.ToResponse());
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    [HttpGet("api/enrolments/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var enrolments = await EnrolmentsWithDetails()
            .AsNoTracking()
            .Where(en => en.ParticipantId == CurrentUserId)
            .OrderBy(en => en.Event.EventDate)
            .ToListAsync();

        return Ok(enrolments.Select(en => en.ToResponse()).ToList());
    }

    /// <summary>Lists everyone enrolled in an event the Organiser owns.</summary>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only view enrolments for events you created.");
        }

        var enrolments = await EnrolmentsWithDetails()
            .AsNoTracking()
            .Where(en => en.EventId == eventId)
            .OrderBy(en => en.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(en => en.ToResponse()).ToList());
    }

    /// <summary>Lets an Organiser set an enrolment to Pending, Confirmed or Cancelled.</summary>
    [HttpPut("api/enrolments/{id:int}/status")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, EnrolmentStatusRequest request)
    {
        var enrolment = await EnrolmentsWithDetails().FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null) return Error(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.Event.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only manage enrolments for events you created.");
        }

        enrolment.Status = request.Status!.Value;
        await _db.SaveChangesAsync();
        return Ok(enrolment.ToResponse());
    }

    /// <summary>Withdraws the logged-in Participant from an event. Blocked once a result exists.</summary>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null) return Error(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.ParticipantId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your enrolment", "You can only cancel your own enrolment.");
        }

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == id))
        {
            return Error(StatusCodes.Status409Conflict, "Result already recorded", "This enrolment already has a result.");
        }

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
