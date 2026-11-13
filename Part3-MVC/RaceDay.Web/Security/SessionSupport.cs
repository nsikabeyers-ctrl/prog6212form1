using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Security;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
    public const string ApiCookie = "ApiCookie";
}

public record CurrentUser(int UserId, string Role, string FullName)
{
    public bool IsOrganiser => Role == Roles.Organiser;
    public bool IsParticipant => Role == Roles.Participant;
}

public static class SessionExtensions
{
    /// <summary>Who is logged in, or null when nobody is.</summary>
    public static CurrentUser? GetCurrentUser(this ISession session)
    {
        var id = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);
        if (id is null || string.IsNullOrEmpty(role)) return null;

        return new CurrentUser(id.Value, role, session.GetString(SessionKeys.FullName) ?? string.Empty);
    }

    public static void SignIn(this ISession session, AuthResponse user)
    {
        session.SetInt32(SessionKeys.UserId, user.UserId);
        session.SetString(SessionKeys.Role, user.Role);
        session.SetString(SessionKeys.FullName, user.FullName);
    }
}

/// <summary>
/// Guards an MVC page. Nobody logged in is sent to the login page.
/// Someone logged in with the wrong role is sent to the access denied page.
/// No roles listed means any logged-in user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RoleRequiredAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RoleRequiredAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.Session.GetCurrentUser();

        if (user is null)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == HttpMethods.Get ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(user.Role))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
