using System.Globalization;
using System.Text;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Services
{
    public sealed class OperacionesIngresoEquipoGondolaTraceStore : IOperacionesIngresoEquipoGondolaTraceStore
    {
        private readonly ApplicationDbContext _context;

        public OperacionesIngresoEquipoGondolaTraceStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public OperacionesIngresoEquipoGondolaTraceResult ResolveOrCreate(string tipo, string asunto)
        {
            EnsurePersistenceTable();

            var tipoValue = (tipo ?? string.Empty).Trim();
            var asuntoValue = (asunto ?? string.Empty).Trim();
            var solicitanteKey = NormalizeForKey(asuntoValue);

            if (string.IsNullOrWhiteSpace(solicitanteKey))
            {
                return new OperacionesIngresoEquipoGondolaTraceResult
                {
                    Success = false,
                    Message = "Ingresa el nombre en Asunto para generar la trazabilidad."
                };
            }

            var now = DateTime.Now;
            var existing = _context.OperacionesIngresoEquipoGondolaTareasPadre
                .SingleOrDefault(x => x.SolicitanteKey == solicitanteKey);

            if (existing is not null)
            {
                existing.UltimoTipo = tipoValue;
                existing.UltimoAsunto = asuntoValue;
                existing.UpdatedAt = now;
                _context.SaveChanges();

                return new OperacionesIngresoEquipoGondolaTraceResult
                {
                    Success = true,
                    TareaPadre = existing.TareaPadre,
                    Created = false,
                    Message = $"Tarea padre vinculada: {existing.TareaPadre}"
                };
            }

            if (!IsIngresoEquipo(tipoValue))
            {
                return new OperacionesIngresoEquipoGondolaTraceResult
                {
                    Success = false,
                    Message = "No existe Tarea padre para este asunto. Debes crear primero en Ingreso de Equipo."
                };
            }

            var nextTaskCode = BuildNextTaskCode(now.Year);

            var nuevo = new OperacionesIngresoEquipoGondolaTareaPadre
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

            _context.OperacionesIngresoEquipoGondolaTareasPadre.Add(nuevo);
            _context.SaveChanges();

            return new OperacionesIngresoEquipoGondolaTraceResult
            {
                Success = true,
                TareaPadre = nextTaskCode,
                Created = true,
                Message = $"Tarea padre creada: {nextTaskCode}"
            };
        }

        public IReadOnlyList<OperacionesIngresoEquipoGondolaTraceHistoryItem> GetRecent(int take)
        {
            EnsurePersistenceTable();

            var limit = take <= 0 ? 20 : Math.Min(take, 200);

            return _context.OperacionesIngresoEquipoGondolaTareasPadre
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Take(limit)
                .Select(x => new OperacionesIngresoEquipoGondolaTraceHistoryItem
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
            var prefix = $"TEG-{year}-";

            var current = _context.OperacionesIngresoEquipoGondolaTareasPadre
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

        private static bool IsIngresoEquipo(string tipo) =>
            !string.IsNullOrWhiteSpace(tipo) &&
            tipo.Contains("Ingreso de Equipo", StringComparison.OrdinalIgnoreCase);

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
            if (!_context.Database.IsSqlServer())
            {
                return;
            }

            const string sql = """
                IF OBJECT_ID(N'dbo.OperacionesIngresoEquipoGondolaTareasPadre', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OperacionesIngresoEquipoGondolaTareasPadre](
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

                    CREATE UNIQUE INDEX [UX_OperacionesIngresoEquipoGondolaTareasPadre_SolicitanteKey]
                        ON [dbo].[OperacionesIngresoEquipoGondolaTareasPadre]([SolicitanteKey]);

                    CREATE UNIQUE INDEX [UX_OperacionesIngresoEquipoGondolaTareasPadre_TareaPadre]
                        ON [dbo].[OperacionesIngresoEquipoGondolaTareasPadre]([TareaPadre]);
                END;
                """;

            _context.Database.ExecuteSqlRaw(sql);
        }
    }
}
