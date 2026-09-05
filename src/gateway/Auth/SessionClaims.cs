using System.Security.Claims;

namespace Gateway.Auth;

public static class SessionClaims
{
    public const string UserId = "user_id";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string LeadUserId = "lead_user_id";

    public static readonly string[] Roles =
    [
        "Business Analyst",
        "Team Lead",
        "Engineering Manager",
        "Data Owner / Admin"
    ];

    public static string DisplayName(ClaimsPrincipal user)
    {
        return user.FindFirstValue(Name)
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.Identity?.Name
            ?? "there";
    }
}
