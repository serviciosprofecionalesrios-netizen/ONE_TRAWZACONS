using System.Globalization;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.ViewModels.Hs;
namespace ITServiceDeskApp.Services;
public sealed class HsCalendarSource(IServiceProvider services, HsAlertInbox inbox)
{
    private ITServiceDeskApp.ViewModels.Operaciones.ControlDocumentosViewModel? _documents;
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
    public static string State(int days) => days <= 0 ? "vencida" : days <= 30 ? "porvencer" : "vigente";
    public static string Tone(int days) => days <= 0 ? "danger" : days <= 15 ? "orange" : days <= 30 ? "warning" : "success";
    public HsOperationalCalendar Read(DateTime today)
    {
        // The controller mutates its view model; never share that instance with the bell.
        if (_documents == null)
        {
            using var controller = ActivatorUtilities.CreateInstance<OperacionesController>(services);
            _documents = controller.ReadHsCalendarDocuments();
        }
        var result = Project(_documents, today);
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
                    Key = $"{id}|{cap.NombreCapacitacion}|{expiry:yyyy-MM-dd}", Date = expiry, AllDay = true,
                    Title = cap.NombreCapacitacion, Training = isTraining ? cap.NombreCapacitacion : "", IsTraining = isTraining, Driver = row.Conductor,
                    DriverId = id, Code = row.MasterCode, Performed = cap.Fecha, Days = days, Status = State(days), Tone = Tone(days),
                    Group = !isTraining ? "Otras actividades H&S" : days <= 0 ? "Capacitaciones vencidas" : days <= 30 ? "Capacitaciones próximas a vencer" : "Vencimientos vigentes",
                    Url = "/Operaciones/ControlDocumentosQrDetalle?key=" + Uri.EscapeDataString(row.Conductor)
                });
            }
        }
        result.Events = result.Events.DistinctBy(x => x.Key).ToList();
        result.Trainings = result.Events.Where(x => x.IsTraining).Select(x => x.Training).Distinct().Order().ToList();
        result.Valid = result.Events.Count(x => x.IsTraining && x.Days > 30);
        result.Due = result.Events.Count(x => x.IsTraining && x.Days > 0 && x.Days <= 30);
        result.Expired = result.Events.Count(x => x.IsTraining && x.Days <= 0);
        result.Alerts = result.Events.Where(x => x.IsTraining && x.Days <= 30).OrderBy(x => x.Date).ToList();
        return result;
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
