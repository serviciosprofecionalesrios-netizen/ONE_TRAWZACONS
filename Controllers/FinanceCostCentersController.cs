using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.FinanceCostCenters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
    public class FinanceCostCentersController : Controller
    {
        private const decimal FinanceUsdToCordobaRateFallback = 36.5m;

        private readonly ApplicationDbContext _context;

        public FinanceCostCentersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? periodPreset = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? area = null,
            string? costCenterNumber = null)
        {
            var preset = NormalizePreset(periodPreset);
            var (effectiveStart, effectiveEnd) = ResolveDateRange(preset, startDate, endDate);
            var normalizedArea = NormalizeFilter(area);
            var normalizedCcNumber = NormalizeFilter(costCenterNumber);

            var allCenters = await _context.FinanceCostCenters
                .AsNoTracking()
                .OrderBy(x => x.Area)
                .ThenBy(x => x.CostCenterNumber)
                .ToListAsync();

            var filteredCenters = allCenters.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(normalizedArea))
            {
                filteredCenters = filteredCenters.Where(x => x.Area.Equals(normalizedArea, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(normalizedCcNumber))
            {
                filteredCenters = filteredCenters.Where(x => x.CostCenterNumber.Equals(normalizedCcNumber, StringComparison.OrdinalIgnoreCase));
            }

            var centers = filteredCenters.ToList();

            var startUtc = DateTime.SpecifyKind(effectiveStart.Date, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(effectiveEnd.Date.AddDays(1), DateTimeKind.Utc);

            var spendEntries = await BuildTicketSpendEntriesAsync(startUtc, endExclusiveUtc);
            var areaCostCenterMap = BuildUniqueAreaCostCenterMap(allCenters.Where(x => x.IsActive));

            var summaries = centers
                .Select(center =>
                {
                    var matchedRows = spendEntries
                        .Where(row => IsEntryForCenter(row, center, areaCostCenterMap))
                        .ToList();

                    decimal spent = 0m;
                    var overBudgetRequests = 0;
                    foreach (var row in matchedRows)
                    {
                        spent += row.AmountCordoba;
                        if (row.HasBudgetRisk)
                        {
                            overBudgetRequests++;
                        }
                    }

                    var budget = Math.Max(0m, center.MonthlyBudgetCordoba);
                    var available = budget - spent;
                    var usage = budget > 0
                        ? Math.Round((double)(spent / budget) * 100, 2)
                        : 0;
                    var near = usage >= center.AlertThresholdPercent && usage < 100;
                    var over = usage >= 100;

                    return new FinanceCostCenterSummaryRowViewModel
                    {
                        Id = center.Id,
                        Area = center.Area,
                        CostCenterNumber = center.CostCenterNumber,
                        CostCenterName = center.CostCenterName,
                        BudgetCordoba = budget,
                        SpentCordoba = spent,
                        AvailableCordoba = available,
                        UsagePercent = usage,
                        AlertThresholdPercent = center.AlertThresholdPercent,
                        IsNearThreshold = near,
                        IsOverBudget = over,
                        RequestCount = matchedRows.Count,
                        OverBudgetRequestCount = overBudgetRequests,
                        HardStopOnOverrun = center.HardStopOnOverrun,
                        RequireAuthorizationOnOverrun = center.RequireAuthorizationOnOverrun,
                        IsActive = center.IsActive
                    };
                })
                .OrderBy(x => x.Area)
                .ThenBy(x => x.CostCenterNumber)
                .ToList();

            var totalBudget = summaries.Sum(x => x.BudgetCordoba);
            var totalSpent = summaries.Sum(x => x.SpentCordoba);
            var totalAvailable = totalBudget - totalSpent;
            var usagePercent = totalBudget > 0
                ? Math.Round((double)(totalSpent / totalBudget) * 100, 2)
                : 0;

            var alerts = new List<FinanceCostCenterAlertViewModel>();
            alerts.AddRange(summaries
                .Where(x => x.IsOverBudget)
                .OrderByDescending(x => x.UsagePercent)
                .Select(x => new FinanceCostCenterAlertViewModel
                {
                    Severity = "danger",
                    Message = $"#{x.CostCenterNumber} ({x.Area}) excede presupuesto en C$ {Math.Abs(x.AvailableCordoba):N2}."
                }));

            alerts.AddRange(summaries
                .Where(x => !x.IsOverBudget && x.IsNearThreshold)
                .OrderByDescending(x => x.UsagePercent)
                .Select(x => new FinanceCostCenterAlertViewModel
                {
                    Severity = "warning",
                    Message = $"#{x.CostCenterNumber} ({x.Area}) alcanzo {x.UsagePercent:0.#}% del presupuesto mensual."
                }));

            if (alerts.Count == 0)
            {
                alerts.Add(new FinanceCostCenterAlertViewModel
                {
                    Severity = "success",
                    Message = "Sin alertas de sobregiro para el rango seleccionado."
                });
            }

            var areaOptions = new List<FinanceCostCenterSelectOptionViewModel>
            {
                new() { Value = string.Empty, Label = "Todas las areas" }
            };
            areaOptions.AddRange(allCenters
                .Select(x => x.Area)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new FinanceCostCenterSelectOptionViewModel
                {
                    Value = x,
                    Label = x
                }));

            var ccOptions = new List<FinanceCostCenterSelectOptionViewModel>
            {
                new() { Value = string.Empty, Label = "Todos los #CC" }
            };
            ccOptions.AddRange(allCenters
                .OrderBy(x => x.Area)
                .ThenBy(x => x.CostCenterNumber)
                .Select(x => new FinanceCostCenterSelectOptionViewModel
                {
                    Value = x.CostCenterNumber,
                    Label = $"#{x.CostCenterNumber} - {x.Area} - {x.CostCenterName}"
                }));

            var model = new FinanceCostCenterIndexViewModel
            {
                PeriodPreset = preset,
                StartDate = effectiveStart,
                EndDate = effectiveEnd,
                SelectedArea = normalizedArea,
                SelectedCostCenterNumber = normalizedCcNumber,
                AreaOptions = areaOptions,
                CostCenterOptions = ccOptions,
                TotalBudgetCordoba = totalBudget,
                TotalSpentCordoba = totalSpent,
                TotalAvailableCordoba = totalAvailable,
                UsagePercent = usagePercent,
                NearThresholdCount = summaries.Count(x => x.IsNearThreshold),
                OverBudgetCount = summaries.Count(x => x.IsOverBudget),
                OverBudgetRequestsCount = summaries.Sum(x => x.OverBudgetRequestCount),
                Alerts = alerts,
                Rows = summaries
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new FinanceCostCenterEditViewModel();
            await PopulateAreaOptionsAsync(model.Area);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateNumber(string? area)
        {
            var normalizedArea = string.IsNullOrWhiteSpace(area) ? "General" : area.Trim();
            var number = await GenerateCostCenterNumberAsync(normalizedArea, null);
            return Json(new
            {
                number
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FinanceCostCenterEditViewModel model)
        {
            NormalizeModel(model);

            if (string.IsNullOrWhiteSpace(model.CostCenterNumber))
            {
                model.CostCenterNumber = await GenerateCostCenterNumberAsync(model.Area, null);
            }

            if (!await IsCostCenterNumberUniqueAsync(model.CostCenterNumber, null))
            {
                ModelState.AddModelError(nameof(model.CostCenterNumber), "Ya existe un #CC con ese numero.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateAreaOptionsAsync(model.Area);
                return View(model);
            }

            var entity = new FinanceCostCenter
            {
                Area = model.Area,
                CostCenterNumber = model.CostCenterNumber,
                CostCenterName = model.CostCenterName,
                MonthlyBudgetCordoba = model.MonthlyBudgetCordoba,
                AlertThresholdPercent = model.AlertThresholdPercent,
                HardStopOnOverrun = model.HardStopOnOverrun,
                RequireAuthorizationOnOverrun = model.RequireAuthorizationOnOverrun,
                IsActive = model.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.FinanceCostCenters.Add(entity);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Centro de costo #{entity.CostCenterNumber} creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.FinanceCostCenters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            var model = new FinanceCostCenterEditViewModel
            {
                Id = entity.Id,
                Area = entity.Area,
                CostCenterNumber = entity.CostCenterNumber,
                CostCenterName = entity.CostCenterName,
                MonthlyBudgetCordoba = entity.MonthlyBudgetCordoba,
                AlertThresholdPercent = entity.AlertThresholdPercent,
                HardStopOnOverrun = entity.HardStopOnOverrun,
                RequireAuthorizationOnOverrun = entity.RequireAuthorizationOnOverrun,
                IsActive = entity.IsActive
            };

            await PopulateAreaOptionsAsync(model.Area);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FinanceCostCenterEditViewModel model)
        {
            if (!model.Id.HasValue)
            {
                return BadRequest();
            }

            NormalizeModel(model);
            if (string.IsNullOrWhiteSpace(model.CostCenterNumber))
            {
                model.CostCenterNumber = await GenerateCostCenterNumberAsync(model.Area, model.Id);
            }

            if (!await IsCostCenterNumberUniqueAsync(model.CostCenterNumber, model.Id.Value))
            {
                ModelState.AddModelError(nameof(model.CostCenterNumber), "Ya existe un #CC con ese numero.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateAreaOptionsAsync(model.Area);
                return View(model);
            }

            var entity = await _context.FinanceCostCenters.FirstOrDefaultAsync(x => x.Id == model.Id.Value);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Area = model.Area;
            entity.CostCenterNumber = model.CostCenterNumber;
            entity.CostCenterName = model.CostCenterName;
            entity.MonthlyBudgetCordoba = model.MonthlyBudgetCordoba;
            entity.AlertThresholdPercent = model.AlertThresholdPercent;
            entity.HardStopOnOverrun = model.HardStopOnOverrun;
            entity.RequireAuthorizationOnOverrun = model.RequireAuthorizationOnOverrun;
            entity.IsActive = model.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Centro de costo #{entity.CostCenterNumber} actualizado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var entity = await _context.FinanceCostCenters.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.IsActive = !entity.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Centro de costo #{entity.CostCenterNumber} {(entity.IsActive ? "activado" : "desactivado")}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetMenuMetrics()
        {
            var now = DateTime.UtcNow;
            var startUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endExclusiveUtc = startUtc.AddMonths(1);

            var centers = await _context.FinanceCostCenters
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToListAsync();

            var spendEntries = await BuildTicketSpendEntriesAsync(startUtc, endExclusiveUtc);
            var areaCostCenterMap = BuildUniqueAreaCostCenterMap(centers);

            var near = 0;
            var over = 0;

            foreach (var center in centers)
            {
                decimal spent = 0m;
                foreach (var row in spendEntries.Where(x => IsEntryForCenter(x, center, areaCostCenterMap)))
                {
                    spent += row.AmountCordoba;
                }

                var budget = Math.Max(0m, center.MonthlyBudgetCordoba);
                var usage = budget > 0 ? (double)(spent / budget) * 100 : 0;
                if (usage >= 100)
                {
                    over++;
                }
                else if (usage >= center.AlertThresholdPercent)
                {
                    near++;
                }
            }

            return Json(new
            {
                nearThreshold = near,
                overBudget = over
            });
        }

        private static string NormalizePreset(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "month";
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is "month" or "week" or "fortnight" or "custom"
                ? normalized
                : "month";
        }

        private static string? NormalizeFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static (DateTime StartDate, DateTime EndDate) ResolveDateRange(string preset, DateTime? startDate, DateTime? endDate)
        {
            var today = DateTime.Today;
            return preset switch
            {
                "week" => (today.AddDays(-6), today),
                "fortnight" => today.Day <= 15
                    ? (new DateTime(today.Year, today.Month, 1), today)
                    : (new DateTime(today.Year, today.Month, 16), today),
                "custom" => ResolveCustomRange(startDate, endDate, today),
                _ => (new DateTime(today.Year, today.Month, 1), today)
            };
        }

        private static (DateTime StartDate, DateTime EndDate) ResolveCustomRange(DateTime? startDate, DateTime? endDate, DateTime today)
        {
            var start = (startDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var end = (endDate ?? today).Date;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return (start, end);
        }

        private static void NormalizeModel(FinanceCostCenterEditViewModel model)
        {
            model.Area = model.Area?.Trim() ?? string.Empty;
            model.CostCenterNumber = model.CostCenterNumber?.Trim() ?? string.Empty;
            model.CostCenterName = model.CostCenterName?.Trim() ?? string.Empty;
        }

        private async Task PopulateAreaOptionsAsync(string? selectedArea)
        {
            var baseAreas = new[]
            {
                "IT",
                "Operaciones",
                "Finanzas",
                "Mantenimiento",
                "Administracion",
                "Compras"
            };

            var departmentAreas = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Department))
                .Select(t => t.Department!.Trim())
                .Distinct()
                .ToListAsync();

            var configuredAreas = await _context.FinanceCostCenters
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.Area))
                .Select(x => x.Area.Trim())
                .Distinct()
                .ToListAsync();

            var areas = baseAreas
                .Concat(departmentAreas)
                .Concat(configuredAreas)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.Equals("Portal", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedArea) &&
                !selectedArea.Equals("Portal", StringComparison.OrdinalIgnoreCase) &&
                areas.All(x => !x.Equals(selectedArea, StringComparison.OrdinalIgnoreCase)))
            {
                areas.Insert(0, selectedArea);
            }

            ViewBag.AreaOptions = areas
                .Select(x => new SelectListItem(
                    x,
                    x,
                    !string.IsNullOrWhiteSpace(selectedArea) && x.Equals(selectedArea, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private async Task<string> GenerateCostCenterNumberAsync(string area, int? excludingId)
        {
            var normalizedArea = string.IsNullOrWhiteSpace(area) ? "General" : area.Trim();
            var areaToken = BuildAreaToken(normalizedArea);
            var prefix = $"CC-{areaToken}-";

            var existingNumbers = await _context.FinanceCostCenters
                .AsNoTracking()
                .Where(x =>
                    x.CostCenterNumber.StartsWith(prefix) &&
                    (!excludingId.HasValue || x.Id != excludingId.Value))
                .Select(x => x.CostCenterNumber)
                .ToListAsync();

            var maxSequence = 0;
            foreach (var existing in existingNumbers)
            {
                if (existing.Length <= prefix.Length)
                {
                    continue;
                }

                var suffix = existing[prefix.Length..];
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq) && seq > maxSequence)
                {
                    maxSequence = seq;
                }
            }

            return $"{prefix}{maxSequence + 1:000}";
        }

        private static string BuildAreaToken(string area)
        {
            var clean = (area ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return "GEN";
            }

            var words = clean
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var tokenBuilder = new StringBuilder();
            if (words.Count > 1)
            {
                foreach (var word in words)
                {
                    var first = word.FirstOrDefault(char.IsLetterOrDigit);
                    if (first != default)
                    {
                        tokenBuilder.Append(char.ToUpperInvariant(first));
                    }
                }
            }
            else
            {
                foreach (var c in clean)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        tokenBuilder.Append(char.ToUpperInvariant(c));
                    }
                }
            }

            var token = tokenBuilder.ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                return "GEN";
            }

            return token.Length > 6 ? token[..6] : token;
        }

        private async Task<bool> IsCostCenterNumberUniqueAsync(string number, int? excludingId)
        {
            var normalized = (number ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            return !await _context.FinanceCostCenters
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CostCenterNumber == normalized &&
                    (!excludingId.HasValue || x.Id != excludingId.Value));
        }

        private static bool IsSameCostCenter(string? rawCostCenter, string ccNumber, string ccName)
        {
            var raw = (rawCostCenter ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return raw.Equals(ccNumber, StringComparison.OrdinalIgnoreCase) ||
                   raw.Equals(ccName, StringComparison.OrdinalIgnoreCase) ||
                   raw.StartsWith($"{ccNumber} -", StringComparison.OrdinalIgnoreCase) ||
                   raw.Contains($"#{ccNumber}", StringComparison.OrdinalIgnoreCase);
        }

        private static FinanceMeta ParseFinanceMeta(string? description)
        {
            var meta = new FinanceMeta();
            if (string.IsNullOrWhiteSpace(description))
            {
                return meta;
            }

            var lines = description
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .ToList();

            var amountLine = ReadValue(lines, "Monto solicitado:");
            if (!string.IsNullOrWhiteSpace(amountLine))
            {
                meta.Currency = amountLine.StartsWith("USD", StringComparison.OrdinalIgnoreCase) ||
                                amountLine.StartsWith("$", StringComparison.OrdinalIgnoreCase)
                    ? "USD"
                    : "C$";
                meta.Amount = ParseAmount(amountLine);
            }

            meta.CostCenter = ReadValue(lines, "Centro de costo:");
            var risk = ReadValue(lines, "Riesgo presupuestario:");
            meta.HasBudgetRisk = risk.Equals("Sobregiro", StringComparison.OrdinalIgnoreCase);
            return meta;
        }

        private static string ReadValue(List<string> lines, string key)
        {
            var line = lines.FirstOrDefault(x => x.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            return line == null ? string.Empty : line.Substring(key.Length).Trim();
        }

        private static decimal ParseAmount(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0m;
            }

            var normalized = value
                .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("C$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                return amount;
            }

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
                ? amount
                : 0m;
        }

        private static decimal ConvertToCordoba(decimal amount, string? currency, decimal usdToCordobaRate)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currency, "$", StringComparison.OrdinalIgnoreCase))
            {
                return decimal.Round(amount * usdToCordobaRate, 2, MidpointRounding.AwayFromZero);
            }

            return amount;
        }

        private static bool IsEntryForCenter(TicketSpendEntry entry, FinanceCostCenter center, IReadOnlyDictionary<string, string> areaCostCenterMap)
        {
            if (!string.IsNullOrWhiteSpace(entry.CostCenterCode) &&
                IsSameCostCenter(entry.CostCenterCode, center.CostCenterNumber, center.CostCenterName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entry.CostCenterRaw) &&
                IsSameCostCenter(entry.CostCenterRaw, center.CostCenterNumber, center.CostCenterName))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(entry.Department))
            {
                return false;
            }

            if (!areaCostCenterMap.TryGetValue(entry.Department, out var mappedCostCenter))
            {
                return false;
            }

            return mappedCostCenter.Equals(center.CostCenterNumber, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> BuildUniqueAreaCostCenterMap(IEnumerable<FinanceCostCenter> centers)
        {
            return centers
                .Where(x => x.IsActive)
                .GroupBy(x => x.Area, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() == 1)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().CostCenterNumber,
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<TicketSpendEntry>> BuildTicketSpendEntriesAsync(DateTime startUtc, DateTime endExclusiveUtc)
        {
            var usdToCordobaRate = await GetUsdToCordobaRateAsync();
            var ticketRows = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.CreatedDate >= startUtc && t.CreatedDate < endExclusiveUtc)
                .Select(t => new TicketSpendSource
                {
                    Id = t.Id,
                    Department = t.Department,
                    CostCenterCode = t.CostCenterCode ?? string.Empty,
                    Description = t.Description,
                    Status = t.Status,
                    RequiresCostApproval = t.RequiresCostApproval,
                    CostApproved = t.CostApproved,
                    ChangedComponentCostCordoba = t.ChangedComponentCostCordoba,
                    ChangedComponentCostUsd = t.ChangedComponentCostUsd,
                    LaborCostCordoba = t.LaborCostCordoba,
                    LaborCostUsd = t.LaborCostUsd,
                    ExternalCostCordoba = t.ExternalCostCordoba,
                    ExternalCostUsd = t.ExternalCostUsd
                })
                .ToListAsync();

            if (ticketRows.Count == 0)
            {
                return new List<TicketSpendEntry>();
            }

            var ticketIds = ticketRows.Select(x => x.Id).ToList();
            var dispatchTotals = await _context.TicketSparePartDispatches
                .AsNoTracking()
                .Where(x => ticketIds.Contains(x.TicketId))
                .GroupBy(x => x.TicketId)
                .Select(g => new
                {
                    TicketId = g.Key,
                    TotalCordoba = g.Sum(x => x.TotalCostCordoba ?? 0m),
                    TotalUsd = g.Sum(x => x.TotalCostUsd ?? 0m)
                })
                .ToListAsync();

            var dispatchByTicket = dispatchTotals.ToDictionary(
                x => x.TicketId,
                x => x.TotalCordoba + ConvertToCordoba(x.TotalUsd, "USD", usdToCordobaRate));

            var results = new List<TicketSpendEntry>();
            foreach (var row in ticketRows)
            {
                if (row.Status == TicketStatus.Closed &&
                    row.RequiresCostApproval &&
                    !row.CostApproved)
                {
                    continue;
                }

                var meta = ParseFinanceMeta(row.Description);
                var financeAmount = ConvertToCordoba(meta.Amount, meta.Currency, usdToCordobaRate);
                var structuredAmount = CalculateStructuredAmountCordoba(row, dispatchByTicket, usdToCordobaRate);
                var totalAmount = financeAmount + structuredAmount;

                if (totalAmount <= 0)
                {
                    continue;
                }

                results.Add(new TicketSpendEntry
                {
                    Department = string.IsNullOrWhiteSpace(row.Department) ? string.Empty : row.Department.Trim(),
                    CostCenterCode = string.IsNullOrWhiteSpace(row.CostCenterCode) ? string.Empty : row.CostCenterCode.Trim(),
                    CostCenterRaw = string.IsNullOrWhiteSpace(meta.CostCenter) ? string.Empty : meta.CostCenter.Trim(),
                    AmountCordoba = totalAmount,
                    HasBudgetRisk = meta.HasBudgetRisk
                });
            }

            return results;
        }

        private static decimal CalculateStructuredAmountCordoba(TicketSpendSource row, IReadOnlyDictionary<int, decimal> dispatchByTicket, decimal usdToCordobaRate)
        {
            var componentCordoba = row.ChangedComponentCostCordoba ?? 0m;
            var componentUsdAsCordoba = ConvertToCordoba(row.ChangedComponentCostUsd ?? 0m, "USD", usdToCordobaRate);
            var laborCordoba = row.LaborCostCordoba ?? 0m;
            var laborUsdAsCordoba = ConvertToCordoba(row.LaborCostUsd ?? 0m, "USD", usdToCordobaRate);
            var externalCordoba = row.ExternalCostCordoba ?? 0m;
            var externalUsdAsCordoba = ConvertToCordoba(row.ExternalCostUsd ?? 0m, "USD", usdToCordobaRate);
            var dispatchCordoba = dispatchByTicket.TryGetValue(row.Id, out var dispatchTotal)
                ? dispatchTotal
                : 0m;

            return componentCordoba +
                   componentUsdAsCordoba +
                   laborCordoba +
                   laborUsdAsCordoba +
                   externalCordoba +
                   externalUsdAsCordoba +
                   dispatchCordoba;
        }

        private async Task<decimal> GetUsdToCordobaRateAsync()
        {
            var configured = await _context.FinanceSettings
                .AsNoTracking()
                .Select(x => (decimal?)x.UsdToCordobaRate)
                .FirstOrDefaultAsync();

            return configured.GetValueOrDefault(FinanceUsdToCordobaRateFallback);
        }

        private sealed class FinanceMeta
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "C$";
            public string CostCenter { get; set; } = string.Empty;
            public bool HasBudgetRisk { get; set; }
        }

        private sealed class TicketSpendSource
        {
            public int Id { get; set; }
            public string Department { get; set; } = string.Empty;
            public string CostCenterCode { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public TicketStatus Status { get; set; }
            public bool RequiresCostApproval { get; set; }
            public bool CostApproved { get; set; }
            public decimal? ChangedComponentCostCordoba { get; set; }
            public decimal? ChangedComponentCostUsd { get; set; }
            public decimal? LaborCostCordoba { get; set; }
            public decimal? LaborCostUsd { get; set; }
            public decimal? ExternalCostCordoba { get; set; }
            public decimal? ExternalCostUsd { get; set; }
        }

        private sealed class TicketSpendEntry
        {
            public string Department { get; set; } = string.Empty;
            public string CostCenterCode { get; set; } = string.Empty;
            public string CostCenterRaw { get; set; } = string.Empty;
            public decimal AmountCordoba { get; set; }
            public bool HasBudgetRisk { get; set; }
        }
    }
}
