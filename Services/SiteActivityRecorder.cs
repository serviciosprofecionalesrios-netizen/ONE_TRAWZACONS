using System.Security.Claims;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
namespace ITServiceDeskApp.Services;
// Tracking writes use a separate context so a logging failure cannot commit or roll back business changes.
public sealed class SiteActivityRecorder(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SiteActivityRecorder> logger)
{
    public static Guid? SessionId(HttpContext http, bool create)
    {
        if (Guid.TryParse(http.User.FindFirst(SiteActivityPolicy.SessionClaim)?.Value, out var claimed)) return claimed;
        if (Guid.TryParse(http.Request.Cookies[SiteActivityPolicy.SessionCookie], out var cookie)) return cookie;
        if (!create || http.Response.HasStarted) return null;
        var id = Guid.NewGuid();
        http.Response.Cookies.Append(SiteActivityPolicy.SessionCookie, id.ToString("N"), new CookieOptions {HttpOnly=true, Secure=http.Request.IsHttps, SameSite=SameSiteMode.Lax, IsEssential=true, MaxAge=TimeSpan.FromHours(8), Path="/"});
        return id;
    }
    private static string Limit(string? value, int length) => (value ?? "").Length > length ? value![..length] : value ?? "";
    private async Task<bool> Write(Guid id, ClaimsPrincipal user, Func<ApplicationDbContext,SiteActivitySession,DateTime,Task> change)
    {
        if (user.Identity?.IsAuthenticated != true || !int.TryParse(user.FindFirst("UserId")?.Value, out var userId) || userId <= 0) return false;
        for (var attempt = 0; attempt < 2; attempt++)
        {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = clock.GetUtcNow().UtcDateTime;
            var session = await db.SiteActivitySessions.FindAsync(id);
            if (session != null && (session.UserId != userId || session.ClosedAtUtc != null)) return false;
            if (session == null)
            {
                session = new SiteActivitySession {Id=id, UserId=userId, Email=Limit(user.FindFirst(ClaimTypes.Email)?.Value,150), DisplayName=Limit(user.Identity.Name,120), StartedAtUtc=now, LastSeenAtUtc=now};
                db.SiteActivitySessions.Add(session);
            }
            await change(db,session,now);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) when (attempt == 0)
        {
            // Re-read after another tab's heartbeat. A fresh context keeps both timing and events consistent.
            continue;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            logger.LogWarning(ex,"No se pudo registrar actividad del sitio.");
            return false;
        }
        }
        return false;
    }
    public Task<bool> Record(Guid id, ClaimsPrincipal user, string kind, string controller, string action, string title, int status) => Write(id,user,(db,session,now) => {
        session.LastSeenAtUtc=now; session.LastActivityAtUtc=now;
        session.LastController=Limit(controller,80); session.LastAction=Limit(action,80);
        session.IsVisible=true; session.HasRecentActivity=true;
        db.SiteActivityEvents.Add(new SiteActivityEvent {SessionId=id,UserId=session.UserId,OccurredAtUtc=now,Kind=Limit(kind,30),Controller=Limit(controller,80),Action=Limit(action,80),Title=Limit(title,140),ResponseStatus=status});
        return Task.CompletedTask;
    });
    public Task<bool> Pulse(Guid id, ClaimsPrincipal user, bool visible, bool recent) => Write(id,user,(db,session,now) => {
        var elapsed=session.LastPulseAtUtc.HasValue ? (now-session.LastPulseAtUtc.Value).TotalSeconds : double.MaxValue;
        if (elapsed < 10 && session.IsVisible == visible && session.HasRecentActivity == recent) return Task.CompletedTask;
        var active=visible && recent;
        session.ActiveSeconds+=SiteActivityPolicy.CreditSeconds(session.LastPulseAtUtc,now,session.IsVisible && session.HasRecentActivity,active);
        session.LastSeenAtUtc=now; session.LastPulseAtUtc=now;
        session.IsVisible=visible; session.HasRecentActivity=recent;
        if(active) session.LastActivityAtUtc=now;
        return Task.CompletedTask;
    });
    public Task<bool> Close(Guid id, ClaimsPrincipal user) => Write(id,user,(db,session,now) => {
        session.ClosedAtUtc=now; session.LastSeenAtUtc=now; session.IsVisible=false; session.HasRecentActivity=false;
        db.SiteActivityEvents.Add(new SiteActivityEvent {SessionId=id,UserId=session.UserId,OccurredAtUtc=now,Kind="Cierre de sesión",Controller="Account",Action="Logout",Title="Cierre de sesión",ResponseStatus=302});
        return Task.CompletedTask;
    });
}
