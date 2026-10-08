using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDeskApp.Controllers;

[Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral,GestorHS,GerenteHS,SupervisorHS,EndUser")]
public sealed class HsDriverScoreController(IServiceProvider services) : Controller
{
    private OperacionesController Shared()
    {
        var controller = ActivatorUtilities.CreateInstance<OperacionesController>(services);
        controller.ControllerContext = ControllerContext;
        return controller;
    }
    private IActionResult ViewFromOperations(IActionResult result, string name)
    {
        if (result is ViewResult view) view.ViewName = $"~/Views/Operaciones/{name}.cshtml";
        return result;
    }
    [HttpGet]
    public async Task<IActionResult> DriverScore(string? period, DateTime? from, DateTime? to, string? driver, bool refresh = false, CancellationToken cancellationToken = default) =>
        ViewFromOperations(await Shared().DriverScore(period, from, to, driver, refresh, cancellationToken), "DriverScore");
    [HttpGet]
    public async Task<IActionResult> DriverScoreCases(string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null) =>
        ViewFromOperations(await Shared().DriverScoreCases(period, driver, cancellationToken, from, to), "DriverScoreCases");
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveDriverScoreCase(string eventKey, string status, string? responsible, string? notes, IFormFile? evidence, string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null) =>
        Shared().SaveDriverScoreCase(eventKey, status, responsible, notes, evidence, period, driver, cancellationToken, from, to);
    [HttpGet]
    public Task<IActionResult> DriverScorePdf(string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null) => Shared().DriverScorePdf(period, driver, cancellationToken, from, to);
    [HttpGet]
    public Task<IActionResult> DriverScoreEvidence(int id, CancellationToken cancellationToken) => Shared().DriverScoreEvidence(id, cancellationToken);
    [HttpGet]
    public Task<IActionResult> DriverScoreCaseEvidence(string eventKey, CancellationToken cancellationToken) => Shared().DriverScoreCaseEvidence(eventKey, cancellationToken);
}
