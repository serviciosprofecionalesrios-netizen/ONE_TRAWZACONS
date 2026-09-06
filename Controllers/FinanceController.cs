using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
    public class FinanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FinanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(DateTime? startDate = null, DateTime? endDate = null)
        {
            var today = DateTime.Today;
            var start = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
            var end = endDate?.Date ?? today;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc);
            var settings = await GetSettingsAsync();
            var tickets = await _context.Tickets
                .AsNoTracking()
                .Where(x => x.Department == "Finanzas" && x.CreatedDate >= startUtc && x.CreatedDate < endExclusiveUtc)
                .Select(x => new
                {
                    x.Id,
                    x.TicketNumber,
                    x.CostCenterCode,
                    x.Description,
                    x.Status,
                    x.CostApproved,
                    x.FinanceReturnedForCorrection,
                    x.FinanceEscalatedAtUtc,
                    x.CreatedDate
                })
                .ToListAsync();

            var invoices = await _context.FinanceInvoices
                .AsNoTracking()
                .Where(x => x.InvoiceDateUtc >= startUtc && x.InvoiceDateUtc < endExclusiveUtc)
                .ToListAsync();
            var receipts = await _context.FinanceReceipts
                .AsNoTracking()
                .Where(x => x.ReceiptDateUtc >= startUtc && x.ReceiptDateUtc < endExclusiveUtc)
                .ToListAsync();
            var centers = await _context.FinanceCostCenters.AsNoTracking().ToListAsync();

            var ticketSpending = tickets
                .Select(x => new
                {
                    Center = string.IsNullOrWhiteSpace(x.CostCenterCode) ? ParseMeta(x.Description).CostCenter : x.CostCenterCode!,
                    Meta = ParseMeta(x.Description),
                    x.Status,
                    x.CostApproved
                })
                .Where(x => !(x.Status == TicketStatus.Closed && !x.CostApproved))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Center) ? "General" : x.Center.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(row => ToCordoba(row.Meta.Amount, row.Meta.Currency, settings.UsdToCordobaRate)),
                    StringComparer.OrdinalIgnoreCase);

            var topCenters = centers
                .Select(center =>
                {
                    ticketSpending.TryGetValue(center.CostCenterNumber, out var spent);
                    if (spent == 0)
                    {
                        ticketSpending.TryGetValue(center.CostCenterName, out spent);
                    }

                    var usage = center.MonthlyBudgetCordoba > 0
                        ? Math.Round((double)(spent / center.MonthlyBudgetCordoba) * 100, 2)
                        : 0d;

                    return new FinanceDashboardCostCenterRowViewModel
                    {
                        Area = center.Area,
                        CostCenterNumber = center.CostCenterNumber,
                        CostCenterName = center.CostCenterName,
                        BudgetCordoba = center.MonthlyBudgetCordoba,
                        SpentCordoba = spent,
                        UsagePercent = usage
                    };
                })
                .OrderByDescending(x => x.UsagePercent)
                .ThenByDescending(x => x.SpentCordoba)
                .Take(10)
                .ToList();

            var recentDocuments = invoices
                .Select(x => new FinanceDashboardDocumentRowViewModel
                {
                    Type = "Factura",
                    Number = x.InvoiceNumber,
                    Counterparty = x.CounterpartyName,
                    Status = x.ApprovalStatus,
                    Amount = x.Amount,
                    Currency = x.Currency,
                    DateUtc = x.InvoiceDateUtc
                })
                .Concat(receipts.Select(x => new FinanceDashboardDocumentRowViewModel
                {
                    Type = "Recibo",
                    Number = x.ReceiptNumber,
                    Counterparty = x.ReceivedFrom,
                    Status = x.ApprovalStatus,
                    Amount = x.Amount,
                    Currency = x.Currency,
                    DateUtc = x.ReceiptDateUtc
                }))
                .OrderByDescending(x => x.DateUtc)
                .Take(12)
                .ToList();

            var audit = await _context.FinanceAuditLogs
                .AsNoTracking()
                .OrderByDescending(x => x.PerformedAtUtc)
                .Take(12)
                .Select(x => new FinanceDashboardAuditRowViewModel
                {
                    EntityName = x.EntityName,
                    Action = x.Action,
                    PerformedBy = x.PerformedBy,
                    PerformedAtUtc = x.PerformedAtUtc,
                    Details = x.Details
                })
                .ToListAsync();

            var totalBudget = centers.Where(x => x.IsActive).Sum(x => x.MonthlyBudgetCordoba);
            var totalSpent = topCenters.Sum(x => x.SpentCordoba);
            var model = new FinanceDashboardViewModel
            {
                StartDate = start,
                EndDate = end,
                UsdToCordobaRate = settings.UsdToCordobaRate,
                OpenRequests = tickets.Count(x => x.Status == TicketStatus.Open || x.Status == TicketStatus.InProgress),
                PendingApprovals = tickets.Count(x => !x.CostApproved && x.Status != TicketStatus.Closed) +
                                   invoices.Count(x => x.ApprovalStatus == "Pendiente") +
                                   receipts.Count(x => x.ApprovalStatus == "Pendiente"),
                ReturnedRequests = tickets.Count(x => x.FinanceReturnedForCorrection),
                EscalatedRequests = tickets.Count(x => x.FinanceEscalatedAtUtc.HasValue),
                InvoicesIssued = invoices.Count,
                ReceiptsIssued = receipts.Count,
                UnsignedDocuments = invoices.Count(x => !x.SignedAtUtc.HasValue) + receipts.Count(x => !x.SignedAtUtc.HasValue),
                InvoiceTotalCordoba = invoices.Sum(x => ToCordoba(x.Amount, x.Currency, settings.UsdToCordobaRate)),
                ReceiptTotalCordoba = receipts.Sum(x => ToCordoba(x.Amount, x.Currency, settings.UsdToCordobaRate)),
                BudgetTotalCordoba = totalBudget,
                BudgetSpentCordoba = totalSpent,
                BudgetAvailableCordoba = totalBudget - totalSpent,
                BudgetUsagePercent = totalBudget > 0 ? Math.Round((double)(totalSpent / totalBudget) * 100, 2) : 0,
                TopCostCenters = topCenters,
                RecentDocuments = recentDocuments,
                RecentAudit = audit
            };

            model.Alerts.AddRange(topCenters
                .Where(x => x.BudgetCordoba > 0 && x.UsagePercent >= 100)
                .Select(x => new FinanceDashboardAlertViewModel { Tone = "danger", Message = $"#{x.CostCenterNumber} excede presupuesto." }));
            model.Alerts.AddRange(topCenters
                .Where(x => x.BudgetCordoba > 0 && x.UsagePercent >= 80 && x.UsagePercent < 100)
                .Select(x => new FinanceDashboardAlertViewModel { Tone = "warning", Message = $"#{x.CostCenterNumber} esta en {x.UsagePercent:0.#}% de uso." }));
            if (model.UnsignedDocuments > 0)
            {
                model.Alerts.Add(new FinanceDashboardAlertViewModel { Tone = "warning", Message = $"{model.UnsignedDocuments} documento(s) tienen firma pendiente." });
            }
            if (model.Alerts.Count == 0)
            {
                model.Alerts.Add(new FinanceDashboardAlertViewModel { Tone = "success", Message = "Sin alertas criticas para el rango seleccionado." });
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = await BuildDashboardExportAsync(startDate, endDate);
            return File(result.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> Counterparties()
        {
            var rows = await _context.FinanceCounterparties
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
            return View(rows);
        }

        [HttpGet]
        public IActionResult CounterpartyCreate()
        {
            return View("CounterpartyEdit", new FinanceCounterpartyEditViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> CounterpartyEdit(int id)
        {
            var entity = await _context.FinanceCounterparties.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            return View(new FinanceCounterpartyEditViewModel
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                TaxId = entity.TaxId,
                Phone = entity.Phone,
                Email = entity.Email,
                Address = entity.Address,
                IsActive = entity.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CounterpartyEdit(FinanceCounterpartyEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = model.Id.HasValue
                ? await _context.FinanceCounterparties.FindAsync(model.Id.Value)
                : new FinanceCounterparty { CreatedAtUtc = DateTime.UtcNow };
            if (entity == null)
            {
                return NotFound();
            }

            entity.Name = model.Name.Trim();
            entity.Type = model.Type.Trim();
            entity.TaxId = string.IsNullOrWhiteSpace(model.TaxId) ? null : model.TaxId.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            entity.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            entity.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            entity.IsActive = model.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (!model.Id.HasValue)
            {
                _context.FinanceCounterparties.Add(entity);
            }

            AddAudit("FinanceCounterparty", entity.Id == 0 ? null : entity.Id, model.Id.HasValue ? "Actualizada" : "Creada", entity.Name);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = "Contraparte guardada correctamente.";
            return RedirectToAction(nameof(Counterparties));
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await GetSettingsAsync();
            return View(new FinanceSettingsViewModel
            {
                UsdToCordobaRate = settings.UsdToCordobaRate,
                DefaultEscalationHours = settings.DefaultEscalationHours,
                DefaultAlertThresholdPercent = settings.DefaultAlertThresholdPercent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(FinanceSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var settings = await _context.FinanceSettings.FirstOrDefaultAsync() ?? new FinanceSetting();
            if (settings.Id == 0)
            {
                _context.FinanceSettings.Add(settings);
            }

            settings.UsdToCordobaRate = model.UsdToCordobaRate;
            settings.DefaultEscalationHours = model.DefaultEscalationHours;
            settings.DefaultAlertThresholdPercent = model.DefaultAlertThresholdPercent;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedBy = CurrentUserName();
            AddAudit("FinanceSetting", settings.Id == 0 ? null : settings.Id, "Actualizada", $"TC {settings.UsdToCordobaRate:N4}");
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = "Configuracion financiera actualizada.";
            return RedirectToAction(nameof(Settings));
        }

        private async Task<(byte[] Bytes, string FileName)> BuildDashboardExportAsync(DateTime? startDate, DateTime? endDate)
        {
            var dashboard = (ViewResult)await Dashboard(startDate, endDate);
            var model = (FinanceDashboardViewModel)dashboard.Model!;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var summary = package.Workbook.Worksheets.Add("Resumen");
            summary.Cells[1, 1].Value = "Indicador";
            summary.Cells[1, 2].Value = "Valor";
            summary.Cells[2, 1].Value = "Solicitudes abiertas";
            summary.Cells[2, 2].Value = model.OpenRequests;
            summary.Cells[3, 1].Value = "Aprobaciones pendientes";
            summary.Cells[3, 2].Value = model.PendingApprovals;
            summary.Cells[4, 1].Value = "Facturas";
            summary.Cells[4, 2].Value = model.InvoicesIssued;
            summary.Cells[5, 1].Value = "Recibos";
            summary.Cells[5, 2].Value = model.ReceiptsIssued;
            summary.Cells[6, 1].Value = "Presupuesto";
            summary.Cells[6, 2].Value = model.BudgetTotalCordoba;
            summary.Cells[7, 1].Value = "Ejecutado";
            summary.Cells[7, 2].Value = model.BudgetSpentCordoba;
            summary.Cells[1, 1, 1, 2].Style.Font.Bold = true;
            summary.Cells.AutoFitColumns();

            var centers = package.Workbook.Worksheets.Add("Centros de costo");
            centers.Cells[1, 1].LoadFromCollection(model.TopCostCenters, true);
            centers.Cells.AutoFitColumns();

            var docs = package.Workbook.Worksheets.Add("Documentos");
            docs.Cells[1, 1].LoadFromCollection(model.RecentDocuments, true);
            docs.Cells.AutoFitColumns();

            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return (package.GetAsByteArray(), $"Finanzas_{suffix}.xlsx");
        }

        private async Task<FinanceSetting> GetSettingsAsync()
        {
            var settings = await _context.FinanceSettings.AsNoTracking().FirstOrDefaultAsync();
            return settings ?? new FinanceSetting();
        }

        private static (decimal Amount, string Currency, string CostCenter) ParseMeta(string? description)
        {
            var amount = 0m;
            var currency = "C$";
            var costCenter = "General";
            if (string.IsNullOrWhiteSpace(description))
            {
                return (amount, currency, costCenter);
            }

            var amountMatch = Regex.Match(description, @"Monto solicitado:\s*(?<currency>C\$|USD|\$)\s*(?<amount>[0-9,.]+)", RegexOptions.IgnoreCase);
            if (amountMatch.Success)
            {
                currency = amountMatch.Groups["currency"].Value.Equals("$", StringComparison.OrdinalIgnoreCase) ? "USD" : amountMatch.Groups["currency"].Value;
                decimal.TryParse(amountMatch.Groups["amount"].Value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
            }

            var centerMatch = Regex.Match(description, @"Centro de costo:\s*(?<center>[^\r\n]+)", RegexOptions.IgnoreCase);
            if (centerMatch.Success)
            {
                costCenter = centerMatch.Groups["center"].Value.Trim();
                var dash = costCenter.IndexOf(" - ", StringComparison.Ordinal);
                if (dash > 0)
                {
                    costCenter = costCenter[..dash].Trim();
                }
            }

            return (amount, currency, costCenter);
        }

        private static decimal ToCordoba(decimal amount, string? currency, decimal rate)
        {
            return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) || string.Equals(currency, "$", StringComparison.OrdinalIgnoreCase)
                ? amount * rate
                : amount;
        }

        private string CurrentUserName()
        {
            return string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "Sistema" : User.Identity!.Name!.Trim();
        }

        private void AddAudit(string entityName, int? entityId, string action, string? details)
        {
            _context.FinanceAuditLogs.Add(new FinanceAuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Action = action,
                PerformedBy = CurrentUserName(),
                PerformedAtUtc = DateTime.UtcNow,
                Details = details
            });
        }
    }
}
