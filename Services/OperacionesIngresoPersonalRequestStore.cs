using System.Text.Json;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Services
{
    public sealed class OperacionesIngresoPersonalRequestStore : IOperacionesIngresoPersonalRequestStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;

        public OperacionesIngresoPersonalRequestStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public OperacionesIngresoPersonalRequestSaveResult Save(OperacionesIngresoPersonalRequestSaveInput input)
        {
            EnsurePersistenceTable();

            var tipo = (input.Tipo ?? string.Empty).Trim();
            var asunto = (input.Asunto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(asunto))
            {
                return new OperacionesIngresoPersonalRequestSaveResult
                {
                    Success = false,
                    Message = "Tipo y asunto son obligatorios."
                };
            }

            var now = DateTime.Now;
            var payload = input.Payload ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            if (input.Id.HasValue && input.Id.Value > 0)
            {
                var existing = _context.OperacionesIngresoPersonalSolicitudes
                    .SingleOrDefault(x => x.Id == input.Id.Value);

                if (existing is null)
                {
                    return new OperacionesIngresoPersonalRequestSaveResult
                    {
                        Success = false,
                        Message = "No se encontró la solicitud para actualizar."
                    };
                }

                existing.Tipo = tipo;
                existing.Asunto = asunto;
                existing.SolicitanteNombre = (input.SolicitanteNombre ?? string.Empty).Trim();
                existing.TareaPadre = (input.TareaPadre ?? string.Empty).Trim();
                existing.Estado = (input.Estado ?? string.Empty).Trim();
                existing.Prioridad = (input.Prioridad ?? string.Empty).Trim();
                existing.PayloadJson = payloadJson;
                existing.UpdatedAt = now;

                _context.SaveChanges();

                return new OperacionesIngresoPersonalRequestSaveResult
                {
                    Success = true,
                    Created = false,
                    Id = existing.Id,
                    Message = "Solicitud actualizada correctamente."
                };
            }

            var request = new OperacionesIngresoPersonalSolicitud
            {
                Tipo = tipo,
                Asunto = asunto,
                SolicitanteNombre = (input.SolicitanteNombre ?? string.Empty).Trim(),
                TareaPadre = (input.TareaPadre ?? string.Empty).Trim(),
                Estado = (input.Estado ?? string.Empty).Trim(),
                Prioridad = (input.Prioridad ?? string.Empty).Trim(),
                PayloadJson = payloadJson,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.OperacionesIngresoPersonalSolicitudes.Add(request);
            _context.SaveChanges();

            return new OperacionesIngresoPersonalRequestSaveResult
            {
                Success = true,
                Created = true,
                Id = request.Id,
                Message = "Solicitud creada correctamente."
            };
        }

        public IReadOnlyList<OperacionesIngresoPersonalRequestListItem> GetRecent(int take)
        {
            EnsurePersistenceTable();

            var limit = take <= 0 ? 50 : Math.Min(take, 500);

            return _context.OperacionesIngresoPersonalSolicitudes
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Take(limit)
                .Select(x => new OperacionesIngresoPersonalRequestListItem
                {
                    Id = x.Id,
                    Tipo = x.Tipo,
                    Asunto = x.Asunto,
                    SolicitanteNombre = x.SolicitanteNombre,
                    TareaPadre = x.TareaPadre,
                    Estado = x.Estado,
                    Prioridad = x.Prioridad,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList();
        }

        public OperacionesIngresoPersonalRequestDetail? GetById(int id)
        {
            EnsurePersistenceTable();

            var row = _context.OperacionesIngresoPersonalSolicitudes
                .AsNoTracking()
                .SingleOrDefault(x => x.Id == id);

            if (row is null)
            {
                return null;
            }

            Dictionary<string, string[]> payload;
            try
            {
                payload = JsonSerializer.Deserialize<Dictionary<string, string[]>>(row.PayloadJson, JsonOptions)
                    ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                payload = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            return new OperacionesIngresoPersonalRequestDetail
            {
                Id = row.Id,
                Tipo = row.Tipo,
                Asunto = row.Asunto,
                SolicitanteNombre = row.SolicitanteNombre,
                TareaPadre = row.TareaPadre,
                Estado = row.Estado,
                Prioridad = row.Prioridad,
                Payload = payload,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt
            };
        }

        public bool Delete(int id)
        {
            EnsurePersistenceTable();

            var row = _context.OperacionesIngresoPersonalSolicitudes
                .SingleOrDefault(x => x.Id == id);

            if (row is null)
            {
                return false;
            }

            _context.OperacionesIngresoPersonalSolicitudes.Remove(row);
            _context.SaveChanges();
            return true;
        }

        private void EnsurePersistenceTable()
        {
            if (!_context.Database.IsSqlServer())
            {
                return;
            }

            const string sql = """
                IF OBJECT_ID(N'dbo.OperacionesIngresoPersonalSolicitudes', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OperacionesIngresoPersonalSolicitudes](
                        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Tipo] NVARCHAR(60) NOT NULL,
                        [Asunto] NVARCHAR(260) NOT NULL,
                        [SolicitanteNombre] NVARCHAR(220) NOT NULL,
                        [TareaPadre] NVARCHAR(40) NOT NULL,
                        [Estado] NVARCHAR(60) NOT NULL,
                        [Prioridad] NVARCHAR(40) NOT NULL,
                        [PayloadJson] NVARCHAR(MAX) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL,
                        [UpdatedAt] DATETIME2 NOT NULL
                    );

                    CREATE INDEX [IX_OperacionesIngresoPersonalSolicitudes_UpdatedAt]
                        ON [dbo].[OperacionesIngresoPersonalSolicitudes]([UpdatedAt] DESC);
                END;
                """;

            _context.Database.ExecuteSqlRaw(sql);
        }
    }
}
