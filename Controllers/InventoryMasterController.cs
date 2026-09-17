using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace ITServiceDeskApp.Controllers;

[Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
public class InventoryMasterController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public InventoryMasterController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string? search, string? warehouse, string? category, string? stockStatus, bool includeInactive = false)
    {
        var items = await BuildFilteredQuery(search, warehouse, category, stockStatus, includeInactive)
            .OrderBy(x => x.Site).ThenBy(x => x.ItemCode).ThenBy(x => x.PartName).ToListAsync();
        await PopulateFiltersAsync(warehouse, category, stockStatus);
        ViewBag.Search = search;
        ViewBag.IncludeInactive = includeInactive;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? search, string? warehouse, string? category, string? stockStatus, bool includeInactive = false)
    {
        var items = await BuildFilteredQuery(search, warehouse, category, stockStatus, includeInactive)
            .OrderBy(x => x.Site).ThenBy(x => x.ItemCode).ThenBy(x => x.PartName).ToListAsync();
        return File(BuildPhysicalInventoryExcel(items), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"InventarioFisico_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(string? search, string? warehouse, string? category, string? stockStatus, bool includeInactive = false)
    {
        var items = await BuildFilteredQuery(search, warehouse, category, stockStatus, includeInactive)
            .OrderBy(x => x.Site).ThenBy(x => x.ItemCode).ThenBy(x => x.PartName).ToListAsync();
        var bytes = InventoryPhysicalCountPdfReportService.GeneratePdf(items, _environment.WebRootPath);
        return File(bytes, "application/pdf", $"InventarioFisico_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
    public IActionResult Create() => RedirectToAction(nameof(MaintenanceInventoryController.Create), "MaintenanceInventory");

    [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
    public IActionResult Edit(int id) => RedirectToAction(nameof(MaintenanceInventoryController.Details), "MaintenanceInventory", new { id });

    private IQueryable<MaintenanceInventoryPart> BuildFilteredQuery(string? search, string? warehouse, string? category, string? stockStatus, bool includeInactive)
    {
        var query = _context.MaintenanceInventoryParts.AsNoTracking();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => (x.ItemCode != null && x.ItemCode.Contains(term)) || x.PartCode.Contains(term) || x.PartName.Contains(term) ||
                (x.ManufacturerPartNumber != null && x.ManufacturerPartNumber.Contains(term)) || (x.Brand != null && x.Brand.Contains(term)) ||
                (x.ShelfLocation != null && x.ShelfLocation.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(warehouse)) query = query.Where(x => x.Site == warehouse);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(stockStatus)) query = query.Where(x => x.StockStatus == stockStatus);
        return query;
    }

    private async Task PopulateFiltersAsync(string? warehouse, string? category, string? stockStatus)
    {
        var rows = _context.MaintenanceInventoryParts.AsNoTracking();
        ViewBag.Warehouses = await rows.Select(x => x.Site).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Categories = await rows.Select(x => x.Category).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.StockStatuses = await rows.Select(x => x.StockStatus).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Warehouse = warehouse; ViewBag.Category = category; ViewBag.StockStatus = stockStatus;
    }

    private static byte[] BuildPhysicalInventoryExcel(IReadOnlyList<MaintenanceInventoryPart> items)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Inventario físico");
        sheet.Cells[1, 1].Value = "INVENTARIO FÍSICO - MAESTRO DE INVENTARIOS";
        sheet.Cells[2, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Cells[4, 1].LoadFromArrays(new[] { new object[] { "Ítem", "Código", "Artículo / descripción", "N.º parte", "Almacén", "Ubicación", "U/M", "Stock sistema", "Conteo físico", "Diferencia", "Observaciones" } });
        var row = 5;
        foreach (var item in items)
        {
            sheet.Cells[row, 1].Value = item.ItemCode; sheet.Cells[row, 2].Value = item.PartCode; sheet.Cells[row, 3].Value = item.PartName;
            sheet.Cells[row, 4].Value = item.ManufacturerPartNumber; sheet.Cells[row, 5].Value = item.Site; sheet.Cells[row, 6].Value = item.ShelfLocation;
            sheet.Cells[row, 7].Value = item.UnitOfMeasure; sheet.Cells[row, 8].Value = item.QuantityOnHand;
            sheet.Cells[row, 10].Formula = $"=IF(I{row}=\"\",\"\",I{row}-H{row})"; row++;
        }
        using (var title = sheet.Cells[1, 1, 1, 11]) { title.Merge = true; title.Style.Font.Bold = true; title.Style.Font.Size = 14; title.Style.Font.Color.SetColor(Color.White); title.Style.Fill.PatternType = ExcelFillStyle.Solid; title.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(17, 67, 154)); }
        using (var headers = sheet.Cells[4, 1, 4, 11]) { headers.Style.Font.Bold = true; headers.Style.Fill.PatternType = ExcelFillStyle.Solid; headers.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(221, 235, 247)); }
        sheet.View.FreezePanes(5, 1); sheet.Cells.AutoFitColumns(); sheet.Column(3).Width = 32; sheet.Column(11).Width = 28;
        return package.GetAsByteArray();
    }
}
