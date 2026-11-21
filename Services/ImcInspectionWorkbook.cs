using System.Globalization;
using System.IO.Compression;
using ITServiceDeskApp.Models;
using OfficeOpenXml;

namespace ITServiceDeskApp.Services;

internal static class ImcInspectionWorkbook
{
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
                int unit = 0, date = 0, color = 0;
                for (var column = dimension.Start.Column; column <= Math.Min(dimension.End.Column, dimension.Start.Column + 149); column++)
                {
                    switch (Header(sheet.Cells[row, column].Text))
                    {
                        case "UNIDAD" or "EQUIPO" or "VEHICULO" or "CODIGODEEQUIPO" or "UNIDADEQUIPO":
                            unit = column; break;
                        case "PRESENTADA" or "FECHADEPRESENTACION" or "FECHADEINSPECCION" or "FECHA":
                            date = column; break;
                        case "SEMAFORO" or "COLOR" or "RESULTADO":
                            color = column; break;
                    }
                }
                if (unit > 0 && date > 0 && color > 0)
                    return ReadRows(sheet, row + 1, dimension.End.Row, unit, date, color, package.Workbook.Date1904);
            }
        }
        throw new InvalidDataException("El Excel debe tener las columnas Unidad, Presentada y Semáforo en la misma hoja.");
    }

    private static List<OperationalImcInspection> ReadRows(ExcelWorksheet sheet, int start, int end,
        int unitColumn, int dateColumn, int colorColumn, bool date1904)
    {
        if (end - start >= 50_000) throw new InvalidDataException("El Excel admite hasta 50,000 filas por carga.");
        var results = new List<OperationalImcInspection>();
        var errors = new List<int>();
        for (var row = start; row <= end; row++)
        {
            var unit = sheet.Cells[row, unitColumn].Text.Trim();
            var time = sheet.Cells[row, dateColumn];
            var colorText = sheet.Cells[row, colorColumn].Text.Trim();
            if (unit.Length == 0 && string.IsNullOrWhiteSpace(time.Text) && colorText.Length == 0) continue;
            var key = HolcimMasterCatalog.Identifier(unit);
            var color = Header(colorText) switch
            {
                "ROJO" or "ROJA" or "IMCROJO" or "IMCROJA" => "Rojo",
                "AMARILLO" or "AMARILLA" or "IMCAMARILLO" or "IMCAMARILLA" => "Amarillo",
                "VERDE" or "IMCVERDE" => "Verde",
                _ => null
            };
            if (key.Length == 0 || unit.Length > 160 || color is null || !TryDate(time.Value, date1904, out var presentedAt))
            {
                errors.Add(row);
                continue;
            }
            results.Add(new OperationalImcInspection
            {
                Month = presentedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                UnitKey = key, UnitCode = unit, PresentedAt = presentedAt, Color = color
            });
        }
        if (errors.Count > 0)
            throw new InvalidDataException($"Revisa la unidad, la fecha y el color en {errors.Count:N0} filas. Primeras filas con error: {string.Join(", ", errors.Take(10))}.");
        if (results.Count == 0) throw new InvalidDataException("El Excel no contiene inspecciones para importar.");
        return results;
    }

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
