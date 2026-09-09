using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;

namespace ITServiceDeskApp.Services;

// Only this fixed, user-provided public document is fetched. No arbitrary URLs.
public sealed class PublishedInventoryService(IHttpClientFactory clients, ILogger<PublishedInventoryService> logger)
{
    public const string SourceUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vT9UL6D90oBYQxXoKBRow9Zhvlmd7D7dL7QYwMgUYMLafBE9eHDkFcQFQfmXcK4v799GiSboWOm3hKC/pubhtml";
    public static readonly IReadOnlyList<InventorySheet> Sheets = Array.AsReadOnly(new[]
    {
        new InventorySheet("inventario", "Inventario", "1210627928", ["ID_PRODUCTO", "ITEM", "DESCRIPCION"]),
        new InventorySheet("entradas", "Entradas", "660591573", ["IDENTRADA", "ID_O_C", "ORDEN DE COMPRA", "DESCRIPCION"]),
        new InventorySheet("salidas", "Salidas", "588150938", ["ID_DESPACHO", "ID_Salida", "N° REQUISA", "ITEM", "DESCRIPCION"]),
        new InventorySheet("compras", "Órdenes de compra", "583426373", ["ID_OC", "ORDEN DE COMPRA", "FACTURA #"])
    });
    private readonly ConcurrentDictionary<string, InventorySourceResult> cache = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> retryAfter = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();

    public async Task<InventorySourceResult> GetAsync(InventorySheet sheet, CancellationToken cancellationToken)
    {
        var gate = gates.GetOrAdd(sheet.Key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            cache.TryGetValue(sheet.Key, out var previous);
            if (previous?.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(-5) && previous.Warning == null)
                return previous;
            if (retryAfter.TryGetValue(sheet.Key, out var retry) && retry > DateTimeOffset.UtcNow)
                return previous ?? new(null, null, "No se pudo consultar Google Sheets. Intenta de nuevo en un minuto.");
            try
            {
                var url = SourceUrl.Replace("/pubhtml", $"/pub?output=csv&gid={sheet.Gid}");
                var csv = await clients.CreateClient("PublishedInventory").GetStringAsync(url, cancellationToken);
                var result = new InventorySourceResult(Parse(csv, sheet), DateTimeOffset.UtcNow, null);
                cache[sheet.Key] = result;
                retryAfter.TryRemove(sheet.Key, out _);
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or MalformedLineException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Cannot refresh published inventory sheet {Sheet}", sheet.Key);
                var result = new InventorySourceResult(previous?.Table, previous?.RetrievedAt,
                    previous?.Table == null ? "No se pudo consultar Google Sheets. Intenta de nuevo en un minuto."
                    : "Google Sheets no está disponible. Se muestra la última consulta guardada; los datos pueden haber cambiado.");
                cache[sheet.Key] = result;
                retryAfter[sheet.Key] = DateTimeOffset.UtcNow.AddMinutes(1);
                return result;
            }
        }
        finally { gate.Release(); }
    }

    public static InventorySourceTable Parse(string csv, InventorySheet sheet)
    {
        using var parser = new TextFieldParser(new StringReader(csv));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var headers = parser.ReadFields()?.Select(x => x.Trim().TrimStart('\uFEFF')).ToArray()
            ?? throw new InvalidDataException("Missing CSV headers.");
        if (!sheet.IdentityColumns.All(c => headers.Contains(c, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("The published sheet columns have changed.");
        var identities = sheet.IdentityColumns.Select(c => Array.FindIndex(headers, h => h.Equals(c, StringComparison.OrdinalIgnoreCase))).ToArray();
        var rows = new List<InventorySourceRow>();
        var excluded = 0;
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            var cells = parser.ReadFields()!;
            rowNumber++;
            if (cells.Length > headers.Length) throw new InvalidDataException("Unexpected CSV column count.");
            // Retain blank versus zero, identifiers, dates, currency and duplicate source rows.
            cells = Enumerable.Range(0, headers.Length).Select(i => i < cells.Length ? cells[i] : "").ToArray();
            if (!identities.Any(i => !string.IsNullOrWhiteSpace(cells[i]))) { excluded++; continue; }
            rows.Add(new InventorySourceRow(rowNumber, cells));
        }
        return new(headers, rows, excluded);
    }
}
