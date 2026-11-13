namespace RaceDay.Web.Services;

/// <summary>What came back from the API: did it work, and if not, why.</summary>
public class ApiResult
{
    public bool Success { get; init; }

    /// <summary>The HTTP status from the API. 0 means the API could not be reached at all.</summary>
    public int StatusCode { get; init; }

    public string? Error { get; init; }

    /// <summary>Field level messages when the API rejected the input with a 400.</summary>
    public List<string> ValidationMessages { get; init; } = new();

    public bool IsUnauthorized => StatusCode == 401;
    public bool IsNotFound => StatusCode == 404;
    public bool IsForbidden => StatusCode == 403;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}

/// <summary>Used for calls that return no body, like DELETE.</summary>
public sealed class None
{
}
