using ITServiceDeskApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace ITServiceDeskApp.Security;
public sealed class SiteActivityFilter(SiteActivityRecorder recorder) : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http=context.HttpContext;
        var controller=context.RouteData.Values["controller"]?.ToString() ?? "";
        var action=context.RouteData.Values["action"]?.ToString() ?? "";
        var skip=http.User.Identity?.IsAuthenticated != true || controller == "Account" || (controller == "SiteActivity" && action != "Index");
        var isView=context.Result is ViewResult;
        var isFile=context.Result is FileResult;
        var denied=context.Result is ForbidResult;
        var isWrite=!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method);
        var session=!skip && (isView || isFile || denied || isWrite) ? SiteActivityRecorder.SessionId(http,true) : null;
        var title=context.Result is ViewResult view ? view.ViewData?["Title"]?.ToString() : null;
        var result=await next();
        if(session.HasValue && result.Exception == null)
            await recorder.Record(session.Value,http.User,denied ? "Acceso denegado" : isFile ? "Descarga" : isWrite ? "Envío de formulario" : "Página visitada",controller,action,title ?? controller+" · "+action,http.Response.StatusCode);
    }
}
