using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
    }

    /// <summary>Creates an account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Anyone can register as a Participant. Registering as an Organiser needs the organiser invite code,
    /// otherwise any visitor could give themselves organiser powers.
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var wantsOrganiser = string.Equals(request.Role.Trim(), Roles.Organiser, StringComparison.OrdinalIgnoreCase);
        var wantsParticipant = string.Equals(request.Role.Trim(), Roles.Participant, StringComparison.OrdinalIgnoreCase);

        if (!wantsOrganiser && !wantsParticipant)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid role", "Role must be Organiser or Participant.");
        }

        if (wantsOrganiser && !InviteCodeIsValid(request.OrganiserInviteCode))
        {
            return Error(StatusCodes.Status403Forbidden, "Invalid organiser invite code",
                "A valid organiser invite code is needed to register as an Organiser.");
        }

        var emailTaken = await _db.Organisers.AnyAsync(o => o.Email == email)
                         || await _db.Participants.AnyAsync(p => p.Email == email);
        if (emailTaken)
        {
            return Error(StatusCodes.Status409Conflict, "Email already registered");
        }

        int userId;
        string role;

        if (wantsOrganiser)
        {
            var organiser = new Organiser
            {
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = _hasher.Hash(request.Password),
                PhoneNumber = request.PhoneNumber?.Trim()
            };
            _db.Organisers.Add(organiser);
            await _db.SaveChangesAsync();
            userId = organiser.OrganiserId;
            role = Roles.Organiser;
        }
        else
        {
            var participant = new Participant
            {
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = _hasher.Hash(request.Password),
                DateOfBirth = request.DateOfBirth
            };
            _db.Participants.Add(participant);
            await _db.SaveChangesAsync();
            userId = participant.ParticipantId;
            role = Roles.Participant;
        }

        return Created("/api/profile", new AuthResponse(userId, request.FullName.Trim(), email, role));
    }

    /// <summary>Logs in and starts a session that remembers the user id and role.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.Email == email);
        if (organiser is not null && _hasher.Verify(request.Password, organiser.PasswordHash))
        {
            return StartSession(organiser.OrganiserId, organiser.FullName, organiser.Email, Roles.Organiser);
        }

        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Email == email);
        if (participant is not null && _hasher.Verify(request.Password, participant.PasswordHash))
        {
            return StartSession(participant.ParticipantId, participant.FullName, participant.Email, Roles.Participant);
        }

        // Same message for unknown email and wrong password.
        return Error(StatusCodes.Status401Unauthorized, "Invalid email or password");
    }

    /// <summary>Ends the current session.</summary>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }

    private IActionResult StartSession(int userId, string fullName, string email, string role)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
        HttpContext.Session.SetString(SessionKeys.Role, role);
        return Ok(new AuthResponse(userId, fullName, email, role));
    }

    private bool InviteCodeIsValid(string? supplied)
    {
        var expected = _config["Auth:OrganiserInviteCode"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied.Trim()),
            Encoding.UTF8.GetBytes(expected.Trim()));
    }
}
