using System.Net;
using System.Net.Http.Headers;
using RaceDay.Api.DTOs;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Profile pictures and event banners: who can upload, and what gets rejected.</summary>
public class ImageUploadTests : ApiTestBase
{
    // The first bytes of a real PNG. Enough for the signature check.
    private static readonly byte[] TinyPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };

    public ImageUploadTests(RaceDayFactory factory) : base(factory)
    {
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string contentType = "image/png", string fileName = "jarred.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Participant_Can_Upload_A_Profile_Picture()
    {
        var (participant, _) = await LoggedInParticipant();

        var response = await participant.PostAsync("/api/profile/picture", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await Read<ProfileResponse>(response);
        Assert.StartsWith("https://fake-storage.raceday.test/profiles/", profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task Uploaded_Profile_Picture_Url_Is_Saved_On_The_Profile()
    {
        var (participant, _) = await LoggedInParticipant();
        await participant.PostAsync("/api/profile/picture", ImageForm(TinyPng));

        var profile = await Read<ProfileResponse>(await participant.GetAsync("/api/profile"));

        Assert.NotNull(profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task Organiser_Cannot_Upload_A_Profile_Picture()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await organiser.PostAsync("/api/profile/picture", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logged_Out_User_Cannot_Upload_A_Profile_Picture()
    {
        var response = await NewClient().PostAsync("/api/profile/picture", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Text_File_Is_Rejected_Even_When_Called_An_Image()
    {
        var (participant, _) = await LoggedInParticipant();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just text, not a picture");

        var response = await participant.PostAsync("/api/profile/picture", ImageForm(notAnImage));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Pdf_Content_Type_Is_Rejected()
    {
        var (participant, _) = await LoggedInParticipant();

        var response = await participant.PostAsync("/api/profile/picture",
            ImageForm(TinyPng, "application/pdf", "jarred.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Image_Over_Two_Megabytes_Is_Rejected()
    {
        var (participant, _) = await LoggedInParticipant();
        var big = new byte[3 * 1024 * 1024];
        TinyPng.CopyTo(big, 0);

        var response = await participant.PostAsync("/api/profile/picture", ImageForm(big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Can_Upload_A_Banner_For_Their_Own_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(organiser, "Jarred's Banner Run");

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Read<EventResponse>(response);
        Assert.StartsWith("https://fake-storage.raceday.test/events/", updated!.BannerImageUrl);
    }

    [Fact]
    public async Task Organiser_Cannot_Upload_A_Banner_For_Another_Organisers_Event()
    {
        var (owner, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(owner, "Jarred's Private Run");
        var (other, _) = await LoggedInOrganiser();

        var response = await other.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Upload_An_Event_Banner()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(organiser, "Jarred's Closed Run");
        var (participant, _) = await LoggedInParticipant();

        var response = await participant.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Banner_For_An_Event_That_Does_Not_Exist_Gives_Not_Found()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var response = await organiser.PostAsync("/api/events/999999/banner", ImageForm(TinyPng));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
