using System.Globalization;
using System.Text;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Services
{
    public sealed class OperacionesIngresoPersonalTraceStore : IOperacionesIngresoPersonalTraceStore
    {
        private readonly ApplicationDbContext _context;

        public OperacionesIngresoPersonalTraceStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public OperacionesIngresoPersonalTraceResult ResolveOrCreate(string tipo, string asunto)
        {
            EnsurePersistenceTable();

            var tipoValue = (tipo ?? string.Empty).Trim();
            var asuntoValue = (asunto ?? string.Empty).Trim();
            var solicitanteKey = NormalizeForKey(asuntoValue);

            if (string.IsNullOrWhiteSpace(solicitanteKey))
            {
                return new OperacionesIngresoPersonalTraceResult
                {
                    Success = false,
                    Message = "Ingresa el nombre en Asunto para generar la trazabilidad."
                };
            }

            var now = DateTime.Now;
            var existing = _context.OperacionesIngresoPersonalTareasPadre
                .SingleOrDefault(x => x.SolicitanteKey == solicitanteKey);

            if (existing is not null)
            {
                existing.UltimoTipo = tipoValue;
                existing.UltimoAsunto = asuntoValue;
                existing.UpdatedAt = now;
                _context.SaveChanges();

                return new OperacionesIngresoPersonalTraceResult
                {
                    Success = true,
                    TareaPadre = existing.TareaPadre,
                    Created = false,
                    Message = $"Tarea padre vinculada: {existing.TareaPadre}"
                };
            }

            if (!IsSolicitudIngreso(tipoValue))
            {
                return new OperacionesIngresoPersonalTraceResult
                {
                    Success = false,
                    Message = "No existe Tarea padre para este solicitante. Debes crear primero en 1. Solicitud Ingreso."
                };
            }

            var nextTaskCode = BuildNextTaskCode(now.Year);

            var nuevo = new OperacionesIngresoPersonalTareaPadre
            {
                SolicitanteKey = solicitanteKey,
                SolicitanteNombre = asuntoValue,
                TareaPadre = nextTaskCode,
                TipoOrigen = tipoValue,
                UltimoTipo = tipoValue,
                UltimoAsunto = asuntoValue,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.OperacionesIngresoPersonalTareasPadre.Add(nuevo);
            _context.SaveChanges();

            return new OperacionesIngresoPersonalTraceResult
            {
                Success = true,
                TareaPadre = nextTaskCode,
                Created = true,
                Message = $"Tarea padre creada: {nextTaskCode}"
            };
        }

        public IReadOnlyList<OperacionesIngresoPersonalTraceHistoryItem> GetRecent(int take)
        {
            EnsurePersistenceTable();

            var limit = take <= 0 ? 20 : Math.Min(take, 200);

            return _context.OperacionesIngresoPersonalTareasPadre
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Take(limit)
                .Select(x => new OperacionesIngresoPersonalTraceHistoryItem
                {
                    Solicitante = x.SolicitanteNombre,
                    TareaPadre = x.TareaPadre,
                    TipoOrigen = x.TipoOrigen,
                    UltimoTipo = x.UltimoTipo,
                    UltimoAsunto = x.UltimoAsunto,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList();
        }

        private string BuildNextTaskCode(int year)
        {
            var prefix = $"TP-{year}-";

            var current = _context.OperacionesIngresoPersonalTareasPadre
                .AsNoTracking()
                .Where(x => x.TareaPadre.StartsWith(prefix))
                .Select(x => x.TareaPadre)
                .ToList();

            var maxSequence = current
                .Select(ParseSequence)
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefix}{(maxSequence + 1):D5}";
        }

        private static int ParseSequence(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return 0;
            }

            var lastDash = code.LastIndexOf('-');
            if (lastDash < 0 || lastDash >= code.Length - 1)
            {
                return 0;
            }

            return int.TryParse(code[(lastDash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        private static bool IsSolicitudIngreso(string tipo) =>
            !string.IsNullOrWhiteSpace(tipo) &&
            tipo.StartsWith("1.", StringComparison.Ordinal) &&
            tipo.Contains("Solicitud Ingreso", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeForKey(string value)
        {
            var input = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(input.Length);

            foreach (var c in input)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }

            var normalized = builder.ToString().Normalize(NormalizationForm.FormC);
            return string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private void EnsurePersistenceTable()
        {
            const string sql = """
                IF OBJECT_ID(N'dbo.OperacionesIngresoPersonalTareasPadre', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OperacionesIngresoPersonalTareasPadre](
                        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [SolicitanteKey] NVARCHAR(220) NOT NULL,
                        [SolicitanteNombre] NVARCHAR(220) NOT NULL,
                        [TareaPadre] NVARCHAR(40) NOT NULL,
                        [TipoOrigen] NVARCHAR(60) NOT NULL,
                        [UltimoTipo] NVARCHAR(60) NOT NULL,
                        [UltimoAsunto] NVARCHAR(260) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL,
                        [UpdatedAt] DATETIME2 NOT NULL
                    );

                    CREATE UNIQUE INDEX [UX_OperacionesIngresoPersonalTareasPadre_SolicitanteKey]
                        ON [dbo].[OperacionesIngresoPersonalTareasPadre]([SolicitanteKey]);

                    CREATE UNIQUE INDEX [UX_OperacionesIngresoPersonalTareasPadre_TareaPadre]
                        ON [dbo].[OperacionesIngresoPersonalTareasPadre]([TareaPadre]);
                END;
                """;

            _context.Database.ExecuteSqlRaw(sql);
        }
    }
}
