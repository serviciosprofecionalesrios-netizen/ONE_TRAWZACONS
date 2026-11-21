using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ITServiceDeskApp.Models;
using OfficeOpenXml;

namespace ITServiceDeskApp.Services;

internal static class ImcInspectionWorkbook
{
    internal sealed record Finding(string Item, string Status);
    internal const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly string[] DateFormats =
        ["d/M/yyyy", "d/M/yyyy H:mm", "d/M/yyyy H:mm:ss", "d/M/yyyy h:mm:ss tt",
         "yyyy-MM-dd", "yyyy-MM-dd H:mm", "yyyy-MM-dd H:mm:ss", "yyyy-MM-ddTHH:mm:ss"];

    internal static List<OperationalImcInspection> Read(Stream file)
    {
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (archive.Entries.Sum(entry => (decimal)entry.Length) > 100 * 1024 * 1024)
                throw new InvalidDataException("El contenido del Excel supera el tamaño permitido.");
        }
        file.Position = 0;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(file);
        foreach (var sheet in package.Workbook.Worksheets.OrderByDescending(sheet =>
                     sheet.Name.Equals("Inspecciones", StringComparison.OrdinalIgnoreCase)))
        {
            if (sheet.Dimension is not { } dimension) continue;
            for (var row = dimension.Start.Row; row <= Math.Min(dimension.End.Row, dimension.Start.Row + 19); row++)
            {
                var headers = Enumerable.Range(dimension.Start.Column, dimension.Columns)
                    .Select(column => (Column: column, Key: Header(sheet.Cells[row, column].Text))).ToArray();
                var unitColumns = headers.Where(column => column.Key is
                    "UNIDAD" or "EQUIPO" or "VEHICULO" or "CODIGODEEQUIPO" or "UNIDADEQUIPO")
                    .Select(column => column.Column).ToArray();
                var inspections = new List<OperationalImcInspection>();
                var recognized = false;
                for (var index = 0; index < unitColumns.Length; index++)
                {
                    var unit = unitColumns[index];
                    var firstColumn = index == 0 ? dimension.Start.Column : unit;
                    var lastColumn = index + 1 < unitColumns.Length ? unitColumns[index + 1] - 1 : dimension.End.Column;
                    var block = headers.Where(column => column.Column >= firstColumn && column.Column <= lastColumn).ToArray();
                    var date = block.FirstOrDefault(column => column.Key is
                        "PRESENTADA" or "FECHADEPRESENTACION" or "FECHADEINSPECCION" or "FECHA").Column;
                    var color = block.FirstOrDefault(column => column.Key == "SEMAFORO").Column;
                    if (color == 0) color = block.FirstOrDefault(column => column.Key is "COLOR" or "RESULTADO").Column;
                    if (date == 0 || color == 0) continue;

                    recognized = true;
                    inspections.AddRange(ReadRows(sheet, row, dimension.End.Row, firstColumn, lastColumn,
                        unit, date, color, package.Workbook.Date1904));
                }
                if (inspections.Count > 0) return inspections;
                if (recognized) break;
            }
        }
        throw new InvalidDataException("No se encontraron inspecciones. Revisa que el Excel tenga datos y las columnas Unidad, Fecha o Presentada, y Semáforo.");
    }

    private static List<OperationalImcInspection> ReadRows(ExcelWorksheet sheet, int header, int end,
        int firstColumn, int lastColumn, int unitColumn, int dateColumn, int colorColumn, bool date1904)
    {
        if (end - header > 50_000) throw new InvalidDataException("El Excel admite hasta 50,000 filas por carga.");
        var evaluated = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
            .Where(column => column != unitColumn && column != dateColumn && column != colorColumn)
            .Select(column => (Column: column, Item: sheet.Cells[header, column].Text.Trim()))
            .Where(column => column.Item.Length > 0 && Header(column.Item) is not
                ("PLANTA" or "PLACADELCABEZAL" or "PLACADELREMOLQUEGONDOLA" or "ODOMETRO" or
                 "OBSERVACIONESDELINSPECTOR" or "COORDENADAS"))
            .ToArray();
        if ((long)(end - header) * evaluated.Length > 5_000_000)
            throw new InvalidDataException("El informe contiene demasiados puntos evaluados para una sola carga.");
        var results = new List<OperationalImcInspection>();
        var errors = new List<int>();
        for (var row = header + 1; row <= end; row++)
        {
            var unit = sheet.Cells[row, unitColumn].Text.Trim();
            var time = sheet.Cells[row, dateColumn];
            var colorText = sheet.Cells[row, colorColumn].Text.Trim();
            if (unit.Length == 0 && string.IsNullOrWhiteSpace(time.Text) && colorText.Length == 0) continue;
            var key = HolcimMasterCatalog.Identifier(unit);
            var color = Header(colorText) switch
            {
                "ROJO" or "ROJA" or "IMCROJO" or "IMCROJA" => "Rojo",
                "AMARILLO" or "AMARILO" or "AMARILLA" or "IMCAMARILLO" or "IMCAMARILLA" => "Amarillo",
                "VERDE" or "IMCVERDE" => "Verde",
                _ => null
            };
            if (key.Length == 0 || unit.Length > 160 || color is null || !TryDate(time.Value, date1904, out var presentedAt))
            {
                errors.Add(row);
                continue;
            }
            var findings = new List<Finding>();
            foreach (var point in evaluated)
            {
                var status = Header(sheet.Cells[row, point.Column].Text.Trim()) switch
                {
                    "APROBADOCONRESTRICCION" => "Aprobado con Restricción",
                    "NOCUMPLE" => "No cumple",
                    _ => null
                };
                if (status is not null) findings.Add(new Finding(point.Item, status));
            }
            results.Add(new OperationalImcInspection
            {
                Month = presentedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                UnitKey = key, UnitCode = unit, PresentedAt = presentedAt, Color = color,
                FindingsJson = findings.Count == 0 ? null : JsonSerializer.Serialize(findings.Distinct())
            });
        }
        if (errors.Count > 0)
            throw new InvalidDataException($"Revisa la unidad, la fecha y el color en {errors.Count:N0} filas. Primeras filas con error: {string.Join(", ", errors.Take(10))}.");
        return results;
    }

    internal static Finding[] Findings(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<Finding[]>(json) ?? [];

    private static string Header(string text) => HolcimMasterCatalog.Identifier(text.Normalize(System.Text.NormalizationForm.FormD));

    private static bool TryDate(object? value, bool date1904, out DateTime date)
    {
        if (value is DateTime timestamp)
            date = timestamp;
        else if (value is double serial && serial >= 0 && serial + (date1904 ? 1462 : 0) < 2_958_466)
            date = DateTime.FromOADate(serial + (date1904 ? 1462 : 0));
        else if (!DateTime.TryParseExact(value?.ToString()?.Trim(), DateFormats, CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowWhiteSpaces, out date))
            return false;
        date = new DateTime(date.Ticks - date.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
        return date.Year >= 1900;
    }
}
