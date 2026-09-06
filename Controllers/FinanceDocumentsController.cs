using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.FinanceDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
    public class FinanceDocumentsController : Controller
    {
        private static readonly string[] FinanceSites =
        {
            "Plantel San Benito",
            "Mina El Limón",
            "Mina La Libertad",
            "Oficinas Centrales TW",
            "Comprador"
        };
        private static readonly Dictionary<string, string> FinanceSiteAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mina El Limon"] = "Mina El Limón"
        };

        private static readonly string[] CurrencyOptions =
        {
            "C$",
            "USD"
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FinanceDocumentsController> _logger;

        public FinanceDocumentsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<FinanceDocumentsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceCreate()
        {
            var model = new FinanceInvoiceCreateViewModel
            {
                InvoiceDate = DateTime.Today,
                Currency = "C$"
            };

            await PopulateInvoiceFormOptionsAsync(model);
            ViewBag.NextNumberPreview = BuildNumberPreview("FAC");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InvoiceCreate(FinanceInvoiceCreateViewModel model)
        {
            NormalizeInvoice(model);
            ValidateInvoice(model);

            if (!ModelState.IsValid)
            {
                await PopulateInvoiceFormOptionsAsync(model);
                ViewBag.NextNumberPreview = BuildNumberPreview("FAC");
                return View(model);
            }

            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                FinanceInvoice? entity = null;
                try
                {
                    entity = new FinanceInvoice
                    {
                        InvoiceNumber = await GenerateNextInvoiceNumberAsync(),
                        CounterpartyId = model.CounterpartyId,
                        Site = CanonicalSite(model.Site),
                        CounterpartyName = model.CounterpartyName,
                        CounterpartyTaxId = model.CounterpartyTaxId,
                        Concept = model.Concept,
                        Amount = decimal.Round(model.Amount, 2, MidpointRounding.AwayFromZero),
                        Currency = CanonicalCurrency(model.Currency),
                        InvoiceDateUtc = DateTime.SpecifyKind(model.InvoiceDate.Date, DateTimeKind.Utc),
                        Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                        CreatedBy = CurrentUserName(),
                        CreatedAtUtc = DateTime.UtcNow,
                        SignatureToken = Guid.NewGuid().ToString("N")
                    };

                    _context.FinanceInvoices.Add(entity);
                    await _context.SaveChangesAsync();
                    await SaveAttachmentsAsync("Factura", entity.Id, model.Attachments);
                    await AddAuditAsync("FinanceInvoice", entity.Id, "Creada", $"Factura {entity.InvoiceNumber} por {entity.Currency} {entity.Amount:N2}.");
                    await _context.SaveChangesAsync();
                    TempData["TicketsMessage"] = $"Factura {entity.InvoiceNumber} creada correctamente.";
                    return RedirectToAction(nameof(InvoiceDetails), new { id = entity.Id });
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts)
                {
                    DetachTrackedEntity(entity);
                    _logger.LogWarning(ex, "Reintentando guardado de factura por error de actualización. Intento {Attempt} de {MaxAttempts}.", attempt, maxAttempts);
                }
                catch (Exception ex)
                {
                    DetachTrackedEntity(entity);
                    _logger.LogError(ex, "Error al guardar factura en el intento {Attempt}.", attempt);
                    ModelState.AddModelError(
                        string.Empty,
                        "No fue posible guardar la factura. Verifica conexión SQL/migraciones y vuelve a intentar.");
                    break;
                }
            }

            await PopulateInvoiceFormOptionsAsync(model);
            ViewBag.NextNumberPreview = BuildNumberPreview("FAC");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceDetails(int id)
        {
            var entity = await _context.FinanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            ViewBag.SignUrl = BuildAbsoluteUrl(nameof(SignInvoice), new { token = entity.SignatureToken });
            ViewBag.Attachments = await GetAttachmentsAsync("Factura", entity.Id);
            return View(entity);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoicePdf(int id)
        {
            var entity = await _context.FinanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            var signUrl = entity.SignedAtUtc.HasValue
                ? null
                : BuildAbsoluteUrl(nameof(SignInvoice), new { token = entity.SignatureToken });
            var bytes = FinanceInvoicePdfReportService.GeneratePdf(entity, _environment.WebRootPath, signUrl);
            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return File(bytes, "application/pdf", $"Factura_{entity.InvoiceNumber}_{suffix}.pdf");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DownloadInvoicePdfByToken(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            var signUrl = entity.SignedAtUtc.HasValue
                ? null
                : BuildAbsoluteUrl(nameof(SignInvoice), new { token = entity.SignatureToken });
            var bytes = FinanceInvoicePdfReportService.GeneratePdf(entity, _environment.WebRootPath, signUrl);
            return File(bytes, "application/pdf", $"Factura_{entity.InvoiceNumber}.pdf");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignInvoice(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            var model = BuildInvoiceSignModel(entity);
            ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadInvoicePdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignInvoice(FinanceInvoiceSignViewModel model)
        {
            var normalizedToken = NormalizeToken(model.Token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceInvoices
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            model.Token = normalizedToken;
            model.InvoiceId = entity.Id;
            model.InvoiceNumber = entity.InvoiceNumber;
            model.Site = entity.Site;
            model.CounterpartyName = entity.CounterpartyName;
            model.Concept = entity.Concept;
            model.Currency = entity.Currency;
            model.Amount = entity.Amount;
            model.InvoiceDate = entity.InvoiceDateUtc.ToLocalTime().Date;
            model.AlreadySigned = entity.SignedAtUtc.HasValue;
            model.SignedByName = entity.SignedByName;
            model.SignedAtLocal = entity.SignedAtUtc?.ToLocalTime();

            if (entity.SignedAtUtc.HasValue)
            {
                TempData["TicketsMessage"] = "Esta factura ya fue firmada.";
                return RedirectToAction(nameof(SignInvoiceDone), new { token = normalizedToken });
            }

            model.SignerName = model.SignerName?.Trim() ?? string.Empty;
            model.SignatureDataUrl = model.SignatureDataUrl?.Trim() ?? string.Empty;

            ValidateSignatureInput(model.SignerName, model.SignatureDataUrl);

            if (!ModelState.IsValid)
            {
                ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadInvoicePdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
                return View(model);
            }

            entity.SignedByName = model.SignerName;
            entity.SignatureDataUrl = model.SignatureDataUrl;
            entity.SignedAtUtc = DateTime.UtcNow;
            entity.SignatureDeviceInfo = Request.Headers.UserAgent.ToString();

            await AddAuditAsync("FinanceInvoice", entity.Id, "Firmada", $"Firmada por {model.SignerName}.");
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(SignInvoiceDone), new { token = normalizedToken });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignInvoiceDone(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadInvoicePdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
            return View(entity);
        }

        [HttpGet]
        public async Task<IActionResult> ReceiptCreate()
        {
            var model = new FinanceReceiptCreateViewModel
            {
                ReceiptDate = DateTime.Today,
                Currency = "C$"
            };

            await PopulateReceiptFormOptionsAsync(model);
            ViewBag.NextNumberPreview = BuildNumberPreview("REC");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiptCreate(FinanceReceiptCreateViewModel model)
        {
            NormalizeReceipt(model);
            ValidateReceipt(model);

            if (!ModelState.IsValid)
            {
                await PopulateReceiptFormOptionsAsync(model);
                ViewBag.NextNumberPreview = BuildNumberPreview("REC");
                return View(model);
            }

            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                FinanceReceipt? entity = null;
                try
                {
                    entity = new FinanceReceipt
                    {
                        ReceiptNumber = await GenerateNextReceiptNumberAsync(),
                        CounterpartyId = model.CounterpartyId,
                        Site = CanonicalSite(model.Site),
                        ReceivedFrom = model.ReceivedFrom,
                        CounterpartyTaxId = model.CounterpartyTaxId,
                        Concept = model.Concept,
                        Amount = decimal.Round(model.Amount, 2, MidpointRounding.AwayFromZero),
                        Currency = CanonicalCurrency(model.Currency),
                        ReceiptDateUtc = DateTime.SpecifyKind(model.ReceiptDate.Date, DateTimeKind.Utc),
                        Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                        CreatedBy = CurrentUserName(),
                        CreatedAtUtc = DateTime.UtcNow,
                        SignatureToken = Guid.NewGuid().ToString("N")
                    };

                    _context.FinanceReceipts.Add(entity);
                    await _context.SaveChangesAsync();
                    await SaveAttachmentsAsync("Recibo", entity.Id, model.Attachments);
                    await AddAuditAsync("FinanceReceipt", entity.Id, "Creado", $"Recibo {entity.ReceiptNumber} por {entity.Currency} {entity.Amount:N2}.");
                    await _context.SaveChangesAsync();
                    TempData["TicketsMessage"] = $"Recibo {entity.ReceiptNumber} creado correctamente.";
                    return RedirectToAction(nameof(ReceiptDetails), new { id = entity.Id });
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts)
                {
                    DetachTrackedEntity(entity);
                    _logger.LogWarning(ex, "Reintentando guardado de recibo por error de actualización. Intento {Attempt} de {MaxAttempts}.", attempt, maxAttempts);
                }
                catch (Exception ex)
                {
                    DetachTrackedEntity(entity);
                    _logger.LogError(ex, "Error al guardar recibo en el intento {Attempt}.", attempt);
                    ModelState.AddModelError(
                        string.Empty,
                        "No fue posible guardar el recibo. Verifica conexión SQL/migraciones y vuelve a intentar.");
                    break;
                }
            }

            await PopulateReceiptFormOptionsAsync(model);
            ViewBag.NextNumberPreview = BuildNumberPreview("REC");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ReceiptDetails(int id)
        {
            var entity = await _context.FinanceReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            ViewBag.SignUrl = BuildAbsoluteUrl(nameof(SignReceipt), new { token = entity.SignatureToken });
            ViewBag.Attachments = await GetAttachmentsAsync("Recibo", entity.Id);
            return View(entity);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReceiptPdf(int id)
        {
            var entity = await _context.FinanceReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            var signUrl = entity.SignedAtUtc.HasValue
                ? null
                : BuildAbsoluteUrl(nameof(SignReceipt), new { token = entity.SignatureToken });
            var bytes = FinanceReceiptPdfReportService.GeneratePdf(entity, _environment.WebRootPath, signUrl);
            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return File(bytes, "application/pdf", $"Recibo_{entity.ReceiptNumber}_{suffix}.pdf");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DownloadReceiptPdfByToken(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            var signUrl = entity.SignedAtUtc.HasValue
                ? null
                : BuildAbsoluteUrl(nameof(SignReceipt), new { token = entity.SignatureToken });
            var bytes = FinanceReceiptPdfReportService.GeneratePdf(entity, _environment.WebRootPath, signUrl);
            return File(bytes, "application/pdf", $"Recibo_{entity.ReceiptNumber}.pdf");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignReceipt(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            var model = BuildReceiptSignModel(entity);
            ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadReceiptPdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignReceipt(FinanceReceiptSignViewModel model)
        {
            var normalizedToken = NormalizeToken(model.Token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceReceipts
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            model.Token = normalizedToken;
            model.ReceiptId = entity.Id;
            model.ReceiptNumber = entity.ReceiptNumber;
            model.Site = entity.Site;
            model.ReceivedFrom = entity.ReceivedFrom;
            model.Concept = entity.Concept;
            model.Currency = entity.Currency;
            model.Amount = entity.Amount;
            model.ReceiptDate = entity.ReceiptDateUtc.ToLocalTime().Date;
            model.AlreadySigned = entity.SignedAtUtc.HasValue;
            model.SignedByName = entity.SignedByName;
            model.SignedAtLocal = entity.SignedAtUtc?.ToLocalTime();

            if (entity.SignedAtUtc.HasValue)
            {
                TempData["TicketsMessage"] = "Este recibo ya fue firmado.";
                return RedirectToAction(nameof(SignReceiptDone), new { token = normalizedToken });
            }

            model.SignerName = model.SignerName?.Trim() ?? string.Empty;
            model.SignatureDataUrl = model.SignatureDataUrl?.Trim() ?? string.Empty;

            ValidateSignatureInput(model.SignerName, model.SignatureDataUrl);

            if (!ModelState.IsValid)
            {
                ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadReceiptPdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
                return View(model);
            }

            entity.SignedByName = model.SignerName;
            entity.SignatureDataUrl = model.SignatureDataUrl;
            entity.SignedAtUtc = DateTime.UtcNow;
            entity.SignatureDeviceInfo = Request.Headers.UserAgent.ToString();

            await AddAuditAsync("FinanceReceipt", entity.Id, "Firmado", $"Firmado por {model.SignerName}.");
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(SignReceiptDone), new { token = normalizedToken });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignReceiptDone(string token)
        {
            var normalizedToken = NormalizeToken(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return NotFound();
            }

            var entity = await _context.FinanceReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SignatureToken == normalizedToken);

            if (entity == null)
            {
                return NotFound();
            }

            ViewBag.PdfByTokenUrl = Url.Action(nameof(DownloadReceiptPdfByToken), "FinanceDocuments", new { token = entity.SignatureToken });
            return View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveInvoice(int id, string? notes)
        {
            var entity = await _context.FinanceInvoices.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.ApprovalStatus = "Aprobado";
            entity.ApprovedBy = CurrentUserName();
            entity.ApprovedAtUtc = DateTime.UtcNow;
            entity.ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            await AddAuditAsync("FinanceInvoice", entity.Id, "Aprobada", entity.ApprovalNotes);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Factura {entity.InvoiceNumber} aprobada.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectInvoice(int id, string? notes)
        {
            var entity = await _context.FinanceInvoices.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.ApprovalStatus = "Rechazado";
            entity.ApprovedBy = CurrentUserName();
            entity.ApprovedAtUtc = DateTime.UtcNow;
            entity.ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? "Sin detalle." : notes.Trim();
            await AddAuditAsync("FinanceInvoice", entity.Id, "Rechazada", entity.ApprovalNotes);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Factura {entity.InvoiceNumber} rechazada.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReceipt(int id, string? notes)
        {
            var entity = await _context.FinanceReceipts.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.ApprovalStatus = "Aprobado";
            entity.ApprovedBy = CurrentUserName();
            entity.ApprovedAtUtc = DateTime.UtcNow;
            entity.ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            await AddAuditAsync("FinanceReceipt", entity.Id, "Aprobado", entity.ApprovalNotes);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Recibo {entity.ReceiptNumber} aprobado.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReceipt(int id, string? notes)
        {
            var entity = await _context.FinanceReceipts.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.ApprovalStatus = "Rechazado";
            entity.ApprovedBy = CurrentUserName();
            entity.ApprovedAtUtc = DateTime.UtcNow;
            entity.ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? "Sin detalle." : notes.Trim();
            await AddAuditAsync("FinanceReceipt", entity.Id, "Rechazado", entity.ApprovalNotes);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Recibo {entity.ReceiptNumber} rechazado.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var attachment = await _context.FinanceAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            var fullPath = Path.Combine(_environment.WebRootPath, attachment.StoredPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        private async Task<string> GenerateNextInvoiceNumberAsync()
        {
            var prefix = BuildNumberPrefix("FAC");
            var last = await _context.FinanceInvoices
                .AsNoTracking()
                .Where(x => EF.Functions.Like(x.InvoiceNumber, prefix + "-%"))
                .OrderByDescending(x => x.InvoiceNumber)
                .Select(x => x.InvoiceNumber)
                .FirstOrDefaultAsync();

            var next = ParseSequence(last) + 1;
            return $"{prefix}-{next:D4}";
        }

        private async Task<string> GenerateNextReceiptNumberAsync()
        {
            var prefix = BuildNumberPrefix("REC");
            var last = await _context.FinanceReceipts
                .AsNoTracking()
                .Where(x => EF.Functions.Like(x.ReceiptNumber, prefix + "-%"))
                .OrderByDescending(x => x.ReceiptNumber)
                .Select(x => x.ReceiptNumber)
                .FirstOrDefaultAsync();

            var next = ParseSequence(last) + 1;
            return $"{prefix}-{next:D4}";
        }

        private static int ParseSequence(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var index = value.LastIndexOf('-');
            if (index < 0 || index == value.Length - 1)
            {
                return 0;
            }

            return int.TryParse(value[(index + 1)..], out var number)
                ? number
                : 0;
        }

        private static string BuildNumberPrefix(string key)
        {
            return $"{key}-{DateTime.UtcNow:yyyyMM}";
        }

        private static string BuildNumberPreview(string key)
        {
            return $"{BuildNumberPrefix(key)}-0001";
        }

        private static string NormalizeToken(string? token)
        {
            return string.IsNullOrWhiteSpace(token)
                ? string.Empty
                : token.Trim();
        }

        private static string CanonicalCurrency(string? currency)
        {
            var normalized = string.IsNullOrWhiteSpace(currency) ? "C$" : currency.Trim();
            var canonical = CurrencyOptions.FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return canonical ?? "C$";
        }

        private static string CanonicalSite(string? site)
        {
            var normalized = string.IsNullOrWhiteSpace(site) ? string.Empty : site.Trim();
            if (FinanceSiteAliases.TryGetValue(normalized, out var aliasCanonical))
            {
                normalized = aliasCanonical;
            }

            var canonical = FinanceSites.FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return canonical ?? normalized;
        }

        private string CurrentUserName()
        {
            return string.IsNullOrWhiteSpace(User?.Identity?.Name)
                ? "Sistema"
                : User.Identity!.Name!.Trim();
        }

        private async Task PopulateInvoiceFormOptionsAsync(FinanceInvoiceCreateViewModel model)
        {
            ViewBag.SiteOptions = FinanceSites
                .Select(site => new SelectListItem(site, site, string.Equals(model.Site, site, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.CurrencyOptions = CurrencyOptions
                .Select(currency => new SelectListItem(currency, currency, string.Equals(model.Currency, currency, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.CounterpartyOptions = await BuildCounterpartyOptionsAsync(model.CounterpartyId);
        }

        private async Task PopulateReceiptFormOptionsAsync(FinanceReceiptCreateViewModel model)
        {
            ViewBag.SiteOptions = FinanceSites
                .Select(site => new SelectListItem(site, site, string.Equals(model.Site, site, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.CurrencyOptions = CurrencyOptions
                .Select(currency => new SelectListItem(currency, currency, string.Equals(model.Currency, currency, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.CounterpartyOptions = await BuildCounterpartyOptionsAsync(model.CounterpartyId);
        }

        private static void NormalizeInvoice(FinanceInvoiceCreateViewModel model)
        {
            model.Site = CanonicalSite(model.Site);
            model.CounterpartyName = model.CounterpartyName?.Trim() ?? string.Empty;
            model.CounterpartyTaxId = string.IsNullOrWhiteSpace(model.CounterpartyTaxId) ? null : model.CounterpartyTaxId.Trim();
            model.Concept = model.Concept?.Trim() ?? string.Empty;
            model.Currency = model.Currency?.Trim() ?? "C$";
            model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        }

        private static void NormalizeReceipt(FinanceReceiptCreateViewModel model)
        {
            model.Site = CanonicalSite(model.Site);
            model.ReceivedFrom = model.ReceivedFrom?.Trim() ?? string.Empty;
            model.CounterpartyTaxId = string.IsNullOrWhiteSpace(model.CounterpartyTaxId) ? null : model.CounterpartyTaxId.Trim();
            model.Concept = model.Concept?.Trim() ?? string.Empty;
            model.Currency = model.Currency?.Trim() ?? "C$";
            model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        }

        private void ValidateInvoice(FinanceInvoiceCreateViewModel model)
        {
            if (!FinanceSites.Contains(CanonicalSite(model.Site), StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Site), "Selecciona un sitio valido.");
            }

            if (!CurrencyOptions.Contains(model.Currency, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Currency), "Selecciona una moneda valida.");
            }
        }

        private void ValidateReceipt(FinanceReceiptCreateViewModel model)
        {
            if (!FinanceSites.Contains(CanonicalSite(model.Site), StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Site), "Selecciona un sitio valido.");
            }

            if (!CurrencyOptions.Contains(model.Currency, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Currency), "Selecciona una moneda valida.");
            }
        }

        private FinanceReceiptSignViewModel BuildReceiptSignModel(FinanceReceipt entity)
        {
            return new FinanceReceiptSignViewModel
            {
                Token = entity.SignatureToken,
                ReceiptId = entity.Id,
                ReceiptNumber = entity.ReceiptNumber,
                Site = entity.Site,
                ReceivedFrom = entity.ReceivedFrom,
                Concept = entity.Concept,
                Currency = entity.Currency,
                Amount = entity.Amount,
                ReceiptDate = entity.ReceiptDateUtc.ToLocalTime().Date,
                AlreadySigned = entity.SignedAtUtc.HasValue,
                SignedByName = entity.SignedByName,
                SignedAtLocal = entity.SignedAtUtc?.ToLocalTime()
            };
        }

        private FinanceInvoiceSignViewModel BuildInvoiceSignModel(FinanceInvoice entity)
        {
            return new FinanceInvoiceSignViewModel
            {
                Token = entity.SignatureToken,
                InvoiceId = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                Site = entity.Site,
                CounterpartyName = entity.CounterpartyName,
                Concept = entity.Concept,
                Currency = entity.Currency,
                Amount = entity.Amount,
                InvoiceDate = entity.InvoiceDateUtc.ToLocalTime().Date,
                AlreadySigned = entity.SignedAtUtc.HasValue,
                SignedByName = entity.SignedByName,
                SignedAtLocal = entity.SignedAtUtc?.ToLocalTime()
            };
        }

        private void ValidateSignatureInput(string signerName, string signatureDataUrl)
        {
            if (string.IsNullOrWhiteSpace(signerName))
            {
                ModelState.AddModelError("SignerName", "Debes ingresar el nombre de quien firma.");
            }

            if (string.IsNullOrWhiteSpace(signatureDataUrl))
            {
                ModelState.AddModelError("SignatureDataUrl", "Debes capturar la firma.");
                return;
            }

            if (!signatureDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
                !signatureDataUrl.Contains("base64,", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("SignatureDataUrl", "La firma no tiene un formato válido.");
            }

            if (signatureDataUrl.Length > 900_000)
            {
                ModelState.AddModelError("SignatureDataUrl", "La firma excede el tamaño permitido.");
            }
        }

        private string BuildAbsoluteUrl(string action, object values)
        {
            return Url.Action(action, "FinanceDocuments", values, Request.Scheme, Request.Host.ToString())
                ?? string.Empty;
        }

        private async Task<List<SelectListItem>> BuildCounterpartyOptionsAsync(int? selectedId)
        {
            var counterparties = await _context.FinanceCounterparties
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Type,
                    x.TaxId
                })
                .ToListAsync();

            var options = new List<SelectListItem>
            {
                new("Registrar manualmente", string.Empty, !selectedId.HasValue)
            };

            options.AddRange(counterparties.Select(x =>
                new SelectListItem(
                    string.IsNullOrWhiteSpace(x.TaxId) ? $"{x.Name} ({x.Type})" : $"{x.Name} ({x.Type}) - {x.TaxId}",
                    x.Id.ToString(),
                    selectedId == x.Id)));

            return options;
        }

        private async Task SaveAttachmentsAsync(string documentType, int documentId, IEnumerable<IFormFile>? files)
        {
            if (files == null)
            {
                return;
            }

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "finance", documentType.ToLowerInvariant(), documentId.ToString());
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files.Where(x => x != null && x.Length > 0))
            {
                if (file.Length > 10 * 1024 * 1024)
                {
                    continue;
                }

                var safeName = Path.GetFileName(file.FileName);
                var storedName = $"{Guid.NewGuid():N}_{safeName}";
                var fullPath = Path.Combine(uploadRoot, storedName);

                await using (var stream = System.IO.File.Create(fullPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/finance/{documentType.ToLowerInvariant()}/{documentId}/{storedName}";
                _context.FinanceAttachments.Add(new FinanceAttachment
                {
                    DocumentType = documentType,
                    DocumentId = documentId,
                    FileName = safeName,
                    StoredPath = relativePath,
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    UploadedBy = CurrentUserName(),
                    UploadedAtUtc = DateTime.UtcNow
                });
            }
        }

        private Task<List<FinanceAttachment>> GetAttachmentsAsync(string documentType, int documentId)
        {
            return _context.FinanceAttachments
                .AsNoTracking()
                .Where(x => x.DocumentType == documentType && x.DocumentId == documentId)
                .OrderByDescending(x => x.UploadedAtUtc)
                .ToListAsync();
        }

        private Task AddAuditAsync(string entityName, int? entityId, string action, string? details)
        {
            _context.FinanceAuditLogs.Add(new FinanceAuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Action = action,
                PerformedBy = CurrentUserName(),
                PerformedAtUtc = DateTime.UtcNow,
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim()
            });

            return Task.CompletedTask;
        }

        private void DetachTrackedEntity<TEntity>(TEntity? entity) where TEntity : class
        {
            if (entity == null)
            {
                return;
            }

            _context.Entry(entity).State = EntityState.Detached;
        }
    }
}
