using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using QRCoder;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class OperacionesController : Controller
    {
        private const decimal LitrosPorGalon = 3.78541m;
        private const string ControlDocumentosOverridesFileName = "ControlDocumentosOverrides.json";
        private const string ControlDocumentosPersonasFileName = "ControlDocumentosPersonas.json";
        private const string ControlDocumentosHistoryFileName = "ControlDocumentosHistory.json";
        private const string ControlDocumentosQrScansFileName = "ControlDocumentosQrScans.json";
        private const string ControlDocumentosAlertDispatchFileName = "ControlDocumentosAlertDispatch.json";
        private const string OperacionesMetasProduccionMensualFileName = "OperacionesMetasProduccionMensual.json";
        private const string OperacionesReportesAuditFileName = "OperacionesReportesAudit.json";
        private const string OperacionesReportesScheduleFileName = "OperacionesReportesSchedule.json";
        private const string SeguimientoDieselUnitExtrasFileName = "SeguimientoDieselUnitExtras.json";
        private const decimal DefaultMetaMensualTriton = 7500m;
        private const decimal DefaultMetaMensualPavonAsm = 0m;
        private const decimal DefaultMetaDiariaTritonObjetivo = 500m;
        private const decimal DefaultMetaDiariaPavonAsmObjetivo = 0m;
        private const decimal DefaultMetaDiariaObjetivo = DefaultMetaDiariaTritonObjetivo;
        private const decimal DieselObjetivoKmPorGalon = 8m;
        private const decimal DieselDistanciaPromedioKmPorEvento = 520m;
        private const decimal DieselObjetivoGalonesPorEvento = DieselDistanciaPromedioKmPorEvento / DieselObjetivoKmPorGalon;
        private const int ControlDocumentosHistoryMaxEntries = 5000;
        private const int ControlDocumentosQrScansMaxEntries = 8000;
        private const double IngresoPersonalSlaHorasLaborales = 48d;
        private const double IngresoPersonalSlaHorasPorVencer = 8d;
        private static readonly CultureInfo EsCulture = CultureInfo.GetCultureInfo("es-ES");
        private static readonly TimeSpan IngresoPersonalHorarioInicio = new(7, 30, 0);
        private static readonly TimeSpan IngresoPersonalHorarioFin = new(17, 0, 0);
        private static readonly int[] ControlDocumentosAlertThresholdDays = new[] { 30, 15, 7, 1 };
        private static readonly string[] ControlDocumentoDateFormats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"
        };
        private static readonly Dictionary<string, int> ControlDocumentosValidezDiasByHeader = new(StringComparer.Ordinal)
        {
            [NormalizeHeader("Inducción General de Sitio")] = 365,
            [NormalizeHeader("IPER")] = 730,
            [NormalizeHeader("Manejo de Residuos Sólidos")] = 730,
            [NormalizeHeader("Inducción Sistema de Gestión Social")] = 730,
            [NormalizeHeader("Gestión de Fatiga")] = 730,
            [NormalizeHeader("Sistema 5 puntos")] = 730,
            [NormalizeHeader("Respuesta ante emergencias")] = 730,
            [NormalizeHeader("Primeros Auxilios Teórico")] = 365,
            [NormalizeHeader("Primeros Auxilios Práctico")] = 365,
            [NormalizeHeader("Prevención de Incendios")] = 730,
            [NormalizeHeader("Operación de Traslado Mina a Mina")] = 730,
            [NormalizeHeader("Uso de Formatos de Transporte")] = 730,
            [NormalizeHeader("Educación Vial")] = 730,
            [NormalizeHeader("Manejo Defensivo")] = 730
        };
        private readonly IOperacionesDashboardStore _operacionesDashboardStore;
        private readonly IOperacionesSeguimientoStore _operacionesSeguimientoStore;
        private readonly IOperacionesIngresoPersonalTraceStore _operacionesIngresoPersonalTraceStore;
        private readonly IOperacionesIngresoPersonalRequestStore _operacionesIngresoPersonalRequestStore;
        private readonly IOperacionesIngresoEquipoGondolaTraceStore _operacionesIngresoEquipoGondolaTraceStore;
        private readonly IOperacionesIngresoEquipoGondolaRequestStore _operacionesIngresoEquipoGondolaRequestStore;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OperacionesController> _logger;
        private readonly IWebHostEnvironment _environment;

        public OperacionesController(
            IOperacionesDashboardStore operacionesDashboardStore,
            IOperacionesSeguimientoStore operacionesSeguimientoStore,
            IOperacionesIngresoPersonalTraceStore operacionesIngresoPersonalTraceStore,
            IOperacionesIngresoPersonalRequestStore operacionesIngresoPersonalRequestStore,
            IOperacionesIngresoEquipoGondolaTraceStore operacionesIngresoEquipoGondolaTraceStore,
            IOperacionesIngresoEquipoGondolaRequestStore operacionesIngresoEquipoGondolaRequestStore,
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<OperacionesController> logger,
            IWebHostEnvironment environment)
        {
            _operacionesDashboardStore = operacionesDashboardStore;
            _operacionesSeguimientoStore = operacionesSeguimientoStore;
            _operacionesIngresoPersonalTraceStore = operacionesIngresoPersonalTraceStore;
            _operacionesIngresoPersonalRequestStore = operacionesIngresoPersonalRequestStore;
            _operacionesIngresoEquipoGondolaTraceStore = operacionesIngresoEquipoGondolaTraceStore;
            _operacionesIngresoEquipoGondolaRequestStore = operacionesIngresoEquipoGondolaRequestStore;
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _environment = environment;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult CalendarioOperativo(
            int? year,
            int? month,
            string? sitio,
            string? turno,
            string? estado,
            string? equipo,
            string? conductor,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            bool soloConflictos = false)
        {
            var seguimiento = _operacionesSeguimientoStore.Get();
            var model = BuildCalendarioOperativoModel(
                seguimiento,
                year,
                month,
                sitio,
                turno,
                estado,
                equipo,
                conductor,
                fechaDesde,
                fechaHasta,
                soloConflictos);
            return View(model);
        }

        [HttpGet]
        public IActionResult SeguimientoToneladas()
        {
            var storedSeguimiento = _operacionesSeguimientoStore.Get();
            if (storedSeguimiento is not null)
            {
                ApplyMetasProduccionMensual(storedSeguimiento);
                return View(storedSeguimiento);
            }

            var sampleModel = CreateSampleToneladasModel();
            ApplyMetasProduccionMensual(sampleModel);
            return View(sampleModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeguimientoToneladas(IFormFile? excelFile)
        {
            var fallbackModel = CreateSampleToneladasModel();
            ApplyMetasProduccionMensual(fallbackModel);

            if (excelFile is null || excelFile.Length == 0)
            {
                fallbackModel.ErrorMessage = "Selecciona un archivo Excel (.xlsx) para cargar la informacion.";
                return View(fallbackModel);
            }

            var extension = Path.GetExtension(excelFile.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                fallbackModel.ErrorMessage = "Formato no permitido. Solo se aceptan archivos .xlsx.";
                return View(fallbackModel);
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await excelFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage(memoryStream);
                if (package.Workbook.Worksheets.Count == 0)
                {
                    fallbackModel.ErrorMessage = "El archivo no contiene hojas para procesar.";
                    return View(fallbackModel);
                }

                var rows = ParseToneladasRowsFromWorkbook(package.Workbook, out var parseError, out var sourceSheetName);
                if (!string.IsNullOrWhiteSpace(parseError))
                {
                    fallbackModel.ErrorMessage = parseError;
                    return View(fallbackModel);
                }

                if (!rows.Any())
                {
                    fallbackModel.ErrorMessage = "No se encontraron filas validas con Equipo y Toneladas en el Excel.";
                    return View(fallbackModel);
                }

                var model = BuildToneladasModel(rows);
                model.IsFromUpload = true;
                model.SourceFileName = string.IsNullOrWhiteSpace(sourceSheetName)
                    ? excelFile.FileName
                    : $"{excelFile.FileName} (Hoja: {sourceSheetName})";

                _operacionesSeguimientoStore.Save(model);
                var consolidatedModel = _operacionesSeguimientoStore.Get() ?? model;
                ApplyMetasProduccionMensual(consolidatedModel);

                return View(consolidatedModel);
            }
            catch
            {
                fallbackModel.ErrorMessage = "No se pudo procesar el archivo Excel. Verifica la estructura y vuelve a intentar.";
                return View(fallbackModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarMetaProduccionMensual(
            int metaAnio,
            int metaMes,
            decimal metaMensualTriton,
            decimal metaMensualPavonAsm,
            decimal metaDiariaTritonObjetivo,
            decimal metaDiariaPavonAsmObjetivo,
            bool confirmarCambioMeta = false)
        {
            // Seguimiento de toneladas opera solo para TRITON.
            metaMensualPavonAsm = 0m;
            metaDiariaPavonAsmObjetivo = 0m;

            if (!confirmarCambioMeta)
            {
                TempData["SeguimientoToneladasMetaError"] = "Marca la casilla de confirmacion para actualizar la meta operativa.";
                return RedirectToAction(nameof(SeguimientoToneladas));
            }

            if (metaAnio < 2020 || metaAnio > 2100 || metaMes < 1 || metaMes > 12)
            {
                TempData["SeguimientoToneladasMetaError"] = "El periodo de meta mensual no es valido.";
                return RedirectToAction(nameof(SeguimientoToneladas));
            }

            if (metaMensualTriton < 0m)
            {
                TempData["SeguimientoToneladasMetaError"] = "La meta mensual de TRITON no puede ser negativa.";
                return RedirectToAction(nameof(SeguimientoToneladas));
            }

            if (metaDiariaTritonObjetivo <= 0m)
            {
                TempData["SeguimientoToneladasMetaError"] = "Debes indicar una meta diaria mayor que cero para TRITON.";
                return RedirectToAction(nameof(SeguimientoToneladas));
            }

            var metaDiariaObjetivo = metaDiariaTritonObjetivo;

            var totalMeta = metaMensualTriton;
            if (totalMeta <= 0m)
            {
                TempData["SeguimientoToneladasMetaError"] = "La meta mensual de TRITON debe ser mayor que cero.";
                return RedirectToAction(nameof(SeguimientoToneladas));
            }

            try
            {
                var entries = LoadOperacionesMetasProduccionMensual();
                var existing = entries.FirstOrDefault(x => x.Year == metaAnio && x.Month == metaMes);
                if (existing is null)
                {
                    entries.Add(new OperacionesMetaProduccionMensualEntry
                    {
                        Year = metaAnio,
                        Month = metaMes,
                        MetaMensualTriton = Math.Round(metaMensualTriton, 1, MidpointRounding.AwayFromZero),
                        MetaMensualPavonAsm = Math.Round(metaMensualPavonAsm, 1, MidpointRounding.AwayFromZero),
                        MetaDiariaTritonObjetivo = Math.Round(metaDiariaTritonObjetivo, 1, MidpointRounding.AwayFromZero),
                        MetaDiariaPavonAsmObjetivo = Math.Round(metaDiariaPavonAsmObjetivo, 1, MidpointRounding.AwayFromZero),
                        MetaDiariaObjetivo = Math.Round(metaDiariaObjetivo, 1, MidpointRounding.AwayFromZero),
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "Sistema"
                    });
                }
                else
                {
                    existing.MetaMensualTriton = Math.Round(metaMensualTriton, 1, MidpointRounding.AwayFromZero);
                    existing.MetaMensualPavonAsm = Math.Round(metaMensualPavonAsm, 1, MidpointRounding.AwayFromZero);
                    existing.MetaDiariaTritonObjetivo = Math.Round(metaDiariaTritonObjetivo, 1, MidpointRounding.AwayFromZero);
                    existing.MetaDiariaPavonAsmObjetivo = Math.Round(metaDiariaPavonAsmObjetivo, 1, MidpointRounding.AwayFromZero);
                    existing.MetaDiariaObjetivo = Math.Round(metaDiariaObjetivo, 1, MidpointRounding.AwayFromZero);
                    existing.UpdatedAt = DateTime.Now;
                    existing.UpdatedBy = User.Identity?.Name ?? "Sistema";
                }

                SaveOperacionesMetasProduccionMensual(entries);

                TempData["SeguimientoToneladasMetaOk"] =
                    $"Meta actualizada para {metaMes:00}/{metaAnio}: TRITON {metaMensualTriton:N1} Tn (diaria {metaDiariaTritonObjetivo:N1}).";
            }
            catch
            {
                TempData["SeguimientoToneladasMetaError"] = "No se pudo guardar la meta operativa. Intenta nuevamente.";
            }

            return RedirectToAction(nameof(SeguimientoToneladas));
        }

        public IActionResult SeguimientoDiesel()
        {
            var storedSeguimiento = _operacionesSeguimientoStore.Get();
            if (storedSeguimiento is not null)
            {
                return View(BuildDieselModelFromSeguimiento(storedSeguimiento));
            }

            return View(new SeguimientoDieselViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarAjusteSeguimientoDiesel(
            string? unidad,
            string? galonesExtra,
            string? kilometrosExtra)
        {
            var unidadKey = NormalizeDieselUnidadKey(unidad);
            if (string.IsNullOrWhiteSpace(unidadKey))
            {
                TempData["SeguimientoDieselAjusteError"] = "Selecciona una unidad valida para guardar extras.";
                return RedirectToAction(nameof(SeguimientoDiesel));
            }

            var parsedGalonesExtra = ParseNonNegativeDecimal(galonesExtra);
            var parsedKilometrosExtra = ParseNonNegativeDecimal(kilometrosExtra);

            try
            {
                var ajustes = LoadSeguimientoDieselUnitExtras();
                var existing = ajustes.FirstOrDefault(x =>
                    string.Equals(NormalizeDieselUnidadKey(x.Unidad), unidadKey, StringComparison.OrdinalIgnoreCase));

                if (parsedGalonesExtra <= 0m && parsedKilometrosExtra <= 0m)
                {
                    if (existing is not null)
                    {
                        ajustes.Remove(existing);
                        SaveSeguimientoDieselUnitExtras(ajustes);
                    }

                    TempData["SeguimientoDieselAjusteOk"] = $"Se limpiaron los extras de {unidadKey}.";
                    return RedirectToAction(nameof(SeguimientoDiesel));
                }

                if (existing is null)
                {
                    ajustes.Add(new DieselUnitExtraRecord
                    {
                        Unidad = unidadKey,
                        GalonesExtra = parsedGalonesExtra,
                        KilometrosExtra = parsedKilometrosExtra,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "Sistema"
                    });
                }
                else
                {
                    existing.Unidad = unidadKey;
                    existing.GalonesExtra = parsedGalonesExtra;
                    existing.KilometrosExtra = parsedKilometrosExtra;
                    existing.UpdatedAt = DateTime.Now;
                    existing.UpdatedBy = User.Identity?.Name ?? "Sistema";
                }

                SaveSeguimientoDieselUnitExtras(ajustes);
                TempData["SeguimientoDieselAjusteOk"] =
                    $"Extras guardados para {unidadKey}: +{parsedGalonesExtra:N2} gal y +{parsedKilometrosExtra:N2} km.";
            }
            catch
            {
                TempData["SeguimientoDieselAjusteError"] =
                    "No se pudieron guardar los extras de diesel. Intenta nuevamente.";
            }

            return RedirectToAction(nameof(SeguimientoDiesel));
        }

        public IActionResult Dashboard()
        {
            var seguimientoPersistido = _operacionesSeguimientoStore.Get();
            if (seguimientoPersistido is not null)
            {
                return View(BuildDashboardModelFromSeguimiento(seguimientoPersistido));
            }

            var sampleSeguimiento = CreateSampleToneladasModel();
            return View(BuildDashboardModelFromSeguimiento(sampleSeguimiento));
        }

        public IActionResult Reportes(
            string? fechaDesde,
            string? fechaHasta,
            string? sitio,
            string? turno,
            string? conductor,
            string? equipo,
            string? estado)
        {
            var model = BuildReportesModel(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProgramarReporteOperacion(
            string? reporte,
            string? frecuencia,
            string? hora,
            string? diaSemana,
            string? destinatarios)
        {
            var reporteKey = (reporte ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reporteKey))
            {
                TempData["ReportesError"] = "Selecciona el reporte a programar.";
                return RedirectToAction(nameof(Reportes));
            }

            var frecuenciaSafe = NormalizeReportFilterOption(frecuencia, "DIARIA", new[] { "DIARIA", "SEMANAL" });
            var horaSafe = (hora ?? string.Empty).Trim();
            if (!TimeSpan.TryParse(horaSafe, CultureInfo.InvariantCulture, out var parsedHora))
            {
                TempData["ReportesError"] = "Debes indicar una hora valida (HH:mm).";
                return RedirectToAction(nameof(Reportes));
            }

            var diaSemanaSafe = (diaSemana ?? "MONDAY").Trim().ToUpperInvariant();
            var allowedDays = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };
            if (!allowedDays.Contains(diaSemanaSafe, StringComparer.Ordinal))
            {
                diaSemanaSafe = "MONDAY";
            }

            var destinatariosSafe = (destinatarios ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(destinatariosSafe))
            {
                TempData["ReportesError"] = "Debes indicar al menos un correo destino.";
                return RedirectToAction(nameof(Reportes));
            }

            var schedules = LoadOperacionesReportesProgramaciones();
            var entry = new OperacionesReporteScheduleEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                ReportKey = reporteKey,
                Frecuencia = frecuenciaSafe,
                Hora = parsedHora.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                DiaSemana = diaSemanaSafe,
                Destinatarios = destinatariosSafe,
                IsActive = true,
                CreatedAt = DateTime.Now,
                LastRunAt = null,
                NextRunAt = ComputeNextRun(DateTime.Now, frecuenciaSafe, diaSemanaSafe, parsedHora)
            };
            schedules.Add(entry);
            SaveOperacionesReportesProgramaciones(schedules);
            TempData["ReportesOk"] = "Programacion guardada. Queda lista para ejecucion automatizada.";
            return RedirectToAction(nameof(Reportes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleProgramacionReporteOperacion(string id)
        {
            var key = (id ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["ReportesError"] = "No se recibio la programacion a actualizar.";
                return RedirectToAction(nameof(Reportes));
            }

            var schedules = LoadOperacionesReportesProgramaciones();
            var target = schedules.FirstOrDefault(x => string.Equals(x.Id, key, StringComparison.Ordinal));
            if (target is null)
            {
                TempData["ReportesError"] = "No se encontro la programacion.";
                return RedirectToAction(nameof(Reportes));
            }

            target.IsActive = !target.IsActive;
            if (target.IsActive)
            {
                if (TimeSpan.TryParse(target.Hora, CultureInfo.InvariantCulture, out var hora))
                {
                    target.NextRunAt = ComputeNextRun(DateTime.Now, target.Frecuencia, target.DiaSemana, hora);
                }
            }
            else
            {
                target.NextRunAt = null;
            }

            SaveOperacionesReportesProgramaciones(schedules);
            TempData["ReportesOk"] = target.IsActive
                ? "Programacion activada."
                : "Programacion pausada.";
            return RedirectToAction(nameof(Reportes));
        }

        [HttpGet]
        public async Task<IActionResult> ControlDocumentos()
        {
            var model = BuildControlDocumentosModel();
            model.AlertasResumen = await EjecutarAlertasControlDocumentosAsync(model);
            return View(model);
        }

        [HttpGet]
        public IActionResult ControlDocumentosQrCard(string conductor)
        {
            var conductorKey = NormalizeControlDocumentoKey(conductor);
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return BadRequest("Debes indicar un conductor valido.");
            }

            var model = BuildControlDocumentosQrModel(conductorKey);
            if (model is null)
            {
                return NotFound("No se encontro el conductor solicitado.");
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ControlDocumentosQrDetalle(string key)
        {
            var conductorKey = NormalizeControlDocumentoKey(key);
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return BadRequest("Debes indicar un codigo valido.");
            }

            RegistrarEscaneoQr(conductorKey);
            var model = BuildControlDocumentosQrModel(conductorKey);
            if (model is null)
            {
                return NotFound("No se encontro el conductor solicitado.");
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ControlDocumentosQrImage(string key)
        {
            var conductorKey = NormalizeControlDocumentoKey(key);
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return BadRequest("Debes indicar un codigo valido.");
            }

            var detailUrl = Url.Action(
                nameof(ControlDocumentosQrDetalle),
                "Operaciones",
                new { key = conductorKey },
                Request.Scheme);

            if (string.IsNullOrWhiteSpace(detailUrl))
            {
                return NotFound("No se pudo generar la URL del QR.");
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(detailUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            var bytes = qrCode.GetGraphic(14);

            return File(bytes, "image/png");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarControlDocumentos(ControlDocumentosUpdateInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Conductor))
            {
                TempData["ControlDocumentosError"] = "No se recibio el conductor para actualizar.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            if (input.Encabezados.Count != input.Fechas.Count)
            {
                TempData["ControlDocumentosError"] = "Los datos recibidos para edicion no son consistentes.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            try
            {
                var overrides = LoadControlDocumentosOverrides();
                var historial = LoadControlDocumentosHistory();
                var conductorKey = NormalizeControlDocumentoKey(input.Conductor);
                var supervisor = (input.SupervisorNombre ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(conductorKey))
                {
                    TempData["ControlDocumentosError"] = "El nombre del conductor no es valido.";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                var baseByCapacitacion = LoadControlDocumentosBaseDatesByConductor(conductorKey);
                var conductorOverrides = overrides.TryGetValue(conductorKey, out var current)
                    ? current
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var cambiosPendientes = new List<(string Encabezado, string FechaNueva, string ValorAnterior, string ValorNuevo, bool EsCritico)>();

                for (var i = 0; i < input.Encabezados.Count; i++)
                {
                    var encabezado = input.Encabezados[i]?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(encabezado))
                    {
                        continue;
                    }

                    var baseFecha = baseByCapacitacion.TryGetValue(encabezado, out var baseValue)
                        ? baseValue
                        : "-";
                    var oldVisibleValue = ResolveControlDocumentoVisibleValue(baseFecha, conductorOverrides, encabezado);
                    var estadoAnterior = BuildControlDocumentoCapacitacionEstado(encabezado, oldVisibleValue);
                    var rawNuevaFecha = input.Fechas[i];
                    var fechaEditada = string.Empty;

                    if (!string.IsNullOrWhiteSpace(rawNuevaFecha))
                    {
                        if (!TryValidateAndFormatControlDocumentoDateFromInput(rawNuevaFecha, out fechaEditada, out var validationError))
                        {
                            TempData["ControlDocumentosError"] = $"{encabezado}: {validationError}";
                            return RedirectToAction(nameof(ControlDocumentos));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(fechaEditada))
                    {
                        conductorOverrides.Remove(encabezado);
                    }
                    else
                    {
                        conductorOverrides[encabezado] = fechaEditada;
                    }
                    var newVisibleValue = ResolveControlDocumentoVisibleValue(baseFecha, conductorOverrides, encabezado);
                    if (!string.Equals(oldVisibleValue, newVisibleValue, StringComparison.Ordinal))
                    {
                        var esCritico = IsEstadoControlDocumentoCritico(estadoAnterior.EstadoFiltro);
                        cambiosPendientes.Add((encabezado, fechaEditada, oldVisibleValue, newVisibleValue, esCritico));
                    }
                }

                if (cambiosPendientes.Count == 0)
                {
                    TempData["ControlDocumentosOk"] = "No hubo cambios para guardar.";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                var hayCambiosCriticos = cambiosPendientes.Any(x => x.EsCritico);
                if (hayCambiosCriticos &&
                    (!input.ConfirmacionSupervisor || string.IsNullOrWhiteSpace(supervisor)))
                {
                    TempData["ControlDocumentosError"] = "Para actualizar un registro critico debes indicar supervisor y confirmar la aprobacion.";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                foreach (var cambio in cambiosPendientes)
                {
                    if (string.IsNullOrWhiteSpace(cambio.FechaNueva))
                    {
                        conductorOverrides.Remove(cambio.Encabezado);
                    }
                    else
                    {
                        conductorOverrides[cambio.Encabezado] = cambio.FechaNueva;
                    }

                    historial.Add(new ControlDocumentosHistoryEntryViewModel
                    {
                        FechaCambio = DateTime.Now,
                        Usuario = User.Identity?.Name ?? "Sistema",
                        Supervisor = supervisor,
                        Conductor = input.Conductor.Trim(),
                        Capacitacion = cambio.Encabezado,
                        ValorAnterior = cambio.ValorAnterior,
                        ValorNuevo = cambio.ValorNuevo,
                        RequiereAprobacionCritica = cambio.EsCritico
                    });
                }

                if (conductorOverrides.Count == 0)
                {
                    overrides.Remove(conductorKey);
                }
                else
                {
                    overrides[conductorKey] = conductorOverrides;
                }

                SaveControlDocumentosOverrides(overrides);
                SaveControlDocumentosHistory(historial);
                TempData["ControlDocumentosOk"] = $"Capacitaciones actualizadas para {input.Conductor}.";
            }
            catch
            {
                TempData["ControlDocumentosError"] = "No se pudieron guardar los cambios del conductor. Intenta nuevamente.";
            }

            return RedirectToAction(nameof(ControlDocumentos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarControlDocumentosMasivo(ControlDocumentosBulkUpdateInput input)
        {
            var encabezado = (input.Capacitacion ?? string.Empty).Trim();
            var supervisor = (input.SupervisorNombre ?? string.Empty).Trim();
            if (!TryValidateAndFormatControlDocumentoDateFromInput(input.Fecha, out var fechaEditada, out var validationError))
            {
                TempData["ControlDocumentosError"] = validationError;
                return RedirectToAction(nameof(ControlDocumentos));
            }
            var conductores = input.Conductores
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(encabezado))
            {
                TempData["ControlDocumentosError"] = "Selecciona una capacitacion para la accion masiva.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            if (string.IsNullOrWhiteSpace(fechaEditada))
            {
                TempData["ControlDocumentosError"] = "Selecciona una fecha valida para la accion masiva.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            if (conductores.Count == 0)
            {
                TempData["ControlDocumentosError"] = "No hay conductores seleccionados para la accion masiva.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            try
            {
                var overrides = LoadControlDocumentosOverrides();
                var historial = LoadControlDocumentosHistory();
                var afectados = 0;
                var cambiosCriticos = false;
                var cambiosPendientes = new List<(string Conductor, string ConductorKey, string Encabezado, string ValorAnterior, string ValorNuevo, bool EsCritico)>();

                foreach (var conductor in conductores)
                {
                    var conductorKey = NormalizeControlDocumentoKey(conductor);
                    if (string.IsNullOrWhiteSpace(conductorKey))
                    {
                        continue;
                    }

                    var baseByCapacitacion = LoadControlDocumentosBaseDatesByConductor(conductorKey);
                    var conductorOverrides = overrides.TryGetValue(conductorKey, out var current)
                        ? current
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    var baseFecha = baseByCapacitacion.TryGetValue(encabezado, out var baseValue)
                        ? baseValue
                        : "-";
                    var oldVisibleValue = ResolveControlDocumentoVisibleValue(baseFecha, conductorOverrides, encabezado);
                    var estadoAnterior = BuildControlDocumentoCapacitacionEstado(encabezado, oldVisibleValue);

                    conductorOverrides[encabezado] = fechaEditada;
                    var newVisibleValue = ResolveControlDocumentoVisibleValue(baseFecha, conductorOverrides, encabezado);

                    if (!string.Equals(oldVisibleValue, newVisibleValue, StringComparison.Ordinal))
                    {
                        var esCritico = IsEstadoControlDocumentoCritico(estadoAnterior.EstadoFiltro);
                        cambiosPendientes.Add((conductor, conductorKey, encabezado, oldVisibleValue, newVisibleValue, esCritico));
                        if (esCritico)
                        {
                            cambiosCriticos = true;
                        }
                    }

                    overrides[conductorKey] = conductorOverrides;
                }

                if (cambiosPendientes.Count == 0)
                {
                    TempData["ControlDocumentosOk"] = "No hubo cambios en la accion masiva.";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                if (cambiosCriticos &&
                    (!input.ConfirmacionSupervisor || string.IsNullOrWhiteSpace(supervisor)))
                {
                    TempData["ControlDocumentosError"] = "La accion masiva afecta registros criticos: debes indicar supervisor y confirmar la aprobacion.";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                foreach (var cambio in cambiosPendientes)
                {
                    var byConductor = overrides.TryGetValue(cambio.ConductorKey, out var existing)
                        ? existing
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    byConductor[cambio.Encabezado] = fechaEditada;
                    overrides[cambio.ConductorKey] = byConductor;

                    historial.Add(new ControlDocumentosHistoryEntryViewModel
                    {
                        FechaCambio = DateTime.Now,
                        Usuario = User.Identity?.Name ?? "Sistema",
                        Supervisor = supervisor,
                        Conductor = cambio.Conductor,
                        Capacitacion = cambio.Encabezado,
                        ValorAnterior = cambio.ValorAnterior,
                        ValorNuevo = cambio.ValorNuevo,
                        RequiereAprobacionCritica = cambio.EsCritico
                    });
                    afectados++;
                }

                SaveControlDocumentosOverrides(overrides);
                SaveControlDocumentosHistory(historial);
                TempData["ControlDocumentosOk"] = afectados > 0
                    ? $"Accion masiva aplicada a {afectados} conductor(es) para {encabezado}."
                    : "No hubo cambios en la accion masiva.";
            }
            catch
            {
                TempData["ControlDocumentosError"] = "No se pudo completar la accion masiva. Intenta nuevamente.";
            }

            return RedirectToAction(nameof(ControlDocumentos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearControlDocumentosRegistro(ControlDocumentosNuevoRegistroInput input)
        {
            var tipoRegistro = (input.TipoRegistro ?? string.Empty).Trim().ToLowerInvariant();
            if (tipoRegistro == "persona")
            {
                return CrearControlDocumentosPersona(input);
            }

            if (tipoRegistro == "camion")
            {
                return CrearControlDocumentosEquipoGondola(input);
            }

            TempData["ControlDocumentosError"] = "Selecciona si el nuevo registro es de Persona o de Camion / Gondola.";
            return RedirectToAction(nameof(ControlDocumentos));
        }

        private IActionResult CrearControlDocumentosPersona(ControlDocumentosNuevoRegistroInput input)
        {
            var nombre = (input.PersonaNombre ?? string.Empty).Trim();
            var personaKey = NormalizeControlDocumentoKey(nombre);
            if (string.IsNullOrWhiteSpace(personaKey))
            {
                TempData["ControlDocumentosError"] = "Indica el nombre de la persona.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            if (input.PersonaEncabezados.Count != input.PersonaFechas.Count)
            {
                TempData["ControlDocumentosError"] = "Las fechas de documentos de la persona no son consistentes.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var fechas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < input.PersonaEncabezados.Count; i++)
            {
                var encabezado = (input.PersonaEncabezados[i] ?? string.Empty).Trim();
                var fechaRaw = input.PersonaFechas[i];
                if (string.IsNullOrWhiteSpace(encabezado) || string.IsNullOrWhiteSpace(fechaRaw))
                {
                    continue;
                }

                if (!TryValidateAndFormatControlDocumentoDateFromInput(fechaRaw, out var fecha, out var error))
                {
                    TempData["ControlDocumentosError"] = $"{encabezado}: {error}";
                    return RedirectToAction(nameof(ControlDocumentos));
                }

                if (!string.IsNullOrWhiteSpace(fecha))
                {
                    fechas[encabezado] = fecha;
                }
            }

            const string tipoSolicitud = "Control documentos H&S - Persona";
            var traceResult = _operacionesIngresoPersonalTraceStore.ResolveOrCreate(tipoSolicitud, nombre);
            if (!traceResult.Success)
            {
                TempData["ControlDocumentosError"] = traceResult.Message;
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var payload = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["tipo"] = new[] { tipoSolicitud },
                ["asunto"] = new[] { nombre },
                ["nombres_apellidos"] = new[] { nombre },
                ["tarea_padre"] = new[] { traceResult.TareaPadre },
                ["estado"] = new[] { "Registrado" },
                ["prioridad"] = new[] { "Media" }
            };

            foreach (var fecha in fechas)
            {
                payload[$"control_documento_{NormalizeHeader(fecha.Key)}"] = new[] { fecha.Value };
            }

            var existingRequestId = _context.OperacionesIngresoPersonalSolicitudes
                .AsNoTracking()
                .Where(x => x.Tipo == tipoSolicitud && x.SolicitanteNombre == nombre)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

            var saveResult = _operacionesIngresoPersonalRequestStore.Save(new OperacionesIngresoPersonalRequestSaveInput
            {
                Id = existingRequestId,
                Tipo = tipoSolicitud,
                Asunto = nombre,
                SolicitanteNombre = nombre,
                TareaPadre = traceResult.TareaPadre,
                Estado = "Registrado",
                Prioridad = "Media",
                Payload = payload
            });

            if (!saveResult.Success)
            {
                TempData["ControlDocumentosError"] = $"No se pudo guardar la persona en la base de datos: {saveResult.Message}";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var personas = LoadControlDocumentosPersonas();
            var existing = personas.FirstOrDefault(x =>
                string.Equals(NormalizeControlDocumentoKey(x.Nombre), personaKey, StringComparison.Ordinal));
            var now = DateTime.Now;
            if (existing is null)
            {
                personas.Add(new ControlDocumentosPersonaRecord
                {
                    Nombre = nombre,
                    Fechas = fechas,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.Nombre = nombre;
                existing.Fechas = fechas;
                existing.UpdatedAt = now;
            }

            SaveControlDocumentosPersonas(personas);
            TempData["ControlDocumentosOk"] =
                $"Registro de persona guardado en la base de datos. ID: {saveResult.Id}. Tarea padre: {traceResult.TareaPadre}.";
            return RedirectToAction(nameof(ControlDocumentos));
        }

        private IActionResult CrearControlDocumentosEquipoGondola(ControlDocumentosNuevoRegistroInput input)
        {
            var tipoIngreso = (input.EquipoTipoIngreso ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tipoIngreso))
            {
                tipoIngreso = string.IsNullOrWhiteSpace(input.EquipoPlacaCabezal) ? "Ingreso de Gondola" : "Ingreso de Equipo";
            }

            if (!string.Equals(tipoIngreso, "Ingreso de Equipo", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(tipoIngreso, "Ingreso de Gondola", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ControlDocumentosError"] = "Selecciona si el registro es Ingreso de Equipo o Ingreso de Gondola.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var asunto = (input.EquipoAsunto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(asunto))
            {
                asunto = string.Equals(tipoIngreso, "Ingreso de Equipo", StringComparison.OrdinalIgnoreCase)
                    ? FirstNonEmpty(input.EquipoCodigoCabezal, input.EquipoPlacaCabezal, input.EquipoPlacaGondola)
                    : FirstNonEmpty(input.EquipoPlacaGondola, input.EquipoPlacaCabezal);
            }

            if (string.IsNullOrWhiteSpace(asunto))
            {
                TempData["ControlDocumentosError"] = "Indica al menos el asunto, codigo de cabezal, placa de cabezal o placa de gondola.";
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var traceResult = _operacionesIngresoEquipoGondolaTraceStore.ResolveOrCreate(tipoIngreso, asunto);
            if (!traceResult.Success)
            {
                TempData["ControlDocumentosError"] = traceResult.Message;
                return RedirectToAction(nameof(ControlDocumentos));
            }

            var isEquipo = string.Equals(tipoIngreso, "Ingreso de Equipo", StringComparison.OrdinalIgnoreCase);
            var payload = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["tipo_ingreso"] = new[] { tipoIngreso },
                ["asunto"] = new[] { asunto },
                ["tarea_padre"] = new[] { traceResult.TareaPadre }
            };

            if (isEquipo)
            {
                payload["equipo_estado"] = new[] { "New" };
                payload["equipo_prioridad"] = new[] { "Media" };
                payload["equipo_tarea_padre"] = new[] { traceResult.TareaPadre };
                payload["equipo_empresa"] = new[] { input.EquipoEmpresa ?? string.Empty };
                payload["equipo_codigo_cabezal"] = new[] { input.EquipoCodigoCabezal ?? string.Empty };
                payload["equipo_placa_cabezal"] = new[] { input.EquipoPlacaCabezal ?? string.Empty };
                payload["equipo_placa_gondola"] = new[] { input.EquipoPlacaGondola ?? string.Empty };
                payload["equipo_tenencia"] = new[] { input.EquipoTenencia ?? string.Empty };
                payload["equipo_propietario"] = new[] { input.EquipoPropietario ?? string.Empty };
                payload["equipo_vencimiento_emision_gases"] = new[] { input.EquipoVencimientoEmisionGases ?? string.Empty };
                payload["equipo_vencimiento_inspeccion_mecanica"] = new[] { input.EquipoVencimientoInspeccionMecanica ?? string.Empty };
                payload["equipo_vencimiento_seguro_vehicular"] = new[] { input.EquipoVencimientoSeguroVehicular ?? string.Empty };
            }
            else
            {
                payload["gondola_estado"] = new[] { "New" };
                payload["gondola_prioridad"] = new[] { "Media" };
                payload["gondola_tarea_padre"] = new[] { traceResult.TareaPadre };
                payload["gondola_placa"] = new[] { input.EquipoPlacaGondola ?? string.Empty };
            }

            var saveResult = _operacionesIngresoEquipoGondolaRequestStore.Save(new OperacionesIngresoEquipoGondolaRequestSaveInput
            {
                Tipo = tipoIngreso,
                Asunto = asunto,
                SolicitanteNombre = asunto,
                TareaPadre = traceResult.TareaPadre,
                Estado = "New",
                Prioridad = "Media",
                Payload = payload
            });

            if (!saveResult.Success)
            {
                TempData["ControlDocumentosError"] = saveResult.Message;
                return RedirectToAction(nameof(ControlDocumentos));
            }

            TempData["ControlDocumentosOk"] =
                $"Registro guardado en la base de datos para {tipoIngreso}. ID: {saveResult.Id}. Tarea padre: {traceResult.TareaPadre}.";
            return RedirectToAction(nameof(ControlDocumentos));
        }

        [HttpGet]
        public IActionResult NuevoIngresoPersonal()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NuevoIngresoPersonal(IFormCollection form)
        {
            var tipo = form["tipo"].ToString();
            var asunto = form["asunto"].ToString();
            var solicitudId = TryReadPositiveInt(form["solicitud_id"].ToString());

            var traceResult = _operacionesIngresoPersonalTraceStore.ResolveOrCreate(tipo, asunto);
            if (!traceResult.Success)
            {
                TempData["NuevoIngresoPersonalError"] = traceResult.Message;
                return RedirectToAction(nameof(NuevoIngresoPersonal));
            }

            var payload = form.Keys
                .Where(k => !string.Equals(k, "__RequestVerificationToken", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    k => k,
                    k => form[k]
                        .Where(v => v is not null)
                        .Select(v => v ?? string.Empty)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var tareaPadreForm = FirstNonEmpty(form, "tarea_padre", "equipo_tarea_padre", "gondola_tarea_padre");
            var estadoForm = FirstNonEmpty(form, "estado", "equipo_estado", "gondola_estado");
            var prioridadForm = FirstNonEmpty(form, "prioridad", "equipo_prioridad", "gondola_prioridad");
            var solicitanteForm = FirstNonEmpty(form, "nombres_apellidos");

            payload["tarea_padre"] = new[] { traceResult.TareaPadre };

            var saveResult = _operacionesIngresoPersonalRequestStore.Save(new OperacionesIngresoPersonalRequestSaveInput
            {
                Id = solicitudId,
                Tipo = tipo,
                Asunto = asunto,
                SolicitanteNombre = string.IsNullOrWhiteSpace(solicitanteForm) ? asunto : solicitanteForm,
                TareaPadre = string.IsNullOrWhiteSpace(tareaPadreForm) ? traceResult.TareaPadre : tareaPadreForm,
                Estado = estadoForm,
                Prioridad = prioridadForm,
                Payload = payload
            });

            if (!saveResult.Success)
            {
                TempData["NuevoIngresoPersonalError"] = saveResult.Message;
                return RedirectToAction(nameof(NuevoIngresoPersonal));
            }

            TempData["NuevoIngresoPersonalOk"] = saveResult.Created
                ? $"Solicitud registrada correctamente. Tarea padre: {traceResult.TareaPadre}"
                : $"Solicitud actualizada correctamente. Tarea padre: {traceResult.TareaPadre}";

            return RedirectToAction(nameof(NuevoIngresoPersonal));
        }

        [HttpGet]
        public IActionResult ResolveTareaPadreIngresoPersonal(string tipo, string asunto)
        {
            var traceResult = _operacionesIngresoPersonalTraceStore.ResolveOrCreate(tipo, asunto);
            return Json(new
            {
                success = traceResult.Success,
                tareaPadre = traceResult.TareaPadre,
                created = traceResult.Created,
                message = traceResult.Message
            });
        }

        [HttpGet]
        public IActionResult GetIngresoPersonalTraceHistory(int take = 20)
        {
            var rows = _operacionesIngresoPersonalTraceStore.GetRecent(take)
                .Select(x => new
                {
                    solicitante = x.Solicitante,
                    tareaPadre = x.TareaPadre,
                    tipoOrigen = x.TipoOrigen,
                    ultimoTipo = x.UltimoTipo,
                    ultimoAsunto = x.UltimoAsunto,
                    createdAt = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = x.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToList();

            return Json(new { success = true, rows });
        }

        [HttpGet]
        public IActionResult GetIngresoPersonalRequests(int take = 50)
        {
            var nowLocal = DateTime.Now;
            var rows = _operacionesIngresoPersonalRequestStore.GetRecent(take)
                .Select(x =>
                {
                    var sla = BuildIngresoPersonalSlaSnapshot(x.CreatedAt, x.UpdatedAt, x.Estado, nowLocal);
                    return new
                    {
                        id = x.Id,
                        tipo = x.Tipo,
                        asunto = x.Asunto,
                        solicitante = x.SolicitanteNombre,
                        tareaPadre = x.TareaPadre,
                        estado = x.Estado,
                        prioridad = x.Prioridad,
                        createdAt = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        createdAtIso = x.CreatedAt.ToString("o"),
                        updatedAt = x.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                        updatedAtIso = x.UpdatedAt.ToString("o"),
                        slaDeadline = sla.Deadline.ToString("dd/MM/yyyy HH:mm"),
                        slaDeadlineIso = sla.Deadline.ToString("o"),
                        slaStatus = sla.StatusLabel,
                        slaStatusClass = sla.StatusClass,
                        slaRemainingLabel = sla.RemainingLabel,
                        slaProgress = Math.Round(sla.ProgressPercent, 1, MidpointRounding.AwayFromZero),
                        slaRemainingHours = Math.Round(sla.RemainingHours, 1, MidpointRounding.AwayFromZero),
                        slaConsumedHours = Math.Round(sla.ConsumedHours, 1, MidpointRounding.AwayFromZero),
                        slaIsClosed = sla.IsClosed
                    };
                })
                .ToList();

            return Json(new { success = true, rows });
        }

        [HttpGet]
        public IActionResult GetIngresoPersonalRequestById(int id)
        {
            if (id <= 0)
            {
                return Json(new { success = false, message = "Id inválido." });
            }

            var detail = _operacionesIngresoPersonalRequestStore.GetById(id);
            if (detail is null)
            {
                return Json(new { success = false, message = "Solicitud no encontrada." });
            }

            var sla = BuildIngresoPersonalSlaSnapshot(detail.CreatedAt, detail.UpdatedAt, detail.Estado, DateTime.Now);
            return Json(new
            {
                success = true,
                detail = new
                {
                    id = detail.Id,
                    tipo = detail.Tipo,
                    asunto = detail.Asunto,
                    solicitante = detail.SolicitanteNombre,
                    tareaPadre = detail.TareaPadre,
                    estado = detail.Estado,
                    prioridad = detail.Prioridad,
                    payload = detail.Payload,
                    createdAt = detail.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdAtIso = detail.CreatedAt.ToString("o"),
                    updatedAt = detail.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAtIso = detail.UpdatedAt.ToString("o"),
                    sla = new
                    {
                        deadline = sla.Deadline.ToString("dd/MM/yyyy HH:mm"),
                        deadlineIso = sla.Deadline.ToString("o"),
                        status = sla.StatusLabel,
                        statusClass = sla.StatusClass,
                        remainingLabel = sla.RemainingLabel,
                        progress = Math.Round(sla.ProgressPercent, 1, MidpointRounding.AwayFromZero),
                        remainingHours = Math.Round(sla.RemainingHours, 1, MidpointRounding.AwayFromZero),
                        consumedHours = Math.Round(sla.ConsumedHours, 1, MidpointRounding.AwayFromZero),
                        isClosed = sla.IsClosed
                    }
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteIngresoPersonalRequest(int id)
        {
            if (id <= 0)
            {
                return Json(new { success = false, message = "Id inválido." });
            }

            var deleted = _operacionesIngresoPersonalRequestStore.Delete(id);
            if (!deleted)
            {
                return Json(new { success = false, message = "No se encontró el registro." });
            }

            return Json(new { success = true, message = "Solicitud eliminada correctamente." });
        }

        private static IngresoPersonalSlaSnapshot BuildIngresoPersonalSlaSnapshot(
            DateTime createdAt,
            DateTime updatedAt,
            string? estado,
            DateTime nowLocal)
        {
            var closed = IsIngresoPersonalEstadoCerrado(estado);
            var evaluationPoint = closed ? updatedAt : nowLocal;
            var deadline = AddIngresoPersonalHorasLaborales(createdAt, IngresoPersonalSlaHorasLaborales);
            var consumed = CalculateIngresoPersonalHorasLaborales(createdAt, evaluationPoint);
            var remaining = IngresoPersonalSlaHorasLaborales - consumed;
            var progress = IngresoPersonalSlaHorasLaborales <= 0d
                ? 0d
                : Math.Clamp(consumed / IngresoPersonalSlaHorasLaborales * 100d, 0d, 100d);

            string statusLabel;
            string statusClass;
            string remainingLabel;

            if (remaining <= 0d)
            {
                statusLabel = "Vencido";
                statusClass = "danger";
                remainingLabel = closed
                    ? $"Cerrado {Math.Abs(remaining):0.#} h laborales fuera de SLA."
                    : $"Vencido por {Math.Abs(remaining):0.#} h laborales.";
            }
            else if (remaining <= IngresoPersonalSlaHorasPorVencer)
            {
                statusLabel = "Por vencer";
                statusClass = "warning";
                remainingLabel = $"Restan {remaining:0.#} h laborales.";
            }
            else
            {
                statusLabel = "Cumplido";
                statusClass = "success";
                remainingLabel = closed
                    ? $"Cerrado con {remaining:0.#} h laborales de margen."
                    : $"Restan {remaining:0.#} h laborales.";
            }

            return new IngresoPersonalSlaSnapshot(deadline, statusLabel, statusClass, remainingLabel, progress, remaining, consumed, closed);
        }

        private static bool IsIngresoPersonalEstadoCerrado(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return false;
            }

            var normalized = estado.Trim();
            return normalized.Equals("Aprobado", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Rechazado", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Cerrado", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Finalizado", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime AddIngresoPersonalHorasLaborales(DateTime start, double businessHours)
        {
            if (businessHours <= 0d)
            {
                return NormalizeIngresoPersonalHorario(start);
            }

            var current = NormalizeIngresoPersonalHorario(start);
            var remaining = businessHours;

            while (remaining > 0d)
            {
                var dayEnd = current.Date.Add(IngresoPersonalHorarioFin);
                var available = (dayEnd - current).TotalHours;
                if (available <= 0d)
                {
                    current = NextIngresoPersonalBusinessStart(current);
                    continue;
                }

                var used = Math.Min(remaining, available);
                current = current.AddHours(used);
                remaining -= used;

                if (remaining > 0d)
                {
                    current = NextIngresoPersonalBusinessStart(current);
                }
            }

            return current;
        }

        private static double CalculateIngresoPersonalHorasLaborales(DateTime from, DateTime to)
        {
            if (to <= from)
            {
                return 0d;
            }

            var total = 0d;
            var current = from;

            while (current < to)
            {
                if (!IsIngresoPersonalBusinessDay(current))
                {
                    current = NextIngresoPersonalBusinessStart(current);
                    continue;
                }

                var dayStart = current.Date.Add(IngresoPersonalHorarioInicio);
                var dayEnd = current.Date.Add(IngresoPersonalHorarioFin);

                if (current < dayStart)
                {
                    current = dayStart;
                }

                if (current >= dayEnd)
                {
                    current = NextIngresoPersonalBusinessStart(current);
                    continue;
                }

                var segmentEnd = to < dayEnd ? to : dayEnd;
                if (segmentEnd > current)
                {
                    total += (segmentEnd - current).TotalHours;
                }

                current = segmentEnd;
                if (current >= dayEnd)
                {
                    current = NextIngresoPersonalBusinessStart(current);
                }
            }

            return total;
        }

        private static DateTime NormalizeIngresoPersonalHorario(DateTime value)
        {
            var current = value;
            while (!IsIngresoPersonalBusinessDay(current))
            {
                current = current.Date.AddDays(1).Add(IngresoPersonalHorarioInicio);
            }

            var time = current.TimeOfDay;
            if (time < IngresoPersonalHorarioInicio)
            {
                return current.Date.Add(IngresoPersonalHorarioInicio);
            }

            if (time >= IngresoPersonalHorarioFin)
            {
                return NextIngresoPersonalBusinessStart(current);
            }

            return current;
        }

        private static DateTime NextIngresoPersonalBusinessStart(DateTime value)
        {
            var current = value.Date.AddDays(1);
            while (!IsIngresoPersonalBusinessDay(current))
            {
                current = current.AddDays(1);
            }

            return current.Add(IngresoPersonalHorarioInicio);
        }

        private static bool IsIngresoPersonalBusinessDay(DateTime value) =>
            value.DayOfWeek != DayOfWeek.Saturday && value.DayOfWeek != DayOfWeek.Sunday;

        private sealed record IngresoPersonalSlaSnapshot(
            DateTime Deadline,
            string StatusLabel,
            string StatusClass,
            string RemainingLabel,
            double ProgressPercent,
            double RemainingHours,
            double ConsumedHours,
            bool IsClosed);

        [HttpGet]
        public IActionResult NuevoIngresoEquipoGondola()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NuevoIngresoEquipoGondola(IFormCollection form)
        {
            var tipoIngreso = form["tipo_ingreso"].ToString();
            var asunto = form["asunto"].ToString();
            var solicitudId = TryReadPositiveInt(form["solicitud_id"].ToString());

            if (string.IsNullOrWhiteSpace(tipoIngreso) || string.IsNullOrWhiteSpace(asunto))
            {
                TempData["NuevoIngresoEquipoGondolaError"] = "Debes seleccionar el tipo de ingreso y completar el asunto.";
                return RedirectToAction(nameof(NuevoIngresoEquipoGondola));
            }

            var traceResult = _operacionesIngresoEquipoGondolaTraceStore.ResolveOrCreate(tipoIngreso, asunto);
            if (!traceResult.Success)
            {
                TempData["NuevoIngresoEquipoGondolaError"] = traceResult.Message;
                return RedirectToAction(nameof(NuevoIngresoEquipoGondola));
            }

            var payload = form.Keys
                .Where(k => !string.Equals(k, "__RequestVerificationToken", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    k => k,
                    k => form[k]
                        .Where(v => v is not null)
                        .Select(v => v ?? string.Empty)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var tareaPadreForm = FirstNonEmpty(form, "equipo_tarea_padre", "gondola_tarea_padre");
            var estadoForm = FirstNonEmpty(form, "equipo_estado", "gondola_estado");
            var prioridadForm = FirstNonEmpty(form, "equipo_prioridad", "gondola_prioridad");
            var solicitanteForm = asunto;

            payload["tarea_padre"] = new[] { traceResult.TareaPadre };

            var saveResult = _operacionesIngresoEquipoGondolaRequestStore.Save(new OperacionesIngresoEquipoGondolaRequestSaveInput
            {
                Id = solicitudId,
                Tipo = tipoIngreso,
                Asunto = asunto,
                SolicitanteNombre = string.IsNullOrWhiteSpace(solicitanteForm) ? asunto : solicitanteForm,
                TareaPadre = string.IsNullOrWhiteSpace(tareaPadreForm) ? traceResult.TareaPadre : tareaPadreForm,
                Estado = estadoForm,
                Prioridad = prioridadForm,
                Payload = payload
            });

            if (!saveResult.Success)
            {
                TempData["NuevoIngresoEquipoGondolaError"] = saveResult.Message;
                return RedirectToAction(nameof(NuevoIngresoEquipoGondola));
            }

            TempData["NuevoIngresoEquipoGondolaOk"] = saveResult.Created
                ? $"Registro guardado correctamente para {tipoIngreso}. Tarea padre: {traceResult.TareaPadre}"
                : $"Registro actualizado correctamente para {tipoIngreso}. Tarea padre: {traceResult.TareaPadre}";
            return RedirectToAction(nameof(NuevoIngresoEquipoGondola));
        }

        [HttpGet]
        public IActionResult ResolveTareaPadreIngresoEquipoGondola(string tipo, string asunto)
        {
            var traceResult = _operacionesIngresoEquipoGondolaTraceStore.ResolveOrCreate(tipo, asunto);
            return Json(new
            {
                success = traceResult.Success,
                tareaPadre = traceResult.TareaPadre,
                created = traceResult.Created,
                message = traceResult.Message
            });
        }

        [HttpGet]
        public IActionResult GetIngresoEquipoGondolaTraceHistory(int take = 20)
        {
            var rows = _operacionesIngresoEquipoGondolaTraceStore.GetRecent(take)
                .Select(x => new
                {
                    solicitante = x.Solicitante,
                    tareaPadre = x.TareaPadre,
                    tipoOrigen = x.TipoOrigen,
                    ultimoTipo = x.UltimoTipo,
                    ultimoAsunto = x.UltimoAsunto,
                    createdAt = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = x.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToList();

            return Json(new { success = true, rows });
        }

        [HttpGet]
        public IActionResult GetIngresoEquipoGondolaRequests(int take = 50)
        {
            var nowLocal = DateTime.Now;
            var rows = _operacionesIngresoEquipoGondolaRequestStore.GetRecent(take)
                .Select(x =>
                {
                    var sla = BuildIngresoPersonalSlaSnapshot(x.CreatedAt, x.UpdatedAt, x.Estado, nowLocal);
                    return new
                    {
                        id = x.Id,
                        tipo = x.Tipo,
                        asunto = x.Asunto,
                        solicitante = x.SolicitanteNombre,
                        tareaPadre = x.TareaPadre,
                        estado = x.Estado,
                        prioridad = x.Prioridad,
                        createdAt = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        createdAtIso = x.CreatedAt.ToString("o"),
                        updatedAt = x.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                        updatedAtIso = x.UpdatedAt.ToString("o"),
                        slaDeadline = sla.Deadline.ToString("dd/MM/yyyy HH:mm"),
                        slaDeadlineIso = sla.Deadline.ToString("o"),
                        slaStatus = sla.StatusLabel,
                        slaStatusClass = sla.StatusClass,
                        slaRemainingLabel = sla.RemainingLabel,
                        slaProgress = Math.Round(sla.ProgressPercent, 1, MidpointRounding.AwayFromZero),
                        slaRemainingHours = Math.Round(sla.RemainingHours, 1, MidpointRounding.AwayFromZero),
                        slaConsumedHours = Math.Round(sla.ConsumedHours, 1, MidpointRounding.AwayFromZero),
                        slaIsClosed = sla.IsClosed
                    };
                })
                .ToList();

            return Json(new { success = true, rows });
        }

        [HttpGet]
        public IActionResult GetIngresoEquipoGondolaRequestById(int id)
        {
            if (id <= 0)
            {
                return Json(new { success = false, message = "Id inválido." });
            }

            var detail = _operacionesIngresoEquipoGondolaRequestStore.GetById(id);
            if (detail is null)
            {
                return Json(new { success = false, message = "Solicitud no encontrada." });
            }

            var sla = BuildIngresoPersonalSlaSnapshot(detail.CreatedAt, detail.UpdatedAt, detail.Estado, DateTime.Now);
            return Json(new
            {
                success = true,
                detail = new
                {
                    id = detail.Id,
                    tipo = detail.Tipo,
                    asunto = detail.Asunto,
                    solicitante = detail.SolicitanteNombre,
                    tareaPadre = detail.TareaPadre,
                    estado = detail.Estado,
                    prioridad = detail.Prioridad,
                    payload = detail.Payload,
                    createdAt = detail.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdAtIso = detail.CreatedAt.ToString("o"),
                    updatedAt = detail.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAtIso = detail.UpdatedAt.ToString("o"),
                    sla = new
                    {
                        deadline = sla.Deadline.ToString("dd/MM/yyyy HH:mm"),
                        deadlineIso = sla.Deadline.ToString("o"),
                        status = sla.StatusLabel,
                        statusClass = sla.StatusClass,
                        remainingLabel = sla.RemainingLabel,
                        progress = Math.Round(sla.ProgressPercent, 1, MidpointRounding.AwayFromZero),
                        remainingHours = Math.Round(sla.RemainingHours, 1, MidpointRounding.AwayFromZero),
                        consumedHours = Math.Round(sla.ConsumedHours, 1, MidpointRounding.AwayFromZero),
                        isClosed = sla.IsClosed
                    }
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteIngresoEquipoGondolaRequest(int id)
        {
            if (id <= 0)
            {
                return Json(new { success = false, message = "Id inválido." });
            }

            var deleted = _operacionesIngresoEquipoGondolaRequestStore.Delete(id);
            if (!deleted)
            {
                return Json(new { success = false, message = "No se encontró el registro." });
            }

            return Json(new { success = true, message = "Solicitud eliminada correctamente." });
        }

        [HttpGet]
        public IActionResult ExportSeguimientoToneladasPdf(string? range = null, string? from = null, string? to = null)
        {
            var model = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            ApplyMetasProduccionMensual(model);
            var bytes = OperacionesToneladasPdfReportService.GenerateSeguimientoPdf(model, _environment.WebRootPath, range, from, to);
            var fileName = $"SeguimientoToneladas_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        [HttpGet]
        public IActionResult ExportSeguimientoDieselPdf()
        {
            var seguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var model = BuildDieselModelFromSeguimiento(seguimiento);
            var bytes = OperacionesDieselPdfReportService.GenerateSeguimientoPdf(model, _environment.WebRootPath);
            var fileName = $"SeguimientoDiesel_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        [HttpGet]
        public IActionResult ExportDashboardPdf()
        {
            var seguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var model = BuildDashboardModelFromSeguimiento(seguimiento);
            var bytes = OperacionesDashboardPdfReportService.GenerateDashboardPdf(model, _environment.WebRootPath);
            var fileName = $"DashboardOperaciones_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteToneladasRutaPdf(
            bool inline = false,
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseModel = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredModel = BuildFilteredSeguimiento(baseModel, filtros);
            var bytes = OperacionesToneladasRutaPdfReportService.GenerateReportePdf(
                filteredModel,
                _environment.WebRootPath,
                filteredModel.MetaMensualTotal);
            var fileName = $"Reporte_Operaciones_ToneladasRuta_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            RegistrarReporteGenerado("Toneladas por ruta", "PDF", filtros, filteredModel.Rows.Count);

            return BuildPdfResult(bytes, fileName, inline);
        }

        [HttpGet]
        public IActionResult ExportReporteToneladasRutaCsv(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseModel = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredModel = BuildFilteredSeguimiento(baseModel, filtros);
            var rows = BuildToneladasRutaAggregatedRows(filteredModel.Rows);
            var headers = new[]
            {
                "Ruta", "Toneladas", "DieselGalones", "DieselLitros", "Equipos", "Viajes",
                "T/GAL", "L/T", "ParticipacionToneladasPct", "VarVsPromPct", "EstadoRendimiento"
            };
            var data = rows.Select(x => new[]
            {
                x.Ruta,
                x.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                x.DieselGalones.ToString("N2", CultureInfo.InvariantCulture),
                x.DieselLitros.ToString("N2", CultureInfo.InvariantCulture),
                x.Equipos.ToString(CultureInfo.InvariantCulture),
                x.Viajes.ToString(CultureInfo.InvariantCulture),
                x.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture),
                x.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture),
                x.ParticipacionToneladasPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.VariacionVsPromedioPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.EstadoRendimiento
            }).ToList();
            var fileName = $"Reporte_Operaciones_ToneladasRuta_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            RegistrarReporteGenerado("Toneladas por ruta", "CSV", filtros, filteredModel.Rows.Count);

            return BuildCsvResult(headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteToneladasRutaExcel(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseModel = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredModel = BuildFilteredSeguimiento(baseModel, filtros);
            var rows = BuildToneladasRutaAggregatedRows(filteredModel.Rows);
            var headers = new[]
            {
                "Ruta", "Toneladas", "DieselGalones", "DieselLitros", "Equipos", "Viajes",
                "T/GAL", "L/T", "ParticipacionToneladasPct", "VarVsPromPct", "EstadoRendimiento"
            };
            var data = rows.Select(x => new[]
            {
                x.Ruta,
                x.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                x.DieselGalones.ToString("N2", CultureInfo.InvariantCulture),
                x.DieselLitros.ToString("N2", CultureInfo.InvariantCulture),
                x.Equipos.ToString(CultureInfo.InvariantCulture),
                x.Viajes.ToString(CultureInfo.InvariantCulture),
                x.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture),
                x.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture),
                x.ParticipacionToneladasPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.VariacionVsPromedioPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.EstadoRendimiento
            }).ToList();
            var fileName = $"Reporte_Operaciones_ToneladasRuta_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            RegistrarReporteGenerado("Toneladas por ruta", "XLSX", filtros, filteredModel.Rows.Count);

            return BuildExcelResult("ToneladasRuta", headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteDieselUnidadPdf(
            bool inline = false,
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var model = BuildDieselModelFromSeguimiento(filteredSeguimiento);
            var bytes = OperacionesDieselPdfReportService.GenerateSeguimientoPdf(model, _environment.WebRootPath);
            var fileName = $"Reporte_Operaciones_DieselUnidad_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            RegistrarReporteGenerado("Diesel por unidad", "PDF", filtros, filteredSeguimiento.Rows.Count);

            return BuildPdfResult(bytes, fileName, inline);
        }

        [HttpGet]
        public IActionResult ExportReporteDieselUnidadCsv(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var diesel = BuildDieselModelFromSeguimiento(filteredSeguimiento);
            var headers = new[]
            {
                "Unidad", "Ruta", "Toneladas", "Galones", "Litros", "T/GAL", "L/T",
                "VarVsProm", "EstadoConsumo", "ParticipacionGalonesPct", "Recomendacion"
            };
            var data = diesel.Rows.Select(x => new[]
            {
                x.Unidad,
                x.RutaPrincipal,
                x.ToneladasMovilizadas.ToString("N1", CultureInfo.InvariantCulture),
                x.GalonesDespachados.ToString("N2", CultureInfo.InvariantCulture),
                x.LitrosEquivalentes.ToString("N2", CultureInfo.InvariantCulture),
                x.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture),
                x.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture),
                x.VariacionVsPromedioPorcentaje.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.EstadoConsumo,
                (diesel.TotalGalones > 0m
                    ? Math.Round((x.GalonesDespachados / diesel.TotalGalones) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m).ToString("N1", CultureInfo.InvariantCulture) + "%",
                ResolveConsumoRecommendation(x.EstadoConsumo)
            }).ToList();
            var fileName = $"Reporte_Operaciones_DieselUnidad_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            RegistrarReporteGenerado("Diesel por unidad", "CSV", filtros, filteredSeguimiento.Rows.Count);

            return BuildCsvResult(headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteDieselUnidadExcel(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var diesel = BuildDieselModelFromSeguimiento(filteredSeguimiento);
            var headers = new[]
            {
                "Unidad", "Ruta", "Toneladas", "Galones", "Litros", "T/GAL", "L/T",
                "VarVsProm", "EstadoConsumo", "ParticipacionGalonesPct", "Recomendacion"
            };
            var data = diesel.Rows.Select(x => new[]
            {
                x.Unidad,
                x.RutaPrincipal,
                x.ToneladasMovilizadas.ToString("N1", CultureInfo.InvariantCulture),
                x.GalonesDespachados.ToString("N2", CultureInfo.InvariantCulture),
                x.LitrosEquivalentes.ToString("N2", CultureInfo.InvariantCulture),
                x.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture),
                x.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture),
                x.VariacionVsPromedioPorcentaje.ToString("N1", CultureInfo.InvariantCulture) + "%",
                x.EstadoConsumo,
                (diesel.TotalGalones > 0m
                    ? Math.Round((x.GalonesDespachados / diesel.TotalGalones) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m).ToString("N1", CultureInfo.InvariantCulture) + "%",
                ResolveConsumoRecommendation(x.EstadoConsumo)
            }).ToList();
            var fileName = $"Reporte_Operaciones_DieselUnidad_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            RegistrarReporteGenerado("Diesel por unidad", "XLSX", filtros, filteredSeguimiento.Rows.Count);

            return BuildExcelResult("DieselUnidad", headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteCumplimientoOperativoPdf(
            bool inline = false,
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var metasMensualesByPeriodo = BuildMetaMensualObjetivosByPeriodo(fechaCorte.Year, fechaCorte.Month);
            var bytes = OperacionesCumplimientoOperativoPdfReportService.GenerateReportePdf(
                filteredSeguimiento,
                _environment.WebRootPath,
                metaObjetivo.MetaDiaria,
                metaObjetivo.MetaMensualTotal,
                metasMensualesByPeriodo);
            var fileName = $"Reporte_Operaciones_Cumplimiento_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            RegistrarReporteGenerado("Cumplimiento operativo", "PDF", filtros, filteredSeguimiento.Rows.Count);

            return BuildPdfResult(bytes, fileName, inline);
        }

        [HttpGet]
        public IActionResult ExportReporteCumplimientoOperativoCsv(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var comparativos = BuildReportesComparativos(filteredSeguimiento.Rows, fechaCorte);
            var headers = new[]
            {
                "Periodo", "MetaToneladas", "ToneladasActual", "ToneladasAnterior",
                "DieselActualGal", "DieselAnteriorGal", "CumplimientoActualPct", "CumplimientoAnteriorPct",
                "DeltaToneladas", "DeltaPorcentaje", "Semaforo"
            };
            var data = comparativos.Select(x =>
            {
                var metaPeriodo = ResolveMetaToneladasForPeriodo(x.Periodo, metaObjetivo.MetaDiaria, metaObjetivo.MetaMensualTotal);
                return new[]
                {
                    x.Periodo,
                    metaPeriodo.ToString("N1", CultureInfo.InvariantCulture),
                    x.ActualToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.AnteriorToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.ActualDieselGal.ToString("N2", CultureInfo.InvariantCulture),
                    x.AnteriorDieselGal.ToString("N2", CultureInfo.InvariantCulture),
                    CalculateCumplimientoPorcentaje(x.ActualToneladas, metaPeriodo).ToString("N1", CultureInfo.InvariantCulture) + "%",
                    CalculateCumplimientoPorcentaje(x.AnteriorToneladas, metaPeriodo).ToString("N1", CultureInfo.InvariantCulture) + "%",
                    x.DeltaToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.DeltaPorcentaje.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    ResolveCumplimientoSemaforo(CalculateCumplimientoPorcentaje(x.ActualToneladas, metaPeriodo))
                };
            }).ToList();
            var fileName = $"Reporte_Operaciones_Cumplimiento_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            RegistrarReporteGenerado("Cumplimiento operativo", "CSV", filtros, filteredSeguimiento.Rows.Count);

            return BuildCsvResult(headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteCumplimientoOperativoExcel(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var comparativos = BuildReportesComparativos(filteredSeguimiento.Rows, fechaCorte);
            var headers = new[]
            {
                "Periodo", "MetaToneladas", "ToneladasActual", "ToneladasAnterior",
                "DieselActualGal", "DieselAnteriorGal", "CumplimientoActualPct", "CumplimientoAnteriorPct",
                "DeltaToneladas", "DeltaPorcentaje", "Semaforo"
            };
            var data = comparativos.Select(x =>
            {
                var metaPeriodo = ResolveMetaToneladasForPeriodo(x.Periodo, metaObjetivo.MetaDiaria, metaObjetivo.MetaMensualTotal);
                return new[]
                {
                    x.Periodo,
                    metaPeriodo.ToString("N1", CultureInfo.InvariantCulture),
                    x.ActualToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.AnteriorToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.ActualDieselGal.ToString("N2", CultureInfo.InvariantCulture),
                    x.AnteriorDieselGal.ToString("N2", CultureInfo.InvariantCulture),
                    CalculateCumplimientoPorcentaje(x.ActualToneladas, metaPeriodo).ToString("N1", CultureInfo.InvariantCulture) + "%",
                    CalculateCumplimientoPorcentaje(x.AnteriorToneladas, metaPeriodo).ToString("N1", CultureInfo.InvariantCulture) + "%",
                    x.DeltaToneladas.ToString("N1", CultureInfo.InvariantCulture),
                    x.DeltaPorcentaje.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    ResolveCumplimientoSemaforo(CalculateCumplimientoPorcentaje(x.ActualToneladas, metaPeriodo))
                };
            }).ToList();
            var fileName = $"Reporte_Operaciones_Cumplimiento_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            RegistrarReporteGenerado("Cumplimiento operativo", "XLSX", filtros, filteredSeguimiento.Rows.Count);

            return BuildExcelResult("CumplimientoOperativo", headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteGerencialMensualToneladasPdf(
            bool inline = false,
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var reporte = BuildReporteGerencialMensualToneladas(filteredSeguimiento, fechaCorte, metaObjetivo);
            var bytes = OperacionesGerencialMensualToneladasPdfReportService.GenerateReportePdf(reporte, _environment.WebRootPath);
            var fileName = $"Reporte_Operaciones_GerencialMensualToneladas_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            RegistrarReporteGenerado("Gerencial mensual toneladas por sitio", "PDF", filtros, filteredSeguimiento.Rows.Count);

            return BuildPdfResult(bytes, fileName, inline);
        }

        [HttpGet]
        public IActionResult ExportReporteGerencialMensualToneladasCsv(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var reporte = BuildReporteGerencialMensualToneladas(filteredSeguimiento, fechaCorte, metaObjetivo);

            var headers = new[]
            {
                "Seccion", "Ranking", "Posicion", "Nombre", "Sitio", "Toneladas", "Viajes",
                "Fecha", "MetaDiaria", "CumplimientoPct", "DiasConOperacion", "DiasMetaCumplida", "CumplimientoDiasPct"
            };

            var data = new List<string[]>();
            foreach (var sitioRow in reporte.Sitios)
            {
                data.Add(new[]
                {
                    "ResumenSitio",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    sitioRow.Sitio,
                    sitioRow.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty,
                    sitioRow.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    sitioRow.CumplimientoMensualPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            void AddRankingRows(string ranking, IReadOnlyList<OperacionesReporteRankingRowViewModel> rows)
            {
                foreach (var row in rows)
                {
                    data.Add(new[]
                    {
                        "Ranking",
                        ranking,
                        row.Posicion.ToString(CultureInfo.InvariantCulture),
                        row.Nombre,
                        row.Sitio,
                        row.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                        row.Viajes.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty
                    });
                }
            }

            AddRankingRows("TopOperadores", reporte.TopOperadores);
            AddRankingRows("TopEquipos", reporte.TopEquipos);
            AddRankingRows("BottomConductores", reporte.BottomConductores);
            AddRankingRows("BottomEquipos", reporte.BottomEquipos);

            foreach (var cumplimiento in reporte.CumplimientoPorSitio)
            {
                data.Add(new[]
                {
                    "CumplimientoSitio",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    cumplimiento.Sitio,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    cumplimiento.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    cumplimiento.DiasConOperacion.ToString(CultureInfo.InvariantCulture),
                    cumplimiento.DiasMetaCumplida.ToString(CultureInfo.InvariantCulture),
                    cumplimiento.CumplimientoDiasPct.ToString("N1", CultureInfo.InvariantCulture) + "%"
                });
            }

            foreach (var dia in reporte.DiasMetaCumplidaDetalle.OrderBy(x => x.Sitio).ThenBy(x => x.Fecha))
            {
                data.Add(new[]
                {
                    "DiasMetaCumplida",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    dia.Sitio,
                    dia.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    dia.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    dia.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    dia.CumplimientoPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            var fileName = $"Reporte_Operaciones_GerencialMensualToneladas_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            RegistrarReporteGenerado("Gerencial mensual toneladas por sitio", "CSV", filtros, filteredSeguimiento.Rows.Count);

            return BuildCsvResult(headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReporteGerencialMensualToneladasExcel(
            string? fechaDesde = null,
            string? fechaHasta = null,
            string? sitio = null,
            string? turno = null,
            string? conductor = null,
            string? equipo = null,
            string? estado = null)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseSeguimiento = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredSeguimiento = BuildFilteredSeguimiento(baseSeguimiento, filtros);
            var fechaCorte = filteredSeguimiento.FechaOperativa ?? DateTime.Today;
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var reporte = BuildReporteGerencialMensualToneladas(filteredSeguimiento, fechaCorte, metaObjetivo);

            var headers = new[]
            {
                "Seccion", "Ranking", "Posicion", "Nombre", "Sitio", "Toneladas", "Viajes",
                "Fecha", "MetaDiaria", "CumplimientoPct", "DiasConOperacion", "DiasMetaCumplida", "CumplimientoDiasPct"
            };

            var data = new List<string[]>();
            foreach (var sitioRow in reporte.Sitios)
            {
                data.Add(new[]
                {
                    "ResumenSitio",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    sitioRow.Sitio,
                    sitioRow.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty,
                    sitioRow.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    sitioRow.CumplimientoMensualPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            void AddRankingRows(string ranking, IReadOnlyList<OperacionesReporteRankingRowViewModel> rows)
            {
                foreach (var row in rows)
                {
                    data.Add(new[]
                    {
                        "Ranking",
                        ranking,
                        row.Posicion.ToString(CultureInfo.InvariantCulture),
                        row.Nombre,
                        row.Sitio,
                        row.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                        row.Viajes.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty
                    });
                }
            }

            AddRankingRows("TopOperadores", reporte.TopOperadores);
            AddRankingRows("TopEquipos", reporte.TopEquipos);
            AddRankingRows("BottomConductores", reporte.BottomConductores);
            AddRankingRows("BottomEquipos", reporte.BottomEquipos);

            foreach (var cumplimiento in reporte.CumplimientoPorSitio)
            {
                data.Add(new[]
                {
                    "CumplimientoSitio",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    cumplimiento.Sitio,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    cumplimiento.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    cumplimiento.DiasConOperacion.ToString(CultureInfo.InvariantCulture),
                    cumplimiento.DiasMetaCumplida.ToString(CultureInfo.InvariantCulture),
                    cumplimiento.CumplimientoDiasPct.ToString("N1", CultureInfo.InvariantCulture) + "%"
                });
            }

            foreach (var dia in reporte.DiasMetaCumplidaDetalle.OrderBy(x => x.Sitio).ThenBy(x => x.Fecha))
            {
                data.Add(new[]
                {
                    "DiasMetaCumplida",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    dia.Sitio,
                    dia.Toneladas.ToString("N1", CultureInfo.InvariantCulture),
                    string.Empty,
                    dia.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    dia.MetaDiaria.ToString("N1", CultureInfo.InvariantCulture),
                    dia.CumplimientoPct.ToString("N1", CultureInfo.InvariantCulture) + "%",
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            var fileName = $"Reporte_Operaciones_GerencialMensualToneladas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            RegistrarReporteGenerado("Gerencial mensual toneladas por sitio", "XLSX", filtros, filteredSeguimiento.Rows.Count);

            return BuildExcelResult("GerencialMensualTn", headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportReportesAuditoriaCsv()
        {
            var logs = LoadOperacionesReportesAuditoria()
                .OrderByDescending(x => x.GeneratedAt)
                .Take(500)
                .ToList();
            var headers = new[] { "FechaGeneracion", "Usuario", "Reporte", "Formato", "Registros", "Filtros", "Version" };
            var data = logs.Select(x => new[]
            {
                x.GeneratedAt.ToString("dd/MM/yyyy HH:mm"),
                x.UserName,
                x.ReportName,
                x.Format,
                x.RecordCount.ToString(CultureInfo.InvariantCulture),
                x.FiltersSummary,
                x.Version
            }).ToList();
            var fileName = $"Reporte_Operaciones_Auditoria_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            return BuildCsvResult(headers, data, fileName);
        }

        [HttpGet]
        public IActionResult ExportControlDocumentosConductorPdf(string conductor, bool inline = false)
        {
            var conductorRaw = (conductor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(conductorRaw))
            {
                return BadRequest("Debes indicar un conductor.");
            }

            var model = BuildControlDocumentosModel();
            if (!string.IsNullOrWhiteSpace(model.ErrorMessage))
            {
                return NotFound(model.ErrorMessage);
            }

            var conductorKey = NormalizeControlDocumentoKey(conductorRaw);
            var fila = model.Filas.FirstOrDefault(x =>
                string.Equals(NormalizeControlDocumentoKey(x.Conductor), conductorKey, StringComparison.Ordinal));

            if (fila is null)
            {
                return NotFound("No se encontro el conductor solicitado.");
            }

            var rows = fila.Capacitaciones
                .Select(cap => new ControlDocumentosPdfCapacitacionRow(
                    string.IsNullOrWhiteSpace(cap.NombreCapacitacion) ? "-" : cap.NombreCapacitacion,
                    string.IsNullOrWhiteSpace(cap.Fecha) ? "-" : cap.Fecha,
                    string.IsNullOrWhiteSpace(cap.EtiquetaSemaforo) ? "Sin fecha" : cap.EtiquetaSemaforo,
                    string.IsNullOrWhiteSpace(cap.MensajeAlerta) ? "No hay alerta registrada." : cap.MensajeAlerta))
                .ToList();

            var bytes = OperacionesControlDocumentosPdfReportService.GenerateConductorPdf(
                fila.Conductor,
                rows,
                _environment.WebRootPath);
            var fileName = $"ControlDocumentos_{ToSafeFileNameToken(fila.Conductor)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return BuildPdfResult(bytes, fileName, inline);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ExportControlDocumentosQrDetallePdf(string key, bool inline = false)
        {
            var conductorKey = NormalizeControlDocumentoKey(key);
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return BadRequest("Debes indicar un codigo valido.");
            }

            var model = BuildControlDocumentosQrModel(conductorKey);
            if (model is null)
            {
                return NotFound("No se encontro el conductor solicitado.");
            }

            var bytes = OperacionesControlDocumentosQrDetallePdfReportService.GeneratePdf(model, _environment.WebRootPath);
            var fileName = $"ControlDocumentosQR_{ToSafeFileNameToken(model.Conductor)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return BuildPdfResult(bytes, fileName, inline);
        }

        private IActionResult BuildPdfResult(byte[] bytes, string fileName, bool inline)
        {
            if (inline)
            {
                return File(bytes, "application/pdf");
            }

            return File(bytes, "application/pdf", fileName);
        }

        private OperacionesReportesViewModel BuildReportesModel(
            string? fechaDesde,
            string? fechaHasta,
            string? sitio,
            string? turno,
            string? conductor,
            string? equipo,
            string? estado)
        {
            var filtros = BuildReportesFiltros(fechaDesde, fechaHasta, sitio, turno, conductor, equipo, estado);
            var baseModel = _operacionesSeguimientoStore.Get() ?? CreateSampleToneladasModel();
            var filteredModel = BuildFilteredSeguimiento(baseModel, filtros);
            var fechaCorte = filteredModel.FechaOperativa ?? baseModel.FechaOperativa ?? DateTime.Today;
            var comparativos = BuildReportesComparativos(filteredModel.Rows, fechaCorte);
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);
            var resumen = BuildReportesResumen(filteredModel.Rows, comparativos, metaObjetivo.MetaDiaria);
            var calidad = BuildReportesCalidad(filteredModel.Rows);
            var hallazgos = BuildReportesHallazgos(filteredModel.Rows, resumen, comparativos, calidad);
            var diesel = BuildDieselModelFromSeguimiento(filteredModel);
            var toneladasRuta = BuildToneladasRutaAggregatedRows(filteredModel.Rows);
            var reporteGerencialMensual = BuildReporteGerencialMensualToneladas(filteredModel, fechaCorte, metaObjetivo);
            var historialAuditoria = LoadOperacionesReportesAuditoria();
            var historialHoy = historialAuditoria
                .Where(x => x.GeneratedAt.Date == DateTime.Today)
                .OrderByDescending(x => x.GeneratedAt)
                .ToList();

            if (historialHoy.Count != historialAuditoria.Count)
            {
                SaveOperacionesReportesAuditoria(historialHoy);
            }

            var historial = historialHoy
                .Take(100)
                .Select(x => new OperacionesReportesAuditoriaViewModel
                {
                    FechaGeneracion = x.GeneratedAt,
                    Usuario = x.UserName,
                    Reporte = x.ReportName,
                    Formato = x.Format,
                    Registros = x.RecordCount,
                    FiltrosAplicados = x.FiltersSummary
                })
                .ToList();
            var programaciones = LoadOperacionesReportesProgramaciones()
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .Select(x => new OperacionesReportesProgramacionViewModel
                {
                    Id = x.Id,
                    Reporte = x.ReportKey,
                    Frecuencia = x.Frecuencia,
                    Hora = x.Hora,
                    DiaSemana = x.DiaSemana,
                    Destinatarios = x.Destinatarios,
                    Activo = x.IsActive,
                    FechaCreacion = x.CreatedAt,
                    ProximaEjecucion = x.NextRunAt,
                    UltimaEjecucion = x.LastRunAt
                })
                .ToList();

            var sitiosDisponibles = new List<string> { "TRITON" };

            return new OperacionesReportesViewModel
            {
                Filtros = filtros,
                ResumenEjecutivo = resumen,
                Comparativos = comparativos,
                Hallazgos = hallazgos,
                CalidadDatos = calidad,
                Categorias = BuildReportesCategorias(
                    filtros,
                    resumen,
                    comparativos,
                    toneladasRuta,
                    diesel,
                    reporteGerencialMensual),
                HistorialGeneracion = historial,
                Programaciones = programaciones,
                SitiosDisponibles = sitiosDisponibles,
                IsFromUpload = filteredModel.IsFromUpload,
                SourceFileName = filteredModel.SourceFileName,
                FechaCorte = fechaCorte,
                ErrorMessage = filteredModel.Rows.Count == 0
                    ? "No hay registros con los filtros seleccionados."
                    : null
            };
        }

        private OperacionesReportesFiltrosViewModel BuildReportesFiltros(
            string? fechaDesde,
            string? fechaHasta,
            string? sitio,
            string? turno,
            string? conductor,
            string? equipo,
            string? estado)
        {
            var filtros = new OperacionesReportesFiltrosViewModel
            {
                Sitio = NormalizeReportFilterOption(sitio, "TRITON", new[] { "TRITON" }),
                Turno = NormalizeReportFilterOption(turno, "TODOS", new[] { "TODOS", "DIA", "NOCHE" }),
                Estado = NormalizeReportFilterOption(estado, "TODOS", new[] { "TODOS", "FINALIZADO", "EN RUTA" }),
                Conductor = (conductor ?? string.Empty).Trim(),
                Equipo = (equipo ?? string.Empty).Trim()
            };

            if (TryParseReportDate(fechaDesde, out var desde))
            {
                filtros.FechaDesde = desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (TryParseReportDate(fechaHasta, out var hasta))
            {
                filtros.FechaHasta = hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return filtros;
        }

        private static bool TryParseReportDate(string? input, out DateTime date)
        {
            date = default;
            var text = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return DateTime.TryParseExact(
                       text,
                       new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy" },
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AllowWhiteSpaces,
                       out date)
                   || DateTime.TryParse(text, EsCulture, DateTimeStyles.AllowWhiteSpaces, out date)
                   || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        private static string NormalizeReportFilterOption(string? value, string fallback, IReadOnlyList<string> allowed)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            var match = allowed.FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match) ? fallback : match;
        }

        private SeguimientoToneladasViewModel BuildFilteredSeguimiento(
            SeguimientoToneladasViewModel source,
            OperacionesReportesFiltrosViewModel filtros)
        {
            var rows = source.Rows.AsEnumerable();

            if (TryParseReportDate(filtros.FechaDesde, out var desde))
            {
                rows = rows.Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date >= desde.Date);
            }

            if (TryParseReportDate(filtros.FechaHasta, out var hasta))
            {
                rows = rows.Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date <= hasta.Date);
            }

            if (!string.Equals(filtros.Sitio, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                rows = string.Equals(filtros.Sitio, "TRITON", StringComparison.OrdinalIgnoreCase)
                    ? rows.Where(IsSitioTriton)
                    : rows.Where(IsSitioPavonAsm);
            }

            if (!string.Equals(filtros.Turno, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows.Where(r => string.Equals(
                    ResolveTurnoOperacion(r.FechaEvento),
                    filtros.Turno,
                    StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Conductor))
            {
                rows = rows.Where(r => (r.Conductor ?? string.Empty)
                    .Contains(filtros.Conductor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Equipo))
            {
                rows = rows.Where(r => (r.Equipo ?? string.Empty)
                    .Contains(filtros.Equipo, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(filtros.Estado, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows.Where(r => string.Equals(
                    MapEstadoCategoria(r.Estado),
                    filtros.Estado,
                    StringComparison.OrdinalIgnoreCase));
            }

            var model = BuildToneladasModel(rows.ToList());
            model.IsFromUpload = source.IsFromUpload;
            model.SourceFileName = source.SourceFileName;
            ApplyMetasProduccionMensual(model);

            return model;
        }

        private static List<OperacionesReportesComparativoViewModel> BuildReportesComparativos(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            DateTime fechaCorte)
        {
            var rowsConFecha = rows.Where(r => r.FechaEvento.HasValue).ToList();
            (decimal Ton, decimal Gal) SumRange(DateTime start, DateTime end)
            {
                var subset = rowsConFecha
                    .Where(r => r.FechaEvento!.Value.Date >= start.Date && r.FechaEvento.Value.Date <= end.Date)
                    .ToList();
                return (subset.Sum(x => x.Toneladas), subset.Sum(x => x.CombustibleLitros));
            }

            OperacionesReportesComparativoViewModel BuildItem(string nombre, DateTime actualInicio, DateTime actualFin, DateTime anteriorInicio, DateTime anteriorFin)
            {
                var actual = SumRange(actualInicio, actualFin);
                var anterior = SumRange(anteriorInicio, anteriorFin);
                var deltaTon = actual.Ton - anterior.Ton;
                var deltaPct = anterior.Ton > 0m
                    ? (deltaTon / anterior.Ton) * 100m
                    : actual.Ton > 0m ? 100m : 0m;

                return new OperacionesReportesComparativoViewModel
                {
                    Periodo = nombre,
                    ActualToneladas = Math.Round(actual.Ton, 1),
                    AnteriorToneladas = Math.Round(anterior.Ton, 1),
                    ActualDieselGal = Math.Round(actual.Gal, 2),
                    AnteriorDieselGal = Math.Round(anterior.Gal, 2),
                    DeltaToneladas = Math.Round(deltaTon, 1),
                    DeltaPorcentaje = Math.Round(deltaPct, 1)
                };
            }

            var diaActualInicio = fechaCorte.Date;
            var diaActualFin = fechaCorte.Date;
            var diaAnteriorInicio = fechaCorte.Date.AddDays(-1);
            var diaAnteriorFin = fechaCorte.Date.AddDays(-1);

            var offsetSemana = ((int)fechaCorte.DayOfWeek + 6) % 7;
            var semanaActualInicio = fechaCorte.Date.AddDays(-offsetSemana);
            var semanaActualFin = semanaActualInicio.AddDays(6);
            var semanaAnteriorInicio = semanaActualInicio.AddDays(-7);
            var semanaAnteriorFin = semanaActualInicio.AddDays(-1);

            var mesActualInicio = new DateTime(fechaCorte.Year, fechaCorte.Month, 1);
            var mesActualFin = mesActualInicio.AddMonths(1).AddDays(-1);
            var mesAnteriorInicio = mesActualInicio.AddMonths(-1);
            var mesAnteriorFin = mesActualInicio.AddDays(-1);

            return new List<OperacionesReportesComparativoViewModel>
            {
                BuildItem("Dia", diaActualInicio, diaActualFin, diaAnteriorInicio, diaAnteriorFin),
                BuildItem("Semana", semanaActualInicio, semanaActualFin, semanaAnteriorInicio, semanaAnteriorFin),
                BuildItem("Mes", mesActualInicio, mesActualFin, mesAnteriorInicio, mesAnteriorFin)
            };
        }

        private static decimal ResolveMetaToneladasForPeriodo(
            string? periodo,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo)
        {
            if (string.Equals(periodo, "Dia", StringComparison.OrdinalIgnoreCase))
            {
                return metaDiariaObjetivo;
            }

            if (string.Equals(periodo, "Semana", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(metaDiariaObjetivo * 7m, 1, MidpointRounding.AwayFromZero);
            }

            if (string.Equals(periodo, "Mes", StringComparison.OrdinalIgnoreCase))
            {
                return metaMensualObjetivo;
            }

            return metaDiariaObjetivo;
        }

        private static decimal CalculateCumplimientoPorcentaje(decimal toneladas, decimal metaToneladas)
        {
            if (metaToneladas <= 0m)
            {
                return 0m;
            }

            return Math.Round((toneladas / metaToneladas) * 100m, 1, MidpointRounding.AwayFromZero);
        }

        private static string ResolveCumplimientoSemaforo(decimal cumplimientoPct)
        {
            if (cumplimientoPct >= 100m)
            {
                return "VERDE";
            }

            if (cumplimientoPct >= 90m)
            {
                return "AMARILLO";
            }

            return "ROJO";
        }

        private static string ResolveConsumoRecommendation(string? estadoConsumo)
        {
            if (string.Equals(estadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase))
            {
                return "Prioridad alta: revisar operador, ruta y calibracion.";
            }

            if (string.Equals(estadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase))
            {
                return "Monitorear 7 dias y validar tendencia.";
            }

            return "Mantener control operativo actual.";
        }

        private static OperacionesReportesResumenEjecutivoViewModel BuildReportesResumen(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            IReadOnlyList<OperacionesReportesComparativoViewModel> comparativos,
            decimal metaDiariaObjetivo)
        {
            var totalToneladas = Math.Round(rows.Sum(x => x.Toneladas), 1);
            var totalDieselGal = Math.Round(rows.Sum(x => x.CombustibleLitros), 2);
            var diasOperacion = rows
                .Where(r => r.FechaEvento.HasValue)
                .Select(r => r.FechaEvento!.Value.Date)
                .Distinct()
                .Count();
            if (diasOperacion <= 0)
            {
                diasOperacion = 1;
            }

            var meta = Math.Round(metaDiariaObjetivo * diasOperacion, 1, MidpointRounding.AwayFromZero);
            var cumplimiento = meta > 0m
                ? (int)Math.Round((double)((totalToneladas / meta) * 100m), MidpointRounding.AwayFromZero)
                : 0;
            var variacion = comparativos
                .FirstOrDefault(x => string.Equals(x.Periodo, "Mes", StringComparison.OrdinalIgnoreCase))
                ?.DeltaPorcentaje ?? 0m;

            var estado = "Sin datos";
            var estadoClase = "secondary";
            if (rows.Count > 0)
            {
                if (cumplimiento >= 100)
                {
                    estado = "Operacion estable";
                    estadoClase = "success";
                }
                else if (cumplimiento >= 90)
                {
                    estado = "Operacion en seguimiento";
                    estadoClase = "warning";
                }
                else
                {
                    estado = "Operacion en riesgo";
                    estadoClase = "danger";
                }
            }

            return new OperacionesReportesResumenEjecutivoViewModel
            {
                Toneladas = totalToneladas,
                DieselGalones = totalDieselGal,
                CumplimientoPorcentaje = Math.Clamp(cumplimiento, 0, 300),
                VariacionVsPeriodoAnteriorPorcentaje = Math.Round(variacion, 1),
                EstadoOperativo = estado,
                EstadoOperativoClase = estadoClase,
                Registros = rows.Count,
                EquiposUnicos = rows
                    .Select(x => (x.Equipo ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                ConductoresUnicos = rows
                    .Select(x => (x.Conductor ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
            };
        }

        private static OperacionesReportesCalidadDatosViewModel BuildReportesCalidad(IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            var total = rows.Count;
            var sinFecha = rows.Count(x => !x.FechaEvento.HasValue);
            var sinConductor = rows.Count(x => string.IsNullOrWhiteSpace(x.Conductor));
            var sinEquipo = rows.Count(x => string.IsNullOrWhiteSpace(x.Equipo));
            var sinEstado = rows.Count(x => string.IsNullOrWhiteSpace(x.Estado));
            var sinRuta = rows.Count(x => string.IsNullOrWhiteSpace(x.Ruta) || string.Equals(x.Ruta, "Sin ruta", StringComparison.OrdinalIgnoreCase));
            var incompletos = rows.Count(x =>
                !x.FechaEvento.HasValue ||
                string.IsNullOrWhiteSpace(x.Equipo) ||
                string.IsNullOrWhiteSpace(x.Conductor) ||
                string.IsNullOrWhiteSpace(x.Estado) ||
                string.IsNullOrWhiteSpace(x.Ruta) ||
                x.Toneladas <= 0m);
            var pct = total > 0 ? (decimal)incompletos * 100m / total : 0m;

            return new OperacionesReportesCalidadDatosViewModel
            {
                RegistrosTotales = total,
                RegistrosIncompletos = incompletos,
                PorcentajeIncompleto = Math.Round(pct, 1),
                SinFecha = sinFecha,
                SinConductor = sinConductor,
                SinEquipo = sinEquipo,
                SinEstado = sinEstado,
                SinRuta = sinRuta
            };
        }

        private static OperacionesReporteGerencialMensualToneladasViewModel BuildReporteGerencialMensualToneladas(
            SeguimientoToneladasViewModel model,
            DateTime fechaCorte,
            OperacionesMetaObjetivo metaObjetivo)
        {
            var monthStart = new DateTime(fechaCorte.Year, fechaCorte.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var rowsConFecha = model.Rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            var rowsMes = rowsConFecha
                .Where(r => r.FechaEvento!.Value.Date >= monthStart.Date && r.FechaEvento.Value.Date <= monthEnd.Date)
                .ToList();

            if (!rowsMes.Any())
            {
                rowsMes = model.Rows.ToList();
            }

            var finalizadosMes = rowsMes
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .ToList();
            if (!finalizadosMes.Any())
            {
                finalizadosMes = rowsMes
                    .Where(r => r.Toneladas > 0m)
                    .ToList();
            }

            var resumenSitios = new List<OperacionesReporteSitioResumenViewModel>
            {
                BuildSitioResumen("TRITON", finalizadosMes, metaObjetivo.MetaMensualTriton, metaObjetivo.MetaDiariaTriton),
                BuildSitioResumen("PAVON ASM", finalizadosMes, metaObjetivo.MetaMensualPavonAsm, metaObjetivo.MetaDiariaPavonAsm)
            };

            var topOperadores = BuildRankingRows(
                finalizadosMes,
                r => r.Conductor,
                descending: true,
                limit: 10);
            var topEquipos = BuildRankingRows(
                finalizadosMes,
                r => r.Equipo,
                descending: true,
                limit: 10);
            var bottomConductores = BuildRankingRows(
                finalizadosMes,
                r => r.Conductor,
                descending: false,
                limit: 5);
            var bottomEquipos = BuildRankingRows(
                finalizadosMes,
                r => r.Equipo,
                descending: false,
                limit: 5);

            var cumplimientoPorSitio = new List<OperacionesReporteCumplimientoDiaSitioViewModel>();
            var diasCumplidos = new List<OperacionesReporteDiaCumplidoViewModel>();

            BuildCumplimientoSitio(
                "TRITON",
                finalizadosMes,
                metaObjetivo.MetaDiariaTriton,
                monthStart,
                monthEnd,
                cumplimientoPorSitio,
                diasCumplidos);
            BuildCumplimientoSitio(
                "PAVON ASM",
                finalizadosMes,
                metaObjetivo.MetaDiariaPavonAsm,
                monthStart,
                monthEnd,
                cumplimientoPorSitio,
                diasCumplidos);

            return new OperacionesReporteGerencialMensualToneladasViewModel
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                FechaCorte = fechaCorte,
                PeriodoLabel = monthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                FuenteDatos = string.IsNullOrWhiteSpace(model.SourceFileName) ? "Seguimiento de toneladas" : model.SourceFileName!,
                TotalRegistros = rowsMes.Count,
                TotalToneladas = Math.Round(finalizadosMes.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero),
                TotalDieselGalones = Math.Round(finalizadosMes.Sum(x => x.CombustibleLitros), 2, MidpointRounding.AwayFromZero),
                MetaDiariaTriton = metaObjetivo.MetaDiariaTriton,
                MetaDiariaPavonAsm = metaObjetivo.MetaDiariaPavonAsm,
                MetaMensualTriton = metaObjetivo.MetaMensualTriton,
                MetaMensualPavonAsm = metaObjetivo.MetaMensualPavonAsm,
                Sitios = resumenSitios,
                TopOperadores = topOperadores,
                TopEquipos = topEquipos,
                BottomConductores = bottomConductores,
                BottomEquipos = bottomEquipos,
                CumplimientoPorSitio = cumplimientoPorSitio
                    .OrderBy(x => x.Sitio)
                    .ToList(),
                DiasMetaCumplidaDetalle = diasCumplidos
                    .OrderBy(x => x.Sitio)
                    .ThenBy(x => x.Fecha)
                    .ToList()
            };
        }

        private static OperacionesReporteSitioResumenViewModel BuildSitioResumen(
            string sitio,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            decimal metaMensual,
            decimal metaDiaria)
        {
            var rowsSitio = rows.Where(r => string.Equals(ResolveSitioGerencial(r), sitio, StringComparison.Ordinal)).ToList();
            var toneladas = Math.Round(rowsSitio.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero);
            var diesel = Math.Round(rowsSitio.Sum(x => x.CombustibleLitros), 2, MidpointRounding.AwayFromZero);
            var cumplimiento = metaMensual > 0m
                ? Math.Round((toneladas / metaMensual) * 100m, 1, MidpointRounding.AwayFromZero)
                : 0m;

            return new OperacionesReporteSitioResumenViewModel
            {
                Sitio = sitio,
                Toneladas = toneladas,
                DieselGalones = diesel,
                MetaMensual = metaMensual,
                CumplimientoMensualPct = cumplimiento,
                MetaDiaria = metaDiaria
            };
        }

        private static List<OperacionesReporteRankingRowViewModel> BuildRankingRows(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            Func<SeguimientoToneladasRowViewModel, string?> keySelector,
            bool descending,
            int limit)
        {
            var grouped = rows
                .Select(r => new
                {
                    Key = (keySelector(r) ?? string.Empty).Trim(),
                    Sitio = ResolveSitioGerencial(r),
                    Toneladas = r.Toneladas,
                    Viajes = r.Viajes
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Toneladas > 0m)
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var sitioPrincipal = g
                        .GroupBy(x => x.Sitio)
                        .OrderByDescending(x => x.Sum(y => y.Toneladas))
                        .Select(x => x.Key)
                        .FirstOrDefault() ?? "OTROS";

                    return new OperacionesReporteRankingRowViewModel
                    {
                        Nombre = g.First().Key,
                        Sitio = sitioPrincipal,
                        Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero),
                        Viajes = g.Sum(x => x.Viajes)
                    };
                })
                .Where(x => x.Toneladas > 0m)
                .ToList();

            var ordered = descending
                ? grouped.OrderByDescending(x => x.Toneladas).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase)
                : grouped.OrderBy(x => x.Toneladas).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase);

            return ordered
                .Take(Math.Max(1, limit))
                .Select((x, index) =>
                {
                    x.Posicion = index + 1;
                    return x;
                })
                .ToList();
        }

        private static void BuildCumplimientoSitio(
            string sitio,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            decimal metaDiaria,
            DateTime monthStart,
            DateTime monthEnd,
            List<OperacionesReporteCumplimientoDiaSitioViewModel> resumen,
            List<OperacionesReporteDiaCumplidoViewModel> detalle)
        {
            var rowsSitio = rows
                .Where(r => string.Equals(ResolveSitioGerencial(r), sitio, StringComparison.Ordinal))
                .Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date >= monthStart.Date && r.FechaEvento.Value.Date <= monthEnd.Date)
                .ToList();

            var daily = rowsSitio
                .GroupBy(r => r.FechaEvento!.Value.Date)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero)
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            var diasConOperacion = daily.Count(x => x.Toneladas > 0m);
            var diasCumplidos = metaDiaria > 0m
                ? daily.Count(x => x.Toneladas >= metaDiaria)
                : 0;
            var cumplimientoDias = diasConOperacion > 0
                ? Math.Round((decimal)diasCumplidos * 100m / diasConOperacion, 1, MidpointRounding.AwayFromZero)
                : 0m;
            var promedioDia = daily.Any()
                ? Math.Round(daily.Average(x => x.Toneladas), 1, MidpointRounding.AwayFromZero)
                : 0m;
            var mejorDia = daily.OrderByDescending(x => x.Toneladas).FirstOrDefault();

            resumen.Add(new OperacionesReporteCumplimientoDiaSitioViewModel
            {
                Sitio = sitio,
                MetaDiaria = metaDiaria,
                DiasConOperacion = diasConOperacion,
                DiasMetaCumplida = diasCumplidos,
                CumplimientoDiasPct = cumplimientoDias,
                PromedioToneladasDia = promedioDia,
                MejorDiaToneladas = mejorDia?.Toneladas ?? 0m,
                FechaMejorDia = mejorDia?.Fecha
            });

            if (metaDiaria <= 0m)
            {
                return;
            }

            foreach (var day in daily.Where(x => x.Toneladas >= metaDiaria))
            {
                detalle.Add(new OperacionesReporteDiaCumplidoViewModel
                {
                    Sitio = sitio,
                    Fecha = day.Fecha,
                    Toneladas = day.Toneladas,
                    MetaDiaria = metaDiaria,
                    CumplimientoPct = Math.Round((day.Toneladas / metaDiaria) * 100m, 1, MidpointRounding.AwayFromZero)
                });
            }
        }

        private static string ResolveSitioGerencial(SeguimientoToneladasRowViewModel row)
        {
            if (IsSitioTriton(row))
            {
                return "TRITON";
            }

            if (IsSitioPavonAsm(row))
            {
                return "PAVON ASM";
            }

            return "OTROS";
        }

        private static List<string> BuildReportesHallazgos(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            OperacionesReportesResumenEjecutivoViewModel resumen,
            IReadOnlyList<OperacionesReportesComparativoViewModel> comparativos,
            OperacionesReportesCalidadDatosViewModel calidad)
        {
            var hallazgos = new List<string>();
            if (resumen.Registros == 0)
            {
                hallazgos.Add("No hay datos con el filtro actual. Ajusta fechas, sitio o estado.");
                return hallazgos;
            }

            if (resumen.CumplimientoPorcentaje < 100)
            {
                hallazgos.Add($"Cumplimiento por debajo de meta: {resumen.CumplimientoPorcentaje}%.");
            }

            var compMes = comparativos.FirstOrDefault(x => string.Equals(x.Periodo, "Mes", StringComparison.OrdinalIgnoreCase));
            if (compMes is not null && compMes.DeltaToneladas < 0m)
            {
                hallazgos.Add($"Tendencia mensual a la baja: {compMes.DeltaToneladas:N1} Tn vs mes anterior.");
            }

            if (calidad.PorcentajeIncompleto >= 5m)
            {
                hallazgos.Add($"Calidad de datos en riesgo: {calidad.PorcentajeIncompleto:N1}% de registros incompletos.");
            }

            var finalizados = rows.Count(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal));
            var enRuta = rows.Count(r => string.Equals(MapEstadoCategoria(r.Estado), "EN RUTA", StringComparison.Ordinal));
            if (enRuta > finalizados)
            {
                hallazgos.Add("Hay mas unidades en ruta que finalizadas; revisar cuellos de botella de descarga.");
            }

            var estadosNoMapeados = rows.Count(r => string.IsNullOrWhiteSpace(MapEstadoCategoria(r.Estado)));
            if (estadosNoMapeados > 0)
            {
                hallazgos.Add($"Se detectaron {estadosNoMapeados} registros con estado no estandar.");
            }

            if (!hallazgos.Any())
            {
                hallazgos.Add("Sin hallazgos criticos: operacion estable en el periodo filtrado.");
            }

            return hallazgos.Take(5).ToList();
        }

        private List<OperacionesReportesCategoriaViewModel> BuildReportesCategorias(
            OperacionesReportesFiltrosViewModel filtros,
            OperacionesReportesResumenEjecutivoViewModel resumen,
            IReadOnlyList<OperacionesReportesComparativoViewModel> comparativos,
            IReadOnlyList<ToneladasRutaAggregateRow> toneladasRuta,
            SeguimientoDieselViewModel diesel,
            OperacionesReporteGerencialMensualToneladasViewModel gerencialMensual)
        {
            OperacionesReportesItemViewModel Item(
                string nombre,
                string descripcion,
                IReadOnlyList<string> highlights,
                string pdfAction,
                string excelAction,
                string csvAction)
            {
                return new OperacionesReportesItemViewModel
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Highlights = highlights.ToList(),
                    PdfDownloadUrl = BuildReportActionUrl(pdfAction, filtros, false),
                    PdfPreviewUrl = BuildReportActionUrl(pdfAction, filtros, true),
                    ExcelDownloadUrl = BuildReportActionUrl(excelAction, filtros, false),
                    CsvDownloadUrl = BuildReportActionUrl(csvAction, filtros, false)
                };
            }

            var cumplimientoHighlights = BuildCumplimientoHighlights(resumen, comparativos);
            var toneladasHighlights = BuildToneladasRutaHighlights(toneladasRuta);
            var dieselHighlights = BuildDieselHighlights(diesel);
            var gerencialMensualHighlights = BuildGerencialMensualHighlights(gerencialMensual);

            return new List<OperacionesReportesCategoriaViewModel>
            {
                new()
                {
                    Nombre = "Ejecutivo",
                    Descripcion = "Vista de cumplimiento y comparativos para gerencia.",
                    Reportes = new List<OperacionesReportesItemViewModel>
                    {
                        Item(
                            "Cumplimiento Operativo",
                            "Incluye comparativos dia/semana/mes, semaforo de estado y meta operativa.",
                            cumplimientoHighlights,
                            nameof(ExportReporteCumplimientoOperativoPdf),
                            nameof(ExportReporteCumplimientoOperativoExcel),
                            nameof(ExportReporteCumplimientoOperativoCsv)),
                        Item(
                            "Gerencial mensual toneladas por sitio",
                            "Reporte ejecutivo mensual con comportamiento por sitio, top operadores/equipos, bottom 5 y dias con meta cumplida.",
                            gerencialMensualHighlights,
                            nameof(ExportReporteGerencialMensualToneladasPdf),
                            nameof(ExportReporteGerencialMensualToneladasExcel),
                            nameof(ExportReporteGerencialMensualToneladasCsv))
                    }
                },
                new()
                {
                    Nombre = "Produccion",
                    Descripcion = "Toneladas movilizadas y rendimiento por ruta.",
                    Reportes = new List<OperacionesReportesItemViewModel>
                    {
                        Item(
                            "Toneladas por ruta",
                            "Acumulado por ruta con rendimiento T/GAL, L/T y variacion vs promedio.",
                            toneladasHighlights,
                            nameof(ExportReporteToneladasRutaPdf),
                            nameof(ExportReporteToneladasRutaExcel),
                            nameof(ExportReporteToneladasRutaCsv))
                    }
                },
                new()
                {
                    Nombre = "Consumo",
                    Descripcion = "Consumo de diesel y eficiencia por unidad.",
                    Reportes = new List<OperacionesReportesItemViewModel>
                    {
                        Item(
                            "Diesel por unidad",
                            "Indicadores por unidad con estado, variacion y recomendacion operativa.",
                            dieselHighlights,
                            nameof(ExportReporteDieselUnidadPdf),
                            nameof(ExportReporteDieselUnidadExcel),
                            nameof(ExportReporteDieselUnidadCsv))
                    }
                },
                new()
                {
                    Nombre = "Historico",
                    Descripcion = "Trazabilidad completa de reportes generados.",
                    Reportes = new List<OperacionesReportesItemViewModel>
                    {
                        new()
                        {
                            Nombre = "Bitacora de generacion",
                            Descripcion = "Exporta historial de reportes en CSV para auditoria.",
                            PdfDownloadUrl = "#",
                            PdfPreviewUrl = "#",
                            ExcelDownloadUrl = "#",
                            CsvDownloadUrl = Url.Action(nameof(ExportReportesAuditoriaCsv), "Operaciones") ?? "#"
                        }
                    }
                }
            };
        }

        private static IReadOnlyList<string> BuildCumplimientoHighlights(
            OperacionesReportesResumenEjecutivoViewModel resumen,
            IReadOnlyList<OperacionesReportesComparativoViewModel> comparativos)
        {
            var mes = comparativos.FirstOrDefault(x => string.Equals(x.Periodo, "Mes", StringComparison.OrdinalIgnoreCase));
            var eficiencia = resumen.DieselGalones > 0m
                ? Math.Round(resumen.Toneladas / resumen.DieselGalones, 3, MidpointRounding.AwayFromZero)
                : 0m;

            return new List<string>
            {
                $"Cumplimiento actual: {resumen.CumplimientoPorcentaje}% | Estado: {resumen.EstadoOperativo}.",
                mes is null
                    ? "Sin comparativo mensual disponible para este filtro."
                    : $"Delta mensual: {mes.DeltaToneladas:N1} Tn ({mes.DeltaPorcentaje:N1}%).",
                $"Eficiencia global: {eficiencia:N3} T/GAL con {resumen.Registros} registros."
            };
        }

        private static IReadOnlyList<string> BuildToneladasRutaHighlights(IReadOnlyList<ToneladasRutaAggregateRow> rows)
        {
            if (rows.Count == 0)
            {
                return new List<string>
                {
                    "Sin datos por ruta para el filtro aplicado.",
                    "No se puede calcular rendimiento por ruta.",
                    "Ajusta fechas, sitio o estado para visualizar datos."
                };
            }

            var topRuta = rows.OrderByDescending(x => x.Toneladas).First();
            var critica = rows
                .Where(x => string.Equals(x.EstadoRendimiento, "CRITICO", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.DieselGalones)
                .FirstOrDefault();
            var promedioRuta = rows.Average(x => x.ToneladasPorGalon);

            return new List<string>
            {
                $"Top ruta: {topRuta.Ruta} con {topRuta.Toneladas:N1} Tn ({topRuta.ParticipacionToneladasPct:N1}% del total).",
                critica is null
                    ? "No hay rutas criticas en rendimiento para este filtro."
                    : $"Ruta critica: {critica.Ruta} ({critica.ToneladasPorGalon:N3} T/GAL, {critica.VariacionVsPromedioPct:N1}% vs prom.).",
                $"Promedio de rendimiento por ruta: {promedioRuta:N3} T/GAL."
            };
        }

        private static IReadOnlyList<string> BuildDieselHighlights(SeguimientoDieselViewModel model)
        {
            if (model.Rows.Count == 0)
            {
                return new List<string>
                {
                    "Sin unidades para el filtro aplicado.",
                    "No se puede estimar sobreconsumo.",
                    "Ajusta fechas, sitio o estado para visualizar datos."
                };
            }

            var mayorConsumo = model.Rows
                .OrderByDescending(x => x.GalonesDespachados)
                .First();
            var critica = model.Rows.Count(x => string.Equals(x.EstadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase));
            var vigilar = model.Rows.Count(x => string.Equals(x.EstadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase));

            return new List<string>
            {
                $"Consumo promedio por evento: {model.ConsumoPromedioGalonesPorEvento:N2} gal (meta {model.ObjetivoGalonesPorEvento:N1} gal).",
                $"Rendimiento estimado: {model.RendimientoPromedioKmPorGalon:N2} km/gal (objetivo {model.ObjetivoKmPorGalon:N1} km/gal).",
                $"Unidades criticas: {critica} | En vigilar: {vigilar} | Optimas: {model.EquiposEstadoOptimo}.",
                $"Mayor consumo: {mayorConsumo.Unidad} con {mayorConsumo.GalonesDespachados:N2} gal."
            };
        }

        private static IReadOnlyList<string> BuildGerencialMensualHighlights(OperacionesReporteGerencialMensualToneladasViewModel model)
        {
            if (model.TotalRegistros <= 0)
            {
                return new List<string>
                {
                    "Sin registros para construir el reporte gerencial mensual.",
                    "No hay top de operadores ni equipos con el filtro actual.",
                    "Ajusta filtros o carga datos del mes para generar el reporte."
                };
            }

            var topOperador = model.TopOperadores.FirstOrDefault();
            var topEquipo = model.TopEquipos.FirstOrDefault();
            var mejorCumplimientoSitio = model.CumplimientoPorSitio
                .OrderByDescending(x => x.CumplimientoDiasPct)
                .ThenByDescending(x => x.DiasMetaCumplida)
                .FirstOrDefault();

            return new List<string>
            {
                $"Periodo {model.PeriodoLabel}: {model.TotalToneladas:N1} Tn con {model.TotalRegistros} registros.",
                topOperador is null
                    ? "Sin top operador para el periodo."
                    : $"Top operador: {topOperador.Nombre} ({topOperador.Toneladas:N1} Tn).",
                topEquipo is null
                    ? "Sin top equipo para el periodo."
                    : $"Top equipo: {topEquipo.Nombre} ({topEquipo.Toneladas:N1} Tn).",
                mejorCumplimientoSitio is null
                    ? "Sin detalle de cumplimiento por sitio."
                    : $"Mayor cumplimiento diario: {mejorCumplimientoSitio.Sitio} con {mejorCumplimientoSitio.CumplimientoDiasPct:N1}% de dias en meta."
            };
        }

        private string BuildReportActionUrl(string action, OperacionesReportesFiltrosViewModel filtros, bool inline)
        {
            return Url.Action(action, "Operaciones", new
            {
                fechaDesde = filtros.FechaDesde,
                fechaHasta = filtros.FechaHasta,
                sitio = filtros.Sitio,
                turno = filtros.Turno,
                conductor = filtros.Conductor,
                equipo = filtros.Equipo,
                estado = filtros.Estado,
                inline
            }) ?? "#";
        }

        private static FileContentResult BuildCsvResult(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string fileName)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
            }

            var csvText = sb.ToString();
            var preamble = Encoding.UTF8.GetPreamble();
            var data = Encoding.UTF8.GetBytes(csvText);
            var bytes = new byte[preamble.Length + data.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(data, 0, bytes, preamble.Length, data.Length);

            return new FileContentResult(bytes, "text/csv; charset=utf-8")
            {
                FileDownloadName = fileName
            };
        }

        private static string EscapeCsvField(string? value)
        {
            var text = value ?? string.Empty;
            if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
            {
                return text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static FileContentResult BuildExcelResult(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string fileName)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Reporte" : sheetName);

            for (var col = 0; col < headers.Count; col++)
            {
                worksheet.Cells[1, col + 1].Value = headers[col];
                worksheet.Cells[1, col + 1].Style.Font.Bold = true;
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (var col = 0; col < headers.Count && col < row.Length; col++)
                {
                    worksheet.Cells[rowIndex + 2, col + 1].Value = row[col];
                }
            }

            if (worksheet.Dimension is not null)
            {
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            var bytes = package.GetAsByteArray();
            return new FileContentResult(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                FileDownloadName = fileName
            };
        }

        private List<OperacionesReporteAuditEntry> LoadOperacionesReportesAuditoria()
        {
            var path = GetOperacionesReportesAuditPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<OperacionesReporteAuditEntry>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<OperacionesReporteAuditEntry>();
                }

                return JsonSerializer.Deserialize<List<OperacionesReporteAuditEntry>>(json)
                    ?? new List<OperacionesReporteAuditEntry>();
            }
            catch
            {
                return new List<OperacionesReporteAuditEntry>();
            }
        }

        private void SaveOperacionesReportesAuditoria(List<OperacionesReporteAuditEntry> logs)
        {
            var path = GetOperacionesReportesAuditPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var depurado = logs
                .Where(x => x.GeneratedAt.Date == DateTime.Today)
                .OrderByDescending(x => x.GeneratedAt)
                .Take(5000)
                .ToList();

            var json = JsonSerializer.Serialize(depurado, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            System.IO.File.WriteAllText(path, json);
        }

        private string GetOperacionesReportesAuditPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", OperacionesReportesAuditFileName);
        }

        private void RegistrarReporteGenerado(
            string reportName,
            string format,
            OperacionesReportesFiltrosViewModel filtros,
            int recordCount)
        {
            try
            {
                var logs = LoadOperacionesReportesAuditoria();
                logs.Add(new OperacionesReporteAuditEntry
                {
                    GeneratedAt = DateTime.Now,
                    UserName = User.Identity?.Name ?? "Sistema",
                    ReportName = reportName,
                    Format = format,
                    RecordCount = recordCount,
                    FiltersSummary = BuildFiltrosResumen(filtros),
                    Version = "v1.0"
                });
                SaveOperacionesReportesAuditoria(logs);
            }
            catch
            {
                // No se bloquea la exportacion por errores de auditoria.
            }
        }

        private List<OperacionesReporteScheduleEntry> LoadOperacionesReportesProgramaciones()
        {
            var path = GetOperacionesReportesSchedulePath();
            if (!System.IO.File.Exists(path))
            {
                return new List<OperacionesReporteScheduleEntry>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<OperacionesReporteScheduleEntry>();
                }

                return JsonSerializer.Deserialize<List<OperacionesReporteScheduleEntry>>(json)
                    ?? new List<OperacionesReporteScheduleEntry>();
            }
            catch
            {
                return new List<OperacionesReporteScheduleEntry>();
            }
        }

        private void SaveOperacionesReportesProgramaciones(List<OperacionesReporteScheduleEntry> schedules)
        {
            var path = GetOperacionesReportesSchedulePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(
                schedules.OrderByDescending(x => x.CreatedAt).ToList(),
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            System.IO.File.WriteAllText(path, json);
        }

        private string GetOperacionesReportesSchedulePath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", OperacionesReportesScheduleFileName);
        }

        private void ApplyMetasProduccionMensual(SeguimientoToneladasViewModel model)
        {
            if (model is null)
            {
                return;
            }

            var fechaReferencia = model.FechaOperativa
                ?? model.Rows
                    .Where(r => r.FechaEvento.HasValue)
                    .Select(r => r.FechaEvento!.Value.Date)
                    .DefaultIfEmpty(DateTime.Now.Date)
                    .Max();
            var metaObjetivo = ResolveMetaObjetivo(fechaReferencia);
            model.MetaAnio = metaObjetivo.Year;
            model.MetaMes = metaObjetivo.Month;
            model.MetaMensualTriton = metaObjetivo.MetaMensualTriton;
            model.MetaMensualPavonAsm = metaObjetivo.MetaMensualPavonAsm;
            model.MetaMensualTotal = metaObjetivo.MetaMensualTotal;
            model.MetaDiariaTritonObjetivo = metaObjetivo.MetaDiariaTriton;
            model.MetaDiariaPavonAsmObjetivo = metaObjetivo.MetaDiariaPavonAsm;
            model.MetaDiariaObjetivo = metaObjetivo.MetaDiaria;
            model.MetaMensualPersonalizada = metaObjetivo.IsCustom;
        }

        private OperacionesMetaObjetivo ResolveMetaObjetivo(DateTime fechaReferencia)
        {
            var metas = ResolveMetasProduccionMensual(fechaReferencia);
            var metaMensualTriton = Math.Max(0m, metas.MetaMensualTriton);
            var metaMensualPavonAsm = 0m;
            var metaMensualTotal = Math.Round(metaMensualTriton, 1, MidpointRounding.AwayFromZero);
            var metaDiariaTriton = metas.MetaDiariaTritonObjetivo > 0m
                ? Math.Round(metas.MetaDiariaTritonObjetivo, 1, MidpointRounding.AwayFromZero)
                : DefaultMetaDiariaTritonObjetivo;
            var metaDiariaPavonAsm = 0m;
            var metaDiaria = Math.Round(metaDiariaTriton, 1, MidpointRounding.AwayFromZero);

            return new OperacionesMetaObjetivo
            {
                Year = fechaReferencia.Year,
                Month = fechaReferencia.Month,
                MetaMensualTriton = metaMensualTriton,
                MetaMensualPavonAsm = metaMensualPavonAsm,
                MetaMensualTotal = metaMensualTotal,
                MetaDiariaTriton = metaDiariaTriton,
                MetaDiariaPavonAsm = metaDiariaPavonAsm,
                MetaDiaria = metaDiaria,
                IsCustom = metas.IsCustom
            };
        }

        private Dictionary<int, decimal> BuildMetaMensualObjetivosByPeriodo(int year, int upToMonth)
        {
            var objetivos = new Dictionary<int, decimal>();
            var mesMaximo = Math.Clamp(upToMonth, 1, 12);

            for (var month = 1; month <= mesMaximo; month++)
            {
                var fecha = new DateTime(year, month, 1);
                var meta = ResolveMetaObjetivo(fecha);
                objetivos[(fecha.Year * 100) + fecha.Month] = meta.MetaMensualTotal;
            }

            var fechaSiguiente = new DateTime(year, mesMaximo, 1).AddMonths(1);
            var metaSiguiente = ResolveMetaObjetivo(fechaSiguiente);
            objetivos[(fechaSiguiente.Year * 100) + fechaSiguiente.Month] = metaSiguiente.MetaMensualTotal;

            return objetivos;
        }

        private (
            decimal MetaMensualTriton,
            decimal MetaMensualPavonAsm,
            decimal MetaDiariaTritonObjetivo,
            decimal MetaDiariaPavonAsmObjetivo,
            bool IsCustom) ResolveMetasProduccionMensual(DateTime fechaReferencia)
        {
            var entries = LoadOperacionesMetasProduccionMensual();
            var item = entries
                .Where(x => x.Year == fechaReferencia.Year && x.Month == fechaReferencia.Month)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            if (item is null)
            {
                return (
                    DefaultMetaMensualTriton,
                    DefaultMetaMensualPavonAsm,
                    DefaultMetaDiariaTritonObjetivo,
                    DefaultMetaDiariaPavonAsmObjetivo,
                    false);
            }

            var metaDiariaTriton = item.MetaDiariaTritonObjetivo;
            var metaDiariaPavonAsm = item.MetaDiariaPavonAsmObjetivo;

            if (metaDiariaTriton <= 0m || metaDiariaPavonAsm <= 0m)
            {
                var legacyMetaTotal = item.MetaDiariaObjetivo > 0m ? item.MetaDiariaObjetivo : DefaultMetaDiariaObjetivo;
                var metaMensualTotal = Math.Max(0m, item.MetaMensualTriton) + Math.Max(0m, item.MetaMensualPavonAsm);

                if (metaMensualTotal > 0m)
                {
                    metaDiariaTriton = Math.Round(
                        legacyMetaTotal * Math.Max(0m, item.MetaMensualTriton) / metaMensualTotal,
                        1,
                        MidpointRounding.AwayFromZero);
                    metaDiariaPavonAsm = Math.Round(Math.Max(0m, legacyMetaTotal - metaDiariaTriton), 1, MidpointRounding.AwayFromZero);
                }
                else
                {
                    metaDiariaTriton = DefaultMetaDiariaTritonObjetivo;
                    metaDiariaPavonAsm = DefaultMetaDiariaPavonAsmObjetivo;
                }
            }

            return (
                item.MetaMensualTriton,
                item.MetaMensualPavonAsm,
                metaDiariaTriton > 0m ? metaDiariaTriton : DefaultMetaDiariaTritonObjetivo,
                metaDiariaPavonAsm > 0m ? metaDiariaPavonAsm : DefaultMetaDiariaPavonAsmObjetivo,
                true);
        }

        private List<OperacionesMetaProduccionMensualEntry> LoadOperacionesMetasProduccionMensual()
        {
            var path = GetOperacionesMetasProduccionMensualPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<OperacionesMetaProduccionMensualEntry>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<OperacionesMetaProduccionMensualEntry>();
                }

                return JsonSerializer.Deserialize<List<OperacionesMetaProduccionMensualEntry>>(json)
                    ?? new List<OperacionesMetaProduccionMensualEntry>();
            }
            catch
            {
                return new List<OperacionesMetaProduccionMensualEntry>();
            }
        }

        private void SaveOperacionesMetasProduccionMensual(List<OperacionesMetaProduccionMensualEntry> entries)
        {
            var path = GetOperacionesMetasProduccionMensualPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var cleanEntries = entries
                .Where(x => x.Year > 0 && x.Month >= 1 && x.Month <= 12)
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            var json = JsonSerializer.Serialize(cleanEntries, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            System.IO.File.WriteAllText(path, json);
        }

        private string GetOperacionesMetasProduccionMensualPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", OperacionesMetasProduccionMensualFileName);
        }

        private string GetSeguimientoDieselUnitExtrasPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", SeguimientoDieselUnitExtrasFileName);
        }

        private List<DieselUnitExtraRecord> LoadSeguimientoDieselUnitExtras()
        {
            var path = GetSeguimientoDieselUnitExtrasPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<DieselUnitExtraRecord>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<DieselUnitExtraRecord>();
                }

                var raw = JsonSerializer.Deserialize<List<DieselUnitExtraRecord>>(json)
                    ?? new List<DieselUnitExtraRecord>();

                return raw
                    .Where(x => !string.IsNullOrWhiteSpace(x.Unidad) &&
                                (x.GalonesExtra > 0m || x.KilometrosExtra > 0m))
                    .Select(x => new DieselUnitExtraRecord
                    {
                        Unidad = NormalizeDieselUnidadKey(x.Unidad),
                        GalonesExtra = Math.Round(Math.Max(0m, x.GalonesExtra), 2, MidpointRounding.AwayFromZero),
                        KilometrosExtra = Math.Round(Math.Max(0m, x.KilometrosExtra), 2, MidpointRounding.AwayFromZero),
                        UpdatedAt = x.UpdatedAt,
                        UpdatedBy = string.IsNullOrWhiteSpace(x.UpdatedBy) ? "Sistema" : x.UpdatedBy.Trim()
                    })
                    .ToList();
            }
            catch
            {
                return new List<DieselUnitExtraRecord>();
            }
        }

        private void SaveSeguimientoDieselUnitExtras(List<DieselUnitExtraRecord> extras)
        {
            var path = GetSeguimientoDieselUnitExtrasPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var clean = extras
                .Where(x => !string.IsNullOrWhiteSpace(x.Unidad) &&
                            (x.GalonesExtra > 0m || x.KilometrosExtra > 0m))
                .GroupBy(x => NormalizeDieselUnidadKey(x.Unidad), StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(x => x.UpdatedAt)
                    .ThenByDescending(x => x.GalonesExtra + x.KilometrosExtra)
                    .First())
                .Select(x => new DieselUnitExtraRecord
                {
                    Unidad = NormalizeDieselUnidadKey(x.Unidad),
                    GalonesExtra = Math.Round(Math.Max(0m, x.GalonesExtra), 2, MidpointRounding.AwayFromZero),
                    KilometrosExtra = Math.Round(Math.Max(0m, x.KilometrosExtra), 2, MidpointRounding.AwayFromZero),
                    UpdatedAt = x.UpdatedAt == default ? DateTime.Now : x.UpdatedAt,
                    UpdatedBy = string.IsNullOrWhiteSpace(x.UpdatedBy) ? "Sistema" : x.UpdatedBy.Trim()
                })
                .OrderBy(x => x.Unidad, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var json = JsonSerializer.Serialize(clean, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            System.IO.File.WriteAllText(path, json);
        }

        private static decimal ParseNonNegativeDecimal(string? raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentCultureParsed))
            {
                return Math.Round(Math.Max(0m, currentCultureParsed), 2, MidpointRounding.AwayFromZero);
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantParsed))
            {
                return Math.Round(Math.Max(0m, invariantParsed), 2, MidpointRounding.AwayFromZero);
            }

            return 0m;
        }

        private static string NormalizeDieselUnidadKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static DateTime ComputeNextRun(DateTime now, string frecuencia, string diaSemana, TimeSpan hora)
        {
            var next = now.Date.Add(hora);
            if (string.Equals(frecuencia, "DIARIA", StringComparison.OrdinalIgnoreCase))
            {
                if (next <= now)
                {
                    next = next.AddDays(1);
                }

                return next;
            }

            var targetDay = diaSemana switch
            {
                "MONDAY" => DayOfWeek.Monday,
                "TUESDAY" => DayOfWeek.Tuesday,
                "WEDNESDAY" => DayOfWeek.Wednesday,
                "THURSDAY" => DayOfWeek.Thursday,
                "FRIDAY" => DayOfWeek.Friday,
                "SATURDAY" => DayOfWeek.Saturday,
                "SUNDAY" => DayOfWeek.Sunday,
                _ => DayOfWeek.Monday
            };

            var probe = now.Date;
            while (probe.DayOfWeek != targetDay)
            {
                probe = probe.AddDays(1);
            }

            next = probe.Add(hora);
            if (next <= now)
            {
                next = next.AddDays(7);
            }

            return next;
        }

        private static string BuildFiltrosResumen(OperacionesReportesFiltrosViewModel filtros)
        {
            return $"Desde:{(string.IsNullOrWhiteSpace(filtros.FechaDesde) ? "-" : filtros.FechaDesde)} | " +
                   $"Hasta:{(string.IsNullOrWhiteSpace(filtros.FechaHasta) ? "-" : filtros.FechaHasta)} | " +
                   $"Sitio:{filtros.Sitio} | Turno:{filtros.Turno} | Conductor:{(string.IsNullOrWhiteSpace(filtros.Conductor) ? "-" : filtros.Conductor)} | " +
                   $"Equipo:{(string.IsNullOrWhiteSpace(filtros.Equipo) ? "-" : filtros.Equipo)} | Estado:{filtros.Estado}";
        }

        private static List<ToneladasRutaAggregateRow> BuildToneladasRutaAggregatedRows(IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            var aggregated = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Ruta) ? "Sin ruta" : r.Ruta.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var dieselGal = g.Sum(x => x.CombustibleLitros);
                    var toneladas = g.Sum(x => x.Toneladas);
                    var litros = dieselGal * LitrosPorGalon;
                    return new ToneladasRutaAggregateRow
                    {
                        Ruta = g.Key,
                        Toneladas = Math.Round(toneladas, 1),
                        DieselGalones = Math.Round(dieselGal, 2),
                        DieselLitros = Math.Round(litros, 2),
                        Equipos = g.Select(x => (x.Equipo ?? string.Empty).Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count(),
                        Viajes = g.Sum(x => x.Viajes),
                        ToneladasPorGalon = dieselGal > 0m
                            ? Math.Round(toneladas / dieselGal, 3, MidpointRounding.AwayFromZero)
                            : 0m,
                        LitrosPorTonelada = toneladas > 0m
                            ? Math.Round(litros / toneladas, 3, MidpointRounding.AwayFromZero)
                            : 0m
                    };
                })
                .ToList();

            var totalToneladas = aggregated.Sum(x => x.Toneladas);
            var totalGalones = aggregated.Sum(x => x.DieselGalones);
            var rendimientoPromedio = totalGalones > 0m
                ? Math.Round(totalToneladas / totalGalones, 3, MidpointRounding.AwayFromZero)
                : 0m;

            foreach (var row in aggregated)
            {
                row.ParticipacionToneladasPct = totalToneladas > 0m
                    ? Math.Round((row.Toneladas / totalToneladas) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m;
                row.VariacionVsPromedioPct = rendimientoPromedio > 0m
                    ? Math.Round(((row.ToneladasPorGalon - rendimientoPromedio) / rendimientoPromedio) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m;
                row.EstadoRendimiento = ResolveEstadoConsumo(row.ToneladasPorGalon, row.VariacionVsPromedioPct);
            }

            return aggregated
                .OrderByDescending(x => x.Toneladas)
                .ThenBy(x => x.Ruta)
                .ToList();
        }

        private static SeguimientoToneladasViewModel CreateSampleToneladasModel()
        {
            var rows = new List<SeguimientoToneladasRowViewModel>
            {
                new()
                {
                    Equipo = "Cabezal 12",
                    Procedencia = "PAVON ASM",
                    Ruta = "Managua - Puerto",
                    Toneladas = 128.4m,
                    Viajes = 6,
                    CombustibleLitros = 420m,
                    FechaEvento = DateTime.Now,
                    Conductor = "Luis Perez",
                    Estado = "FINALIZADO",
                    Eficiencia = "Alta"
                },
                new()
                {
                    Equipo = "Cabezal 18",
                    Procedencia = "TRITON",
                    Ruta = "Leon - Managua",
                    Toneladas = 94.1m,
                    PesoSugerido = 98.7m,
                    Viajes = 4,
                    CombustibleLitros = 390m,
                    FechaEvento = DateTime.Now,
                    Conductor = "Miguel Diaz",
                    Estado = "EN RUTA",
                    Eficiencia = "Media"
                },
                new()
                {
                    Equipo = "Volqueta 05",
                    Procedencia = "PAVON ASM",
                    Ruta = "Chinandega - Puerto",
                    Toneladas = 76.8m,
                    Viajes = 3,
                    CombustibleLitros = 265m,
                    FechaEvento = DateTime.Now,
                    Conductor = "Carlos Mejia",
                    Estado = "FINALIZADO",
                    Eficiencia = "Alta"
                }
            };

            return BuildToneladasModel(rows);
        }

        private static SeguimientoToneladasViewModel BuildToneladasModel(List<SeguimientoToneladasRowViewModel> rows)
        {
            var totalToneladas = rows.Sum(r => r.Toneladas);
            var totalViajes = rows.Sum(r => r.Viajes);
            var totalCombustibleLitros = rows.Sum(r => r.CombustibleLitros);
            var rowsConFecha = rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();
            var fechaOperativa = rowsConFecha.Any()
                ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                : (DateTime?)null;
            var monthReference = fechaOperativa ?? DateTime.Now.Date;
            var toneladasAcumuladasMes = rows
                .Where(r =>
                {
                    if (!r.FechaEvento.HasValue)
                    {
                        return false;
                    }

                    var d = r.FechaEvento.Value.Date;
                    return d.Year == monthReference.Year && d.Month == monthReference.Month;
                })
                .Sum(r => r.Toneladas);
            var rowsBaseEstado = fechaOperativa.HasValue
                ? rowsConFecha.Where(r => r.FechaEvento!.Value.Date == fechaOperativa.Value).ToList()
                : rows;
            var estadoResumen = BuildEstadoResumen(rowsBaseEstado);
            var toneladasAlDia = rowsBaseEstado
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .Sum(r => r.Toneladas);
            var equiposDescargados = estadoResumen
                .FirstOrDefault(x => x.Estado == "FINALIZADO")
                ?.Equipos ?? 0;
            var equiposEnRuta = estadoResumen
                .FirstOrDefault(x => x.Estado == "EN RUTA")
                ?.Equipos ?? 0;
            var toneladasEnRuta = rowsBaseEstado
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "EN RUTA", StringComparison.Ordinal))
                .Sum(GetToneladasEnRuta);

            return new SeguimientoToneladasViewModel
            {
                Rows = rows,
                EstadoResumen = estadoResumen,
                TotalToneladas = totalToneladas,
                ToneladasAcumuladasMes = toneladasAcumuladasMes,
                TotalViajes = totalViajes,
                PromedioPorViaje = totalViajes > 0
                    ? Math.Round(totalToneladas / totalViajes, 1)
                    : 0,
                TotalEquipos = rows
                    .Select(r => r.Equipo.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                ToneladasAlDia = toneladasAlDia,
                TotalCombustibleLitros = totalCombustibleLitros,
                EquiposDescargados = equiposDescargados,
                EquiposEnRuta = equiposEnRuta,
                ToneladasEnRuta = toneladasEnRuta,
                FechaOperativa = fechaOperativa
            };
        }

        private SeguimientoDieselViewModel BuildDieselModelFromSeguimiento(SeguimientoToneladasViewModel seguimiento)
        {
            var rowsConFecha = seguimiento.Rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            var fechaCorte = seguimiento.FechaOperativa
                ?? (rowsConFecha.Any()
                    ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                    : (DateTime?)null);

            var rowsBase = fechaCorte.HasValue
                ? rowsConFecha
                    .Where(r => r.FechaEvento!.Value.Year == fechaCorte.Value.Year &&
                                r.FechaEvento.Value.Month == fechaCorte.Value.Month)
                    .ToList()
                : seguimiento.Rows.ToList();

            var rowsOperativas = rowsBase
                .Where(r => !string.IsNullOrWhiteSpace((r.Equipo ?? string.Empty).Trim()))
                .ToList();

            var ajustesExtrasByUnidad = LoadSeguimientoDieselUnitExtras()
                .Where(x => !string.IsNullOrWhiteSpace(x.Unidad))
                .GroupBy(x => NormalizeDieselUnidadKey(x.Unidad), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.UpdatedAt).First(),
                    StringComparer.OrdinalIgnoreCase);

            var eventosConGalon = rowsOperativas
                .Where(r => r.CombustibleLitros > 0)
                .Select(r => r.CombustibleLitros)
                .ToList();
            var mediaEvento = eventosConGalon.Any() ? eventosConGalon.Average() : 0m;
            var desviacionEvento = eventosConGalon.Any()
                ? (decimal)Math.Sqrt((double)eventosConGalon.Average(x => Math.Pow((double)(x - mediaEvento), 2)))
                : 0m;
            var umbralAtipico = mediaEvento + (desviacionEvento * 2m);

            var agregados = rowsOperativas
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var unidadKey = NormalizeDieselUnidadKey(g.Key);
                    var extra = ajustesExtrasByUnidad.TryGetValue(unidadKey, out var extraRow)
                        ? extraRow
                        : null;
                    var galonesExtra = extra?.GalonesExtra ?? 0m;
                    var kilometrosExtra = extra?.KilometrosExtra ?? 0m;

                    var galonesBase = Math.Round(g.Sum(x => x.CombustibleLitros), 2);
                    var galones = Math.Round(galonesBase + galonesExtra, 2, MidpointRounding.AwayFromZero);
                    if (galones < 0m)
                    {
                        galones = 0m;
                    }

                    var litros = Math.Round(galones * LitrosPorGalon, 2);
                    var toneladas = Math.Round(g.Sum(x => x.Toneladas), 2);
                    var eventos = g.Count();
                    var distanciaTotalKm = Math.Round((eventos * DieselDistanciaPromedioKmPorEvento) + kilometrosExtra, 2, MidpointRounding.AwayFromZero);
                    if (distanciaTotalKm < 0m)
                    {
                        distanciaTotalKm = 0m;
                    }

                    var galonesPromedioEvento = eventos > 0
                        ? Math.Round(galones / eventos, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    var rendimientoEstimadoKmPorGalon = galones > 0
                        ? Math.Round(distanciaTotalKm / galones, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    var variacionVsMetaConsumoPct = DieselObjetivoGalonesPorEvento > 0m
                        ? Math.Round(((galonesPromedioEvento - DieselObjetivoGalonesPorEvento) / DieselObjetivoGalonesPorEvento) * 100m, 1, MidpointRounding.AwayFromZero)
                        : 0m;
                    var tonPorGalon = galones > 0 ? Math.Round(toneladas / galones, 3) : 0m;
                    var litrosPorTon = toneladas > 0 ? Math.Round(litros / toneladas, 3) : 0m;
                    var rutaPrincipal = g
                        .Select(x => string.IsNullOrWhiteSpace(x.Ruta) ? "Sin ruta" : x.Ruta.Trim())
                        .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(x => x.Count())
                        .ThenBy(x => x.Key)
                        .Select(x => x.Key)
                        .FirstOrDefault() ?? "Sin ruta";
                    var eventosNocturnos = g.Count(x =>
                        x.FechaEvento.HasValue &&
                        (x.FechaEvento.Value.Hour >= 22 || x.FechaEvento.Value.Hour <= 5));
                    var eventosAtipicos = g.Count(x => x.CombustibleLitros > 0 && x.CombustibleLitros >= umbralAtipico && umbralAtipico > 0);
                    return new DieselUnitAggregate
                    {
                        Unidad = g.Key,
                        Galones = galones,
                        GalonesExtra = galonesExtra,
                        KilometrosExtra = kilometrosExtra,
                        DistanciaTotalKm = distanciaTotalKm,
                        Litros = litros,
                        Toneladas = toneladas,
                        ToneladasPorGalon = tonPorGalon,
                        LitrosPorTonelada = litrosPorTon,
                        GalonesPromedioPorEvento = galonesPromedioEvento,
                        RendimientoEstimadoKmPorGalon = rendimientoEstimadoKmPorGalon,
                        VariacionVsMetaConsumoPorcentaje = variacionVsMetaConsumoPct,
                        EstadoOperacion = ResolveEstado(g.Select(x => x.Estado)),
                        RutaPrincipal = rutaPrincipal,
                        Eventos = eventos,
                        EventosNocturnos = eventosNocturnos,
                        EventosAtipicos = eventosAtipicos
                    };
                })
                .ToList();

            var totalGalones = Math.Round(agregados.Sum(x => x.Galones), 2);
            var totalLitros = Math.Round(agregados.Sum(x => x.Litros), 2);
            var totalToneladas = Math.Round(agregados.Sum(x => x.Toneladas), 2);
            var rendimientoPromedio = totalGalones > 0 ? Math.Round(totalToneladas / totalGalones, 3) : 0m;
            var intensidadLitrosTon = totalToneladas > 0 ? Math.Round(totalLitros / totalToneladas, 3) : 0m;

            foreach (var agg in agregados)
            {
                agg.VariacionPorcentaje = rendimientoPromedio > 0
                    ? Math.Round(((agg.ToneladasPorGalon - rendimientoPromedio) / rendimientoPromedio) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m;
                agg.EstadoConsumo = ResolveEstadoConsumoPorMeta(agg.GalonesPromedioPorEvento, agg.VariacionVsMetaConsumoPorcentaje);
            }

            var rows = agregados
                .Select(x => new SeguimientoDieselRowViewModel
                {
                    Unidad = x.Unidad,
                    GalonesDespachados = x.Galones,
                    GalonesExtra = x.GalonesExtra,
                    KilometrosExtra = x.KilometrosExtra,
                    GalonesObjetivoPorEvento = DieselObjetivoGalonesPorEvento,
                    GalonesPromedioPorEvento = x.GalonesPromedioPorEvento,
                    RendimientoEstimadoKmPorGalon = x.RendimientoEstimadoKmPorGalon,
                    VariacionVsMetaConsumoPorcentaje = x.VariacionVsMetaConsumoPorcentaje,
                    LitrosEquivalentes = x.Litros,
                    ToneladasMovilizadas = x.Toneladas,
                    ToneladasPorGalon = x.ToneladasPorGalon,
                    LitrosPorTonelada = x.LitrosPorTonelada,
                    VariacionVsPromedioPorcentaje = x.VariacionPorcentaje,
                    EventosOperativos = x.Eventos,
                    EventosNocturnos = x.EventosNocturnos,
                    EventosAtipicos = x.EventosAtipicos,
                    RutaPrincipal = x.RutaPrincipal,
                    EstadoConsumo = x.EstadoConsumo,
                    Estado = x.EstadoOperacion
                })
                .OrderBy(x => string.Equals(x.EstadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase) ? 0 :
                              string.Equals(x.EstadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenByDescending(x => x.VariacionVsMetaConsumoPorcentaje)
                .ThenByDescending(x => x.GalonesDespachados)
                .ThenBy(x => x.Unidad)
                .ToList();

            var alertas = BuildDieselAlertas(rows);
            var resumenRutas = rowsOperativas
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Ruta) ? "Sin ruta" : x.Ruta.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var gal = Math.Round(g.Sum(x => x.CombustibleLitros), 2);
                    var lit = Math.Round(gal * LitrosPorGalon, 2);
                    var ton = Math.Round(g.Sum(x => x.Toneladas), 2);
                    return new SeguimientoDieselRutaResumenViewModel
                    {
                        Ruta = g.Key,
                        Galones = gal,
                        Toneladas = ton,
                        ToneladasPorGalon = gal > 0 ? Math.Round(ton / gal, 3) : 0m,
                        LitrosPorTonelada = ton > 0 ? Math.Round(lit / ton, 3) : 0m
                    };
                })
                .OrderByDescending(x => x.Galones)
                .Take(12)
                .ToList();

            var cargasAtipicas = rowsOperativas
                .Where(x =>
                    (umbralAtipico > 0 && x.CombustibleLitros >= umbralAtipico) ||
                    (x.FechaEvento.HasValue && (x.FechaEvento.Value.Hour >= 22 || x.FechaEvento.Value.Hour <= 5) && x.CombustibleLitros >= mediaEvento))
                .OrderByDescending(x => x.CombustibleLitros)
                .Take(25)
                .Select(x => new SeguimientoDieselCargaAtipicaViewModel
                {
                    Unidad = (x.Equipo ?? string.Empty).Trim(),
                    Ruta = string.IsNullOrWhiteSpace(x.Ruta) ? "Sin ruta" : x.Ruta.Trim(),
                    FechaEvento = x.FechaEvento,
                    Galones = Math.Round(x.CombustibleLitros, 2),
                    Toneladas = Math.Round(x.Toneladas, 2),
                    Motivo = BuildCargaAtipicaMotivo(x, umbralAtipico, mediaEvento)
                })
                .ToList();

            var tendenciaSemanal = BuildDieselTrendByDay(rowsConFecha, fechaCorte ?? DateTime.Today, 7);
            var tendenciaMensual = BuildDieselTrendByMonth(rowsConFecha, fechaCorte ?? DateTime.Today, 6);
            var mantenimiento = LoadDieselMaintenanceData(rowsConFecha, fechaCorte);
            var costoReferencia = GetDieselCostoReferenciaGalonUsd();
            var equiposSobreconsumo = rows.Count(x => string.Equals(x.EstadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase));
            var porcentajeSobreconsumo = rows.Count > 0
                ? Math.Round((decimal)equiposSobreconsumo * 100m / rows.Count, 1, MidpointRounding.AwayFromZero)
                : 0m;
            var totalEventosOperativos = rows.Sum(x => x.EventosOperativos);
            var totalDistanciaOperativaKm = agregados.Sum(x => x.DistanciaTotalKm);
            var consumoPromedioGalonesEvento = totalEventosOperativos > 0
                ? Math.Round(totalGalones / totalEventosOperativos, 2, MidpointRounding.AwayFromZero)
                : 0m;
            var rendimientoPromedioKmPorGalon = totalGalones > 0
                ? Math.Round(totalDistanciaOperativaKm / totalGalones, 2, MidpointRounding.AwayFromZero)
                : 0m;

            return new SeguimientoDieselViewModel
            {
                Rows = rows,
                TotalGalones = totalGalones,
                TotalLitrosEquivalentes = totalLitros,
                DistanciaPromedioKm = DieselDistanciaPromedioKmPorEvento,
                ObjetivoKmPorGalon = DieselObjetivoKmPorGalon,
                ObjetivoGalonesPorEvento = DieselObjetivoGalonesPorEvento,
                ConsumoPromedioGalonesPorEvento = consumoPromedioGalonesEvento,
                RendimientoPromedioKmPorGalon = rendimientoPromedioKmPorGalon,
                RendimientoPromedioTonPorGalon = rendimientoPromedio,
                IntensidadLitrosPorTonelada = intensidadLitrosTon,
                CostoReferenciaGalonUsd = costoReferencia,
                CostoEstimadoTotalUsd = costoReferencia > 0
                    ? Math.Round(totalGalones * costoReferencia, 2, MidpointRounding.AwayFromZero)
                    : 0m,
                EquiposSobreconsumo = equiposSobreconsumo,
                PorcentajeEquiposSobreconsumo = porcentajeSobreconsumo,
                EquiposEstadoOptimo = rows.Count(x => string.Equals(x.EstadoConsumo, "OPTIMO", StringComparison.OrdinalIgnoreCase)),
                EquiposEstadoVigilar = rows.Count(x => string.Equals(x.EstadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase)),
                EquiposEstadoCritico = equiposSobreconsumo,
                FechaOperativa = fechaCorte,
                IsFromUpload = seguimiento.IsFromUpload,
                SourceFileName = seguimiento.SourceFileName,
                Alertas = alertas,
                ResumenPorRuta = resumenRutas,
                CargasAtipicas = cargasAtipicas,
                TendenciaSemanal = tendenciaSemanal,
                TendenciaMensual = tendenciaMensual,
                MantenimientoRegistros = mantenimiento
            };
        }

        private decimal GetDieselCostoReferenciaGalonUsd()
        {
            var configured = _configuration.GetValue<decimal?>("DieselTracking:CostoReferenciaGalonUsd")
                ?? _configuration.GetValue<decimal?>("Operaciones:DieselCostoReferenciaGalonUsd");
            return configured.HasValue && configured.Value > 0
                ? Math.Round(configured.Value, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        private static string ResolveEstadoConsumo(decimal tonPorGalon, decimal variacionPorcentaje)
        {
            if (tonPorGalon <= 0m)
            {
                return "CRITICO";
            }

            if (variacionPorcentaje <= -15m)
            {
                return "CRITICO";
            }

            if (variacionPorcentaje <= -8m)
            {
                return "VIGILAR";
            }

            return "OPTIMO";
        }

        private static string ResolveEstadoConsumoPorMeta(decimal galonesPromedioPorEvento, decimal variacionVsMetaConsumoPorcentaje)
        {
            if (galonesPromedioPorEvento <= 0m)
            {
                return "CRITICO";
            }

            if (variacionVsMetaConsumoPorcentaje > 25m)
            {
                return "CRITICO";
            }

            if (variacionVsMetaConsumoPorcentaje > 10m)
            {
                return "VIGILAR";
            }

            return "OPTIMO";
        }

        private static List<SeguimientoDieselAlertaViewModel> BuildDieselAlertas(IReadOnlyList<SeguimientoDieselRowViewModel> rows)
        {
            var alertas = new List<SeguimientoDieselAlertaViewModel>();
            foreach (var row in rows)
            {
                if (string.Equals(row.EstadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase))
                {
                    alertas.Add(new SeguimientoDieselAlertaViewModel
                    {
                        Nivel = "danger",
                        Unidad = row.Unidad,
                        Mensaje = $"Sobreconsumo detectado ({row.VariacionVsMetaConsumoPorcentaje}% vs meta {row.GalonesObjetivoPorEvento:N1} gal/evento)."
                    });
                }
                else if (string.Equals(row.EstadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase))
                {
                    alertas.Add(new SeguimientoDieselAlertaViewModel
                    {
                        Nivel = "warning",
                        Unidad = row.Unidad,
                        Mensaje = $"Consumo por vigilar ({row.VariacionVsMetaConsumoPorcentaje}% vs meta {row.GalonesObjetivoPorEvento:N1} gal/evento)."
                    });
                }

                if (row.EventosAtipicos > 0)
                {
                    alertas.Add(new SeguimientoDieselAlertaViewModel
                    {
                        Nivel = "warning",
                        Unidad = row.Unidad,
                        Mensaje = $"Registra {row.EventosAtipicos} carga(s) atipica(s) en el periodo."
                    });
                }
            }

            return alertas
                .OrderBy(x => x.Nivel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Unidad, StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .ToList();
        }

        private static string BuildCargaAtipicaMotivo(SeguimientoToneladasRowViewModel row, decimal umbralAtipico, decimal mediaEvento)
        {
            var motivos = new List<string>();
            if (umbralAtipico > 0 && row.CombustibleLitros >= umbralAtipico)
            {
                motivos.Add("Volumen alto");
            }

            if (row.FechaEvento.HasValue && (row.FechaEvento.Value.Hour >= 22 || row.FechaEvento.Value.Hour <= 5))
            {
                motivos.Add("Horario nocturno");
            }

            if (mediaEvento > 0 && row.CombustibleLitros >= mediaEvento * 1.4m)
            {
                motivos.Add("Muy por encima de promedio");
            }

            return motivos.Count == 0 ? "Monitorear" : string.Join(" + ", motivos);
        }

        private static List<SeguimientoDieselTrendPointViewModel> BuildDieselTrendByDay(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsConFecha,
            DateTime fechaReferencia,
            int dias)
        {
            var start = fechaReferencia.Date.AddDays(-(dias - 1));
            var grouped = rowsConFecha
                .Where(x => x.FechaEvento.HasValue && x.FechaEvento.Value.Date >= start && x.FechaEvento.Value.Date <= fechaReferencia.Date)
                .GroupBy(x => x.FechaEvento!.Value.Date)
                .ToDictionary(g => g.Key, g => new
                {
                    Galones = Math.Round(g.Sum(x => x.CombustibleLitros), 2),
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 2)
                });

            var points = new List<SeguimientoDieselTrendPointViewModel>();
            for (var i = dias - 1; i >= 0; i--)
            {
                var date = fechaReferencia.Date.AddDays(-i);
                grouped.TryGetValue(date, out var value);
                var gal = value?.Galones ?? 0m;
                var ton = value?.Toneladas ?? 0m;
                var lit = Math.Round(gal * LitrosPorGalon, 2);
                points.Add(new SeguimientoDieselTrendPointViewModel
                {
                    Etiqueta = date.ToString("dd/MM", EsCulture),
                    Galones = gal,
                    Toneladas = ton,
                    LitrosPorTonelada = ton > 0 ? Math.Round(lit / ton, 3) : 0m
                });
            }

            for (var i = 0; i < points.Count; i++)
            {
                var from = Math.Max(0, i - 2);
                var window = points.Skip(from).Take(i - from + 1).ToList();
                points[i].MediaMovil = window.Count > 0 ? Math.Round(window.Average(x => x.Galones), 2) : 0m;
            }

            return points;
        }

        private static List<SeguimientoDieselTrendPointViewModel> BuildDieselTrendByMonth(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsConFecha,
            DateTime fechaReferencia,
            int meses)
        {
            var start = new DateTime(fechaReferencia.Year, fechaReferencia.Month, 1).AddMonths(-(meses - 1));
            var grouped = rowsConFecha
                .Where(x => x.FechaEvento.HasValue)
                .GroupBy(x => new DateTime(x.FechaEvento!.Value.Year, x.FechaEvento.Value.Month, 1))
                .ToDictionary(g => g.Key, g => new
                {
                    Galones = Math.Round(g.Sum(x => x.CombustibleLitros), 2),
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 2)
                });

            var points = new List<SeguimientoDieselTrendPointViewModel>();
            for (var i = 0; i < meses; i++)
            {
                var month = start.AddMonths(i);
                grouped.TryGetValue(month, out var value);
                var gal = value?.Galones ?? 0m;
                var ton = value?.Toneladas ?? 0m;
                var lit = Math.Round(gal * LitrosPorGalon, 2);
                points.Add(new SeguimientoDieselTrendPointViewModel
                {
                    Etiqueta = month.ToString("MMM yyyy", EsCulture),
                    Galones = gal,
                    Toneladas = ton,
                    LitrosPorTonelada = ton > 0 ? Math.Round(lit / ton, 3) : 0m
                });
            }

            for (var i = 0; i < points.Count; i++)
            {
                var from = Math.Max(0, i - 2);
                var window = points.Skip(from).Take(i - from + 1).ToList();
                points[i].MediaMovil = window.Count > 0 ? Math.Round(window.Average(x => x.Galones), 2) : 0m;
            }

            return points;
        }

        private List<SeguimientoDieselMantenimientoViewModel> LoadDieselMaintenanceData(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsConFecha,
            DateTime? fechaCorte)
        {
            var path = Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", "DieselMantenimientoLog.json");
            if (!System.IO.File.Exists(path))
            {
                return new List<SeguimientoDieselMantenimientoViewModel>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<SeguimientoDieselMantenimientoViewModel>();
                }

                var raw = JsonSerializer.Deserialize<List<DieselMaintenanceLogRecord>>(json) ?? new List<DieselMaintenanceLogRecord>();
                var cutoff = fechaCorte ?? DateTime.Today;
                return raw
                    .Where(x => !string.IsNullOrWhiteSpace(x.Unidad))
                    .OrderByDescending(x => x.Fecha)
                    .Take(35)
                    .Select(x =>
                    {
                        var unidad = x.Unidad.Trim();
                        var desde = x.Fecha.Date.AddDays(-30);
                        var galonesUltimos30 = rowsConFecha
                            .Where(r => string.Equals((r.Equipo ?? string.Empty).Trim(), unidad, StringComparison.OrdinalIgnoreCase) &&
                                        r.FechaEvento.HasValue &&
                                        r.FechaEvento.Value.Date >= desde &&
                                        r.FechaEvento.Value.Date <= cutoff.Date)
                            .Sum(r => r.CombustibleLitros);
                        return new SeguimientoDieselMantenimientoViewModel
                        {
                            Fecha = x.Fecha.Date,
                            Unidad = unidad,
                            Tipo = string.IsNullOrWhiteSpace(x.Tipo) ? "Mantenimiento" : x.Tipo.Trim(),
                            Observacion = string.IsNullOrWhiteSpace(x.Observacion) ? "-" : x.Observacion.Trim(),
                            GalonesUltimos30Dias = Math.Round(galonesUltimos30, 2)
                        };
                    })
                    .ToList();
            }
            catch
            {
                return new List<SeguimientoDieselMantenimientoViewModel>();
            }
        }

        private static string ResolveEstado(IEnumerable<string?> estados)
        {
            var normalized = estados
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(MapEstadoCategoria)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            if (normalized.Contains("EN RUTA", StringComparer.Ordinal))
            {
                return "EN RUTA";
            }

            if (normalized.Contains("FINALIZADO", StringComparer.Ordinal))
            {
                return "FINALIZADO";
            }

            return "SIN ESTADO";
        }

        private CalendarioOperativoViewModel BuildCalendarioOperativoModel(
            SeguimientoToneladasViewModel? seguimiento,
            int? year,
            int? month,
            string? sitio,
            string? turno,
            string? estado,
            string? equipo,
            string? conductor,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            bool soloConflictos)
        {
            var rowsConFecha = (seguimiento?.Rows ?? new List<SeguimientoToneladasRowViewModel>())
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            var sitiosBase = rowsConFecha
                .Select(r => NormalizeProcedenciaLabel(ResolveProcedencia(r)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var selectedSitio = NormalizeCalendarioOption(sitio, "TODOS", sitiosBase);
            var selectedTurno = NormalizeCalendarioOption(turno, "TODOS", new[] { "DIA", "NOCHE" });
            var selectedEstado = NormalizeCalendarioOption(estado, "TODOS", new[] { "FINALIZADO", "EN RUTA", "SIN ESTADO" });
            var searchEquipo = (equipo ?? string.Empty).Trim();
            var searchConductor = (conductor ?? string.Empty).Trim();
            var fechaDesdeFiltro = fechaDesde?.Date;
            var fechaHastaFiltro = fechaHasta?.Date;

            if (fechaDesdeFiltro.HasValue && !fechaHastaFiltro.HasValue)
            {
                fechaHastaFiltro = fechaDesdeFiltro.Value;
            }
            else if (!fechaDesdeFiltro.HasValue && fechaHastaFiltro.HasValue)
            {
                fechaDesdeFiltro = fechaHastaFiltro.Value;
            }

            if (fechaDesdeFiltro.HasValue && fechaHastaFiltro.HasValue && fechaHastaFiltro.Value < fechaDesdeFiltro.Value)
            {
                var swap = fechaDesdeFiltro.Value;
                fechaDesdeFiltro = fechaHastaFiltro.Value;
                fechaHastaFiltro = swap;
            }

            var hasDateRangeFilter = fechaDesdeFiltro.HasValue && fechaHastaFiltro.HasValue;
            var fechaReferencia = rowsConFecha.Any()
                ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                : DateTime.Now.Date;

            var selectedYear = year ?? fechaReferencia.Year;
            if (selectedYear < 2000 || selectedYear > 2100)
            {
                selectedYear = fechaReferencia.Year;
            }

            var selectedMonth = month ?? fechaReferencia.Month;
            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = fechaReferencia.Month;
            }

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);

            IEnumerable<SeguimientoToneladasRowViewModel> ApplyCalendarioFilters(
                IEnumerable<SeguimientoToneladasRowViewModel> source)
            {
                var query = source;

                if (!string.Equals(selectedSitio, "TODOS", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals(
                            NormalizeProcedenciaLabel(ResolveProcedencia(r)),
                            selectedSitio,
                            StringComparison.OrdinalIgnoreCase));
                }

                if (!string.Equals(selectedTurno, "TODOS", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals(
                            ResolveTurnoOperacion(r.FechaEvento),
                            selectedTurno,
                            StringComparison.OrdinalIgnoreCase));
                }

                if (!string.Equals(selectedEstado, "TODOS", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                    {
                        var estadoCategoria = MapEstadoCategoria(r.Estado);
                        if (string.IsNullOrWhiteSpace(estadoCategoria))
                        {
                            estadoCategoria = "SIN ESTADO";
                        }

                        return string.Equals(estadoCategoria, selectedEstado, StringComparison.OrdinalIgnoreCase);
                    });
                }

                if (!string.IsNullOrWhiteSpace(searchEquipo))
                {
                    query = query.Where(r =>
                        (r.Equipo ?? string.Empty).Contains(searchEquipo, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(searchConductor))
                {
                    query = query.Where(r =>
                        (r.Conductor ?? string.Empty).Contains(searchConductor, StringComparison.OrdinalIgnoreCase));
                }

                return query;
            }

            if (!hasDateRangeFilter && (!year.HasValue || !month.HasValue))
            {
                var filteredRowsForReference = ApplyCalendarioFilters(rowsConFecha).ToList();
                if (filteredRowsForReference.Any())
                {
                    fechaReferencia = filteredRowsForReference.Max(r => r.FechaEvento!.Value.Date);
                    selectedYear = fechaReferencia.Year;
                    selectedMonth = fechaReferencia.Month;
                    monthStart = new DateTime(selectedYear, selectedMonth, 1);
                    monthEnd = monthStart.AddMonths(1);
                }
            }

            var periodStart = hasDateRangeFilter ? fechaDesdeFiltro!.Value.Date : monthStart.Date;
            var periodEndExclusive = hasDateRangeFilter ? fechaHastaFiltro!.Value.Date.AddDays(1) : monthEnd.Date;
            var metaObjetivo = ResolveMetaObjetivo(periodStart);
            var metaDiaria = metaObjetivo.MetaDiaria;
            var offsetToMonday = ((int)periodStart.DayOfWeek + 6) % 7;
            var calendarStart = periodStart.AddDays(-offsetToMonday);
            DateTime calendarEnd;
            if (hasDateRangeFilter)
            {
                var periodLastDay = periodEndExclusive.AddDays(-1);
                var offsetToSunday = 6 - (((int)periodLastDay.DayOfWeek + 6) % 7);
                calendarEnd = periodLastDay.AddDays(offsetToSunday + 1);
            }
            else
            {
                calendarEnd = calendarStart.AddDays(42);
            }

            var canUseWindowPrefilter = !hasDateRangeFilter && year.HasValue && month.HasValue;
            var rowsSourceForFilters = canUseWindowPrefilter
                ? rowsConFecha.Where(r =>
                    r.FechaEvento!.Value.Date >= calendarStart.Date &&
                    r.FechaEvento.Value.Date < calendarEnd.Date)
                : rowsConFecha;

            var filteredRowsList = ApplyCalendarioFilters(rowsSourceForFilters).ToList();

            var rowsPeriodo = filteredRowsList
                .Where(r =>
                    r.FechaEvento!.Value.Date >= periodStart.Date &&
                    r.FechaEvento.Value.Date < periodEndExclusive.Date)
                .ToList();

            var rowsVentana = hasDateRangeFilter
                ? rowsPeriodo
                : canUseWindowPrefilter
                    ? filteredRowsList
                    : filteredRowsList
                        .Where(r =>
                            r.FechaEvento!.Value.Date >= calendarStart.Date &&
                            r.FechaEvento.Value.Date < calendarEnd.Date)
                        .ToList();

            var conflictosVentana = BuildCalendarioConflictos(rowsVentana);
            if (soloConflictos)
            {
                var fechasConConflicto = conflictosVentana
                    .Select(c => c.Fecha.Date)
                    .Distinct()
                    .ToHashSet();

                rowsVentana = rowsVentana
                    .Where(r => fechasConConflicto.Contains(r.FechaEvento!.Value.Date))
                    .ToList();

                conflictosVentana = BuildCalendarioConflictos(rowsVentana);
            }

            var rowsFinalizados = rowsVentana
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .ToList();

            var historicoPorDia = rowsFinalizados
                .GroupBy(r => r.FechaEvento!.Value.Date)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Toneladas: Math.Round(g.Sum(x => x.Toneladas), 1),
                        Equipos: g.Select(x => (x.Equipo ?? string.Empty).Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count()),
                    EqualityComparer<DateTime>.Default);

            var toneladasPorSitioDia = rowsFinalizados
                .GroupBy(r => r.FechaEvento!.Value.Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var triton = 0m;
                        var pavonAsm = 0m;
                        var otros = 0m;

                        foreach (var row in g)
                        {
                            var procedencia = NormalizeProcedenciaLabel(ResolveProcedencia(row));
                            if (string.Equals(procedencia, "TRITON", StringComparison.OrdinalIgnoreCase))
                            {
                                triton += row.Toneladas;
                                continue;
                            }

                            if (string.Equals(procedencia, "PAVON ASM", StringComparison.OrdinalIgnoreCase))
                            {
                                pavonAsm += row.Toneladas;
                                continue;
                            }

                            otros += row.Toneladas;
                        }

                        return (
                            Triton: Math.Round(triton, 1),
                            PavonAsm: Math.Round(pavonAsm, 1),
                            Otros: Math.Round(otros, 1));
                    },
                    EqualityComparer<DateTime>.Default);

            var eventosPorDia = rowsVentana
                .GroupBy(r => r.FechaEvento!.Value.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(r => r.FechaEvento!.Value.TimeOfDay)
                        .ThenBy(r => (r.Equipo ?? string.Empty).Trim())
                        .ToList(),
                    EqualityComparer<DateTime>.Default);

            var periodDays = historicoPorDia
                .Where(x => x.Key >= periodStart && x.Key < periodEndExclusive)
                .ToList();

            var toneladasMes = periodDays.Sum(x => x.Value.Toneladas);
            var diasConDescarga = periodDays.Count(x => x.Value.Toneladas > 0);
            var promedioDiario = diasConDescarga > 0
                ? Math.Round(toneladasMes / diasConDescarga, 1)
                : 0;
            var diasMetaCumplida = periodDays.Count(x => x.Value.Toneladas >= metaDiaria && metaDiaria > 0m);
            var diasAlerta = periodDays.Count(x =>
            {
                if (metaDiaria <= 0m || x.Value.Toneladas >= metaDiaria || x.Value.Toneladas <= 0)
                {
                    return false;
                }

                var faltanteRatio = (metaDiaria - x.Value.Toneladas) / metaDiaria;
                return faltanteRatio <= 0.20m;
            });
            var diasCriticos = periodDays.Count(x =>
            {
                if (metaDiaria <= 0m || x.Value.Toneladas <= 0 || x.Value.Toneladas >= metaDiaria)
                {
                    return false;
                }

                var faltanteRatio = (metaDiaria - x.Value.Toneladas) / metaDiaria;
                return faltanteRatio > 0.20m;
            });
            var equiposDescargadosMes = rowsFinalizados
                .Where(r => r.FechaEvento!.Value.Date >= periodStart && r.FechaEvento.Value.Date < periodEndExclusive)
                .Select(r => (r.Equipo ?? string.Empty).Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var conflictosMes = conflictosVentana
                .Where(c => c.Fecha.Date >= periodStart.Date && c.Fecha.Date < periodEndExclusive.Date)
                .ToList();
            var fechasConConflictoSet = conflictosMes
                .Select(c => c.Fecha.Date)
                .Distinct()
                .ToHashSet();

            var totalEventosFiltrados = rowsVentana.Count(r =>
                r.FechaEvento!.Value.Date >= periodStart.Date &&
                r.FechaEvento.Value.Date < periodEndExclusive.Date);

            var totalCalendarCells = Math.Max(7, (calendarEnd.Date - calendarStart.Date).Days);
            var days = new List<CalendarioOperativoDayViewModel>(totalCalendarCells);

            for (var i = 0; i < totalCalendarCells; i++)
            {
                var day = calendarStart.AddDays(i);
                historicoPorDia.TryGetValue(day.Date, out var agregado);
                toneladasPorSitioDia.TryGetValue(day.Date, out var toneladasSitioDia);
                eventosPorDia.TryGetValue(day.Date, out var eventosDiaRaw);
                eventosDiaRaw ??= new List<SeguimientoToneladasRowViewModel>();

                var toneladas = agregado.Toneladas;
                var equiposDia = agregado.Equipos;
                var (estadoDia, iconoDia, labelDia) = ResolveSemaforoDiario(toneladas, metaDiaria);

                var eventosDia = eventosDiaRaw
                    .Take(4)
                    .Select(r =>
                    {
                        var estadoEvento = MapEstadoCategoria(r.Estado);
                        if (string.IsNullOrWhiteSpace(estadoEvento))
                        {
                            estadoEvento = "SIN ESTADO";
                        }

                        var (estadoEventoCss, iconoEvento, labelEvento) = ResolveSemaforoEvento(estadoEvento);
                        var horaLabel = r.FechaEvento!.Value.ToString("HH:mm");

                        return new CalendarioOperativoEventoViewModel
                        {
                            HoraLabel = horaLabel,
                            Equipo = string.IsNullOrWhiteSpace(r.Equipo) ? "Sin equipo" : r.Equipo.Trim(),
                            Conductor = string.IsNullOrWhiteSpace(r.Conductor) ? "Sin conductor" : r.Conductor.Trim(),
                            Sitio = NormalizeProcedenciaLabel(ResolveProcedencia(r)),
                            Turno = ResolveTurnoOperacion(r.FechaEvento),
                            Estado = estadoEvento,
                            SemaforoEstado = estadoEventoCss,
                            SemaforoIcon = iconoEvento,
                            SemaforoLabel = labelEvento
                        };
                    })
                    .ToList();

                days.Add(new CalendarioOperativoDayViewModel
                {
                    Date = day.Date,
                    IsCurrentMonth = hasDateRangeFilter
                        ? day.Date >= periodStart.Date && day.Date < periodEndExclusive.Date
                        : day.Month == selectedMonth,
                    IsToday = day.Date == DateTime.Now.Date,
                    ToneladasDescargadas = toneladas,
                    ToneladasTriton = toneladasSitioDia.Triton,
                    ToneladasPavonAsm = toneladasSitioDia.PavonAsm,
                    ToneladasOtros = toneladasSitioDia.Otros,
                    EquiposDescargados = equiposDia,
                    SemaforoEstado = estadoDia,
                    SemaforoIcon = iconoDia,
                    SemaforoLabel = labelDia,
                    HasConflicto = fechasConConflictoSet.Contains(day.Date),
                    TotalEventos = eventosDiaRaw.Count,
                    Eventos = eventosDia
                });
            }

            var sitiosDisponibles = new List<string> { "TODOS" };
            sitiosDisponibles.AddRange(sitiosBase);

            return new CalendarioOperativoViewModel
            {
                Year = selectedYear,
                Month = selectedMonth,
                PreviousYear = monthStart.AddMonths(-1).Year,
                PreviousMonth = monthStart.AddMonths(-1).Month,
                NextYear = monthStart.AddMonths(1).Year,
                NextMonth = monthStart.AddMonths(1).Month,
                CurrentMonthLabel = hasDateRangeFilter
                    ? $"Rango {periodStart.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} - {periodEndExclusive.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}"
                    : BuildMonthLabel(monthStart),
                MetaDiaria = metaDiaria,
                ToneladasMes = Math.Round(toneladasMes, 1),
                PromedioDiario = promedioDiario,
                DiasConDescarga = diasConDescarga,
                DiasMetaCumplida = diasMetaCumplida,
                DiasAlerta = diasAlerta,
                DiasCriticos = diasCriticos,
                EquiposDescargadosMes = equiposDescargadosMes,
                TotalEventosFiltrados = totalEventosFiltrados,
                DiasConConflictos = conflictosMes
                    .Select(c => c.Fecha.Date)
                    .Distinct()
                    .Count(),
                TotalConflictos = conflictosMes.Count,
                IsFromUpload = seguimiento?.IsFromUpload ?? false,
                SourceFileName = seguimiento?.SourceFileName,
                FechaUltimaData = rowsConFecha.Any()
                    ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                    : null,
                SelectedSitio = selectedSitio,
                SelectedTurno = selectedTurno,
                SelectedEstado = selectedEstado,
                SearchEquipo = searchEquipo,
                SearchConductor = searchConductor,
                FechaDesdeFiltro = fechaDesdeFiltro,
                FechaHastaFiltro = fechaHastaFiltro,
                HasDateRangeFilter = hasDateRangeFilter,
                SoloConflictos = soloConflictos,
                SitiosDisponibles = sitiosDisponibles,
                Days = days,
                Conflictos = conflictosMes
            };
        }

        private static (string Estado, string Icono, string Label) ResolveSemaforoDiario(decimal toneladas, decimal metaDiaria)
        {
            if (toneladas <= 0)
            {
                return ("none", "bi-dash-circle", "Sin descarga");
            }

            if (metaDiaria <= 0m || toneladas >= metaDiaria)
            {
                return ("ok", "bi-check2-circle", "Meta cumplida");
            }

            var faltanteRatio = (metaDiaria - toneladas) / metaDiaria;
            if (faltanteRatio <= 0.20m)
            {
                return ("warn", "bi-exclamation-triangle-fill", "Cerca de meta");
            }

            return ("danger", "bi-x-octagon-fill", "Bajo meta");
        }

        private static (string Estado, string Icono, string Label) ResolveSemaforoEvento(string estadoCategoria)
        {
            if (string.Equals(estadoCategoria, "FINALIZADO", StringComparison.OrdinalIgnoreCase))
            {
                return ("ok", "bi-check-circle-fill", "Finalizado");
            }

            if (string.Equals(estadoCategoria, "EN RUTA", StringComparison.OrdinalIgnoreCase))
            {
                return ("warn", "bi-clock-history", "En ruta");
            }

            return ("danger", "bi-exclamation-octagon-fill", "Sin estado");
        }

        private static string NormalizeCalendarioOption(string? value, string fallback, IReadOnlyList<string> allowed)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            if (string.Equals(normalized, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                return "TODOS";
            }

            var match = allowed.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match) ? fallback : match;
        }

        private static List<CalendarioOperativoConflictoViewModel> BuildCalendarioConflictos(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            var conflictos = new List<CalendarioOperativoConflictoViewModel>();
            var rowsConFecha = rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            IEnumerable<(DateTime Fecha, string Hora, string Clave, int Eventos)> DetectByKey(
                Func<SeguimientoToneladasRowViewModel, string?> keySelector)
            {
                return rowsConFecha
                    .Select(r => new
                    {
                        Row = r,
                        Fecha = r.FechaEvento!.Value.Date,
                        Hora = $"{r.FechaEvento.Value.Hour:00}:00",
                        Key = (keySelector(r) ?? string.Empty).Trim()
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .GroupBy(x => new
                    {
                        x.Fecha,
                        x.Hora,
                        Key = x.Key.ToUpperInvariant()
                    })
                    .Where(g => g.Count() > 1)
                    .Select(g => (g.Key.Fecha, g.Key.Hora, g.First().Key, g.Count()));
            }

            foreach (var item in DetectByKey(r => r.Equipo))
            {
                conflictos.Add(new CalendarioOperativoConflictoViewModel
                {
                    Fecha = item.Fecha,
                    HoraSlot = item.Hora,
                    Tipo = "Equipo duplicado",
                    Clave = item.Clave,
                    Eventos = item.Eventos
                });
            }

            foreach (var item in DetectByKey(r => r.Conductor))
            {
                conflictos.Add(new CalendarioOperativoConflictoViewModel
                {
                    Fecha = item.Fecha,
                    HoraSlot = item.Hora,
                    Tipo = "Conductor duplicado",
                    Clave = item.Clave,
                    Eventos = item.Eventos
                });
            }

            return conflictos
                .OrderBy(c => c.Fecha)
                .ThenBy(c => c.HoraSlot)
                .ThenBy(c => c.Tipo)
                .ThenBy(c => c.Clave)
                .ToList();
        }

        private static string BuildMonthLabel(DateTime monthStart)
        {
            var monthName = EsCulture.DateTimeFormat.GetMonthName(monthStart.Month);
            if (string.IsNullOrWhiteSpace(monthName))
            {
                monthName = monthStart.ToString("MMMM", EsCulture);
            }

            monthName = char.ToUpper(monthName[0], EsCulture) + monthName[1..];
            return $"{monthName} {monthStart.Year}";
        }

        private OperacionesDashboardViewModel BuildDashboardModelFromSeguimiento(SeguimientoToneladasViewModel seguimiento)
        {
            var rowsConFecha = seguimiento.Rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            var fechaCorte = seguimiento.FechaOperativa
                ?? (rowsConFecha.Any()
                    ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                    : DateTime.Now.Date);
            var metaObjetivo = ResolveMetaObjetivo(fechaCorte);

            var rowsMes = rowsConFecha
                .Where(r => r.FechaEvento!.Value.Year == fechaCorte.Year && r.FechaEvento.Value.Month == fechaCorte.Month)
                .ToList();

            if (!rowsMes.Any())
            {
                rowsMes = seguimiento.Rows.ToList();
            }

            var rowsDia = rowsConFecha
                .Where(r => r.FechaEvento!.Value.Date == fechaCorte)
                .ToList();

            var rowsMesTriton = rowsMes
                .Where(IsSitioTriton)
                .ToList();
            if (!rowsMesTriton.Any())
            {
                rowsMesTriton = rowsMes;
            }

            var rowsDiaTriton = rowsDia
                .Where(IsSitioTriton)
                .ToList();
            if (!rowsDiaTriton.Any())
            {
                rowsDiaTriton = rowsDia;
            }

            var finalizadosDia = rowsDiaTriton
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .ToList();
            var enRutaDia = rowsDiaTriton
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "EN RUTA", StringComparison.Ordinal))
                .ToList();
            var despachosDelDia = finalizadosDia
                .Select(r => (r.Equipo ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var conductoresMes = rowsMesTriton
                .Where(r => !string.IsNullOrWhiteSpace(r.Conductor))
                .GroupBy(r => r.Conductor.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new OperacionesDashboardRankingItemViewModel
                {
                    Nombre = g.Key,
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1)
                })
                .OrderByDescending(x => x.Toneladas)
                .ThenBy(x => x.Nombre)
                .ToList();

            var equiposMes = rowsMesTriton
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new OperacionesDashboardRankingItemViewModel
                {
                    Nombre = g.Key,
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1)
                })
                .OrderByDescending(x => x.Toneladas)
                .ThenBy(x => x.Nombre)
                .ToList();

            var topConductores = conductoresMes.Take(10).ToList();
            var topEquipos = equiposMes.Take(10).ToList();

            var distribucionProcedencia = rowsMesTriton
                .GroupBy(r => NormalizeProcedenciaLabel(ResolveProcedencia(r)))
                .Select(g => new OperacionesDashboardDistribucionItemViewModel
                {
                    Categoria = string.IsNullOrWhiteSpace(g.Key) ? "SIN PROCEDENCIA" : g.Key,
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1)
                })
                .OrderByDescending(x => x.Toneladas)
                .ToList();

            var tendenciaDiaria = rowsMesTriton
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .GroupBy(r => r.FechaEvento!.Value.Date)
                .Select(g => new OperacionesDashboardTendenciaItemViewModel
                {
                    Fecha = g.Key,
                    Toneladas = Math.Round(g.Sum(x => x.Toneladas), 1)
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            var hoy = DateTime.Today;
            var fechaLimiteComparativo = fechaCorte.Year == hoy.Year && fechaCorte < hoy
                ? hoy
                : fechaCorte;
            var cantidadMesesComparativo = Math.Clamp(fechaLimiteComparativo.Month, 1, 12);

            var comparativoToneladasSitio = Enumerable.Range(1, cantidadMesesComparativo)
                .Select(monthNumber =>
                {
                    var corteDiaMes = monthNumber < fechaLimiteComparativo.Month
                        ? DateTime.DaysInMonth(fechaLimiteComparativo.Year, monthNumber)
                        : monthNumber == fechaLimiteComparativo.Month
                            ? Math.Min(fechaLimiteComparativo.Day, DateTime.DaysInMonth(fechaLimiteComparativo.Year, monthNumber))
                            : 0;
                    var rowsMesComparativo = rowsConFecha
                        .Where(r =>
                            r.FechaEvento!.Value.Year == fechaLimiteComparativo.Year &&
                            r.FechaEvento.Value.Month == monthNumber &&
                            corteDiaMes > 0 &&
                            r.FechaEvento.Value.Day <= corteDiaMes &&
                            IsSitioTriton(r))
                        .ToList();

                    var toneladasFinalizadas = rowsMesComparativo
                        .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                        .ToList();

                    var tritonToneladas = toneladasFinalizadas.Sum(r => r.Toneladas);

                    var mesNombre = EsCulture.DateTimeFormat.GetMonthName(monthNumber);
                    if (!string.IsNullOrWhiteSpace(mesNombre))
                    {
                        mesNombre = char.ToUpperInvariant(mesNombre[0]) + mesNombre[1..];
                    }

                    return new OperacionesDashboardComparativoMensualItemViewModel
                    {
                        Mes = mesNombre,
                        Triton = Math.Round(tritonToneladas, 1),
                        PavonAsm = 0m
                    };
                })
                .ToList();

            var comparativoDieselSitio = Enumerable.Range(1, cantidadMesesComparativo)
                .Select(monthNumber =>
                {
                    var corteDiaMes = monthNumber < fechaLimiteComparativo.Month
                        ? DateTime.DaysInMonth(fechaLimiteComparativo.Year, monthNumber)
                        : monthNumber == fechaLimiteComparativo.Month
                            ? Math.Min(fechaLimiteComparativo.Day, DateTime.DaysInMonth(fechaLimiteComparativo.Year, monthNumber))
                            : 0;
                    var rowsMesComparativo = rowsConFecha
                        .Where(r =>
                            r.FechaEvento!.Value.Year == fechaLimiteComparativo.Year &&
                            r.FechaEvento.Value.Month == monthNumber &&
                            corteDiaMes > 0 &&
                            r.FechaEvento.Value.Day <= corteDiaMes &&
                            IsSitioTriton(r))
                        .ToList();

                    var tritonDiesel = rowsMesComparativo.Sum(r => r.CombustibleLitros);

                    var mesNombre = EsCulture.DateTimeFormat.GetMonthName(monthNumber);
                    if (!string.IsNullOrWhiteSpace(mesNombre))
                    {
                        mesNombre = char.ToUpperInvariant(mesNombre[0]) + mesNombre[1..];
                    }

                    return new OperacionesDashboardComparativoMensualItemViewModel
                    {
                        Mes = mesNombre,
                        Triton = Math.Round(tritonDiesel, 2),
                        PavonAsm = 0m
                    };
                })
                .ToList();

            var totalToneladasMes = Math.Round(
                rowsMesTriton
                    .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                    .Sum(x => x.Toneladas),
                1);
            if (totalToneladasMes <= 0m)
            {
                totalToneladasMes = Math.Round(rowsMesTriton.Sum(x => x.Toneladas), 1);
            }

            var totalDieselMesGal = Math.Round(rowsMesTriton.Sum(x => x.CombustibleLitros), 2);
            var toneladasEnRutaActual = Math.Round(enRutaDia.Sum(GetToneladasEnRuta), 1);
            var toneladasDescargadasDia = Math.Round(finalizadosDia.Sum(x => x.Toneladas), 1);
            var toneladasDescargadasDiaTriton = Math.Round(finalizadosDia.Sum(x => x.Toneladas), 1);
            var toneladasDescargadasDiaPavonAsm = 0m;
            var prediccionToneladasManana = toneladasEnRutaActual > 0 ? toneladasEnRutaActual : 0m;
            var equiposFinalizadosDia = finalizadosDia
                .Select(r => (r.Equipo ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var equiposEnRutaDia = enRutaDia
                .Select(r => (r.Equipo ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var rendimientoMesTgal = totalDieselMesGal > 0
                ? Math.Round(totalToneladasMes / totalDieselMesGal, 3)
                : 0m;
            var litrosPorToneladaMes = totalToneladasMes > 0
                ? Math.Round((totalDieselMesGal * LitrosPorGalon) / totalToneladasMes, 3)
                : 0m;
            var desviacionesRendimiento = rowsMesTriton
                .Where(r => r.CombustibleLitros > 0m && r.Toneladas > 0m)
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g =>
                {
                    var toneladas = g.Sum(x => x.Toneladas);
                    var galones = g.Sum(x => x.CombustibleLitros);
                    var tGal = galones > 0m ? toneladas / galones : 0m;
                    var variacion = rendimientoMesTgal > 0m
                        ? ((tGal - rendimientoMesTgal) / rendimientoMesTgal) * 100m
                        : 0m;
                    var estado = variacion <= -15m
                        ? "CRITICO"
                        : variacion < -5m
                            ? "VIGILAR"
                            : "OK";

                    return new OperacionesDashboardDesviacionItemViewModel
                    {
                        Nombre = g.Key,
                        Toneladas = Math.Round(toneladas, 1),
                        Galones = Math.Round(galones, 2),
                        ToneladasPorGalon = Math.Round(tGal, 3),
                        VariacionVsPromedioPorcentaje = Math.Round(variacion, 1),
                        Estado = estado
                    };
                })
                .OrderBy(x => x.VariacionVsPromedioPorcentaje)
                .ThenBy(x => x.Nombre)
                .Take(8)
                .ToList();

            var metaDiaria = metaObjetivo.MetaDiaria;
            var metaTurno = Math.Round(metaDiaria / 2m, 1);
            var turnos = new[] { "DIA", "NOCHE" };
            var cumplimientoPorTurno = turnos
                .Select(turno =>
                {
                    var rowsTurno = finalizadosDia
                        .Where(r => string.Equals(ResolveTurnoOperacion(r.FechaEvento), turno, StringComparison.Ordinal))
                        .ToList();
                    var toneladasTurno = Math.Round(rowsTurno.Sum(x => x.Toneladas), 1);
                    var equiposTurno = rowsTurno
                        .Select(r => (r.Equipo ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();
                    var cumplimientoTurno = metaTurno > 0m
                        ? (int)Math.Round((double)(toneladasTurno * 100m / metaTurno), MidpointRounding.AwayFromZero)
                        : 0;

                    return new OperacionesDashboardCumplimientoTurnoViewModel
                    {
                        Turno = turno,
                        Equipos = equiposTurno,
                        Toneladas = toneladasTurno,
                        MetaTurno = metaTurno,
                        CumplimientoPorcentaje = Math.Clamp(cumplimientoTurno, 0, 300)
                    };
                })
                .ToList();

            var toneladasFinalizadasMes = Math.Round(
                rowsMesTriton
                    .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                    .Sum(x => x.Toneladas),
                1);
            var diasDelMes = DateTime.DaysInMonth(fechaCorte.Year, fechaCorte.Month);
            var diasTranscurridos = Math.Clamp(fechaCorte.Day, 1, diasDelMes);
            var ritmoPromedioDiarioMes = diasTranscurridos > 0
                ? toneladasFinalizadasMes / diasTranscurridos
                : 0m;
            var metaMensualToneladas = metaObjetivo.MetaMensualTotal;
            var proyeccionCierreMensualToneladas = Math.Round(ritmoPromedioDiarioMes * diasDelMes, 1);
            var cumplimientoProyectadoMensual = metaMensualToneladas > 0m
                ? (int)Math.Round((double)(proyeccionCierreMensualToneladas * 100m / metaMensualToneladas), MidpointRounding.AwayFromZero)
                : 0;
            var brechaProyectadaMensual = Math.Round(metaMensualToneladas - proyeccionCierreMensualToneladas, 1);
            var estadoProyeccionMensualLabel = "Sin datos";
            var estadoProyeccionMensualClase = "secondary";
            if (proyeccionCierreMensualToneladas > 0m)
            {
                if (cumplimientoProyectadoMensual >= 100)
                {
                    estadoProyeccionMensualLabel = "Cierre proyectado cumplido";
                    estadoProyeccionMensualClase = "success";
                }
                else if (cumplimientoProyectadoMensual >= 90)
                {
                    estadoProyeccionMensualLabel = "Cierre en riesgo controlado";
                    estadoProyeccionMensualClase = "warning";
                }
                else
                {
                    estadoProyeccionMensualLabel = "Cierre en riesgo alto";
                    estadoProyeccionMensualClase = "danger";
                }
            }

            var brechaMetaToneladas = Math.Round(metaDiaria - toneladasDescargadasDia, 1);
            var cumplimientoMetaDia = metaDiaria > 0
                ? (int)Math.Round((double)(toneladasDescargadasDia * 100m / metaDiaria), MidpointRounding.AwayFromZero)
                : 0;
            var cumplimientoEstimadoManana = metaDiaria > 0
                ? (int)Math.Round((double)(prediccionToneladasManana * 100m / metaDiaria), MidpointRounding.AwayFromZero)
                : 0;
            var estadoOperativoLabel = "Sin registros del dia";
            var estadoOperativoClase = "secondary";
            if (rowsDia.Any())
            {
                if (cumplimientoMetaDia >= 100)
                {
                    estadoOperativoLabel = "Meta cumplida";
                    estadoOperativoClase = "success";
                }
                else if (cumplimientoMetaDia >= 80)
                {
                    estadoOperativoLabel = "En seguimiento";
                    estadoOperativoClase = "warning";
                }
                else
                {
                    estadoOperativoLabel = "Bajo desempeno";
                    estadoOperativoClase = "danger";
                }
            }

            var alertasOperativas = new List<string>();
            if (!rowsDia.Any())
            {
                alertasOperativas.Add("No hay registros operativos para la fecha de corte.");
            }
            else
            {
                if (cumplimientoMetaDia < 80)
                {
                    alertasOperativas.Add($"Cumplimiento diario en {cumplimientoMetaDia}% (meta: {metaDiaria:N0} Tn).");
                }

                if (brechaMetaToneladas > 0 && toneladasEnRutaActual < brechaMetaToneladas)
                {
                    alertasOperativas.Add($"Toneladas en ruta insuficientes para cubrir la brecha de {brechaMetaToneladas:N1} Tn.");
                }
            }

            var equiposCriticos = desviacionesRendimiento
                .Where(x => string.Equals(x.Estado, "CRITICO", StringComparison.Ordinal))
                .Take(3)
                .Select(x => x.Nombre)
                .ToList();
            if (equiposCriticos.Count > 0)
            {
                alertasOperativas.Add($"Rendimiento critico en equipos: {string.Join(", ", equiposCriticos)}.");
            }

            if (!alertasOperativas.Any())
            {
                alertasOperativas.Add("Operacion estable: sin alertas criticas en el corte actual.");
            }

            return new OperacionesDashboardViewModel
            {
                DespachosDelDia = despachosDelDia,
                ToneladasMovilizadas = totalToneladasMes,
                DieselConsumidoGalones = totalDieselMesGal,
                DieselConsumidoLitros = Math.Round(totalDieselMesGal * LitrosPorGalon, 2),
                CumplimientoPlanPorcentaje = Math.Clamp(cumplimientoMetaDia, 0, 1000),
                MetaDiaria = metaDiaria,
                ToneladasDescargadasDia = toneladasDescargadasDia,
                ToneladasDescargadasDiaTriton = toneladasDescargadasDiaTriton,
                ToneladasDescargadasDiaPavonAsm = toneladasDescargadasDiaPavonAsm,
                ToneladasEnRutaActual = toneladasEnRutaActual,
                PrediccionToneladasManana = prediccionToneladasManana,
                CumplimientoEstimadoMananaPorcentaje = Math.Clamp(cumplimientoEstimadoManana, 0, 300),
                EquiposFinalizadosDia = equiposFinalizadosDia,
                EquiposEnRutaDia = equiposEnRutaDia,
                BrechaMetaToneladas = brechaMetaToneladas,
                RendimientoToneladasPorGalonMes = rendimientoMesTgal,
                LitrosPorToneladaMes = litrosPorToneladaMes,
                EstadoOperativoLabel = estadoOperativoLabel,
                EstadoOperativoClase = estadoOperativoClase,
                AlertasOperativas = alertasOperativas,
                MetaMensualToneladas = metaMensualToneladas,
                ProyeccionCierreMensualToneladas = proyeccionCierreMensualToneladas,
                CumplimientoProyectadoMensualPorcentaje = Math.Clamp(cumplimientoProyectadoMensual, 0, 300),
                BrechaProyectadaMensualToneladas = brechaProyectadaMensual,
                EstadoProyeccionMensualLabel = estadoProyeccionMensualLabel,
                EstadoProyeccionMensualClase = estadoProyeccionMensualClase,
                FechaCorteOperativo = fechaCorte,
                TopConductores = topConductores,
                TopEquipos = topEquipos,
                ConductoresMes = conductoresMes,
                EquiposMes = equiposMes,
                DistribucionProcedencia = distribucionProcedencia,
                TendenciaDiaria = tendenciaDiaria,
                ComparativoToneladasSitio = comparativoToneladasSitio,
                ComparativoDieselSitio = comparativoDieselSitio,
                DesviacionesRendimiento = desviacionesRendimiento,
                CumplimientoPorTurno = cumplimientoPorTurno,
                IsFromUpload = seguimiento.IsFromUpload,
                SourceFileName = seguimiento.SourceFileName,
                LastUpdatedAt = DateTime.Now
            };
        }

        private static List<SeguimientoToneladasEstadoResumenViewModel> BuildEstadoResumen(IEnumerable<SeguimientoToneladasRowViewModel> rows)
        {
            return rows
                .Select(r => new
                {
                    Row = r,
                    Categoria = MapEstadoCategoria(r.Estado)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Categoria))
                .GroupBy(x => x.Categoria)
                .Select(g => new SeguimientoToneladasEstadoResumenViewModel
                {
                    Estado = g.Key,
                    Equipos = g
                        .Select(x => x.Row.Equipo.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Toneladas = g.Sum(x => x.Row.Toneladas),
                    Viajes = g.Sum(x => x.Row.Viajes),
                    EquiposDetalle = string.Join(", ",
                        g.Select(x => x.Row.Equipo.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase))
                })
                .OrderBy(x => x.Estado == "FINALIZADO" ? 0 : 1)
                .ToList();
        }

        private static string MapEstadoCategoria(string? rawEstado)
        {
            if (string.IsNullOrWhiteSpace(rawEstado))
            {
                return string.Empty;
            }

            var normalized = NormalizeHeader(rawEstado);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.Contains("finalizad", StringComparison.Ordinal) ||
                normalized.Contains("descargad", StringComparison.Ordinal) ||
                normalized.Contains("completad", StringComparison.Ordinal) ||
                normalized.Contains("terminad", StringComparison.Ordinal) ||
                normalized.Contains("cerrad", StringComparison.Ordinal))
            {
                return "FINALIZADO";
            }

            if (normalized.Contains("enruta", StringComparison.Ordinal) ||
                normalized.Contains("entrada", StringComparison.Ordinal) ||
                normalized.Contains("encamino", StringComparison.Ordinal) ||
                normalized.Contains("ruta", StringComparison.Ordinal) ||
                normalized.Contains("transito", StringComparison.Ordinal) ||
                normalized.Contains("proceso", StringComparison.Ordinal) ||
                normalized.Contains("pendiente", StringComparison.Ordinal))
            {
                return "EN RUTA";
            }

            return string.Empty;
        }

        private static string ResolveTurnoOperacion(DateTime? fechaEvento)
        {
            if (!fechaEvento.HasValue)
            {
                return "DIA";
            }

            var hora = fechaEvento.Value.Hour;
            var minuto = fechaEvento.Value.Minute;
            if (hora == 0 && minuto == 0)
            {
                // Si el Excel no trae hora, tratamos el registro como turno dia por defecto.
                return "DIA";
            }

            return hora >= 6 && hora < 18 ? "DIA" : "NOCHE";
        }

        private static List<SeguimientoToneladasRowViewModel> ParseToneladasRowsFromWorkbook(
            ExcelWorkbook workbook,
            out string? error,
            out string? sourceSheetName)
        {
            sourceSheetName = null;
            var allErrors = new List<string>();

            foreach (var worksheet in workbook.Worksheets)
            {
                if (worksheet.Dimension is null)
                {
                    continue;
                }

                if (!TryResolveHeaderMap(worksheet, out var headerRow, out var headerMap, out var headerError))
                {
                    if (!string.IsNullOrWhiteSpace(headerError))
                    {
                        allErrors.Add($"{worksheet.Name}: {headerError}");
                    }

                    continue;
                }

                var rows = ParseToneladasRowsFromWorksheet(worksheet, headerRow, headerMap, out var parseError);
                if (rows.Any())
                {
                    error = null;
                    sourceSheetName = worksheet.Name;
                    return rows;
                }

                if (!string.IsNullOrWhiteSpace(parseError))
                {
                    allErrors.Add($"{worksheet.Name}: {parseError}");
                }
            }

            error = allErrors.Any()
                ? $"No se pudo leer el Excel. Detalle: {string.Join(" | ", allErrors)}"
                : "No se encontro una hoja con encabezados validos. Debe existir C. Equipo y P. Neto en una misma fila de encabezado.";

            return new List<SeguimientoToneladasRowViewModel>();
        }

        private static List<SeguimientoToneladasRowViewModel> ParseToneladasRowsFromWorksheet(
            ExcelWorksheet worksheet,
            int headerRow,
            Dictionary<string, int> headerMap,
            out string? error)
        {
            error = null;
            var rows = new List<SeguimientoToneladasRowViewModel>();

            if (!TryFindColumn(headerMap, out var equipoColumn, "c equipo", "cequipo", "equipo", "equipos", "unidad", "vehiculo", "camion", "cabezal"))
            {
                error = "No se encontro la columna de equipo. En tu formato usa: C. Equipo.";
                return rows;
            }

            if (!TryFindColumn(headerMap, out var toneladasColumn, "p neto", "pneto", "peso neto", "pesoneto", "toneladas", "tonelada"))
            {
                error = "No se encontro la columna de toneladas. En tu formato usa: P. Neto.";
                return rows;
            }

            TryFindColumn(headerMap, out var gondolaColumn, "gondola");
            TryFindColumn(headerMap, out var procedenciaColumn, "procedencia");
            TryFindColumn(headerMap, out var ubicacionColumn, "ubicacion");
            TryFindColumn(headerMap, out var rutaColumn, "ruta", "trayecto", "origendestino");
            TryFindColumn(headerMap, out var viajesColumn, "viajes", "viaje");
            TryFindColumn(headerMap, out var fechaColumn, "fecha", "fechasalida", "fecha salida", "fechaingreso", "fecha ingreso");
            TryFindColumn(headerMap, out var combustibleColumn, "cant combustible", "cantcombustible", "cantidad combustible", "combustible");
            TryFindColumn(headerMap, out var pesoSugeridoColumn, "p sugerido", "psugerido", "peso sugerido", "pesosugerido", "sugerido", "psug");
            TryFindColumn(headerMap, out var conductorColumn, "conductor", "chofer", "piloto");
            TryFindColumn(headerMap, out var estadoColumn, "estado", "estatus", "estado viaje", "estado unidad", "status");
            TryFindColumn(headerMap, out var eficienciaColumn, "eficiencia", "rendimiento");
            TryFindColumn(headerMap, out var brutoColumn, "p bruto", "pbruto", "peso bruto", "pesobruto");
            TryFindColumn(headerMap, out var taraColumn, "p tara", "ptara", "peso tara", "pesotara");
            var estadoCandidateColumns = headerMap
                .Where(x =>
                    x.Key.Contains("estado", StringComparison.Ordinal) ||
                    x.Key.Contains("estatus", StringComparison.Ordinal) ||
                    x.Key.Contains("status", StringComparison.Ordinal) ||
                    x.Key.Contains("situacion", StringComparison.Ordinal))
                .OrderBy(x => x.Value)
                .Select(x => x.Value)
                .Distinct()
                .ToList();

            if (estadoColumn > 0 && !estadoCandidateColumns.Contains(estadoColumn))
            {
                estadoCandidateColumns.Insert(0, estadoColumn);
            }

            var startRow = headerRow + 1;
            var endRow = worksheet.Dimension.End.Row;

            for (var rowIndex = startRow; rowIndex <= endRow; rowIndex++)
            {
                var equipo = GetCellText(worksheet, rowIndex, equipoColumn);
                if (string.IsNullOrWhiteSpace(equipo) && gondolaColumn > 0)
                {
                    equipo = GetCellText(worksheet, rowIndex, gondolaColumn);
                }

                var toneladas = ReadDecimalFromCell(worksheet.Cells[rowIndex, toneladasColumn]);
                if (toneladas is null && brutoColumn > 0 && taraColumn > 0)
                {
                    var bruto = ReadDecimalFromCell(worksheet.Cells[rowIndex, brutoColumn]);
                    var tara = ReadDecimalFromCell(worksheet.Cells[rowIndex, taraColumn]);
                    if (bruto.HasValue && tara.HasValue)
                    {
                        toneladas = bruto.Value - tara.Value;
                    }
                }

                var viajes = viajesColumn > 0
                    ? ReadInt(worksheet.Cells[rowIndex, viajesColumn].Value)
                    : null;

                if (string.IsNullOrWhiteSpace(equipo) && toneladas is null && viajes is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(equipo) || toneladas is null)
                {
                    continue;
                }

                var safeViajes = viajes.GetValueOrDefault(1);
                if (safeViajes <= 0)
                {
                    safeViajes = 1;
                }

                var procedencia = procedenciaColumn > 0 ? GetCellText(worksheet, rowIndex, procedenciaColumn) : string.Empty;
                var ruta = string.Empty;
                if (procedenciaColumn > 0 || ubicacionColumn > 0)
                {
                    var ubicacion = ubicacionColumn > 0 ? GetCellText(worksheet, rowIndex, ubicacionColumn) : string.Empty;
                    ruta = BuildRuta(procedencia, ubicacion);
                }

                if (string.IsNullOrWhiteSpace(ruta) && rutaColumn > 0)
                {
                    ruta = GetCellText(worksheet, rowIndex, rutaColumn);
                }

                var estado = string.Empty;
                foreach (var candidateColumn in estadoCandidateColumns)
                {
                    estado = GetCellText(worksheet, rowIndex, candidateColumn);
                    if (!string.IsNullOrWhiteSpace(estado))
                    {
                        break;
                    }
                }

                var eficiencia = eficienciaColumn > 0
                    ? GetCellText(worksheet, rowIndex, eficienciaColumn)
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(estado) && !string.IsNullOrWhiteSpace(eficiencia))
                {
                    var eficienciaAsEstado = MapEstadoCategoria(eficiencia);
                    if (!string.IsNullOrWhiteSpace(eficienciaAsEstado))
                    {
                        estado = eficiencia;
                    }
                }

                var fechaEvento = fechaColumn > 0
                    ? ReadDateTime(
                        worksheet.Cells[rowIndex, fechaColumn].Value,
                        worksheet.Cells[rowIndex, fechaColumn].Text)
                    : null;

                var combustibleLitros = combustibleColumn > 0
                    ? ReadDecimalFromCell(worksheet.Cells[rowIndex, combustibleColumn]) ?? 0
                    : 0;

                var conductor = conductorColumn > 0
                    ? GetCellText(worksheet, rowIndex, conductorColumn)
                    : string.Empty;
                var pesoSugerido = pesoSugeridoColumn > 0
                    ? ReadDecimalFromCell(worksheet.Cells[rowIndex, pesoSugeridoColumn]) ?? 0
                    : 0;

                rows.Add(new SeguimientoToneladasRowViewModel
                {
                    Equipo = equipo,
                    Procedencia = procedencia,
                    Ruta = string.IsNullOrWhiteSpace(ruta) ? "Sin ruta" : ruta,
                    Toneladas = toneladas.Value,
                    Viajes = safeViajes,
                    CombustibleLitros = combustibleLitros,
                    FechaEvento = fechaEvento,
                    Conductor = conductor,
                    Estado = estado,
                    PesoSugerido = pesoSugerido,
                    Eficiencia = string.IsNullOrWhiteSpace(eficiencia) ||
                        !string.IsNullOrWhiteSpace(MapEstadoCategoria(eficiencia))
                        ? ComputeEficienciaLabel(toneladas.Value, safeViajes)
                        : eficiencia
                });
            }

            return rows;
        }

        private static bool TryResolveHeaderMap(
            ExcelWorksheet worksheet,
            out int headerRow,
            out Dictionary<string, int> headerMap,
            out string? error)
        {
            headerRow = 0;
            headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            error = null;

            var scanStartRow = worksheet.Dimension!.Start.Row;
            var scanEndRow = Math.Min(worksheet.Dimension.End.Row, scanStartRow + 20);

            for (var rowIndex = scanStartRow; rowIndex <= scanEndRow; rowIndex++)
            {
                var candidateMap = BuildHeaderMap(worksheet, rowIndex);
                if (candidateMap.Count == 0)
                {
                    continue;
                }

                var hasEquipo = TryFindColumn(candidateMap, out _, "c equipo", "cequipo", "equipo", "equipos", "unidad", "vehiculo", "camion", "cabezal");
                var hasNeto = TryFindColumn(candidateMap, out _, "p neto", "pneto", "peso neto", "pesoneto", "toneladas", "tonelada");

                if (hasEquipo && hasNeto)
                {
                    headerRow = rowIndex;
                    headerMap = candidateMap;
                    return true;
                }
            }

            error = "No se encontro fila de encabezado con C. Equipo y P. Neto.";
            return false;
        }

        private static string BuildRuta(string procedencia, string ubicacion)
        {
            var safeProcedencia = procedencia.Trim();
            var safeUbicacion = ubicacion.Trim();

            if (!string.IsNullOrWhiteSpace(safeProcedencia) && !string.IsNullOrWhiteSpace(safeUbicacion))
            {
                return $"{safeProcedencia} - {safeUbicacion}";
            }

            if (!string.IsNullOrWhiteSpace(safeProcedencia))
            {
                return safeProcedencia;
            }

            return safeUbicacion;
        }

        private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet worksheet, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var startColumn = worksheet.Dimension.Start.Column;
            var endColumn = worksheet.Dimension.End.Column;

            for (var columnIndex = startColumn; columnIndex <= endColumn; columnIndex++)
            {
                var rawHeader = GetCellText(worksheet, headerRow, columnIndex);
                if (string.IsNullOrWhiteSpace(rawHeader))
                {
                    continue;
                }

                var normalized = NormalizeHeader(rawHeader);
                if (!map.ContainsKey(normalized))
                {
                    map[normalized] = columnIndex;
                }
            }

            return map;
        }

        private static bool TryFindColumn(Dictionary<string, int> headerMap, out int column, params string[] candidates)
        {
            var normalizedCandidates = candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Select(NormalizeHeader)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var candidate in normalizedCandidates)
            {
                if (headerMap.TryGetValue(candidate, out column))
                {
                    return true;
                }
            }

            foreach (var candidate in normalizedCandidates)
            {
                var containsMatch = headerMap
                    .FirstOrDefault(kvp => kvp.Key.Contains(candidate, StringComparison.Ordinal));
                if (containsMatch.Value > 0)
                {
                    column = containsMatch.Value;
                    return true;
                }
            }

            foreach (var candidate in normalizedCandidates)
            {
                var reverseContainsMatch = headerMap
                    .FirstOrDefault(kvp => candidate.Contains(kvp.Key, StringComparison.Ordinal));
                if (reverseContainsMatch.Value > 0)
                {
                    column = reverseContainsMatch.Value;
                    return true;
                }
            }

            column = 0;
            return false;
        }

        private static string GetCellText(ExcelWorksheet worksheet, int row, int column)
        {
            return worksheet.Cells[row, column].Text?.Trim() ?? string.Empty;
        }

        private static int? TryReadPositiveInt(string? rawValue)
        {
            if (int.TryParse(rawValue, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            return null;
        }

        private static string FirstNonEmpty(IFormCollection form, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!form.TryGetValue(key, out var values))
                {
                    continue;
                }

                var value = values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static decimal? ReadDecimal(object? rawValue)
        {
            if (rawValue is null)
            {
                return null;
            }

            return rawValue switch
            {
                decimal decimalValue => decimalValue,
                double doubleValue => Convert.ToDecimal(doubleValue),
                float floatValue => Convert.ToDecimal(floatValue),
                int intValue => intValue,
                long longValue => longValue,
                _ => ParseDecimalFromString(rawValue.ToString())
            };
        }

        private static decimal? ReadDecimalFromCell(ExcelRange cell)
        {
            var parsedFromValue = ReadDecimal(cell.Value);
            if (parsedFromValue.HasValue)
            {
                return parsedFromValue;
            }

            return ParseDecimalFromString(cell.Text);
        }

        private static decimal? ParseDecimalFromString(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var value = rawValue.Trim();
            if (TryParseDecimalWithCultures(value, out var directParsed))
            {
                return directParsed;
            }

            var match = Regex.Match(value, @"[-+]?\d+(?:[.,]\d+)?");
            if (match.Success && TryParseDecimalWithCultures(match.Value, out var extractedParsed))
            {
                return extractedParsed;
            }

            return null;
        }

        private static bool TryParseDecimalWithCultures(string value, out decimal parsedValue)
        {
            var cultures = new[]
            {
                CultureInfo.InvariantCulture,
                CultureInfo.GetCultureInfo("es-NI"),
                CultureInfo.GetCultureInfo("es-ES"),
                CultureInfo.CurrentCulture
            };

            foreach (var culture in cultures)
            {
                if (decimal.TryParse(value, NumberStyles.Any, culture, out parsedValue))
                {
                    return true;
                }
            }

            parsedValue = 0;
            return false;
        }

        private static int? ReadInt(object? rawValue)
        {
            if (rawValue is null)
            {
                return null;
            }

            return rawValue switch
            {
                int intValue => intValue,
                long longValue => Convert.ToInt32(longValue),
                decimal decimalValue => Convert.ToInt32(Math.Round(decimalValue, MidpointRounding.AwayFromZero)),
                double doubleValue => Convert.ToInt32(Math.Round(doubleValue, MidpointRounding.AwayFromZero)),
                _ => ParseIntFromString(rawValue.ToString())
            };
        }

        private static int? ParseIntFromString(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var trimmed = rawValue.Trim();
            if (int.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            var asDecimal = ParseDecimalFromString(trimmed);
            return asDecimal.HasValue
                ? Convert.ToInt32(Math.Round(asDecimal.Value, MidpointRounding.AwayFromZero))
                : null;
        }

        private static DateTime? ReadDateTime(object? rawValue, string? rawText = null)
        {
            if (rawValue is DateTime dateTimeValue)
            {
                return dateTimeValue;
            }

            if (rawValue is double excelSerial && excelSerial > 0)
            {
                try
                {
                    return DateTime.FromOADate(excelSerial);
                }
                catch
                {
                    // Continue with textual parse.
                }
            }

            var value = string.IsNullOrWhiteSpace(rawText)
                ? rawValue?.ToString()
                : rawText;

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var cultures = new[]
            {
                CultureInfo.GetCultureInfo("es-NI"),
                CultureInfo.GetCultureInfo("es-ES"),
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.InvariantCulture,
                CultureInfo.CurrentCulture
            };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParse(trimmed, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
                {
                    return parsed;
                }
            }

            var cleaned = Regex.Replace(trimmed, @"\s*\|\s*", " ");
            foreach (var culture in cultures)
            {
                if (DateTime.TryParse(cleaned, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedCleaned))
                {
                    return parsedCleaned;
                }
            }

            var formats = new[]
            {
                "dd/MM/yyyy H:mm",
                "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy h:mm tt",
                "dd/MM/yyyy hh:mm tt",
                "M/d/yyyy H:mm",
                "M/d/yyyy HH:mm",
                "M/d/yyyy h:mm tt",
                "M/d/yyyy hh:mm tt",
                "M/d/yyyy H:mm:ss",
                "M/d/yyyy h:mm:ss tt"
            };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParseExact(trimmed, formats, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedExact))
                {
                    return parsedExact;
                }
            }

            var dateToken = Regex.Match(cleaned, @"\d{1,2}/\d{1,2}/\d{2,4}(?:\s+\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)?");
            if (dateToken.Success)
            {
                foreach (var culture in cultures)
                {
                    if (DateTime.TryParse(dateToken.Value, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedToken))
                    {
                        return parsedToken;
                    }
                }
            }

            return null;
        }

        private static string NormalizeHeader(string header)
        {
            var normalized = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private ControlDocumentosViewModel BuildControlDocumentosModel()
        {
            var model = new ControlDocumentosViewModel
            {
                FuenteArchivo = "Capacitaciones C-SAFE.xlsx",
                FechaGeneracion = DateTime.Now
            };

            var excelPath = Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", model.FuenteArchivo);
            if (!System.IO.File.Exists(excelPath))
            {
                model.FuenteArchivo = "Registros manuales";
                model.EncabezadosCapacitacion = GetDefaultControlDocumentosPersonaHeaders();
                model.Filas = BuildControlDocumentosPersonaRows(model.EncabezadosCapacitacion);
                model.CumplimientoCapacitaciones = BuildControlDocumentosCumplimientoPorCapacitacion(
                    model.EncabezadosCapacitacion,
                    model.Filas);
                model.HistorialCambios = LoadControlDocumentosHistory()
                    .OrderByDescending(x => x.FechaCambio)
                    .Take(300)
                    .ToList();
                model.TendenciaSemanal = BuildControlDocumentosTrendByDay(model.HistorialCambios, 7);
                model.TendenciaMensual = BuildControlDocumentosTrendByMonth(model.HistorialCambios, 6);
                model.EquiposGondolas = BuildControlDocumentosEquipoGondolaRows();
                return model;
            }

            try
            {
                var overrides = LoadControlDocumentosOverrides();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage(new FileInfo(excelPath));
                var worksheet = package.Workbook.Worksheets.FirstOrDefault(ws => ws.Dimension is not null);
                if (worksheet is null)
                {
                    model.ErrorMessage = "El archivo Excel no contiene hojas con informacion.";
                    return model;
                }

                var startRow = worksheet.Dimension!.Start.Row;
                var endRow = worksheet.Dimension.End.Row;
                var startCol = worksheet.Dimension.Start.Column;
                var endCol = worksheet.Dimension.End.Column;

                if (endRow <= startRow || endCol <= startCol)
                {
                    model.ErrorMessage = "No hay datos suficientes en el Excel para construir la tabla.";
                    return model;
                }

                var headers = new List<string>();
                for (var col = startCol; col <= endCol; col++)
                {
                    var header = (worksheet.Cells[startRow, col].Text ?? string.Empty).Trim();
                    headers.Add(string.IsNullOrWhiteSpace(header) ? $"Columna {col}" : header);
                }

                model.EncabezadosCapacitacion = headers.Skip(1).ToList();
                model.Filas = BuildControlDocumentosPersonaRows(model.EncabezadosCapacitacion);
                model.CumplimientoCapacitaciones = BuildControlDocumentosCumplimientoPorCapacitacion(
                    model.EncabezadosCapacitacion,
                    model.Filas);
                model.HistorialCambios = LoadControlDocumentosHistory()
                    .OrderByDescending(x => x.FechaCambio)
                    .Take(300)
                    .ToList();
                model.TendenciaSemanal = BuildControlDocumentosTrendByDay(model.HistorialCambios, 7);
                model.TendenciaMensual = BuildControlDocumentosTrendByMonth(model.HistorialCambios, 6);
                model.EquiposGondolas = BuildControlDocumentosEquipoGondolaRows();
            }
            catch
            {
                model.ErrorMessage = "No se pudo procesar el archivo de capacitaciones. Verifica su formato.";
            }

            return model;
        }

        private IReadOnlyList<ControlDocumentosEquipoGondolaRowViewModel> BuildControlDocumentosEquipoGondolaRows()
        {
            try
            {
                return _context.OperacionesIngresoEquipoGondolaSolicitudes
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(200)
                    .AsEnumerable()
                    .Select(ToControlDocumentosEquipoGondolaRow)
                    .Where(x => !string.IsNullOrWhiteSpace(x.PlacaCabezal) || !string.IsNullOrWhiteSpace(x.PlacaGondola))
                    .ToList();
            }
            catch
            {
                return Array.Empty<ControlDocumentosEquipoGondolaRowViewModel>();
            }
        }

        private static ControlDocumentosEquipoGondolaRowViewModel ToControlDocumentosEquipoGondolaRow(OperacionesIngresoEquipoGondolaSolicitud solicitud)
        {
            var payload = DeserializeEquipoGondolaPayload(solicitud.PayloadJson);
            var isEquipo = string.Equals((solicitud.Tipo ?? string.Empty).Trim(), "Ingreso de Equipo", StringComparison.OrdinalIgnoreCase);
            var prefix = isEquipo ? "equipo_" : "gondola_";
            var tareaPadre = FirstPayloadValue(payload, "tarea_padre", $"{prefix}tarea_padre", "equipo_tarea_padre", "gondola_tarea_padre");

            return new ControlDocumentosEquipoGondolaRowViewModel
            {
                Id = solicitud.Id,
                Tipo = solicitud.Tipo ?? string.Empty,
                Asunto = solicitud.Asunto ?? string.Empty,
                Estado = solicitud.Estado ?? string.Empty,
                Prioridad = solicitud.Prioridad ?? string.Empty,
                TareaPadre = string.IsNullOrWhiteSpace(tareaPadre) ? solicitud.TareaPadre ?? string.Empty : tareaPadre,
                Empresa = FirstPayloadValue(payload, "equipo_empresa"),
                CodigoCabezal = FirstPayloadValue(payload, "equipo_codigo_cabezal"),
                PlacaCabezal = FirstPayloadValue(payload, "equipo_placa_cabezal"),
                Tenencia = FirstPayloadValue(payload, "equipo_tenencia"),
                Propietario = FirstPayloadValue(payload, "equipo_propietario"),
                PlacaGondola = FirstPayloadValue(payload, "equipo_placa_gondola", "gondola_placa"),
                VencimientoEmisionGases = FormatControlDocumentosDisplayDate(FirstPayloadValue(payload, "equipo_vencimiento_emision_gases")),
                VencimientoInspeccionMecanica = FormatControlDocumentosDisplayDate(FirstPayloadValue(payload, "equipo_vencimiento_inspeccion_mecanica")),
                VencimientoSeguroVehicular = FormatControlDocumentosDisplayDate(FirstPayloadValue(payload, "equipo_vencimiento_seguro_vehicular")),
                FechaInicio = FormatControlDocumentosDisplayDate(FirstPayloadValue(payload, $"{prefix}fecha_inicio", "equipo_fecha_inicio", "gondola_fecha_inicio")),
                FechaFin = FormatControlDocumentosDisplayDate(FirstPayloadValue(payload, $"{prefix}fecha_fin", "equipo_fecha_fin", "gondola_fecha_fin")),
                PorcentajeRealizado = FirstPayloadValue(payload, $"{prefix}porcentaje_realizado", "equipo_porcentaje_realizado", "gondola_porcentaje_realizado"),
                UpdatedAt = solicitud.UpdatedAt
            };
        }

        private static Dictionary<string, string[]> DeserializeEquipoGondolaPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string[]>>(payloadJson)
                    ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string FirstPayloadValue(Dictionary<string, string[]> payload, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (payload.TryGetValue(key, out var values))
                {
                    var value = values?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        private static string FormatControlDocumentosDisplayDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.ToString("dd/MM/yyyy", EsCulture)
                : value.Trim();
        }

        private string GetControlDocumentosPersonasPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", ControlDocumentosPersonasFileName);
        }

        private List<ControlDocumentosPersonaRecord> LoadControlDocumentosPersonas()
        {
            var path = GetControlDocumentosPersonasPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<ControlDocumentosPersonaRecord>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path, Encoding.UTF8);
                return string.IsNullOrWhiteSpace(json)
                    ? new List<ControlDocumentosPersonaRecord>()
                    : JsonSerializer.Deserialize<List<ControlDocumentosPersonaRecord>>(json) ?? new List<ControlDocumentosPersonaRecord>();
            }
            catch
            {
                return new List<ControlDocumentosPersonaRecord>();
            }
        }

        private void SaveControlDocumentosPersonas(List<ControlDocumentosPersonaRecord> personas)
        {
            var path = GetControlDocumentosPersonasPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ordered = personas
                .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
                .OrderBy(x => x.Nombre)
                .ToList();
            var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static IReadOnlyList<string> GetDefaultControlDocumentosPersonaHeaders()
        {
            return new[]
            {
                "Cedula",
                "INSS",
                "Contrato de Trabajo",
                "Licencia",
                "Examen medico"
            };
        }

        private IReadOnlyList<ControlDocumentosRowViewModel> BuildControlDocumentosPersonaRows(IReadOnlyList<string> encabezados)
        {
            var headers = encabezados.Count > 0
                ? encabezados
                : GetDefaultControlDocumentosPersonaHeaders();

            var personas = LoadControlDocumentosPersonas()
                .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
                .ToDictionary(x => NormalizeControlDocumentoKey(x.Nombre), x => x, StringComparer.Ordinal);

            foreach (var persona in LoadControlDocumentosPersonasDesdeIngresoPersonal())
            {
                var key = NormalizeControlDocumentoKey(persona.Nombre);
                if (string.IsNullOrWhiteSpace(key) || personas.ContainsKey(key))
                {
                    continue;
                }

                personas[key] = persona;
            }

            return personas.Values
                .OrderBy(x => x.Nombre)
                .Select(persona =>
                {
                    var fechasCapacitacion = new List<string>();
                    var fechasCapacitacionIso = new List<string>();
                    var capacitaciones = new List<ControlDocumentosCapacitacionEstadoViewModel>();

                    foreach (var encabezado in headers)
                    {
                        var fecha = persona.Fechas is not null && persona.Fechas.TryGetValue(encabezado, out var fechaGuardada)
                            ? fechaGuardada
                            : string.Empty;
                        var estado = BuildControlDocumentoCapacitacionEstado(encabezado, fecha);
                        capacitaciones.Add(estado);
                        fechasCapacitacion.Add(estado.Fecha);
                        fechasCapacitacionIso.Add(estado.FechaIso);
                    }

                    return new ControlDocumentosRowViewModel
                    {
                        Conductor = persona.Nombre,
                        FechasCapacitacion = fechasCapacitacion,
                        FechasCapacitacionIso = fechasCapacitacionIso,
                        Capacitaciones = capacitaciones
                    };
                })
                .ToList();
        }

        private IReadOnlyList<ControlDocumentosPersonaRecord> LoadControlDocumentosPersonasDesdeIngresoPersonal()
        {
            try
            {
                return _context.OperacionesIngresoPersonalSolicitudes
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(300)
                    .AsEnumerable()
                    .Select(ToControlDocumentosPersonaRecord)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
                    .ToList();
            }
            catch
            {
                return Array.Empty<ControlDocumentosPersonaRecord>();
            }
        }

        private static ControlDocumentosPersonaRecord ToControlDocumentosPersonaRecord(OperacionesIngresoPersonalSolicitud solicitud)
        {
            var payload = DeserializeEquipoGondolaPayload(solicitud.PayloadJson);
            var nombre = FirstPayloadValue(payload, "nombres_apellidos", "nombre_completo", "nombre")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombre))
            {
                nombre = string.IsNullOrWhiteSpace(solicitud.SolicitanteNombre)
                    ? solicitud.Asunto
                    : solicitud.SolicitanteNombre;
            }

            var fechas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var encabezado in GetDefaultControlDocumentosPersonaHeaders())
            {
                AddControlDocumentoPersonaFecha(
                    fechas,
                    encabezado,
                    FirstPayloadValue(payload, $"control_documento_{NormalizeHeader(encabezado)}"));
            }

            AddControlDocumentoPersonaFecha(fechas, "Contrato de Trabajo", FirstPayloadValue(payload, "fecha_inicio_contrato", "fecha_inicio"));
            AddControlDocumentoPersonaFecha(fechas, "INSS", FirstPayloadValue(payload, "fecha_inicio_contrato", "fecha_inicio"));
            AddControlDocumentoPersonaFecha(fechas, "Cedula", FirstPayloadValue(payload, "fecha_inicio"));
            AddControlDocumentoPersonaFecha(fechas, "Examen medico", FirstPayloadValue(payload, "fecha_examen_medico", "fecha_inicio"));

            return new ControlDocumentosPersonaRecord
            {
                Nombre = nombre.Trim(),
                Fechas = fechas,
                CreatedAt = solicitud.CreatedAt,
                UpdatedAt = solicitud.UpdatedAt
            };
        }

        private static void AddControlDocumentoPersonaFecha(Dictionary<string, string> fechas, string encabezado, string rawFecha)
        {
            if (string.IsNullOrWhiteSpace(rawFecha))
            {
                return;
            }

            if (TryValidateAndFormatControlDocumentoDateFromInput(rawFecha, out var fecha, out _) &&
                !string.IsNullOrWhiteSpace(fecha))
            {
                fechas[encabezado] = fecha;
            }
        }

        private sealed class ControlDocumentosPersonaRecord
        {
            public string Nombre { get; set; } = string.Empty;
            public Dictionary<string, string> Fechas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private ControlDocumentosQrViewModel? BuildControlDocumentosQrModel(string conductorKey)
        {
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return null;
            }

            var model = BuildControlDocumentosModel();
            if (!string.IsNullOrWhiteSpace(model.ErrorMessage))
            {
                return null;
            }

            var fila = model.Filas.FirstOrDefault(x =>
                string.Equals(NormalizeControlDocumentoKey(x.Conductor), conductorKey, StringComparison.Ordinal));
            if (fila is null)
            {
                return null;
            }

            var qrImageUrl = Url.Action(
                nameof(ControlDocumentosQrImage),
                "Operaciones",
                new { key = conductorKey }) ?? string.Empty;
            var registroUrl = Url.Action(
                nameof(ControlDocumentosQrDetalle),
                "Operaciones",
                new { key = conductorKey },
                Request.Scheme) ?? string.Empty;
            var qrPdfUrl = Url.Action(
                nameof(ExportControlDocumentosQrDetallePdf),
                "Operaciones",
                new { key = conductorKey, inline = false }) ?? string.Empty;
            var escaneosRecientes = LoadControlDocumentosQrScans()
                .Where(x => string.Equals(x.ConductorKey, conductorKey, StringComparison.Ordinal))
                .OrderByDescending(x => x.FechaEscaneo)
                .Take(25)
                .Select(x => new ControlDocumentosQrScanEntryViewModel
                {
                    FechaEscaneo = x.FechaEscaneo,
                    Usuario = x.Usuario,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent
                })
                .ToList();

            return new ControlDocumentosQrViewModel
            {
                Conductor = fila.Conductor,
                ConductorKey = conductorKey,
                QrImageUrl = qrImageUrl,
                RegistroUrl = registroUrl,
                QrPdfUrl = qrPdfUrl,
                FechaGeneracion = DateTime.Now,
                Capacitaciones = fila.Capacitaciones,
                EscaneosRecientes = escaneosRecientes
            };
        }

        private static string FormatControlDocumentoCellValue(object? rawValue)
        {
            if (rawValue is null)
            {
                return "-";
            }

            if (rawValue is DateTime dateTime)
            {
                return dateTime.ToString("dd/MM/yyyy", EsCulture);
            }

            if (rawValue is double d && d > 10000d && d < 80000d)
            {
                try
                {
                    return DateTime.FromOADate(d).ToString("dd/MM/yyyy", EsCulture);
                }
                catch
                {
                    // Si no es fecha OADate valida, continua con conversion textual.
                }
            }

            if (rawValue is decimal m && m > 10000m && m < 80000m)
            {
                try
                {
                    return DateTime.FromOADate(Convert.ToDouble(m)).ToString("dd/MM/yyyy", EsCulture);
                }
                catch
                {
                    // Si no es fecha OADate valida, continua con conversion textual.
                }
            }

            var text = (rawValue.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "-";
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 10000d && parsed < 80000d)
            {
                try
                {
                    return DateTime.FromOADate(parsed).ToString("dd/MM/yyyy", EsCulture);
                }
                catch
                {
                    // Si no es fecha OADate valida, retorna el valor original.
                }
            }

            return text;
        }

        private string GetControlDocumentosOverridesPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", ControlDocumentosOverridesFileName);
        }

        private Dictionary<string, Dictionary<string, string>> LoadControlDocumentosOverrides()
        {
            var path = GetControlDocumentosOverridesPath();
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!System.IO.File.Exists(path))
            {
                return result;
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return result;
                }

                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                    ?? new Dictionary<string, Dictionary<string, string>>();

                foreach (var conductorEntry in raw)
                {
                    var conductorKey = NormalizeControlDocumentoKey(conductorEntry.Key);
                    if (string.IsNullOrWhiteSpace(conductorKey))
                    {
                        continue;
                    }

                    var perConductor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (conductorEntry.Value is not null)
                    {
                        foreach (var fechaEntry in conductorEntry.Value)
                        {
                            var encabezado = (fechaEntry.Key ?? string.Empty).Trim();
                            var fecha = (fechaEntry.Value ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(encabezado))
                            {
                                continue;
                            }

                            perConductor[encabezado] = fecha;
                        }
                    }

                    result[conductorKey] = perConductor;
                }
            }
            catch
            {
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }

        private void SaveControlDocumentosOverrides(Dictionary<string, Dictionary<string, string>> overrides)
        {
            var path = GetControlDocumentosOverridesPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(overrides, options);
            System.IO.File.WriteAllText(path, json);
        }

        private string GetControlDocumentosHistoryPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", ControlDocumentosHistoryFileName);
        }

        private List<ControlDocumentosHistoryEntryViewModel> LoadControlDocumentosHistory()
        {
            var path = GetControlDocumentosHistoryPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<ControlDocumentosHistoryEntryViewModel>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ControlDocumentosHistoryEntryViewModel>();
                }

                return JsonSerializer.Deserialize<List<ControlDocumentosHistoryEntryViewModel>>(json)
                    ?? new List<ControlDocumentosHistoryEntryViewModel>();
            }
            catch
            {
                return new List<ControlDocumentosHistoryEntryViewModel>();
            }
        }

        private void SaveControlDocumentosHistory(List<ControlDocumentosHistoryEntryViewModel> historial)
        {
            var path = GetControlDocumentosHistoryPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var depurado = historial
                .OrderByDescending(x => x.FechaCambio)
                .Take(ControlDocumentosHistoryMaxEntries)
                .ToList();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(depurado, options);
            System.IO.File.WriteAllText(path, json);
        }

        private static IReadOnlyList<ControlDocumentosTrendPointViewModel> BuildControlDocumentosTrendByDay(
            IReadOnlyList<ControlDocumentosHistoryEntryViewModel> historial,
            int days)
        {
            if (days <= 0)
            {
                return Array.Empty<ControlDocumentosTrendPointViewModel>();
            }

            var points = new List<ControlDocumentosTrendPointViewModel>();
            var byDate = historial
                .GroupBy(x => x.FechaCambio.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (var i = days - 1; i >= 0; i--)
            {
                var day = DateTime.Today.AddDays(-i);
                byDate.TryGetValue(day, out var items);
                items ??= new List<ControlDocumentosHistoryEntryViewModel>();
                points.Add(new ControlDocumentosTrendPointViewModel
                {
                    Etiqueta = day.ToString("dd/MM", EsCulture),
                    TotalCambios = items.Count,
                    CambiosCriticos = items.Count(x => x.RequiereAprobacionCritica)
                });
            }

            return points;
        }

        private static IReadOnlyList<ControlDocumentosTrendPointViewModel> BuildControlDocumentosTrendByMonth(
            IReadOnlyList<ControlDocumentosHistoryEntryViewModel> historial,
            int months)
        {
            if (months <= 0)
            {
                return Array.Empty<ControlDocumentosTrendPointViewModel>();
            }

            var points = new List<ControlDocumentosTrendPointViewModel>();
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
            var byMonth = historial
                .GroupBy(x => new DateTime(x.FechaCambio.Year, x.FechaCambio.Month, 1))
                .ToDictionary(g => g.Key, g => g.ToList());

            for (var i = 0; i < months; i++)
            {
                var month = start.AddMonths(i);
                byMonth.TryGetValue(month, out var items);
                items ??= new List<ControlDocumentosHistoryEntryViewModel>();
                points.Add(new ControlDocumentosTrendPointViewModel
                {
                    Etiqueta = month.ToString("MMM yyyy", EsCulture),
                    TotalCambios = items.Count,
                    CambiosCriticos = items.Count(x => x.RequiereAprobacionCritica)
                });
            }

            return points;
        }

        private string GetControlDocumentosQrScansPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", ControlDocumentosQrScansFileName);
        }

        private List<ControlDocumentosQrScanRecord> LoadControlDocumentosQrScans()
        {
            var path = GetControlDocumentosQrScansPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<ControlDocumentosQrScanRecord>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ControlDocumentosQrScanRecord>();
                }

                return JsonSerializer.Deserialize<List<ControlDocumentosQrScanRecord>>(json)
                    ?? new List<ControlDocumentosQrScanRecord>();
            }
            catch
            {
                return new List<ControlDocumentosQrScanRecord>();
            }
        }

        private void SaveControlDocumentosQrScans(List<ControlDocumentosQrScanRecord> scans)
        {
            var path = GetControlDocumentosQrScansPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var depurado = scans
                .OrderByDescending(x => x.FechaEscaneo)
                .Take(ControlDocumentosQrScansMaxEntries)
                .ToList();

            var json = JsonSerializer.Serialize(depurado, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            System.IO.File.WriteAllText(path, json);
        }

        private void RegistrarEscaneoQr(string conductorKey)
        {
            try
            {
                var scans = LoadControlDocumentosQrScans();
                var user = User?.Identity?.IsAuthenticated == true
                    ? (User.Identity?.Name ?? "Autenticado")
                    : "Anonimo";
                var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "-";
                var agent = Request?.Headers.UserAgent.ToString() ?? string.Empty;
                if (agent.Length > 220)
                {
                    agent = agent[..220];
                }

                scans.Add(new ControlDocumentosQrScanRecord
                {
                    ConductorKey = conductorKey,
                    FechaEscaneo = DateTime.Now,
                    Usuario = string.IsNullOrWhiteSpace(user) ? "Anonimo" : user,
                    IpAddress = string.IsNullOrWhiteSpace(ip) ? "-" : ip,
                    UserAgent = string.IsNullOrWhiteSpace(agent) ? "-" : agent
                });

                SaveControlDocumentosQrScans(scans);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar el escaneo QR para {ConductorKey}", conductorKey);
            }
        }

        private string GetControlDocumentosAlertDispatchPath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", ControlDocumentosAlertDispatchFileName);
        }

        private List<ControlDocumentosAlertDispatchRecord> LoadControlDocumentosAlertDispatch()
        {
            var path = GetControlDocumentosAlertDispatchPath();
            if (!System.IO.File.Exists(path))
            {
                return new List<ControlDocumentosAlertDispatchRecord>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ControlDocumentosAlertDispatchRecord>();
                }

                return JsonSerializer.Deserialize<List<ControlDocumentosAlertDispatchRecord>>(json)
                    ?? new List<ControlDocumentosAlertDispatchRecord>();
            }
            catch
            {
                return new List<ControlDocumentosAlertDispatchRecord>();
            }
        }

        private void SaveControlDocumentosAlertDispatch(List<ControlDocumentosAlertDispatchRecord> dispatch)
        {
            var path = GetControlDocumentosAlertDispatchPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(dispatch.OrderByDescending(x => x.FechaEnvio).Take(6000), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            System.IO.File.WriteAllText(path, json);
        }

        private async Task<ControlDocumentosAlertaResumenViewModel> EjecutarAlertasControlDocumentosAsync(ControlDocumentosViewModel model)
        {
            var resumen = new ControlDocumentosAlertaResumenViewModel
            {
                UltimaEjecucion = DateTime.Now
            };

            try
            {
                var dispatch = LoadControlDocumentosAlertDispatch();
                var now = DateTime.Now;
                var pendings = new List<ControlDocumentosAlertCandidate>();

                foreach (var fila in model.Filas)
                {
                    foreach (var cap in fila.Capacitaciones)
                    {
                        var estado = (cap.EstadoFiltro ?? string.Empty).Trim().ToLowerInvariant();
                        if (estado == "vencida")
                        {
                            var key = $"VENCIDA|{NormalizeControlDocumentoKey(fila.Conductor)}|{NormalizeHeader(cap.NombreCapacitacion)}|{now:yyyyMMdd}";
                            var already = dispatch.Any(x => string.Equals(x.DispatchKey, key, StringComparison.Ordinal));
                            if (!already)
                            {
                                pendings.Add(new ControlDocumentosAlertCandidate
                                {
                                    DispatchKey = key,
                                    Conductor = fila.Conductor,
                                    Capacitacion = cap.NombreCapacitacion,
                                    Estado = "VENCIDA",
                                    Mensaje = $"Conductor {fila.Conductor} tiene vencida la capacitacion {cap.NombreCapacitacion}. {cap.MensajeAlerta}"
                                });
                            }
                        }
                        else if (estado == "porvencer" && cap.DiasRestantes.HasValue)
                        {
                            var dias = cap.DiasRestantes.Value;
                            if (!ControlDocumentosAlertThresholdDays.Contains(dias))
                            {
                                continue;
                            }

                            var key = $"PORVENCER|{NormalizeControlDocumentoKey(fila.Conductor)}|{NormalizeHeader(cap.NombreCapacitacion)}|D{dias}";
                            var already = dispatch.Any(x => string.Equals(x.DispatchKey, key, StringComparison.Ordinal));
                            if (!already)
                            {
                                pendings.Add(new ControlDocumentosAlertCandidate
                                {
                                    DispatchKey = key,
                                    Conductor = fila.Conductor,
                                    Capacitacion = cap.NombreCapacitacion,
                                    Estado = "PORVENCER",
                                    Mensaje = $"Conductor {fila.Conductor} tiene por vencer la capacitacion {cap.NombreCapacitacion} en {dias} dia(s)."
                                });
                            }
                        }
                    }
                }

                resumen.Evaluadas = pendings.Count;
                if (pendings.Count == 0)
                {
                    resumen.Mensaje = "Sin alertas nuevas para envio automatico.";
                    return resumen;
                }

                var recipientsEmail = await GetControlDocumentosEmailRecipientsAsync();
                var recipientsWhatsApp = await GetControlDocumentosWhatsAppRecipientsAsync();
                var emailSent = 0;
                var whatsSent = 0;
                var pendientes = 0;

                foreach (var alert in pendings)
                {
                    var emailOk = await SendControlDocumentosAlertEmailAsync(alert.Mensaje, recipientsEmail);
                    var waOk = await SendControlDocumentosAlertWhatsAppAsync(alert.Mensaje, recipientsWhatsApp);

                    if (emailOk)
                    {
                        emailSent++;
                    }

                    if (waOk)
                    {
                        whatsSent++;
                    }

                    if (!emailOk && !waOk)
                    {
                        pendientes++;
                    }

                    dispatch.Add(new ControlDocumentosAlertDispatchRecord
                    {
                        DispatchKey = alert.DispatchKey,
                        Conductor = alert.Conductor,
                        Capacitacion = alert.Capacitacion,
                        Estado = alert.Estado,
                        Mensaje = alert.Mensaje,
                        FechaEnvio = now,
                        EnviadoEmail = emailOk,
                        EnviadoWhatsApp = waOk
                    });
                }

                SaveControlDocumentosAlertDispatch(dispatch);
                resumen.EnviadasEmail = emailSent;
                resumen.EnviadasWhatsApp = whatsSent;
                resumen.Pendientes = pendientes;
                resumen.Mensaje = $"Alertas procesadas: {pendings.Count}. Email: {emailSent}. WhatsApp: {whatsSent}. Pendientes: {pendientes}.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron procesar alertas automaticas de Control de Documentos.");
                resumen.Mensaje = "No se pudo completar el proceso de alertas automaticas.";
            }

            return resumen;
        }

        private async Task<List<string>> GetControlDocumentosEmailRecipientsAsync()
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .Where(x => x.IsActive &&
                                (x.Role == UserRole.Administrator ||
                                 x.Role == UserRole.CoordinadorIT ||
                                 x.Role == UserRole.Technician) &&
                                !string.IsNullOrWhiteSpace(x.Email))
                    .Select(x => x.Email.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToListAsync();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<List<string>> GetControlDocumentosWhatsAppRecipientsAsync()
        {
            try
            {
                var settings = _configuration.GetSection("WhatsAppNotifications");
                var defaultCountryCode = settings["DefaultCountryCode"] ?? "+505";
                var phones = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.IsActive &&
                                (x.Role == UserRole.Administrator ||
                                 x.Role == UserRole.CoordinadorIT ||
                                 x.Role == UserRole.Technician) &&
                                !string.IsNullOrWhiteSpace(x.Phone))
                    .Select(x => x.Phone)
                    .ToListAsync();

                return phones
                    .Select(phone => NormalizePhoneForWhatsApp(phone ?? string.Empty, defaultCountryCode))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()!;
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<bool> SendControlDocumentosAlertEmailAsync(string message, IReadOnlyList<string> recipients)
        {
            var section = _configuration.GetSection("EmailNotifications");
            var enabled = section.GetValue<bool>("Enabled");
            if (!enabled || recipients.Count == 0)
            {
                return false;
            }

            var host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? section["SmtpHost"];
            var user = Environment.GetEnvironmentVariable("SMTP_USER") ?? section["UserName"];
            var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? section["Password"];
            var from = Environment.GetEnvironmentVariable("SMTP_FROM") ?? section["FromEmail"];
            var fromName = section["FromName"] ?? "TRAWZACONS Service Desk";
            var port = section.GetValue<int?>("SmtpPort") ?? 587;
            var useSsl = section.GetValue<bool?>("UseSsl") ?? true;

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(from) ||
                user.Contains("PENDING", StringComparison.OrdinalIgnoreCase) ||
                password.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = useSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(user, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                foreach (var recipient in recipients)
                {
                    using var mail = new MailMessage
                    {
                        From = new MailAddress(from, fromName),
                        Subject = "[Control Documentos] Alerta automatica",
                        Body = $"{message}\n\nFecha: {DateTime.Now:dd/MM/yyyy HH:mm}",
                        IsBodyHtml = false
                    };
                    mail.To.Add(recipient);
                    await client.SendMailAsync(mail);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo envio de alerta email en Control Documentos.");
                return false;
            }
        }

        private async Task<bool> SendControlDocumentosAlertWhatsAppAsync(string message, IReadOnlyList<string> recipients)
        {
            var section = _configuration.GetSection("WhatsAppNotifications");
            var enabled = section.GetValue<bool>("Enabled");
            if (!enabled || recipients.Count == 0)
            {
                return false;
            }

            var sid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? section["TwilioAccountSid"];
            var token = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? section["TwilioAuthToken"];
            var from = Environment.GetEnvironmentVariable("WHATSAPP_FROM_NUMBER") ?? section["FromNumber"];

            if (string.IsNullOrWhiteSpace(sid) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(from) ||
                sid.Contains("PENDING", StringComparison.OrdinalIgnoreCase) ||
                token.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var url = $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json";
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{token}"));
                var client = _httpClientFactory.CreateClient("TwilioWhatsApp");
                foreach (var recipient in recipients)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["From"] = NormalizeWhatsAppNumber(from),
                            ["To"] = recipient,
                            ["Body"] = message
                        })
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo envio de alerta WhatsApp en Control Documentos.");
                return false;
            }
        }

        private static string NormalizeWhatsAppNumber(string value)
        {
            if (value.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return $"whatsapp:{value}";
        }

        private static string? NormalizePhoneForWhatsApp(string value, string defaultCountryCode)
        {
            var raw = value.Trim();
            if (raw.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw["whatsapp:".Length..];
            }

            var hasPlus = raw.StartsWith("+", StringComparison.Ordinal);
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return null;
            }

            if (hasPlus)
            {
                return $"whatsapp:+{digits}";
            }

            if (digits.Length == 8)
            {
                var cc = string.IsNullOrWhiteSpace(defaultCountryCode) ? "+505" : defaultCountryCode.Trim();
                if (!cc.StartsWith("+", StringComparison.Ordinal))
                {
                    cc = "+" + cc;
                }

                return $"whatsapp:{cc}{digits}";
            }

            return $"whatsapp:+{digits}";
        }

        private Dictionary<string, string> LoadControlDocumentosBaseDatesByConductor(string conductorKey)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(conductorKey))
            {
                return result;
            }

            var excelPath = Path.Combine(_environment.ContentRootPath, "Data", "Operaciones", "Capacitaciones C-SAFE.xlsx");
            if (!System.IO.File.Exists(excelPath))
            {
                return result;
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(excelPath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault(ws => ws.Dimension is not null);
            if (worksheet is null)
            {
                return result;
            }

            var startRow = worksheet.Dimension!.Start.Row;
            var endRow = worksheet.Dimension.End.Row;
            var startCol = worksheet.Dimension.Start.Column;
            var endCol = worksheet.Dimension.End.Column;

            var headers = new List<string>();
            for (var col = startCol; col <= endCol; col++)
            {
                var header = (worksheet.Cells[startRow, col].Text ?? string.Empty).Trim();
                headers.Add(string.IsNullOrWhiteSpace(header) ? $"Columna {col}" : header);
            }

            for (var row = startRow + 1; row <= endRow; row++)
            {
                var conductor = (worksheet.Cells[row, startCol].Text ?? string.Empty).Trim();
                if (!string.Equals(NormalizeControlDocumentoKey(conductor), conductorKey, StringComparison.Ordinal))
                {
                    continue;
                }

                for (var col = startCol + 1; col <= endCol; col++)
                {
                    var encabezado = headers[col - startCol];
                    var fecha = FormatControlDocumentoCellValue(worksheet.Cells[row, col].Value);
                    result[encabezado] = string.IsNullOrWhiteSpace(fecha) ? "-" : fecha;
                }

                return result;
            }

            return result;
        }

        private static string ResolveControlDocumentoVisibleValue(
            string baseFecha,
            Dictionary<string, string> conductorOverrides,
            string encabezado)
        {
            if (conductorOverrides.TryGetValue(encabezado, out var overrideFecha))
            {
                return string.IsNullOrWhiteSpace(overrideFecha) ? "-" : overrideFecha.Trim();
            }

            return string.IsNullOrWhiteSpace(baseFecha) ? "-" : baseFecha.Trim();
        }

        private static bool TryGetOverriddenFecha(
            Dictionary<string, Dictionary<string, string>> overrides,
            string conductorKey,
            string encabezado,
            out string fecha)
        {
            fecha = string.Empty;
            if (string.IsNullOrWhiteSpace(conductorKey) || string.IsNullOrWhiteSpace(encabezado))
            {
                return false;
            }

            if (!overrides.TryGetValue(conductorKey, out var byCapacitacion))
            {
                return false;
            }

            if (!byCapacitacion.TryGetValue(encabezado, out var rawFecha))
            {
                return false;
            }

            fecha = string.IsNullOrWhiteSpace(rawFecha) ? "-" : rawFecha.Trim();
            return true;
        }

        private static IReadOnlyList<ControlDocumentosCumplimientoCapacitacionViewModel> BuildControlDocumentosCumplimientoPorCapacitacion(
            IReadOnlyList<string> encabezadosCapacitacion,
            IReadOnlyList<ControlDocumentosRowViewModel> filas)
        {
            var resumen = new List<ControlDocumentosCumplimientoCapacitacionViewModel>();
            if (encabezadosCapacitacion.Count == 0)
            {
                return resumen;
            }

            var totalConductores = filas.Count;
            for (var capIndex = 0; capIndex < encabezadosCapacitacion.Count; capIndex++)
            {
                var vigente = 0;
                var porVencer = 0;
                var vencida = 0;
                var sinFecha = 0;

                foreach (var fila in filas)
                {
                    var estado = capIndex < fila.Capacitaciones.Count
                        ? (fila.Capacitaciones[capIndex].EstadoFiltro ?? string.Empty).Trim().ToLowerInvariant()
                        : "sinfecha";

                    switch (estado)
                    {
                        case "vigente":
                            vigente++;
                            break;
                        case "porvencer":
                            porVencer++;
                            break;
                        case "vencida":
                            vencida++;
                            break;
                        default:
                            sinFecha++;
                            break;
                    }
                }

                var totalEvaluable = totalConductores - sinFecha;
                var porcentaje = totalEvaluable > 0
                    ? Math.Round(((decimal)(vigente + porVencer) / totalEvaluable) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 100m;
                var estadoVisual = "verde";
                if (vencida > 0 || sinFecha > 0)
                {
                    estadoVisual = "rojo";
                }
                else if (porVencer > 0)
                {
                    estadoVisual = "amarillo";
                }

                resumen.Add(new ControlDocumentosCumplimientoCapacitacionViewModel
                {
                    Capacitacion = encabezadosCapacitacion[capIndex],
                    TotalConductores = totalConductores,
                    Vigentes = vigente,
                    PorVencer = porVencer,
                    Vencidas = vencida,
                    SinFecha = sinFecha,
                    CumplimientoPorcentaje = porcentaje,
                    EstadoVisual = estadoVisual
                });
            }

            return resumen;
        }

        private static ControlDocumentosCapacitacionEstadoViewModel BuildControlDocumentoCapacitacionEstado(
            string encabezado,
            string? fechaDisplay)
        {
            var fecha = string.IsNullOrWhiteSpace(fechaDisplay) ? "-" : fechaDisplay.Trim();
            var diasValidez = ResolveControlDocumentoValidezDias(encabezado);
            var estado = new ControlDocumentosCapacitacionEstadoViewModel
            {
                NombreCapacitacion = encabezado,
                Fecha = string.IsNullOrWhiteSpace(fecha) ? "-" : fecha,
                FechaIso = ToIsoDateForInput(fecha),
                ValidezDias = diasValidez,
                EstadoSemaforo = "rojo",
                EstadoFiltro = "sinfecha",
                EtiquetaSemaforo = "Sin fecha",
                MensajeAlerta = "No hay fecha registrada."
            };

            if (!TryParseControlDocumentoDate(fecha, out var fechaCapacitacion))
            {
                return estado;
            }

            var fechaVencimiento = fechaCapacitacion.Date.AddDays(diasValidez);
            var diasRestantes = (fechaVencimiento - DateTime.Today).Days;

            estado.Fecha = fechaCapacitacion.ToString("dd/MM/yyyy", EsCulture);
            estado.FechaIso = fechaCapacitacion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            estado.FechaVencimiento = fechaVencimiento.ToString("dd/MM/yyyy", EsCulture);
            estado.DiasRestantes = diasRestantes;

            if (diasRestantes < 0)
            {
                estado.EstadoSemaforo = "rojo";
                estado.EstadoFiltro = "vencida";
                estado.EtiquetaSemaforo = "Vencida";
                estado.MensajeAlerta = $"Vencida hace {Math.Abs(diasRestantes)} dia(s).";
            }
            else if (diasRestantes <= 30)
            {
                estado.EstadoSemaforo = "amarillo";
                estado.EstadoFiltro = "porvencer";
                estado.EtiquetaSemaforo = "Por vencer";
                estado.MensajeAlerta = diasRestantes == 0
                    ? "Vence hoy."
                    : $"Vence en {diasRestantes} dia(s).";
            }
            else
            {
                estado.EstadoSemaforo = "verde";
                estado.EstadoFiltro = "vigente";
                estado.EtiquetaSemaforo = "Vigente";
                estado.MensajeAlerta = $"Vigente. Vence en {diasRestantes} dia(s).";
            }

            return estado;
        }

        private static int ResolveControlDocumentoValidezDias(string encabezado)
        {
            var key = NormalizeHeader(encabezado);
            return ControlDocumentosValidezDiasByHeader.TryGetValue(key, out var dias) ? dias : 730;
        }

        private static bool TryParseControlDocumentoDate(string? rawDate, out DateTime parsedDate)
        {
            parsedDate = default;
            var text = (rawDate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                return false;
            }

            if (DateTime.TryParseExact(
                    text,
                    ControlDocumentoDateFormats,
                    EsCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out parsedDate) ||
                DateTime.TryParseExact(
                    text,
                    ControlDocumentoDateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out parsedDate) ||
                DateTime.TryParse(text, EsCulture, DateTimeStyles.AllowWhiteSpaces, out parsedDate) ||
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsedDate))
            {
                parsedDate = parsedDate.Date;
                return true;
            }

            return false;
        }

        private static string NormalizeControlDocumentoKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string ToIsoDateForInput(string? displayDate)
        {
            var text = (displayDate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                return string.Empty;
            }

            if (DateTime.TryParseExact(text, ControlDocumentoDateFormats, EsCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedEs) ||
                DateTime.TryParseExact(text, ControlDocumentoDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsedEs) ||
                DateTime.TryParse(text, EsCulture, DateTimeStyles.AllowWhiteSpaces, out parsedEs) ||
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsedEs))
            {
                return parsedEs.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static string FormatControlDocumentoDateFromInput(string? inputDate)
        {
            var text = (inputDate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (DateTime.TryParseExact(
                    text,
                    ControlDocumentoDateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed) ||
                DateTime.TryParse(text, EsCulture, DateTimeStyles.AllowWhiteSpaces, out parsed) ||
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return parsed.ToString("dd/MM/yyyy", EsCulture);
            }

            return string.Empty;
        }

        private static bool TryValidateAndFormatControlDocumentoDateFromInput(
            string? inputDate,
            out string formattedDate,
            out string errorMessage)
        {
            formattedDate = string.Empty;
            errorMessage = string.Empty;
            var text = (inputDate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (!DateTime.TryParseExact(
                    text,
                    ControlDocumentoDateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed) &&
                !DateTime.TryParse(text, EsCulture, DateTimeStyles.AllowWhiteSpaces, out parsed) &&
                !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                errorMessage = "La fecha no tiene un formato valido.";
                return false;
            }

            if (parsed.Date > DateTime.Today)
            {
                errorMessage = "La fecha no puede ser futura.";
                return false;
            }

            formattedDate = parsed.ToString("dd/MM/yyyy", EsCulture);
            return true;
        }

        private static bool IsEstadoControlDocumentoCritico(string? estadoFiltro)
        {
            var estado = (estadoFiltro ?? string.Empty).Trim().ToLowerInvariant();
            return estado is "vencida" or "sinfecha" or "porvencer";
        }

        private static string ToSafeFileNameToken(string? raw)
        {
            var source = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                return "Conductor";
            }

            var normalized = source.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else if (char.IsWhiteSpace(character) || character is '-' or '_')
                {
                    builder.Append('_');
                }
            }

            var token = Regex.Replace(builder.ToString(), "_{2,}", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(token))
            {
                return "Conductor";
            }

            return token.Length > 60 ? token[..60] : token;
        }

        private static string ComputeEficienciaLabel(decimal toneladas, int viajes)
        {
            if (viajes <= 0)
            {
                return "Sin datos";
            }

            var ratio = toneladas / viajes;
            if (ratio >= 22m)
            {
                return "Alta";
            }

            if (ratio >= 16m)
            {
                return "Media";
            }

            return "Baja";
        }

        private static decimal GetToneladasEnRuta(SeguimientoToneladasRowViewModel row)
        {
            return row.PesoSugerido > 0 ? row.PesoSugerido : row.Toneladas;
        }

        private static string ResolveProcedencia(SeguimientoToneladasRowViewModel row)
        {
            if (!string.IsNullOrWhiteSpace(row.Procedencia))
            {
                return row.Procedencia;
            }

            var ruta = row.Ruta ?? string.Empty;
            var separatorIndex = ruta.IndexOf(" - ", StringComparison.Ordinal);
            return separatorIndex > 0 ? ruta[..separatorIndex] : ruta;
        }

        private static string NormalizeProcedenciaLabel(string? procedencia)
        {
            if (string.IsNullOrWhiteSpace(procedencia))
            {
                return string.Empty;
            }

            var tokens = procedencia
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tokens);
        }

        private static bool IsSitioTriton(SeguimientoToneladasRowViewModel row)
        {
            var label = NormalizeProcedenciaLabel(ResolveProcedencia(row));
            return label.Contains("TRITON", StringComparison.Ordinal);
        }

        private static bool IsSitioPavonAsm(SeguimientoToneladasRowViewModel row)
        {
            var label = NormalizeProcedenciaLabel(ResolveProcedencia(row));
            return label.Contains("PAVON ASM", StringComparison.Ordinal) ||
                   label.StartsWith("PAVON", StringComparison.Ordinal);
        }

        private sealed class DieselUnitAggregate
        {
            public string Unidad { get; set; } = string.Empty;
            public decimal Galones { get; set; }
            public decimal GalonesExtra { get; set; }
            public decimal KilometrosExtra { get; set; }
            public decimal DistanciaTotalKm { get; set; }
            public decimal Litros { get; set; }
            public decimal Toneladas { get; set; }
            public decimal ToneladasPorGalon { get; set; }
            public decimal LitrosPorTonelada { get; set; }
            public decimal VariacionPorcentaje { get; set; }
            public decimal GalonesPromedioPorEvento { get; set; }
            public decimal RendimientoEstimadoKmPorGalon { get; set; }
            public decimal VariacionVsMetaConsumoPorcentaje { get; set; }
            public int Eventos { get; set; }
            public int EventosNocturnos { get; set; }
            public int EventosAtipicos { get; set; }
            public string RutaPrincipal { get; set; } = "Sin ruta";
            public string EstadoConsumo { get; set; } = "SIN DATOS";
            public string EstadoOperacion { get; set; } = "SIN ESTADO";
        }

        private sealed class DieselUnitExtraRecord
        {
            public string Unidad { get; set; } = string.Empty;
            public decimal GalonesExtra { get; set; }
            public decimal KilometrosExtra { get; set; }
            public DateTime UpdatedAt { get; set; } = DateTime.Now;
            public string UpdatedBy { get; set; } = "Sistema";
        }

        private sealed class DieselMaintenanceLogRecord
        {
            public DateTime Fecha { get; set; }
            public string Unidad { get; set; } = string.Empty;
            public string Tipo { get; set; } = string.Empty;
            public string Observacion { get; set; } = string.Empty;
        }

        private sealed class ToneladasRutaAggregateRow
        {
            public string Ruta { get; set; } = "Sin ruta";
            public decimal Toneladas { get; set; }
            public decimal DieselGalones { get; set; }
            public decimal DieselLitros { get; set; }
            public int Equipos { get; set; }
            public int Viajes { get; set; }
            public decimal ToneladasPorGalon { get; set; }
            public decimal LitrosPorTonelada { get; set; }
            public decimal ParticipacionToneladasPct { get; set; }
            public decimal VariacionVsPromedioPct { get; set; }
            public string EstadoRendimiento { get; set; } = "SIN DATOS";
        }

        private sealed class OperacionesReporteAuditEntry
        {
            public DateTime GeneratedAt { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string ReportName { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public int RecordCount { get; set; }
            public string FiltersSummary { get; set; } = string.Empty;
            public string Version { get; set; } = "v1.0";
        }

        private sealed class OperacionesReporteScheduleEntry
        {
            public string Id { get; set; } = string.Empty;
            public string ReportKey { get; set; } = string.Empty;
            public string Frecuencia { get; set; } = "DIARIA";
            public string Hora { get; set; } = "06:00";
            public string DiaSemana { get; set; } = "MONDAY";
            public string Destinatarios { get; set; } = string.Empty;
            public bool IsActive { get; set; } = true;
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public DateTime? LastRunAt { get; set; }
            public DateTime? NextRunAt { get; set; }
        }

        private sealed class OperacionesMetaObjetivo
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public decimal MetaMensualTriton { get; set; }
            public decimal MetaMensualPavonAsm { get; set; }
            public decimal MetaMensualTotal { get; set; }
            public decimal MetaDiariaTriton { get; set; }
            public decimal MetaDiariaPavonAsm { get; set; }
            public decimal MetaDiaria { get; set; }
            public bool IsCustom { get; set; }
        }

        private sealed class OperacionesMetaProduccionMensualEntry
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public decimal MetaMensualTriton { get; set; }
            public decimal MetaMensualPavonAsm { get; set; }
            public decimal MetaDiariaTritonObjetivo { get; set; }
            public decimal MetaDiariaPavonAsmObjetivo { get; set; }
            public decimal MetaDiariaObjetivo { get; set; }
            public DateTime UpdatedAt { get; set; } = DateTime.Now;
            public string UpdatedBy { get; set; } = "Sistema";
        }

        private sealed class ControlDocumentosQrScanRecord
        {
            public string ConductorKey { get; set; } = string.Empty;
            public DateTime FechaEscaneo { get; set; }
            public string Usuario { get; set; } = "Anonimo";
            public string IpAddress { get; set; } = "-";
            public string UserAgent { get; set; } = "-";
        }

        private sealed class ControlDocumentosAlertDispatchRecord
        {
            public string DispatchKey { get; set; } = string.Empty;
            public string Conductor { get; set; } = string.Empty;
            public string Capacitacion { get; set; } = string.Empty;
            public string Estado { get; set; } = string.Empty;
            public string Mensaje { get; set; } = string.Empty;
            public DateTime FechaEnvio { get; set; }
            public bool EnviadoEmail { get; set; }
            public bool EnviadoWhatsApp { get; set; }
        }

        private sealed class ControlDocumentosAlertCandidate
        {
            public string DispatchKey { get; set; } = string.Empty;
            public string Conductor { get; set; } = string.Empty;
            public string Capacitacion { get; set; } = string.Empty;
            public string Estado { get; set; } = string.Empty;
            public string Mensaje { get; set; } = string.Empty;
        }
    }
}




