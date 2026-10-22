using System.Security.Claims;
namespace ITServiceDeskApp.Security;
public static class SiteActivityPolicy
{
    public const string SessionClaim = "SiteActivitySessionId";
    public const string SessionCookie = ".TW.ActivitySession";
    public static bool CanView(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true &&
        string.Equals(user.FindFirst(ClaimTypes.Email)?.Value?.Trim(), "admin@trawzacons.com", StringComparison.OrdinalIgnoreCase);
    public static bool IsOnline(DateTime? closed, DateTime lastSeen, DateTime now) => closed == null && lastSeen >= now.AddSeconds(-90);
    public static long CreditSeconds(DateTime? previousPulse, DateTime now, bool wasActive, bool isActive)
    {
        if (!wasActive || !isActive || previousPulse == null) return 0;
        var seconds = (now - previousPulse.Value).TotalSeconds;
        return seconds is >= 10 and <= 60 ? (long)Math.Floor(seconds) : 0;
    }
}
