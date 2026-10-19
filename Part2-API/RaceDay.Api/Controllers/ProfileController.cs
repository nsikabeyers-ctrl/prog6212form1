using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
[SessionAuthorize]
public class ProfileController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ProfileController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the logged-in user's own profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        if (CurrentRole == Roles.Organiser)
        {
            var organiser = await _db.Organisers.FindAsync(CurrentUserId);
            if (organiser is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

            return Ok(new ProfileResponse(organiser.OrganiserId, Roles.Organiser, organiser.FullName,
                organiser.Email, organiser.PhoneNumber, null, null));
        }

        var participant = await _db.Participants.FindAsync(CurrentUserId);
        if (participant is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

        return Ok(new ProfileResponse(participant.ParticipantId, Roles.Participant, participant.FullName,
            participant.Email, null, participant.DateOfBirth, participant.ProfilePictureUrl));
    }

    /// <summary>Updates the logged-in user's own profile. Email and role cannot be changed here.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (CurrentRole == Roles.Organiser)
        {
            var organiser = await _db.Organisers.FindAsync(CurrentUserId);
            if (organiser is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

            organiser.FullName = request.FullName.Trim();
            organiser.PhoneNumber = request.PhoneNumber?.Trim();
            await _db.SaveChangesAsync();

            return Ok(new ProfileResponse(organiser.OrganiserId, Roles.Organiser, organiser.FullName,
                organiser.Email, organiser.PhoneNumber, null, null));
        }

        var participant = await _db.Participants.FindAsync(CurrentUserId);
        if (participant is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

        participant.FullName = request.FullName.Trim();
        participant.DateOfBirth = request.DateOfBirth;
        await _db.SaveChangesAsync();

        return Ok(new ProfileResponse(participant.ParticipantId, Roles.Participant, participant.FullName,
            participant.Email, null, participant.DateOfBirth, participant.ProfilePictureUrl));
    }
}
