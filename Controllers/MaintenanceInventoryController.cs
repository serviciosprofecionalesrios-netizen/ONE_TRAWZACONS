using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class MaintenanceInventoryController : Controller
    {
        private static readonly string[] CategoryOptions =
        {
            "Motor",
            "Transmision",
            "Frenos",
            "Suspension",
            "Electrico",
            "Hidraulico",
            "Neumatico",
            "Llantas",
            "Lubricantes",
            "Carroceria",
            "Seguridad",
            "Otro"
        };

        private static readonly string[] SystemTypeOptions =
        {
            "Mecanico",
            "Electrico",
            "Hidraulico",
            "Neumatico",
            "Carroceria"
        };

        private static readonly string[] EquipmentTypeOptions =
        {
            "Camion Freightliner Cascadia",
            "Camion Freightliner M2",
            "Gondola",
            "Volqueta",
            "Maquinaria Amarilla",
            "Equipo de taller",
            "Otro"
        };

        private static readonly string[] SiteOptions =
        {
            "ACT",
            "ASB"
        };

        private static readonly string[] TenenciaOptions =
        {
            "Propio",
            "Agregado"
        };

        private static readonly string[] UnitOfMeasureOptions =
        {
            "Unidad",
            "Juego",
            "Kit",
            "Litro",
            "Galon",
            "Metro",
            "Caja",
            "Par",
            "Otro"
        };

        private static readonly string[] StockStatusOptions =
        {
            "Disponible",
            "Bajo stock",
            "Agotado",
            "Reservado",
            "Descontinuado"
        };

        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MaintenanceInventoryController> _logger;
        private const string GoogleSheetUrlConfigKey = "MaintenanceInventorySync:GoogleSheetUrl";
        private const string InventoryMasterSheetName = "Inventario";
        private const string DescriptionPricingSheetName = "Descripcion Solicitud";

        public MaintenanceInventoryController(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<MaintenanceInventoryController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? category,
            string? systemType,
            string? stockStatus,
            string? site,
            bool includeInactive = false)
        {
            var query = BuildFilteredQuery(search, category, systemType, stockStatus, site, includeInactive);

            var rows = await query
                .OrderBy(x => x.StockStatus == "Agotado" ? 0 : x.StockStatus == "Bajo stock" ? 1 : 2)
                .ThenBy(x => x.Category)
                .ThenBy(x => x.PartName)
                .ToListAsync();
            rows = ConsolidateRowsForDisplay(rows);

            await PopulateOptionsAsync(category, systemType, stockStatus, site, includeInactive);
            ViewBag.Search = search;
            ViewBag.GoogleSheetUrl = _configuration[GoogleSheetUrlConfigKey] ?? string.Empty;
            SetInventorySummaryMetrics(rows);

            return View(rows);
        }

        public async Task<IActionResult> Detalle(
            string? search,
            string? category,
            string? systemType,
            string? stockStatus,
            string? site,
            bool includeInactive = false)
        {
            var query = BuildFilteredQuery(search, category, systemType, stockStatus, site, includeInactive);

            var rows = await query
                .OrderBy(x => x.StockStatus == "Agotado" ? 0 : x.StockStatus == "Bajo stock" ? 1 : 2)
                .ThenBy(x => x.Category)
                .ThenBy(x => x.PartName)
                .ToListAsync();
            rows = ConsolidateRowsForDisplay(rows);

            await PopulateOptionsAsync(category, systemType, stockStatus, site, includeInactive);
            ViewBag.Search = search;
            SetInventorySummaryMetrics(rows);

            return View(rows);
        }

        public async Task<IActionResult> Transito(
            string? search,
            string? category,
            string? systemType,
            string? site,
            bool includeInactive = false,
            bool delayedOnly = false)
        {
            var query = BuildFilteredQuery(search, category, systemType, null, site, includeInactive)
                .Where(x => x.QuantityInTransit > 0);

            var rows = await query
                .OrderByDescending(x => x.QuantityInTransit)
                .ThenBy(x => x.PartName)
                .ToListAsync();
            rows = ConsolidateRowsForDisplay(rows);

            if (delayedOnly)
            {
                var delayedThresholdDate = DateTime.UtcNow.Date.AddDays(-14);
                rows = rows
                    .Where(x => x.PurchaseDate.HasValue && x.PurchaseDate.Value.Date <= delayedThresholdDate)
                    .ToList();
            }

            await PopulateOptionsAsync(category, systemType, null, site, includeInactive);
            ViewBag.Search = search;
            ViewBag.DelayedOnly = delayedOnly;
            SetInventorySummaryMetrics(rows);

            var nowDate = DateTime.UtcNow.Date;
            ViewBag.TransitSkuCount = rows.Count;
            ViewBag.TransitUnits = rows.Sum(x => x.QuantityInTransit);
            ViewBag.TransitValueCordoba = rows.Sum(x => (x.UnitCostCordoba ?? 0m) * x.QuantityInTransit);
            ViewBag.TransitValueUsd = rows.Sum(x => (x.UnitCostUsd ?? 0m) * x.QuantityInTransit);
            ViewBag.TransitDelayedCount = rows.Count(x =>
                x.PurchaseDate.HasValue &&
                x.PurchaseDate.Value.Date.AddDays(14) < nowDate);
            ViewBag.TransitIncomingSoonCount = rows.Count(x =>
                x.PurchaseDate.HasValue &&
                x.PurchaseDate.Value.Date.AddDays(14) >= nowDate &&
                x.PurchaseDate.Value.Date.AddDays(14) <= nowDate.AddDays(3));

            return View(rows);
        }

        public async Task<IActionResult> Details(int id)
        {
            var row = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
            {
                return NotFound();
            }

            return View(row);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create()
        {
            var model = new MaintenanceInventoryPart
            {
                StockStatus = "Disponible",
                Site = "ACT",
                Tenencia = "Propio",
                UnitOfMeasure = "Unidad",
                QuantityOnHand = 0,
                MinimumStock = 0,
                QuantityIssued = 0,
                QuantityInTransit = 0,
                IsActive = true
            };

            await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, false);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create(MaintenanceInventoryPart model)
        {
            NormalizeModel(model);

            if (string.IsNullOrWhiteSpace(model.PartCode))
            {
                model.PartCode = await GeneratePartCodeAsync();
            }

            ValidateModelOptions(model);
            ApplyStockStatusByQuantity(model);

            if (await _context.MaintenanceInventoryParts.AnyAsync(x => x.PartCode == model.PartCode))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.PartCode), "El codigo del repuesto ya existe.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, model.IsActive == false);
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.MaintenanceInventoryParts.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _context.MaintenanceInventoryParts.FirstOrDefaultAsync(x => x.Id == id);
            if (model == null)
            {
                return NotFound();
            }

            await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, model.IsActive == false);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id, MaintenanceInventoryPart model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del registro.");
                await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, model.IsActive == false);
                return View(model);
            }

            var dbRow = await _context.MaintenanceInventoryParts.FirstOrDefaultAsync(x => x.Id == id);
            if (dbRow == null)
            {
                return NotFound();
            }

            NormalizeModel(model);
            ValidateModelOptions(model);
            ApplyStockStatusByQuantity(model);

            if (await _context.MaintenanceInventoryParts.AnyAsync(x => x.Id != model.Id && x.PartCode == model.PartCode))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.PartCode), "El codigo del repuesto ya existe.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, model.IsActive == false);
                return View(model);
            }

            _context.Entry(dbRow).Property(x => x.RowVersion).OriginalValue = model.RowVersion;

            dbRow.PartCode = model.PartCode;
            dbRow.PartName = model.PartName;
            dbRow.Category = model.Category;
            dbRow.Subcategory = model.Subcategory;
            dbRow.SystemType = model.SystemType;
            dbRow.EquipmentType = model.EquipmentType;
            dbRow.CompatibleModel = model.CompatibleModel;
            dbRow.Brand = model.Brand;
            dbRow.ManufacturerPartNumber = model.ManufacturerPartNumber;
            dbRow.ItemCode = model.ItemCode;
            dbRow.Site = model.Site;
            dbRow.Tenencia = model.Tenencia;
            dbRow.UnitOfMeasure = model.UnitOfMeasure;
            dbRow.QuantityOnHand = model.QuantityOnHand;
            dbRow.MinimumStock = model.MinimumStock;
            dbRow.QuantityIssued = model.QuantityIssued;
            dbRow.QuantityInTransit = model.QuantityInTransit;
            dbRow.LastIssueDate = model.LastIssueDate;
            dbRow.StockStatus = model.StockStatus;
            dbRow.Supplier = model.Supplier;
            dbRow.InvoiceNumber = model.InvoiceNumber;
            dbRow.PurchaseDate = model.PurchaseDate;
            dbRow.UnitCostCordoba = model.UnitCostCordoba;
            dbRow.UnitCostUsd = model.UnitCostUsd;
            dbRow.ShelfLocation = model.ShelfLocation;
            dbRow.Notes = model.Notes;
            dbRow.IsActive = model.IsActive;
            dbRow.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "El registro fue modificado por otro usuario.");
                model.RowVersion = dbRow.RowVersion;
                await PopulateOptionsAsync(model.Category, model.SystemType, model.StockStatus, model.Site, model.IsActive == false);
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Delete(int id)
        {
            var model = await _context.MaintenanceInventoryParts.FirstOrDefaultAsync(x => x.Id == id);
            if (model == null)
            {
                return NotFound();
            }

            model.IsActive = false;
            model.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> SyncFromGoogleSheet(string? sheetUrl)
        {
            var configuredSheetUrl = _configuration[GoogleSheetUrlConfigKey];
            var sourceUrl = string.IsNullOrWhiteSpace(sheetUrl) ? configuredSheetUrl : sheetUrl;

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                TempData["SyncError"] = "No hay URL de Google Sheet. Pegue una URL publica para sincronizar.";
                return RedirectToAction(nameof(Index));
            }

            var csvUrl = BuildGoogleSheetCsvUrl(sourceUrl);
            if (string.IsNullOrWhiteSpace(csvUrl))
            {
                TempData["SyncError"] = "La URL del Google Sheet no es valida.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(45);

                var inventoryCsvUrl = BuildGoogleSheetCsvUrlBySheetName(sourceUrl, InventoryMasterSheetName);
                List<List<string>> csvRows;
                if (!string.IsNullOrWhiteSpace(inventoryCsvUrl))
                {
                    using var inventoryResponse = await client.GetAsync(inventoryCsvUrl);
                    if (inventoryResponse.IsSuccessStatusCode)
                    {
                        var inventoryCsvContent = await inventoryResponse.Content.ReadAsStringAsync();
                        csvRows = ParseCsvRows(inventoryCsvContent);
                    }
                    else
                    {
                        using var fallbackResponse = await client.GetAsync(csvUrl);
                        if (!fallbackResponse.IsSuccessStatusCode)
                        {
                            TempData["SyncError"] = $"No se pudo leer el Google Sheet (HTTP {(int)fallbackResponse.StatusCode}). Verifique que sea publico.";
                            return RedirectToAction(nameof(Index));
                        }

                        var fallbackContent = await fallbackResponse.Content.ReadAsStringAsync();
                        csvRows = ParseCsvRows(fallbackContent);
                    }
                }
                else
                {
                    using var response = await client.GetAsync(csvUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["SyncError"] = $"No se pudo leer el Google Sheet (HTTP {(int)response.StatusCode}). Verifique que sea publico.";
                        return RedirectToAction(nameof(Index));
                    }

                    var csvContent = await response.Content.ReadAsStringAsync();
                    csvRows = ParseCsvRows(csvContent);
                }

                if (csvRows.Count <= 1)
                {
                    TempData["SyncError"] = "El Google Sheet no contiene filas de datos para importar.";
                    return RedirectToAction(nameof(Index));
                }

                var headerMap = BuildCsvHeaderMap(csvRows[0]);
                var existingRows = await _context.MaintenanceInventoryParts.ToListAsync();
                var existingByCode = existingRows.ToDictionary(x => x.PartCode, StringComparer.OrdinalIgnoreCase);
                var existingByItem = existingRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.ItemCode))
                    .GroupBy(x => NormalizeHeaderKey(x.ItemCode))
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(x => x.IsActive)
                            .ThenByDescending(x => x.UpdatedAt)
                            .ThenByDescending(x => x.Id)
                            .First(),
                        StringComparer.OrdinalIgnoreCase);
                var existingByArticle = existingRows
                    .GroupBy(BuildSyncArticleDedupKey, StringComparer.OrdinalIgnoreCase)
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(x => x.IsActive)
                            .ThenByDescending(x => IsAllowedSite(x.Site))
                            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ItemCode))
                            .ThenByDescending(x => GetItemCodeQualityScore(ResolveItemCode(x.PartCode, x.ItemCode, x.ManufacturerPartNumber, x.PartName)))
                            .ThenByDescending(x => x.QuantityOnHand + x.QuantityInTransit)
                            .ThenByDescending(x => x.UpdatedAt)
                            .ThenByDescending(x => x.Id)
                            .First(),
                        StringComparer.OrdinalIgnoreCase);
                var sheetCodesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sheetItemsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sheetArticlesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pricingLookup = await BuildPricingLookupFromSheetsAsync(client, sourceUrl);

                var now = DateTime.UtcNow;
                var baseYearPrefix = $"RMT-{now:yyyy}";
                var generatedCodeCounter = await _context.MaintenanceInventoryParts
                    .AsNoTracking()
                    .CountAsync(x => x.PartCode.StartsWith(baseYearPrefix));

                var added = 0;
                var updated = 0;
                var skipped = 0;
                var priceApplied = 0;
                var duplicateCostBackfill = 0;

                foreach (var row in csvRows.Skip(1))
                {
                    if (row.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    var parsed = BuildInventoryPartFromCsvRow(row, headerMap, now, ref generatedCodeCounter);
                    if (parsed == null)
                    {
                        skipped += 1;
                        continue;
                    }

                    if (TryApplyPricingLookup(parsed, pricingLookup))
                    {
                        priceApplied += 1;
                    }

                    var articleKey = BuildSyncArticleDedupKey(parsed);
                    if (!string.IsNullOrWhiteSpace(articleKey))
                    {
                        if (sheetArticlesSeen.Contains(articleKey))
                        {
                            skipped += 1;
                            continue;
                        }

                        sheetArticlesSeen.Add(articleKey);
                    }

                    var itemKey = NormalizeHeaderKey(parsed.ItemCode);
                    if (!string.IsNullOrWhiteSpace(itemKey))
                    {
                        if (sheetItemsSeen.Contains(itemKey))
                        {
                            skipped += 1;
                            continue;
                        }

                        sheetItemsSeen.Add(itemKey);
                    }

                    if (sheetCodesSeen.Contains(parsed.PartCode))
                    {
                        skipped += 1;
                        continue;
                    }

                    sheetCodesSeen.Add(parsed.PartCode);

                    if (existingByCode.TryGetValue(parsed.PartCode, out var dbRow))
                    {
                        UpdateMaintenanceInventoryPartFromImport(dbRow, parsed, now);
                        if (!string.IsNullOrWhiteSpace(itemKey))
                        {
                            existingByItem[itemKey] = dbRow;
                        }
                        if (!string.IsNullOrWhiteSpace(articleKey))
                        {
                            existingByArticle[articleKey] = dbRow;
                        }
                        updated += 1;
                    }
                    else if (!string.IsNullOrWhiteSpace(itemKey) && existingByItem.TryGetValue(itemKey, out var dbRowByItem))
                    {
                        UpdateMaintenanceInventoryPartFromImport(dbRowByItem, parsed, now);
                        existingByCode[dbRowByItem.PartCode] = dbRowByItem;
                        existingByItem[itemKey] = dbRowByItem;
                        if (!string.IsNullOrWhiteSpace(articleKey))
                        {
                            existingByArticle[articleKey] = dbRowByItem;
                        }
                        updated += 1;
                    }
                    else if (!string.IsNullOrWhiteSpace(articleKey) && existingByArticle.TryGetValue(articleKey, out var dbRowByArticle))
                    {
                        UpdateMaintenanceInventoryPartFromImport(dbRowByArticle, parsed, now);
                        existingByCode[dbRowByArticle.PartCode] = dbRowByArticle;
                        if (!string.IsNullOrWhiteSpace(itemKey))
                        {
                            existingByItem[itemKey] = dbRowByArticle;
                        }
                        existingByArticle[articleKey] = dbRowByArticle;
                        updated += 1;
                    }
                    else
                    {
                        parsed.CreatedAt = now;
                        parsed.UpdatedAt = now;
                        _context.MaintenanceInventoryParts.Add(parsed);
                        existingByCode[parsed.PartCode] = parsed;
                        if (!string.IsNullOrWhiteSpace(itemKey))
                        {
                            existingByItem[itemKey] = parsed;
                        }
                        if (!string.IsNullOrWhiteSpace(articleKey))
                        {
                            existingByArticle[articleKey] = parsed;
                        }
                        added += 1;
                    }
                }

                duplicateCostBackfill = BackfillMissingCostsAcrossSimilarParts(existingByCode.Values);
                await _context.SaveChangesAsync();

                TempData["SyncSuccess"] = $"Sincronizacion completada. Nuevos: {added}, actualizados: {updated}, omitidos: {skipped}, precios aplicados: {priceApplied}, costos ajustados por duplicados: {duplicateCostBackfill}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sincronizando inventario de repuestos desde Google Sheet.");
                TempData["SyncError"] = "No se pudo sincronizar desde Google Sheet. Revise la URL o permisos del archivo.";
            }

            return RedirectToAction(nameof(Index));
        }

        private static string? BuildGoogleSheetCsvUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return null;
            }

            var source = rawUrl.Trim();
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var host = uri.Host?.ToLowerInvariant() ?? string.Empty;
            if (!host.Contains("docs.google.com"))
            {
                return source;
            }

            if (uri.AbsolutePath.Contains("/export", StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }

            var match = Regex.Match(uri.AbsolutePath, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var sheetId = match.Groups[1].Value;
            var gidMatch = Regex.Match(source, @"(?:[?#&]gid=)(\d+)", RegexOptions.IgnoreCase);
            var gidPart = gidMatch.Success ? $"&gid={gidMatch.Groups[1].Value}" : string.Empty;

            return $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv{gidPart}";
        }

        private MaintenanceInventoryPart? BuildInventoryPartFromCsvRow(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            DateTime now,
            ref int generatedCodeCounter)
        {
            var partCode = ReadCsvString(
                row,
                headerMap,
                "partcode",
                "codigo",
                "codigorepuesto",
                "code",
                "item",
                "codigoitem",
                "itemcode");
            var partName = ReadCsvString(row, headerMap, "partname", "repuesto", "nombre", "descripcion");

            if (string.IsNullOrWhiteSpace(partCode) && string.IsNullOrWhiteSpace(partName))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(partCode))
            {
                generatedCodeCounter += 1;
                partCode = $"RMT-{now:yyyy}-{generatedCodeCounter:D4}";
            }

            var itemCode = ReadCsvString(
                row,
                headerMap,
                "item",
                "codigoitem",
                "itemcode");

            var partNumber = ReadCsvString(
                row,
                headerMap,
                "numerodeparte",
                "numeroparte",
                "nparte",
                "noparte",
                "partnumber",
                "manufacturerpartnumber");
            var resolvedItemCode = ResolveItemCode(partCode, itemCode, partNumber, partName);

            var model = new MaintenanceInventoryPart
            {
                PartCode = Truncate(partCode, 30),
                PartName = Truncate(string.IsNullOrWhiteSpace(partName) ? partCode : partName, 180),
                Category = MatchOption(CategoryOptions, ReadCsvString(row, headerMap, "categoria", "category"), "Otro"),
                Subcategory = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "subcategoria", "subcategory"), 120)),
                SystemType = MatchOption(SystemTypeOptions, ReadCsvString(row, headerMap, "sistema", "tiposistema", "systemtype"), "Mecanico"),
                EquipmentType = MatchOption(EquipmentTypeOptions, ReadCsvString(row, headerMap, "equipo", "tipodeequipo", "equipmenttype"), "Otro"),
                CompatibleModel = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "modelocompatible", "compatiblemodel"), 100)),
                Brand = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "marca", "brand"), 120)),
                ManufacturerPartNumber = NullIfWhiteSpace(Truncate(partNumber, 120)),
                ItemCode = NullIfWhiteSpace(Truncate(resolvedItemCode, 80)),
                Site = NormalizeSiteValue(ReadCsvString(row, headerMap, "sitio", "site", "ubicacion")),
                Tenencia = MatchOption(TenenciaOptions, ReadCsvString(row, headerMap, "tenencia"), TenenciaOptions[0]),
                UnitOfMeasure = MatchOption(UnitOfMeasureOptions, ReadCsvString(row, headerMap, "unidad", "unidadmedida", "unitofmeasure", "uom"), "Unidad"),
                QuantityOnHand = ReadCsvInt(row, headerMap, 0, "stock", "stockactual", "cantidad", "quantityonhand"),
                MinimumStock = ReadCsvInt(row, headerMap, 0, "stockminimo", "minimumstock", "minimo"),
                QuantityIssued = ReadCsvInt(row, headerMap, 0, "salidas", "quantityissued", "consumo"),
                QuantityInTransit = ReadCsvInt(row, headerMap, 0, "transito", "entransito", "quantityintransit"),
                LastIssueDate = ReadCsvDate(row, headerMap, "ultimasalida", "lastissuedate"),
                StockStatus = MatchOption(StockStatusOptions, ReadCsvString(row, headerMap, "estadostock", "estado", "stockstatus"), "Disponible"),
                Supplier = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "proveedor", "supplier"), 120)),
                InvoiceNumber = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "factura", "invoicenumber"), 80)),
                PurchaseDate = ReadCsvDate(row, headerMap, "fechacompra", "purchasedate"),
                UnitCostCordoba = ReadCsvDecimal(
                    row,
                    headerMap,
                    "costocordoba",
                    "costoc",
                    "unitc",
                    "unitcordoba",
                    "unitcostcordoba",
                    "preciocordoba",
                    "precioc$",
                    "precioencordoba",
                    "preciocordobas",
                    "precioenccordobas",
                    "precio",
                    "costounitario",
                    "costounitarioc",
                    "costounitariocordoba",
                    "costounitariocordobas",
                    "preciounitario",
                    "preciounitarioc",
                    "preciounitariocordoba",
                    "preciounitariocordobas",
                    "valorunitario",
                    "puc",
                    "pu c$",
                    "p/u c$",
                    "precioc",
                    "valorc$",
                    "valorc",
                    "montocordoba",
                    "montoc$",
                    "montoc"),
                UnitCostUsd = ReadCsvDecimal(
                    row,
                    headerMap,
                    "costousd",
                    "costodolar",
                    "unitusd",
                    "unitd",
                    "unitdolar",
                    "unitcostusd",
                    "preciousd",
                    "preciodolar",
                    "precio$",
                    "precioendolar",
                    "precioendolares",
                    "costounitariousd",
                    "costounitariodolar",
                    "costounitariodolares",
                    "preciounitariousd",
                    "preciounitariodolar",
                    "preciounitariodolares",
                    "valorunitariousd",
                    "valorunitariodolar",
                    "valorunitariodolares",
                    "puusd",
                    "pu $",
                    "p/u $",
                    "pu$",
                    "pus",
                    "preciod",
                    "valorusd",
                    "valor$",
                    "montousd",
                    "monto$"),
                ShelfLocation = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "ubicacionestante", "shelflocation", "estante"), 80)),
                Notes = NullIfWhiteSpace(Truncate(ReadCsvString(row, headerMap, "notas", "observaciones", "notes"), 1000)),
                IsActive = ReadCsvBool(row, headerMap, true, "activo", "isactive")
            };

            NormalizeModel(model);
            ApplyStockStatusByQuantity(model);
            return model;
        }

        private static void UpdateMaintenanceInventoryPartFromImport(
            MaintenanceInventoryPart target,
            MaintenanceInventoryPart source,
            DateTime now)
        {
            target.PartName = source.PartName;
            target.Category = source.Category;
            target.Subcategory = source.Subcategory;
            target.SystemType = source.SystemType;
            target.EquipmentType = source.EquipmentType;
            target.CompatibleModel = source.CompatibleModel;
            target.Brand = source.Brand;
            target.ManufacturerPartNumber = source.ManufacturerPartNumber;
            target.ItemCode = source.ItemCode;
            target.Site = source.Site;
            target.Tenencia = source.Tenencia;
            target.UnitOfMeasure = source.UnitOfMeasure;
            target.QuantityOnHand = source.QuantityOnHand;
            target.MinimumStock = source.MinimumStock;
            target.QuantityIssued = source.QuantityIssued;
            target.QuantityInTransit = source.QuantityInTransit;
            target.LastIssueDate = source.LastIssueDate;
            target.StockStatus = source.StockStatus;
            target.Supplier = source.Supplier;
            target.InvoiceNumber = source.InvoiceNumber;
            target.PurchaseDate = source.PurchaseDate;
            target.UnitCostCordoba = ChooseBestPrice(target.UnitCostCordoba, source.UnitCostCordoba);
            target.UnitCostUsd = ChooseBestPrice(target.UnitCostUsd, source.UnitCostUsd);
            target.ShelfLocation = source.ShelfLocation;
            target.Notes = source.Notes;
            target.IsActive = source.IsActive;
            target.UpdatedAt = now;
        }

        private static Dictionary<string, int> BuildCsvHeaderMap(IReadOnlyList<string> headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headerRow.Count; i += 1)
            {
                var key = NormalizeHeaderKey(headerRow[i]);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!map.ContainsKey(key))
                {
                    map[key] = i;
                }
            }

            return map;
        }

        private static string ReadCsvString(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                var key = NormalizeHeaderKey(alias);
                if (headerMap.TryGetValue(key, out var index) && index >= 0 && index < row.Count)
                {
                    return (row[index] ?? string.Empty).Trim();
                }
            }

            return string.Empty;
        }

        private static int ReadCsvInt(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            int defaultValue,
            params string[] aliases)
        {
            var raw = ReadCsvString(row, headerMap, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return Math.Max(0, parsed);
            }

            if (TryParseDecimalFlexible(raw, out var decimalValue))
            {
                return Math.Max(0, (int)Math.Round(decimalValue, MidpointRounding.AwayFromZero));
            }

            return defaultValue;
        }

        private static decimal? ReadCsvDecimal(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            params string[] aliases)
        {
            var raw = ReadCsvString(row, headerMap, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (TryParseDecimalFlexible(raw, out var value))
            {
                return value < 0 ? 0 : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            }

            return null;
        }

        private static DateTime? ReadCsvDate(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            params string[] aliases)
        {
            var raw = ReadCsvString(row, headerMap, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var formats = new[]
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "MM/dd/yyyy",
                "M/d/yyyy",
                "yyyy-MM-dd",
                "dd-MM-yyyy"
            };

            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
            {
                return exactDate;
            }

            if (DateTime.TryParse(raw, new CultureInfo("es-NI"), DateTimeStyles.None, out var parsedEs))
            {
                return parsedEs;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedInvariant))
            {
                return parsedInvariant;
            }

            return null;
        }

        private static bool ReadCsvBool(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> headerMap,
            bool defaultValue,
            params string[] aliases)
        {
            var raw = NormalizeHeaderKey(ReadCsvString(row, headerMap, aliases));
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (raw is "1" or "true" or "si" or "yes" or "activo")
            {
                return true;
            }

            if (raw is "0" or "false" or "no" or "inactivo")
            {
                return false;
            }

            return defaultValue;
        }

        private static string MatchOption(IEnumerable<string> options, string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var candidate = value.Trim();
            var exact = options.FirstOrDefault(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                return exact;
            }

            var normalizedCandidate = NormalizeHeaderKey(candidate);
            var normalized = options.FirstOrDefault(x => NormalizeHeaderKey(x) == normalizedCandidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            if (normalizedCandidate is "bajo" or "bajostock" or "alerta")
            {
                return "Bajo stock";
            }

            if (normalizedCandidate is "agotado" or "sinstock" or "outofstock")
            {
                return "Agotado";
            }

            if (normalizedCandidate is "disponible" or "ok" or "stockok")
            {
                return "Disponible";
            }

            if (normalizedCandidate is "gln" or "gal" or "galon")
            {
                return "Galon";
            }

            if (normalizedCandidate is "lt" or "ltr" or "litro")
            {
                return "Litro";
            }

            if (normalizedCandidate is "arrendado" or "tercero")
            {
                return "Agregado";
            }

            return fallback;
        }

        private static string NormalizeSiteValue(string? value)
        {
            var normalized = NormalizeHeaderKey(value);
            if (normalized.Contains("act", StringComparison.OrdinalIgnoreCase))
            {
                return "ACT";
            }

            if (normalized.Contains("asb", StringComparison.OrdinalIgnoreCase))
            {
                return "ASB";
            }

            return SiteOptions[0];
        }

        private static bool IsAllowedSite(string? value)
        {
            return SiteOptions.Contains((value ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveItemCode(
            string? partCode,
            string? itemCode,
            string? partNumber,
            string? partName)
        {
            var explicitItem = ExtractLikelyItemCode(itemCode);
            var fromPartCode = ExtractLikelyItemCode(partCode);
            var fromPartNumber = ExtractLikelyItemCode(partNumber);

            return PickBestItemCode(explicitItem, fromPartNumber, fromPartCode);
        }

        private static string PickBestItemCode(params string?[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return string.Empty;
            }

            var best = string.Empty;
            var bestScore = -1;

            foreach (var raw in candidates)
            {
                var normalized = ExtractLikelyItemCode(raw);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                var score = GetItemCodeQualityScore(normalized);
                if (score > bestScore)
                {
                    best = normalized;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int GetItemCodeQualityScore(string? itemCode)
        {
            var item = (itemCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(item))
            {
                return 0;
            }

            var score = 0;
            if (item.StartsWith("101", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            if (item.Length >= 10)
            {
                score += 40;
            }
            else if (item.Length >= 9)
            {
                score += 30;
            }
            else if (item.Length >= 8)
            {
                score += 20;
            }
            else if (item.Length >= 6)
            {
                score += 10;
            }

            if (item.All(char.IsDigit))
            {
                score += 15;
            }

            return score;
        }

        private static string ExtractLikelyItemCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var candidate = raw.Trim();
            if (candidate.Length >= 6 && candidate.All(char.IsDigit))
            {
                return candidate;
            }

            var digitsOnly = new string(candidate.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length >= 6)
            {
                return digitsOnly;
            }

            return string.Empty;
        }

        private static bool TryParseDecimalFlexible(string raw, out decimal value)
        {
            var normalized = (raw ?? string.Empty)
                .Replace("C$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("US$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("es-NI"), out value))
            {
                return true;
            }

            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("es-ES"), out value))
            {
                return true;
            }

            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("en-US"), out value))
            {
                return true;
            }

            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 0m;
            return false;
        }

        private static List<List<string>> ParseCsvRows(string csvContent)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < csvContent.Length; i += 1)
            {
                var current = csvContent[i];

                if (inQuotes)
                {
                    if (current == '"')
                    {
                        var hasEscapedQuote = i + 1 < csvContent.Length && csvContent[i + 1] == '"';
                        if (hasEscapedQuote)
                        {
                            cell.Append('"');
                            i += 1;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(current);
                    }

                    continue;
                }

                if (current == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (current == ',')
                {
                    row.Add(cell.ToString().Trim());
                    cell.Clear();
                    continue;
                }

                if (current == '\r')
                {
                    continue;
                }

                if (current == '\n')
                {
                    row.Add(cell.ToString().Trim());
                    cell.Clear();

                    if (row.Count > 0)
                    {
                        rows.Add(row);
                    }

                    row = new List<string>();
                    continue;
                }

                cell.Append(current);
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString().Trim());
                rows.Add(row);
            }

            return rows;
        }

        private static string NormalizeHeaderKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value
                .Trim()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
            }

            return builder.ToString();
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Truncate(string? value, int maxLength)
        {
            var safe = (value ?? string.Empty).Trim();
            if (safe.Length <= maxLength)
            {
                return safe;
            }

            return safe[..maxLength];
        }

        private async Task<Dictionary<string, (decimal? Cordoba, decimal? Usd)>> BuildPricingLookupFromSheetsAsync(
            HttpClient client,
            string sourceUrl)
        {
            var fromInventory = await BuildSheetPricingLookupAsync(client, sourceUrl, InventoryMasterSheetName);
            var fromDescription = await BuildSheetPricingLookupAsync(client, sourceUrl, DescriptionPricingSheetName);

            var merged = new Dictionary<string, (decimal? Cordoba, decimal? Usd)>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in fromDescription)
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var pair in fromInventory)
            {
                if (!merged.TryGetValue(pair.Key, out var current))
                {
                    merged[pair.Key] = pair.Value;
                    continue;
                }

                merged[pair.Key] = (
                    ChooseBestPrice(current.Cordoba, pair.Value.Cordoba),
                    ChooseBestPrice(current.Usd, pair.Value.Usd));
            }

            return merged;
        }

        private async Task<Dictionary<string, (decimal? Cordoba, decimal? Usd)>> BuildSheetPricingLookupAsync(
            HttpClient client,
            string sourceUrl,
            string sheetName)
        {
            var lookup = new Dictionary<string, (decimal? Cordoba, decimal? Usd)>(StringComparer.OrdinalIgnoreCase);
            var csvUrl = BuildGoogleSheetCsvUrlBySheetName(sourceUrl, sheetName);
            if (string.IsNullOrWhiteSpace(csvUrl))
            {
                return lookup;
            }

            try
            {
                using var response = await client.GetAsync(csvUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return lookup;
                }

                var csvContent = await response.Content.ReadAsStringAsync();
                var csvRows = ParseCsvRows(csvContent);
                if (csvRows.Count <= 1)
                {
                    return lookup;
                }

                var headerRow = csvRows[0];
                var headerMap = BuildCsvHeaderMap(csvRows[0]);
                foreach (var row in csvRows.Skip(1))
                {
                    if (row.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    var unitCostCordoba = ReadCsvDecimal(
                        row,
                        headerMap,
                        "costocordoba",
                        "costoc",
                        "unitc",
                        "unitcordoba",
                        "unitcostcordoba",
                        "preciocordoba",
                        "precioc$",
                        "precioencordoba",
                        "preciocordobas",
                        "precioenccordobas",
                        "precio",
                        "costounitario",
                        "costounitarioc",
                        "costounitariocordoba",
                        "costounitariocordobas",
                        "preciounitario",
                        "preciounitarioc",
                        "preciounitariocordoba",
                        "preciounitariocordobas",
                        "valorunitario",
                        "puc",
                        "pu c$",
                        "p/u c$",
                        "valorc$",
                        "valorc",
                        "montocordoba",
                        "montoc$",
                        "montoc");

                    var unitCostUsd = ReadCsvDecimal(
                        row,
                        headerMap,
                        "costousd",
                        "costodolar",
                        "unitusd",
                        "unitd",
                        "unitdolar",
                        "unitcostusd",
                        "preciousd",
                        "preciodolar",
                        "precio$",
                        "precioendolar",
                        "precioendolares",
                        "costounitariousd",
                        "costounitariodolar",
                        "costounitariodolares",
                        "preciounitariousd",
                        "preciounitariodolar",
                        "preciounitariodolares",
                        "valorunitariousd",
                        "valorunitariodolar",
                        "valorunitariodolares",
                        "puusd",
                        "pu $",
                        "p/u $",
                        "pu$",
                        "pus",
                        "valorusd",
                        "valor$",
                        "montousd",
                        "monto$");

                    unitCostCordoba ??= ReadCsvDecimalByHeuristicCurrency(row, headerRow, preferCordoba: true);
                    unitCostUsd ??= ReadCsvDecimalByHeuristicCurrency(row, headerRow, preferCordoba: false);

                    if (!HasUsablePrice(unitCostCordoba) && !HasUsablePrice(unitCostUsd))
                    {
                        continue;
                    }

                    var description = ReadCsvString(
                        row,
                        headerMap,
                        "descripcion",
                        "descripcionsolicitud",
                        "articulo",
                        "repuesto",
                        "nombre");

                    var descriptionExtra = ReadCsvString(
                        row,
                        headerMap,
                        "descripcionextra",
                        "detalle",
                        "descripcionalterna",
                        "descripcionadicional");

                    var item = ReadCsvString(
                        row,
                        headerMap,
                        "item",
                        "codigoitem",
                        "itemcode");

                    var partNumber = ReadCsvString(
                        row,
                        headerMap,
                        "numeroparte",
                        "numerodeparte",
                        "nparte",
                        "noparte",
                        "partnumber");

                    UpsertPriceLookup(lookup, description, unitCostCordoba, unitCostUsd);
                    UpsertPriceLookup(lookup, descriptionExtra, unitCostCordoba, unitCostUsd);
                    UpsertPriceLookup(lookup, item, unitCostCordoba, unitCostUsd);
                    UpsertPriceLookup(lookup, partNumber, unitCostCordoba, unitCostUsd);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo construir el lookup de precios desde la hoja {SheetName}.", sheetName);
            }

            return lookup;
        }

        private static void UpsertPriceLookup(
            IDictionary<string, (decimal? Cordoba, decimal? Usd)> lookup,
            string? rawKey,
            decimal? unitCostCordoba,
            decimal? unitCostUsd)
        {
            var key = NormalizeHeaderKey(rawKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!lookup.TryGetValue(key, out var current))
            {
                lookup[key] = (unitCostCordoba, unitCostUsd);
                return;
            }

            lookup[key] = (
                ChooseBestPrice(current.Cordoba, unitCostCordoba),
                ChooseBestPrice(current.Usd, unitCostUsd));
        }

        private static bool TryApplyPricingLookup(
            MaintenanceInventoryPart model,
            IReadOnlyDictionary<string, (decimal? Cordoba, decimal? Usd)> lookup)
        {
            if (lookup.Count == 0)
            {
                return false;
            }

            var match = FindPriceLookupMatch(
                lookup,
                model.PartName,
                model.ItemCode,
                model.ManufacturerPartNumber,
                model.PartCode);

            if (!match.HasValue)
            {
                return false;
            }

            var changed = false;
            if (!HasUsablePrice(model.UnitCostCordoba) && HasUsablePrice(match.Value.Cordoba))
            {
                model.UnitCostCordoba = match.Value.Cordoba;
                changed = true;
            }

            if (!HasUsablePrice(model.UnitCostUsd) && HasUsablePrice(match.Value.Usd))
            {
                model.UnitCostUsd = match.Value.Usd;
                changed = true;
            }

            return changed;
        }

        private static (decimal? Cordoba, decimal? Usd)? FindPriceLookupMatch(
            IReadOnlyDictionary<string, (decimal? Cordoba, decimal? Usd)> lookup,
            params string?[] keys)
        {
            var normalizedKeys = keys
                .Select(NormalizeHeaderKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length)
                .ToList();

            foreach (var key in normalizedKeys)
            {
                if (lookup.TryGetValue(key, out var exact))
                {
                    return exact;
                }
            }

            foreach (var key in normalizedKeys)
            {
                if (key.Length < 6)
                {
                    continue;
                }

                foreach (var pair in lookup)
                {
                    var candidate = pair.Key;
                    if (candidate.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                        key.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }

            return null;
        }

        private static decimal? ChooseBestPrice(decimal? current, decimal? incoming)
        {
            if (HasUsablePrice(incoming))
            {
                return decimal.Round(incoming!.Value, 2, MidpointRounding.AwayFromZero);
            }

            if (HasUsablePrice(current))
            {
                return decimal.Round(current!.Value, 2, MidpointRounding.AwayFromZero);
            }

            return incoming ?? current;
        }

        private static bool HasUsablePrice(decimal? value)
        {
            return value.HasValue && value.Value > 0m;
        }

        private static decimal? ReadCsvDecimalByHeuristicCurrency(
            IReadOnlyList<string> row,
            IReadOnlyList<string> headerRow,
            bool preferCordoba)
        {
            if (row.Count == 0 || headerRow.Count == 0)
            {
                return null;
            }

            var limit = Math.Min(row.Count, headerRow.Count);
            for (var i = 0; i < limit; i += 1)
            {
                var rawHeader = (headerRow[i] ?? string.Empty).Trim();
                if (!LooksLikePriceHeader(rawHeader, preferCordoba))
                {
                    continue;
                }

                if (!TryParseDecimalFlexible(row[i], out var parsed))
                {
                    continue;
                }

                if (parsed <= 0)
                {
                    continue;
                }

                return decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
            }

            return null;
        }

        private static bool LooksLikePriceHeader(string rawHeader, bool preferCordoba)
        {
            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                return false;
            }

            var raw = rawHeader.ToUpperInvariant();
            var norm = NormalizeHeaderKey(rawHeader);
            var looksLikePriceField =
                norm.Contains("precio", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("costo", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("valor", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("monto", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("unit", StringComparison.OrdinalIgnoreCase) ||
                norm.StartsWith("pu", StringComparison.OrdinalIgnoreCase);

            if (!looksLikePriceField)
            {
                return false;
            }

            if (preferCordoba)
            {
                return raw.Contains("C$") ||
                       norm.Contains("cordoba", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("nio", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("precioc", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("costoc", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("valorc", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("montoc", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("unitc", StringComparison.OrdinalIgnoreCase) ||
                       norm.Contains("puc", StringComparison.OrdinalIgnoreCase);
            }

            return raw.Contains("USD", StringComparison.OrdinalIgnoreCase) ||
                   raw.Contains("US$", StringComparison.OrdinalIgnoreCase) ||
                   raw.Contains(" D$", StringComparison.OrdinalIgnoreCase) ||
                   (raw.Contains("$", StringComparison.OrdinalIgnoreCase) && !raw.Contains("C$", StringComparison.OrdinalIgnoreCase)) ||
                   norm.Contains("usd", StringComparison.OrdinalIgnoreCase) ||
                   norm.Contains("dolar", StringComparison.OrdinalIgnoreCase) ||
                   norm.Contains("unitd", StringComparison.OrdinalIgnoreCase) ||
                   norm.Contains("unitusd", StringComparison.OrdinalIgnoreCase) ||
                   norm.Contains("pus", StringComparison.OrdinalIgnoreCase) ||
                   norm.Contains("monto", StringComparison.OrdinalIgnoreCase) && norm.Contains("d", StringComparison.OrdinalIgnoreCase);
        }

        private static string? BuildGoogleSheetCsvUrlBySheetName(string rawUrl, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(rawUrl) || string.IsNullOrWhiteSpace(sheetName))
            {
                return null;
            }

            var sheetId = TryExtractGoogleSheetId(rawUrl);
            if (string.IsNullOrWhiteSpace(sheetId))
            {
                return null;
            }

            return $"https://docs.google.com/spreadsheets/d/{sheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(sheetName)}";
        }

        private static string? TryExtractGoogleSheetId(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return null;
            }

            var match = Regex.Match(rawUrl, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Value;
        }

        private static int BackfillMissingCostsAcrossSimilarParts(IEnumerable<MaintenanceInventoryPart> rows)
        {
            var list = rows?
                .Where(x => x != null)
                .ToList() ?? new List<MaintenanceInventoryPart>();

            if (list.Count == 0)
            {
                return 0;
            }

            var updatedCount = 0;

            updatedCount += ApplyBackfillByKey(list, BuildSimilarPartCostKey);
            updatedCount += ApplyBackfillByKey(list, BuildRelaxedPartCostKey);

            return updatedCount;
        }

        private static int ApplyBackfillByKey(
            IReadOnlyCollection<MaintenanceInventoryPart> list,
            Func<MaintenanceInventoryPart, string> keySelector)
        {
            var updatedCount = 0;
            var groups = list
                .GroupBy(keySelector)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToList();

            foreach (var group in groups)
            {
                var donorCordoba = group
                    .Where(x => HasUsablePrice(x.UnitCostCordoba))
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => x.UnitCostCordoba)
                    .FirstOrDefault();

                var donorUsd = group
                    .Where(x => HasUsablePrice(x.UnitCostUsd))
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => x.UnitCostUsd)
                    .FirstOrDefault();

                if (!HasUsablePrice(donorCordoba) && !HasUsablePrice(donorUsd))
                {
                    continue;
                }

                foreach (var row in group)
                {
                    var changed = false;
                    if (!HasUsablePrice(row.UnitCostCordoba) && HasUsablePrice(donorCordoba))
                    {
                        row.UnitCostCordoba = donorCordoba;
                        changed = true;
                    }

                    if (!HasUsablePrice(row.UnitCostUsd) && HasUsablePrice(donorUsd))
                    {
                        row.UnitCostUsd = donorUsd;
                        changed = true;
                    }

                    if (!changed)
                    {
                        continue;
                    }

                    row.UpdatedAt = DateTime.UtcNow;
                    updatedCount += 1;
                }
            }

            return updatedCount;
        }

        private static string BuildSimilarPartCostKey(MaintenanceInventoryPart row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var partName = NormalizeHeaderKey(row.PartName);
            var item = NormalizeHeaderKey(string.IsNullOrWhiteSpace(row.ItemCode)
                ? row.ManufacturerPartNumber
                : row.ItemCode);
            var site = NormalizeHeaderKey(row.Site);
            var tenencia = NormalizeHeaderKey(row.Tenencia);

            if (string.IsNullOrWhiteSpace(partName) && string.IsNullOrWhiteSpace(item))
            {
                return string.Empty;
            }

            return $"{partName}|{item}|{site}|{tenencia}";
        }

        private static string BuildRelaxedPartCostKey(MaintenanceInventoryPart row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var partName = NormalizeHeaderKey(row.PartName);
            var site = NormalizeHeaderKey(row.Site);
            var tenencia = NormalizeHeaderKey(row.Tenencia);

            if (string.IsNullOrWhiteSpace(partName))
            {
                return string.Empty;
            }

            return $"{partName}|{site}|{tenencia}";
        }

        private static string BuildSyncArticleDedupKey(MaintenanceInventoryPart row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var partName = NormalizeHeaderKey(row.PartName);
            var site = NormalizeHeaderKey(NormalizeSiteValue(row.Site));
            if (!string.IsNullOrWhiteSpace(partName))
            {
                return $"name|{partName}|site|{site}";
            }

            var item = NormalizeHeaderKey(ResolveItemCode(row.PartCode, row.ItemCode, row.ManufacturerPartNumber, row.PartName));
            if (!string.IsNullOrWhiteSpace(item))
            {
                return $"item|{item}|site|{site}";
            }

            return string.Empty;
        }

        private IQueryable<MaintenanceInventoryPart> BuildFilteredQuery(
            string? search,
            string? category,
            string? systemType,
            string? stockStatus,
            string? site,
            bool includeInactive)
        {
            var query = _context.MaintenanceInventoryParts.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x =>
                    x.PartCode.Contains(term) ||
                    x.PartName.Contains(term) ||
                    (x.ItemCode != null && x.ItemCode.Contains(term)) ||
                    (x.ManufacturerPartNumber != null && x.ManufacturerPartNumber.Contains(term)) ||
                    (x.Brand != null && x.Brand.Contains(term)) ||
                    (x.CompatibleModel != null && x.CompatibleModel.Contains(term)) ||
                    (x.Site != null && x.Site.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => x.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(systemType))
            {
                query = query.Where(x => x.SystemType == systemType);
            }

            if (!string.IsNullOrWhiteSpace(stockStatus))
            {
                query = query.Where(x => x.StockStatus == stockStatus);
            }

            if (!string.IsNullOrWhiteSpace(site))
            {
                query = query.Where(x => x.Site == site);
            }

            return query;
        }

        private static List<MaintenanceInventoryPart> ConsolidateRowsForDisplay(IEnumerable<MaintenanceInventoryPart> rows)
        {
            var source = rows?.ToList() ?? new List<MaintenanceInventoryPart>();
            if (source.Count <= 1)
            {
                foreach (var row in source)
                {
                    row.Site = NormalizeSiteValue(row.Site);
                }

                return source;
            }

            var consolidated = source
                .GroupBy(BuildInventoryDisplayDedupKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => IsAllowedSite(x.Site))
                    .ThenByDescending(x => GetItemCodeQualityScore(ResolveItemCode(x.PartCode, x.ItemCode, x.ManufacturerPartNumber, x.PartName)))
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(ResolveItemCode(x.PartCode, x.ItemCode, x.ManufacturerPartNumber, x.PartName)))
                    .ThenByDescending(x => x.QuantityOnHand + x.QuantityInTransit)
                    .ThenByDescending(x => HasUsablePrice(x.UnitCostCordoba) || HasUsablePrice(x.UnitCostUsd))
                    .ThenByDescending(x => x.UpdatedAt)
                    .ThenByDescending(x => x.Id)
                    .First())
                .OrderBy(x => x.StockStatus == "Agotado" ? 0 : x.StockStatus == "Bajo stock" ? 1 : 2)
                .ThenBy(x => x.Category)
                .ThenBy(x => x.PartName)
                .ToList();

            foreach (var row in consolidated)
            {
                row.Site = NormalizeSiteValue(row.Site);
                var resolvedItem = ResolveItemCode(row.PartCode, row.ItemCode, row.ManufacturerPartNumber, row.PartName);
                if (!string.IsNullOrWhiteSpace(resolvedItem))
                {
                    row.ItemCode = resolvedItem;
                }
            }

            return consolidated;
        }

        private static string BuildInventoryDisplayDedupKey(MaintenanceInventoryPart row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var name = NormalizeHeaderKey(row.PartName);
            var site = NormalizeHeaderKey(NormalizeSiteValue(row.Site));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return $"name|{name}|site|{site}";
            }

            var item = NormalizeHeaderKey(ResolveItemCode(row.PartCode, row.ItemCode, row.ManufacturerPartNumber, row.PartName));
            if (!string.IsNullOrWhiteSpace(item))
            {
                return $"item|{item}|site|{site}";
            }

            return $"code|{NormalizeHeaderKey(row.PartCode)}";
        }

        private void SetInventorySummaryMetrics(IReadOnlyCollection<MaintenanceInventoryPart> rows)
        {
            ViewBag.TotalPartes = rows.Count;
            ViewBag.TotalStock = rows.Sum(x => x.QuantityOnHand);
            ViewBag.TotalSalidas = rows.Sum(x => x.QuantityIssued);
            ViewBag.TotalTransito = rows.Sum(x => x.QuantityInTransit);
            ViewBag.TotalProyectado = rows.Sum(x => x.QuantityOnHand + x.QuantityInTransit);
            ViewBag.TotalAgotados = rows.Count(x => x.QuantityOnHand <= 0);
            ViewBag.TotalBajoStock = rows.Count(x => x.QuantityOnHand > 0 && x.QuantityOnHand <= x.MinimumStock);
            ViewBag.ValorInventarioCordoba = rows.Sum(x => (x.UnitCostCordoba ?? 0m) * x.QuantityOnHand);
            ViewBag.ValorInventarioUsd = rows.Sum(x => (x.UnitCostUsd ?? 0m) * x.QuantityOnHand);
            ViewBag.ValorTransitoCordoba = rows.Sum(x => (x.UnitCostCordoba ?? 0m) * x.QuantityInTransit);
            ViewBag.ValorTransitoUsd = rows.Sum(x => (x.UnitCostUsd ?? 0m) * x.QuantityInTransit);
        }

        private async Task PopulateOptionsAsync(
            string? selectedCategory,
            string? selectedSystemType,
            string? selectedStockStatus,
            string? selectedSite,
            bool includeInactive)
        {
            ViewBag.CategoryOptions = BuildOptions(CategoryOptions, selectedCategory);
            ViewBag.SystemTypeOptions = BuildOptions(SystemTypeOptions, selectedSystemType);
            ViewBag.StockStatusOptions = BuildOptions(StockStatusOptions, selectedStockStatus);
            ViewBag.SiteOptions = BuildOptions(SiteOptions, selectedSite);
            ViewBag.TenenciaOptions = BuildOptions(TenenciaOptions, null);
            ViewBag.EquipmentTypeOptions = BuildOptions(EquipmentTypeOptions, null);
            ViewBag.UnitOfMeasureOptions = BuildOptions(UnitOfMeasureOptions, null);
            ViewBag.IncludeInactive = includeInactive;

            var lowStockCount = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .CountAsync(x => x.IsActive && x.QuantityOnHand <= x.MinimumStock);

            ViewBag.LowStockCount = lowStockCount;
        }

        private static List<SelectListItem> BuildOptions(IEnumerable<string> source, string? selected)
        {
            var normalizedSelected = selected?.Trim() ?? string.Empty;
            return source
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new SelectListItem(x, x, x.Equals(normalizedSelected, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private void NormalizeModel(MaintenanceInventoryPart model)
        {
            model.PartCode = (model.PartCode ?? string.Empty).Trim();
            model.PartName = (model.PartName ?? string.Empty).Trim();
            model.Category = (model.Category ?? string.Empty).Trim();
            model.Subcategory = string.IsNullOrWhiteSpace(model.Subcategory) ? null : model.Subcategory.Trim();
            model.SystemType = (model.SystemType ?? string.Empty).Trim();
            model.EquipmentType = (model.EquipmentType ?? string.Empty).Trim();
            model.CompatibleModel = string.IsNullOrWhiteSpace(model.CompatibleModel) ? null : model.CompatibleModel.Trim();
            model.Brand = string.IsNullOrWhiteSpace(model.Brand) ? null : model.Brand.Trim();
            model.ManufacturerPartNumber = string.IsNullOrWhiteSpace(model.ManufacturerPartNumber) ? null : model.ManufacturerPartNumber.Trim();
            var resolvedItemCode = ResolveItemCode(model.PartCode, model.ItemCode, model.ManufacturerPartNumber, model.PartName);
            model.ItemCode = string.IsNullOrWhiteSpace(resolvedItemCode) ? null : resolvedItemCode.Trim();
            model.Site = NormalizeSiteValue(model.Site);
            model.Tenencia = (model.Tenencia ?? string.Empty).Trim();
            model.UnitOfMeasure = (model.UnitOfMeasure ?? string.Empty).Trim();
            model.StockStatus = string.IsNullOrWhiteSpace(model.StockStatus) ? "Disponible" : model.StockStatus.Trim();
            model.Supplier = string.IsNullOrWhiteSpace(model.Supplier) ? null : model.Supplier.Trim();
            model.InvoiceNumber = string.IsNullOrWhiteSpace(model.InvoiceNumber) ? null : model.InvoiceNumber.Trim();
            model.ShelfLocation = string.IsNullOrWhiteSpace(model.ShelfLocation) ? null : model.ShelfLocation.Trim();
            model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        }

        private void ValidateModelOptions(MaintenanceInventoryPart model)
        {
            if (!CategoryOptions.Contains(model.Category, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.Category), "Seleccione una categoria valida.");
            }

            if (!SystemTypeOptions.Contains(model.SystemType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.SystemType), "Seleccione un sistema valido.");
            }

            if (!EquipmentTypeOptions.Contains(model.EquipmentType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.EquipmentType), "Seleccione un tipo de equipo valido.");
            }

            if (!SiteOptions.Contains(model.Site, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.Site), "Seleccione un sitio valido.");
            }

            if (!TenenciaOptions.Contains(model.Tenencia, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.Tenencia), "Seleccione una tenencia valida.");
            }

            if (!UnitOfMeasureOptions.Contains(model.UnitOfMeasure, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.UnitOfMeasure), "Seleccione una unidad de medida valida.");
            }

            if (!StockStatusOptions.Contains(model.StockStatus, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceInventoryPart.StockStatus), "Seleccione un estado de stock valido.");
            }
        }

        private static void ApplyStockStatusByQuantity(MaintenanceInventoryPart model)
        {
            if (model.QuantityOnHand <= 0)
            {
                model.StockStatus = "Agotado";
                return;
            }

            if (model.QuantityOnHand <= model.MinimumStock)
            {
                model.StockStatus = "Bajo stock";
                return;
            }

            if (string.Equals(model.StockStatus, "Agotado", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.StockStatus, "Bajo stock", StringComparison.OrdinalIgnoreCase))
            {
                model.StockStatus = "Disponible";
            }
        }

        private async Task<string> GeneratePartCodeAsync()
        {
            var prefix = $"RMT-{DateTime.UtcNow:yyyy}";

            var count = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .CountAsync(x => x.PartCode.StartsWith(prefix));

            return $"{prefix}-{(count + 1):D4}";
        }
    }
}
