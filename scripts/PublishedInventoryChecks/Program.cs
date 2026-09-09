using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}
var definition = new InventorySheet("test", "test", "0", ["ID"]);
var csv = "ID,DESCRIPTION,QUANTITY\r\n001,\"A, B\"" + ",0\r\n002,\"line 1\nline 2 with \"\"quotes\"\"\",\r\n,,0\r\n001,duplicate,2";
var parsed = PublishedInventoryService.Parse(csv, definition);
Check(parsed.Rows.Count == 3 && parsed.ExcludedRows == 1, "Skip formula-only rows, retain duplicate identifiers");
Check(parsed.Rows[0].Cells[0] == "001" && parsed.Rows[0].Cells[1] == "A, B", "Preserve leading zeros and quoted commas");
Check(parsed.Rows[1].Cells[1] == "line 1\nline 2 with \"quotes\"" && parsed.Rows[1].Cells[2] == "", "Multiline CSV and blank values");
Check(parsed.Rows[0].Cells[2] == "0" && parsed.Rows[2].SourceRow == 5, "Zero differs from blank; retain worksheet row");
try { PublishedInventoryService.Parse("<html>Google error</html>", definition); throw new Exception("Accepted HTML"); }
catch (InvalidDataException) { Console.WriteLine("PASS: Reject invalid schema/HTML"); }
var handler = new FixtureHandler(csv);
var service = new PublishedInventoryService(new Factory(handler), NullLogger<PublishedInventoryService>.Instance);
var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.GetAsync(definition, default)));
Check(handler.Calls == 1 && results.All(r => r.Table?.Rows.Count == 3), "Concurrent requests share one successful fetch");
handler.Fail = true;
var failed = await service.GetAsync(definition with { Key = "unavailable" }, default);
Check(failed.Table == null && failed.Warning != null, "Network failure is unavailable, not zero stock");
await service.GetAsync(definition with { Key = "unavailable" }, default);
Check(handler.Calls == 2, "Back off after source failure");
if (args.Length > 0)
{
    string[] files = ["Inventario.csv", "Entradas.csv", "Salidas.csv", "OrdenCompra.csv"];
    int[] expected = [1438, 6, 1126, 1031];
    for (int i = 0; i < files.Length; i++)
    {
        var table = PublishedInventoryService.Parse(File.ReadAllText(Path.Combine(args[0], files[i])), PublishedInventoryService.Sheets[i]);
        Check(table.Rows.Count == expected[i], $"Published snapshot {files[i]}: {expected[i]} records");
    }
}
sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, false);
}
sealed class FixtureHandler(string csv) : HttpMessageHandler
{
    public int Calls;
    public bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(csv) });
    }
}
