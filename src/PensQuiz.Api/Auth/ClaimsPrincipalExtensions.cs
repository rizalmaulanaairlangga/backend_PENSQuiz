using System.Security.Claims;

namespace PensQuiz.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("Invalid JWT: missing or invalid user id claim (sub).");
        }

        return userId;
    }
}

