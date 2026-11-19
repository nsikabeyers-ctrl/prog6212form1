using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;

namespace RaceDay.Web.Services;

/// <summary>
/// The only place the MVC app talks to the API. Controllers call this and never build a request themselves.
/// The API keeps its own session. At login we keep that session cookie in the MVC session and
/// send it back on every call, so the API always knows who is asking and what role they have.
/// </summary>
public class RaceDayApiClient
{
    private const string ApiCookieName = ".RaceDay.Session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    private ISession? Session => _context.HttpContext?.Session;

    // ---------- Auth ----------

    public Task<ApiResult<AuthResponse>> LoginAsync(string email, string password) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", Json(new { email, password }), storeCookie: true);

    public Task<ApiResult<AuthResponse>> RegisterAsync(
        string fullName, string email, string password, string role,
        DateOnly? dateOfBirth, string? phoneNumber, string? organiserInviteCode) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register",
            Json(new { fullName, email, password, role, dateOfBirth, phoneNumber, organiserInviteCode }));

    public Task<ApiResult<None>> LogoutAsync() =>
        SendAsync<None>(HttpMethod.Post, "api/auth/logout");

    // ---------- Profile ----------

    public Task<ApiResult<ProfileDto>> GetProfileAsync() =>
        SendAsync<ProfileDto>(HttpMethod.Get, "api/profile");

    public Task<ApiResult<ProfileDto>> UpdateProfileAsync(string fullName, string? phoneNumber, DateOnly? dateOfBirth) =>
        SendAsync<ProfileDto>(HttpMethod.Put, "api/profile", Json(new { fullName, phoneNumber, dateOfBirth }));

    public Task<ApiResult<ProfileDto>> UploadProfilePictureAsync(IFormFile file) =>
        SendAsync<ProfileDto>(HttpMethod.Post, "api/profile/picture", FileContent(file));

    // ---------- Events ----------

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(string? type, string? search, DateTime? from, DateTime? to) =>
        SendAsync<List<EventDto>>(HttpMethod.Get, WithQuery("api/events",
            ("type", type),
            ("search", search),
            ("from", from?.ToString("yyyy-MM-dd")),
            ("to", to?.ToString("yyyy-MM-dd"))));

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() =>
        SendAsync<List<EventDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> GetEventAsync(int id) =>
        SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<EventDto>> CreateEventAsync(
        string name, string? description, DateTime eventDate, string location, decimal distanceKm, EventType eventType) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events",
            Json(new { name, description, eventDate, location, distanceKm, eventType }));

    public Task<ApiResult<EventDto>> UpdateEventAsync(
        int id, string name, string? description, DateTime eventDate, string location, decimal distanceKm, EventType eventType) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}",
            Json(new { name, description, eventDate, location, distanceKm, eventType }));

    public Task<ApiResult<None>> DeleteEventAsync(int id) =>
        SendAsync<None>(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<EventDto>(HttpMethod.Post, $"api/events/{eventId}/banner", FileContent(file));

    // ---------- Categories ----------

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, string name, int? minAge, int? maxAge, decimal? distanceKm) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories",
            Json(new { name, minAge, maxAge, distanceKm }));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, string name, int? minAge, int? maxAge, decimal? distanceKm) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}",
            Json(new { name, minAge, maxAge, distanceKm }));

    public Task<ApiResult<None>> DeleteCategoryAsync(int id) =>
        SendAsync<None>(HttpMethod.Delete, $"api/categories/{id}");

    // ---------- Enrolments ----------

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int eventId, int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/events/{eventId}/enrolments", Json(new { categoryId }));

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/mine");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<EnrolmentDto>> SetEnrolmentStatusAsync(int id, EnrolmentStatus status) =>
        SendAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{id}/status", Json(new { status }));

    public Task<ApiResult<None>> WithdrawAsync(int id) =>
        SendAsync<None>(HttpMethod.Delete, $"api/enrolments/{id}");

    // ---------- Results ----------

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, TimeSpan finishTime, int finishPosition) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result",
            Json(new { finishTime, finishPosition }));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, TimeSpan finishTime, int finishPosition) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", Json(new { finishTime, finishPosition }));

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/results/mine");

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    // ---------- Plumbing ----------

    private static HttpContent Json<T>(T body) => JsonContent.Create(body, mediaType: null, options: JsonOptions);

    private static HttpContent FileContent(IFormFile file)
    {
        var part = new StreamContent(file.OpenReadStream());
        part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        var form = new MultipartFormDataContent();
        form.Add(part, "file", file.FileName);
        return form;
    }

    private static string WithQuery(string path, params (string Key, string? Value)[] items)
    {
        var parts = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Value))
            .Select(i => $"{Uri.EscapeDataString(i.Key)}={Uri.EscapeDataString(i.Value!)}")
            .ToList();

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null, bool storeCookie = false)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        // Send the API session cookie that was saved at login.
        var cookie = Session?.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return Failed<T>(0, "The RaceDay service cannot be reached right now. Please try again in a moment.");
        }
        catch (TaskCanceledException)
        {
            return Failed<T>(0, "The RaceDay service took too long to answer. Please try again.");
        }

        using (response)
        {
            if (storeCookie && response.IsSuccessStatusCode)
            {
                SaveApiCookie(response);
            }

            var text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ReadProblem<T>((int)response.StatusCode, text);
            }

            T? data = default;
            if (!string.IsNullOrWhiteSpace(text) && typeof(T) != typeof(None))
            {
                try
                {
                    data = JsonSerializer.Deserialize<T>(text, JsonOptions);
                }
                catch (JsonException)
                {
                    return Failed<T>(500, "The RaceDay service sent back something unexpected.");
                }
            }

            return new ApiResult<T> { Success = true, StatusCode = (int)response.StatusCode, Data = data };
        }
    }

    private void SaveApiCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        var prefix = ApiCookieName + "=";
        foreach (var header in values)
        {
            if (!header.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var value = header[prefix.Length..];
            var end = value.IndexOf(';');
            if (end >= 0) value = value[..end];

            Session?.SetString(SessionKeys.ApiCookie, value);
            return;
        }
    }

    private static ApiResult<T> Failed<T>(int status, string message) =>
        new() { Success = false, StatusCode = status, Error = message };

    /// <summary>The API answers errors as problem details JSON. Pull out the readable parts.</summary>
    private static ApiResult<T> ReadProblem<T>(int status, string body)
    {
        var messages = new List<string>();
        string? title = null;
        string? detail = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) title = t.GetString();
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String) detail = d.GetString();

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind != JsonValueKind.Array) continue;
                        foreach (var message in field.Value.EnumerateArray())
                        {
                            if (message.ValueKind == JsonValueKind.String) messages.Add(message.GetString()!);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON. Fall through to the generic message below.
        }

        var text = status switch
        {
            401 => "Your session has ended. Please log in again.",
            403 => detail ?? "You do not have permission to do that.",
            404 => detail ?? title ?? "That could not be found.",
            400 when messages.Count > 0 => "Please fix the problems below.",
            >= 500 => "Something went wrong on our side. Please try again.",
            _ => detail ?? title ?? "That did not work. Please try again."
        };

        return new ApiResult<T>
        {
            Success = false,
            StatusCode = status,
            Error = text,
            ValidationMessages = messages
        };
    }
}
