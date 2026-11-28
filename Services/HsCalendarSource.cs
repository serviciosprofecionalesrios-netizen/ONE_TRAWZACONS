using System.Globalization;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.ViewModels.Hs;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Services;
public sealed class HsCalendarSource(IServiceProvider services, HsAlertInbox inbox)
{
    private ITServiceDeskApp.ViewModels.Operaciones.ControlDocumentosViewModel? _documents;
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
    public static string State(int days) => days <= 0 ? "vencida" : days <= 30 ? "porvencer" : "vigente";
    public static string Tone(int days) => days <= 0 ? "danger" : days <= 15 ? "orange" : days <= 30 ? "warning" : "success";
    // Documentary expiries have no official site. A filter for activity sites must
    // not silently hide them or change the operational alert counters.
    public static bool MatchesActivitySite(HsOperationalEvent item, string site) => string.IsNullOrEmpty(site) || item.Site == site || (string.IsNullOrEmpty(item.Site) && item.Days.HasValue);
    public HsOperationalCalendar Read(DateTime today)
    {
        // The controller mutates its view model; never share that instance with the bell.
        if (_documents == null)
        {
            using var controller = ActivatorUtilities.CreateInstance<OperacionesController>(services);
            _documents = controller.ReadHsCalendarDocuments();
        }
        var result = Project(_documents, today);
        var context = services.GetRequiredService<ApplicationDbContext>();
        AddRegisteredExpiries(result, context.HsTrainingRecords.AsNoTracking().ToList(), context.HsDocuments.AsNoTracking().ToList(), today);
        inbox.Replace(result.Alerts);
        return result;
    }
    public static HsOperationalCalendar Project(ITServiceDeskApp.ViewModels.Operaciones.ControlDocumentosViewModel documents, DateTime today)
    {
        var rows = documents.Filas.Where(x => x.IsActive == true && x.MasterId.HasValue).DistinctBy(x => x.MasterId).ToList();
        var result = new HsOperationalCalendar { ActiveDrivers = rows.Count, SourceWarning = documents.ErrorMessage };
        foreach (var row in rows)
        {
            var id = row.MasterId!.Value.ToString(CultureInfo.InvariantCulture);
            result.Drivers[id] = row.Conductor;
            result.DriverCodes[id] = row.MasterCode;
            foreach (var cap in row.Capacitaciones.Where(x => x.Aplica))
            {
                var isTraining = OperacionesController.IsCalendarTraining(cap.NombreCapacitacion);
                if (!DateTime.TryParseExact(cap.FechaVencimiento, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry)) continue;
                var days = (expiry.Date - today.Date).Days;
                result.Events.Add(new HsOperationalEvent {
                    Key = $"{id}|{cap.NombreCapacitacion}|{expiry:yyyy-MM-dd}", Date = expiry, CalendarDate = expiry.Date < today.Date ? today.Date : expiry.Date, AllDay = true,
                    Title = cap.NombreCapacitacion, Training = isTraining ? cap.NombreCapacitacion : "", IsTraining = isTraining, Driver = row.Conductor,
                    DriverId = id, Code = row.MasterCode, Performed = cap.Fecha, Days = days, Status = State(days), Tone = Tone(days),
                    Group = !isTraining ? "Documentos" : days <= 0 ? "Capacitaciones vencidas" : days <= 30 ? "Capacitaciones próximas a vencer" : "Vencimientos vigentes",
                    Url = "/Operaciones/ControlDocumentosQrDetalle?key=" + Uri.EscapeDataString(row.Conductor)
                });
            }
        }
        result.Events = result.Events.DistinctBy(x => x.Key).ToList();
        RefreshExpiries(result);
        return result;
    }

    public static void AddRegisteredExpiries(HsOperationalCalendar result, IEnumerable<HsTrainingRecord> trainings, IEnumerable<HsDocument> documents, DateTime today)
    {
        static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();
        // A renewal replaces the previous record for that person/topic or document code.
        var latestTrainings = trainings.Where(x => x.TrainingDateUtc.Date <= today.Date && x.Result == "Aprobado")
            .GroupBy(x => (Person: !string.IsNullOrWhiteSpace(x.EmployeeCode) ? "code:" + Normalize(x.EmployeeCode) : !string.IsNullOrWhiteSpace(x.EmployeeName) ? "name:" + Normalize(x.EmployeeName) : "audience:" + Normalize(x.Audience) + "|" + Normalize(x.Site), Topic: Normalize(x.Topic)))
            .Select(g => g.OrderByDescending(x => x.TrainingDateUtc).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).First());
        foreach (var x in latestTrainings.Where(x => x.ExpirationDateUtc.HasValue))
        {
            var ids = !string.IsNullOrWhiteSpace(x.EmployeeCode)
                ? result.DriverCodes.Where(d => Normalize(d.Value) == Normalize(x.EmployeeCode)).Select(d => d.Key).ToList()
                : result.Drivers.Where(d => Normalize(d.Value) == Normalize(x.EmployeeName)).Select(d => d.Key).ToList();
            Add(new() { Key = $"training-expiry:{x.Id}", Title = x.Topic, Training = x.Topic, IsTraining = true,
                Driver = x.EmployeeName ?? x.Audience, DriverId = ids.Count == 1 ? ids[0] : "", Code = x.EmployeeCode ?? "", Site = x.Site,
                Performed = x.TrainingDateUtc.ToString("dd/MM/yyyy"), Url = $"/Hs/Trainings?recordId={x.Id}" }, x.ExpirationDateUtc!.Value.Date);
        }
        var latestDocuments = documents.GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? $"id:{x.Id}" : Normalize(x.Code))
            .Select(g => g.OrderByDescending(x => x.EffectiveDateUtc).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).First());
        foreach (var x in latestDocuments.Where(x => x.Status != "Obsoleto" && x.ReviewDateUtc.HasValue))
            Add(new() { Key = $"document:{x.Id}", Title = $"{x.Code} · {x.Title}", Site = x.Site, Url = "/Hs/Documents" }, x.ReviewDateUtc!.Value.Date);
        RefreshExpiries(result);

        void Add(HsOperationalEvent item, DateTime expiry)
        {
            item.Date = expiry;
            item.CalendarDate = expiry < today.Date ? today.Date : expiry;
            item.AllDay = true;
            item.Days = (expiry - today.Date).Days;
            item.Status = State(item.Days.Value);
            item.Tone = Tone(item.Days.Value);
            item.Group = !item.IsTraining ? "Documentos" : item.Days <= 0 ? "Capacitaciones vencidas" : item.Days <= 30 ? "Capacitaciones próximas a vencer" : "Vencimientos vigentes";
            result.Events.Add(item);
        }
    }

    private static void RefreshExpiries(HsOperationalCalendar result)
    {
        result.Events = result.Events.DistinctBy(x => x.Key).ToList();
        result.Trainings = result.Events.Where(x => x.IsTraining).Select(x => x.Training).Distinct().Order().ToList();
        result.Valid = result.Events.Count(x => x.Days > 30);
        result.Due = result.Events.Count(x => x.Days > 0 && x.Days <= 30);
        result.Expired = result.Events.Count(x => x.Days <= 0);
        result.Alerts = result.Events.Where(x => x.Days <= 30).OrderBy(x => x.Date).ToList();
    }
}
// The bell uses live alerts, as with ticket SLA notifications. Replacing the snapshot
// removes renewed/deleted/inactive records and cannot accumulate duplicate thresholds.
public sealed class HsAlertInbox
{
    private IReadOnlyList<HsOperationalEvent> _alerts = Array.Empty<HsOperationalEvent>();
    public IReadOnlyList<HsOperationalEvent> Alerts => Volatile.Read(ref _alerts);
    public void Replace(IEnumerable<HsOperationalEvent> alerts) => Interlocked.Exchange(ref _alerts,
        alerts.DistinctBy(x => x.Key).OrderBy(x => x.Date).ToArray());
}
// Live projections match the existing bell, without storing duplicate personnel data.
public sealed class HsExpiryMonitor(IServiceScopeFactory scopes, ILogger<HsExpiryMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var snapshot = scope.ServiceProvider.GetRequiredService<HsCalendarSource>().Read(HsCalendarSource.Local(DateTime.UtcNow).Date);
                logger.LogInformation("H&S: {Count} alertas operativas vigentes", snapshot.Alerts.Count);
            }
            catch (Exception ex) { logger.LogError(ex, "No se pudieron evaluar los vencimientos H&S"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
