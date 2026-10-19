using System.Net;
using RaceDay.Api.DTOs;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organisers managing events, and who is allowed to touch them.</summary>
public class EventTests : ApiTestBase
{
    public EventTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Organiser_Can_Create_An_Event()
    {
        var (organiser, organiserId) = await LoggedInOrganiser();

        var response = await Post(organiser, "/api/events", EventBody("Jarred's Sunrise 10km"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Read<EventResponse>(response);
        Assert.Equal("Jarred's Sunrise 10km", created!.Name);
        Assert.Equal(organiserId, created.OrganiserId);
        Assert.Equal(Api.Models.EventType.Run, created.EventType);
    }

    [Fact]
    public async Task Creating_An_Event_With_Missing_Fields_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await Post(organiser, "/api/events", new { name = "", location = "Pretoria" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_An_Event_In_The_Past_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await Post(organiser, "/api/events", EventBody("Jarred's Old Race", daysAhead: -10));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_An_Event_With_An_Unknown_Type_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await Post(organiser, "/api/events", new
        {
            name = "Jarred's Swim",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "Pretoria",
            distanceKm = 2m,
            eventType = "Swim"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Can_Update_Their_Own_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var created = await CreateEvent(organiser);

        var response = await Put(organiser, $"/api/events/{created.EventId}", new
        {
            name = "Jarred's 21km Half",
            description = "Now longer.",
            eventDate = DateTime.UtcNow.AddDays(40),
            location = "Johannesburg",
            distanceKm = 21m,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Read<EventResponse>(response);
        Assert.Equal("Jarred's 21km Half", updated!.Name);
        Assert.Equal(21m, updated.DistanceKm);
    }

    [Fact]
    public async Task Organiser_Can_Delete_Their_Own_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var created = await CreateEvent(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{created.EventId}");
        var after = await organiser.GetAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Organiser_Cannot_Delete_An_Event_That_Already_Has_Enrolments()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(participant, ev.EventId, category.CategoryId);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_Cannot_Update_Another_Organisers_Event()
    {
        var (owner, _) = await LoggedInOrganiser();
        var (otherOrganiser, _) = await LoggedInOrganiser();
        var created = await CreateEvent(owner);

        var response = await Put(otherOrganiser, $"/api/events/{created.EventId}", EventBody("Stolen by Jarred"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Cannot_Delete_Another_Organisers_Event()
    {
        var (owner, _) = await LoggedInOrganiser();
        var (otherOrganiser, _) = await LoggedInOrganiser();
        var created = await CreateEvent(owner);

        var response = await otherOrganiser.DeleteAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Updating_An_Event_That_Does_Not_Exist_Gives_Not_Found()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await Put(organiser, "/api/events/999999", EventBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Both_Roles_Can_View_Events()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var created = await CreateEvent(organiser, "Jarred's Visible Race");

        var asOrganiser = await organiser.GetAsync($"/api/events/{created.EventId}");
        var asParticipant = await participant.GetAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.OK, asOrganiser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asParticipant.StatusCode);
    }

    [Fact]
    public async Task Event_List_Can_Be_Filtered_By_Type()
    {
        var (organiser, _) = await LoggedInOrganiser();
        await CreateEvent(organiser, "Jarred's Run");
        var walk = await Post(organiser, "/api/events", new
        {
            name = "Jarred's Walk",
            eventDate = DateTime.UtcNow.AddDays(20),
            location = "Tembisa",
            distanceKm = 5m,
            eventType = "Walk"
        });
        Assert.Equal(HttpStatusCode.Created, walk.StatusCode);

        var walks = await Read<List<EventResponse>>(await organiser.GetAsync("/api/events?type=Walk"));

        Assert.NotEmpty(walks!);
        Assert.All(walks!, e => Assert.Equal(Api.Models.EventType.Walk, e.EventType));
    }

    [Fact]
    public async Task Organiser_Sees_Only_Their_Own_Events_In_My_Events()
    {
        var (organiser, organiserId) = await LoggedInOrganiser();
        var (otherOrganiser, _) = await LoggedInOrganiser();
        await CreateEvent(organiser, "Jarred's Own Race");
        await CreateEvent(otherOrganiser, "Someone Else's Race");

        var mine = await Read<List<EventResponse>>(await organiser.GetAsync("/api/events/mine"));

        Assert.NotEmpty(mine!);
        Assert.All(mine!, e => Assert.Equal(organiserId, e.OrganiserId));
    }

    [Fact]
    public async Task Organiser_Can_Add_And_List_Categories()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);

        await CreateCategory(organiser, ev.EventId, "Under 20", minAge: 10, maxAge: 19);
        await CreateCategory(organiser, ev.EventId, "Senior", minAge: 20, maxAge: 59);

        var list = await Read<List<CategoryResponse>>(await participant.GetAsync($"/api/events/{ev.EventId}/categories"));
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Category_With_Min_Age_Above_Max_Age_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(organiser);

        var response = await Post(organiser, $"/api/events/{ev.EventId}/categories",
            new { name = "Backwards", minAge = 50, maxAge = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
