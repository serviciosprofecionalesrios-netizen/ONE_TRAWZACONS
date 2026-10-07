using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;

namespace ITServiceDeskApp.Services;

// Fixed, user-provided public spreadsheet; request values never control the URL.
public sealed class DriverScoreSourceService(IHttpClientFactory clients, ILogger<DriverScoreSourceService> logger)
{
    public const string SourceUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ--X8Npjlc7fZoy43I8VVR3CLDXkXT9M9XXpncuDzLNcHtkG0Ij3dYq8enui3dDQEDsPA0b8w_WyqB/pubhtml";
    private const string EventsGid = "1885167141";
    private const string ScoreGid = "0";
    private readonly SemaphoreSlim gate = new(1, 1);
    private DriverScoreSourceResult? cache;

    public async Task<DriverScoreSourceResult> GetAsync(CancellationToken cancellationToken, bool refresh = false)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && cache?.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(-5)) return cache;
            try
            {
                var client = clients.CreateClient("PublishedInventory");
                var baseUrl = SourceUrl.Replace("/pubhtml", "/pub?output=csv&gid=");
                var files = await Task.WhenAll(client.GetStringAsync(baseUrl + EventsGid, cancellationToken), client.GetStringAsync(baseUrl + ScoreGid, cancellationToken));
                cache = new DriverScoreSourceResult(ParseEvents(files[0]), ParseScores(files[1]), DateTimeOffset.UtcNow, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or MalformedLineException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Cannot refresh Driver Score source");
                cache = cache is null
                    ? new DriverScoreSourceResult([], [], null, "No se pudo consultar Driver Score. Intenta de nuevo en un minuto.")
                    : cache with { Warning = "Google Sheets no está disponible. Se muestra la última consulta guardada." };
            }
            return cache;
        }
        finally { gate.Release(); }
    }

    private static List<DriverScoreEvent> ParseEvents(string csv)
    {
        var rows = Read(csv);
        return rows.Select(r => new DriverScoreEvent(
            Get(r, "Fecha/Hora Evento"), Get(r, "Tipo de Evento"), Get(r, "Grupo"), Get(r, "Vehículo"),
            Get(r, "Nombre del Conductor"), Get(r, "Ubicación"), Get(r, "Observación"),
            Get(r, "Gestionado a Tiempo"), Get(r, "Se realizó Coaching")))
            .Where(x => !string.IsNullOrWhiteSpace(x.EventType) && !string.IsNullOrWhiteSpace(x.Driver))
            .ToList();
    }

    private static List<DriverScoreSummary> ParseScores(string csv)
    {
        var rows = Read(csv);
        return rows.Select(r => new DriverScoreSummary(Get(r, "Conductor"), Get(r, "Equipo"), Number(Get(r, "Km Recorridos")),
            Number(Get(r, "Exceso de velocidad")), Number(Get(r, "Fatiga")), Number(Get(r, "Distración, Comer, Fumar, Beber")),
            Number(Get(r, "Uso del celular")), Number(Get(r, "Frenadas Bruscas")), Number(Get(r, "Aceleraciones Bruscas")), Get(r, "Estado de Ansiedad")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Driver) && !x.Driver.Equals("Sin Conductor", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<Dictionary<string, string>> Read(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv));
        parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true;
        var headers = parser.ReadFields()?.Select(x => x.Trim().TrimStart('\uFEFF')).ToArray() ?? throw new InvalidDataException("Faltan encabezados.");
        var output = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var cells = parser.ReadFields() ?? [];
            output.Add(headers.Select((header, index) => new KeyValuePair<string, string>(header, index < cells.Length ? cells[index].Trim() : ""))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
        }
        return output;
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value : "";
    private static decimal Number(string value) => decimal.TryParse(value.Replace(",", ""), out var result) ? result : 0m;
}

public sealed record DriverScoreEvent(string DateText, string EventType, string Group, string Vehicle, string Driver, string Location, string Observation, string Timely, string Coaching);
public sealed record DriverScoreSummary(string Driver, string Vehicle, decimal Kilometers, decimal Speeding, decimal Fatigue, decimal Distraction, decimal PhoneUse, decimal HarshBraking, decimal HarshAcceleration, string Status);
public sealed record DriverScoreSourceResult(IReadOnlyList<DriverScoreEvent> Events, IReadOnlyList<DriverScoreSummary> Scores, DateTimeOffset? RetrievedAt, string? Warning);
