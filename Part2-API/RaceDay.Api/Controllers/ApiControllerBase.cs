using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, read from the session. Only meaningful after [SessionAuthorize].</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role) ?? string.Empty;

    protected ObjectResult Error(int status, string title, string? detail = null) =>
        Problem(statusCode: status, title: title, detail: detail);
}
