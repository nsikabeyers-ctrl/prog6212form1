using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.DTOs;

namespace RaceDay.Tests.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Shared helpers. Every "Jarred" in these tests is a fresh user with a unique email, so tests
/// never trip over each other even though they share one in-memory database per test class.
/// </summary>
public abstract class ApiTestBase : IClassFixture<RaceDayFactory>
{
    protected const string Password = "JarredRuns10km";

    protected readonly RaceDayFactory Factory;

    protected ApiTestBase(RaceDayFactory factory)
    {
        Factory = factory;
    }

    /// <summary>A new client with its own cookie jar, so each client is its own browser session.</summary>
    protected HttpClient NewClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    protected static string UniqueEmail(string who) => $"jarred.{who}.{Guid.NewGuid():N}@raceday.test";

    protected Task<HttpResponseMessage> Post(HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, Json.Options);

    protected Task<HttpResponseMessage> Put(HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json.Options);

    protected static Task<T?> Read<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(Json.Options);

    protected Task<HttpResponseMessage> Register(
        HttpClient client, string role, string email, string password = Password, string? inviteCode = null, string? dateOfBirth = null) =>
        Post(client, "/api/auth/register", new
        {
            fullName = $"Jarred the {role}",
            email,
            password,
            role,
            dateOfBirth,
            organiserInviteCode = inviteCode
        });

    protected Task<HttpResponseMessage> Login(HttpClient client, string email, string password = Password) =>
        Post(client, "/api/auth/login", new { email, password });

    /// <summary>Registers an Organiser, logs in and returns the logged-in client plus the new user id.</summary>
    protected async Task<(HttpClient Client, int UserId)> LoggedInOrganiser()
    {
        var client = NewClient();
        var email = UniqueEmail("organiser");

        var register = await Register(client, "Organiser", email, inviteCode: RaceDayFactory.OrganiserInviteCode);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var created = await Read<AuthResponse>(register);

        var login = await Login(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return (client, created!.UserId);
    }

    /// <summary>Registers a Participant, logs in and returns the logged-in client plus the new user id.</summary>
    protected async Task<(HttpClient Client, int UserId)> LoggedInParticipant(string dateOfBirth = "1990-01-01")
    {
        var client = NewClient();
        var email = UniqueEmail("participant");

        var register = await Register(client, "Participant", email, dateOfBirth: dateOfBirth);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var created = await Read<AuthResponse>(register);

        var login = await Login(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return (client, created!.UserId);
    }

    protected static object EventBody(string name = "Jarred's 10km Fun Run", int daysAhead = 30) => new
    {
        name,
        description = "A friendly 10km run for Jarred and friends.",
        eventDate = DateTime.UtcNow.AddDays(daysAhead),
        location = "Pretoria",
        distanceKm = 10m,
        eventType = "Run"
    };

    protected async Task<EventResponse> CreateEvent(HttpClient organiser, string name = "Jarred's 10km Fun Run", int daysAhead = 30)
    {
        var response = await Post(organiser, "/api/events", EventBody(name, daysAhead));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read<EventResponse>(response))!;
    }

    protected async Task<CategoryResponse> CreateCategory(
        HttpClient organiser, int eventId, string name = "Jarred Senior", int? minAge = null, int? maxAge = null)
    {
        var response = await Post(organiser, $"/api/events/{eventId}/categories", new { name, minAge, maxAge });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read<CategoryResponse>(response))!;
    }

    protected async Task<EnrolmentResponse> Enrol(HttpClient participant, int eventId, int categoryId)
    {
        var response = await Post(participant, $"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read<EnrolmentResponse>(response))!;
    }
}
