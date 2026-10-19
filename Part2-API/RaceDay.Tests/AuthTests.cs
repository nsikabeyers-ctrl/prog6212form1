using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Registration, login, logout and the session that keeps Jarred logged in.</summary>
public class AuthTests : ApiTestBase
{
    public AuthTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Jarred_Can_Register_As_Participant()
    {
        var client = NewClient();

        var response = await Register(client, "Participant", UniqueEmail("participant"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Read<AuthResponse>(response);
        Assert.Equal("Participant", body!.Role);
    }

    [Fact]
    public async Task Jarred_Can_Register_As_Organiser_With_The_Invite_Code()
    {
        var client = NewClient();

        var response = await Register(client, "Organiser", UniqueEmail("organiser"),
            inviteCode: RaceDayFactory.OrganiserInviteCode);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Read<AuthResponse>(response);
        Assert.Equal("Organiser", body!.Role);
    }

    [Fact]
    public async Task Jarred_Cannot_Register_As_Organiser_Without_The_Invite_Code()
    {
        var client = NewClient();

        var response = await Register(client, "Organiser", UniqueEmail("organiser"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Jarred_Cannot_Register_As_Organiser_With_A_Wrong_Invite_Code()
    {
        var client = NewClient();

        var response = await Register(client, "Organiser", UniqueEmail("organiser"), inviteCode: "not-the-code");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Registering_With_An_Unknown_Role_Is_Rejected()
    {
        var client = NewClient();

        var response = await Register(client, "Referee", UniqueEmail("referee"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registering_The_Same_Email_Twice_Gives_Conflict()
    {
        var client = NewClient();
        var email = UniqueEmail("participant");

        var first = await Register(client, "Participant", email);
        var second = await Register(client, "Participant", email);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Registering_With_A_Weak_Password_Is_Rejected()
    {
        var client = NewClient();

        var response = await Register(client, "Participant", UniqueEmail("participant"), password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registering_With_An_Invalid_Email_Is_Rejected()
    {
        var client = NewClient();

        var response = await Register(client, "Participant", "this-is-not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_Is_Stored_Hashed_And_Never_As_Plain_Text()
    {
        var client = NewClient();
        var email = UniqueEmail("participant");
        await Register(client, "Participant", email);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Participants.SingleAsync(p => p.Email == email);

        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.DoesNotContain(Password, stored.PasswordHash);
        Assert.StartsWith("v1.", stored.PasswordHash);
    }

    [Fact]
    public async Task Login_With_Correct_Details_Starts_A_Session()
    {
        var client = NewClient();
        var email = UniqueEmail("participant");
        await Register(client, "Participant", email);

        var login = await Login(client, email);
        var profile = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await Read<AuthResponse>(login);
        Assert.Equal("Participant", auth!.Role);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Login_Works_For_Organisers_Too_And_Reports_The_Organiser_Role()
    {
        var client = NewClient();
        var email = UniqueEmail("organiser");
        await Register(client, "Organiser", email, inviteCode: RaceDayFactory.OrganiserInviteCode);

        var login = await Login(client, email);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await Read<AuthResponse>(login);
        Assert.Equal("Organiser", auth!.Role);
    }

    [Fact]
    public async Task Login_With_A_Wrong_Password_Gives_Unauthorized_And_No_Session()
    {
        var client = NewClient();
        var email = UniqueEmail("participant");
        await Register(client, "Participant", email);

        var login = await Login(client, email, password: "WrongPassword1");
        var profile = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Fact]
    public async Task Login_With_An_Unknown_Email_Gives_Unauthorized()
    {
        var client = NewClient();

        var login = await Login(client, UniqueEmail("nobody"));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Protected_Endpoints_Reject_Requests_Without_A_Session()
    {
        var client = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/enrolments/mine")).StatusCode);
    }

    [Fact]
    public async Task Logout_Ends_The_Session()
    {
        var (client, _) = await LoggedInParticipant();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);

        var logout = await Post(client, "/api/auth/logout");
        var profileAfter = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profileAfter.StatusCode);
    }

    [Fact]
    public async Task Two_Clients_Have_Separate_Sessions()
    {
        var (jarred, _) = await LoggedInParticipant();
        var stranger = NewClient();

        Assert.Equal(HttpStatusCode.OK, (await jarred.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task Jarred_Can_Update_His_Own_Profile()
    {
        var (client, _) = await LoggedInParticipant();

        var update = await Put(client, "/api/profile", new { fullName = "Jarred Runner", dateOfBirth = "1995-06-15" });
        var profile = await Read<ProfileResponse>(await client.GetAsync("/api/profile"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Jarred Runner", profile!.FullName);
        Assert.Equal(new DateOnly(1995, 6, 15), profile.DateOfBirth);
    }
}
