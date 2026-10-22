using ITServiceDeskApp.Data;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
[Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SiteActivityController(ApplicationDbContext db, SiteActivityRecorder recorder, TimeProvider clock) : Controller
{
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Pulse(bool visible, bool recentActivity)
    {
        var id=SiteActivityRecorder.SessionId(HttpContext,true);
        if(id.HasValue) await recorder.Pulse(id.Value,User,visible,recentActivity);
        return NoContent();
    }
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from=null,DateTime? to=null,int? userId=null,int userPage=1,int eventPage=1)
    {
        if(!SiteActivityPolicy.CanView(User)) return Forbid();
        return View(await Report(from,to,userId,userPage,eventPage));
    }
    [HttpGet]
    public async Task<IActionResult> Snapshot(DateTime? from=null,DateTime? to=null,int? userId=null,int userPage=1,int eventPage=1)
    {
        if(!SiteActivityPolicy.CanView(User)) return Forbid();
        return PartialView("_Report",await Report(from,to,userId,userPage,eventPage));
    }
    private async Task<SiteActivityViewModel> Report(DateTime? from,DateTime? to,int? userId,int userPage,int eventPage)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var today=now.AddHours(-6).Date;
        var model=new SiteActivityViewModel{GeneratedAtUtc=now,From=from?.Date ?? today.AddDays(-6),To=to?.Date ?? today,UserId=userId};
        if(model.To<model.From || (model.To-model.From).TotalDays>366) {ModelState.AddModelError("","El historial permite rangos válidos de hasta 366 días."); model.From=today.AddDays(-6);model.To=today;}
        model.Choices=await db.Users.AsNoTracking().OrderBy(x=>x.FullName).Select(x=>new SiteActivityUserChoice(x.Id,x.FullName,x.Email)).ToListAsync();
        var cutoff=now.AddSeconds(-90);
        var online=db.SiteActivitySessions.AsNoTracking().Where(x=>x.ClosedAtUtc==null && x.LastSeenAtUtc>=cutoff);
        model.Online=await online.Select(x=>x.UserId).Distinct().CountAsync();
        model.TrackedUsers=await db.SiteActivitySessions.Select(x=>x.UserId).Distinct().CountAsync();
        model.TotalActiveSeconds=await db.SiteActivitySessions.SumAsync(x=>(long?)x.ActiveSeconds) ?? 0;
        var userQuery=db.Users.AsNoTracking().AsQueryable();if(userId.HasValue)userQuery=userQuery.Where(x=>x.Id==userId);
        model.UserPage=new InventoryTablePage(await userQuery.CountAsync(),userPage,25);
        var users=await userQuery.OrderByDescending(x=>online.Any(s=>s.UserId==x.Id)).ThenBy(x=>x.FullName).Skip(model.UserPage.Skip).Take(25).ToListAsync();
        var ids=users.Select(x=>x.Id).ToList();
        var sessions=await db.SiteActivitySessions.AsNoTracking().Where(x=>ids.Contains(x.UserId)).OrderByDescending(x=>x.LastSeenAtUtc).ToListAsync();
        foreach(var user in users)
        {
            var own=sessions.Where(x=>x.UserId==user.Id).ToList();var last=own.FirstOrDefault();
            var connected=own.Where(x=>SiteActivityPolicy.IsOnline(x.ClosedAtUtc,x.LastSeenAtUtc,now)).ToList();
            model.Users.Add(new SiteActivityUserRow {Id=user.Id,Name=user.FullName,Email=user.Email,
                Status=connected.Any(x=>x.IsVisible&&x.HasRecentActivity) ? "En línea" : connected.Count>0 ? "Ausente" : last==null ? "Sin registro" : "Desconectado",
                LastLogin=own.Count==0 ? null : own.Max(x=>x.StartedAtUtc),LastSeen=last?.LastSeenAtUtc,
                LastActivity=own.Where(x=>x.LastActivityAtUtc.HasValue).Select(x=>x.LastActivityAtUtc).Max(),
                ActiveSeconds=own.Sum(x=>x.ActiveSeconds),Sessions=own.Count,
                Page=last==null ? "—" : last.LastController+" / "+last.LastAction});
        }
        var start=model.From.AddHours(6);var end=model.To.AddDays(1).AddHours(6);
        var events=db.SiteActivityEvents.AsNoTracking().Where(x=>x.OccurredAtUtc>=start && x.OccurredAtUtc<end);
        if(userId.HasValue)events=events.Where(x=>x.UserId==userId);
        model.EventCount=await events.CountAsync();model.EventPage=new InventoryTablePage(model.EventCount,eventPage,50);
        model.Events=await events.OrderByDescending(x=>x.OccurredAtUtc).ThenByDescending(x=>x.Id).Skip(model.EventPage.Skip).Take(50).ToListAsync();
        return model;
    }
}
