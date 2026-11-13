using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
[SessionAuthorize]
public class ProfileController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;

    public ProfileController(RaceDayDbContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
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

    /// <summary>Uploads a profile picture for the logged-in Participant and stores it in Blob Storage.</summary>
    /// <remarks>Send the image as multipart form data in a field called "file". JPG, PNG, WEBP or GIF, up to 2 MB.</remarks>
    /// <param name="file">The image file.</param>
    [HttpPost("picture")]
    [SessionAuthorize(Roles.Participant)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadPicture(IFormFile file, CancellationToken cancellationToken)
    {
        var problem = ImageRules.Validate(file, out var extension);
        if (problem is not null)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid image", problem);
        }

        var participant = await _db.Participants.FindAsync(new object[] { CurrentUserId }, cancellationToken);
        if (participant is null) return Error(StatusCodes.Status401Unauthorized, "Account no longer exists");

        await using var stream = file.OpenReadStream();
        participant.ProfilePictureUrl = await _images.SaveAsync(stream, file.ContentType, "profiles", extension, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ProfileResponse(participant.ParticipantId, Roles.Participant, participant.FullName,
            participant.Email, null, participant.DateOfBirth, participant.ProfilePictureUrl));
    }
}
