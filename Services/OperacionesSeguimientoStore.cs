using System.Globalization;
using System.Text;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Services
{
    public sealed class OperacionesSeguimientoStore : IOperacionesSeguimientoStore
    {
        private const int CargaMetadataId = 1;
        private readonly ApplicationDbContext _context;

        public OperacionesSeguimientoStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public void Save(SeguimientoToneladasViewModel model)
        {
            EnsurePersistenceTables();

            var baseDate = model.FechaOperativa ?? DateTime.Now.Date;
            var uploadedAt = DateTime.Now;

            var normalizedRows = model.Rows
                .Select(r => MapToEntity(r, baseDate, uploadedAt))
                .Where(r => !string.IsNullOrWhiteSpace(r.Equipo))
                .ToList();

            if (!normalizedRows.Any())
            {
                return;
            }

            var datesToReplace = normalizedRows
                .Select(r => r.FechaOperativa)
                .Distinct()
                .ToList();

            var existingRows = _context.OperacionesSeguimientoRegistros
                .Where(r => datesToReplace.Contains(r.FechaOperativa))
                .ToList();

            if (existingRows.Any())
            {
                _context.OperacionesSeguimientoRegistros.RemoveRange(existingRows);
            }

            _context.OperacionesSeguimientoRegistros.AddRange(normalizedRows);

            var metadata = _context.OperacionesSeguimientoCargas
                .SingleOrDefault(x => x.Id == CargaMetadataId);

            if (metadata is null)
            {
                metadata = new OperacionesSeguimientoCarga { Id = CargaMetadataId };
                _context.OperacionesSeguimientoCargas.Add(metadata);
            }

            metadata.SourceFileName = model.SourceFileName;
            metadata.FechaOperativa = model.FechaOperativa ?? datesToReplace.Max();
            metadata.UpdatedAt = uploadedAt;

            _context.SaveChanges();
        }

        public SeguimientoToneladasViewModel? Get()
        {
            EnsurePersistenceTables();

            var rows = _context.OperacionesSeguimientoRegistros
                .AsNoTracking()
                .OrderBy(r => r.FechaOperativa)
                .ThenBy(r => r.Id)
                .Select(r => new SeguimientoToneladasRowViewModel
                {
                    Equipo = r.Equipo,
                    Procedencia = r.Procedencia,
                    Ruta = r.Ruta,
                    Toneladas = r.Toneladas,
                    Viajes = r.Viajes,
                    CombustibleLitros = r.CombustibleGalones,
                    PesoSugerido = r.PesoSugerido,
                    FechaEvento = r.FechaEvento ?? r.FechaOperativa,
                    Conductor = r.Conductor,
                    Estado = r.Estado,
                    Eficiencia = r.Eficiencia
                })
                .ToList();

            if (!rows.Any())
            {
                return null;
            }

            var model = BuildToneladasModel(rows);
            var metadata = _context.OperacionesSeguimientoCargas
                .AsNoTracking()
                .SingleOrDefault(x => x.Id == CargaMetadataId);

            model.IsFromUpload = true;
            model.SourceFileName = metadata?.SourceFileName ?? "Datos SQL - Operaciones";

            return model;
        }

        private void EnsurePersistenceTables()
        {
            // PostgreSQL is provisioned through EF Core migrations. This legacy
            // bootstrap SQL is intentionally SQL Server-specific.
            if (!_context.Database.IsSqlServer())
            {
                return;
            }

            const string sql = """
                IF OBJECT_ID(N'dbo.OperacionesSeguimientoRegistros', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OperacionesSeguimientoRegistros](
                        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [FechaOperativa] DATE NOT NULL,
                        [FechaEvento] DATETIME2 NULL,
                        [Equipo] NVARCHAR(80) NOT NULL,
                        [Procedencia] NVARCHAR(120) NOT NULL,
                        [Ruta] NVARCHAR(180) NOT NULL,
                        [Toneladas] DECIMAL(18,4) NOT NULL,
                        [Viajes] INT NOT NULL,
                        [CombustibleGalones] DECIMAL(18,4) NOT NULL,
                        [PesoSugerido] DECIMAL(18,4) NOT NULL CONSTRAINT [DF_OperacionesSeguimientoRegistros_PesoSugerido] DEFAULT(0),
                        [Conductor] NVARCHAR(120) NOT NULL,
                        [Estado] NVARCHAR(60) NOT NULL,
                        [Eficiencia] NVARCHAR(40) NOT NULL,
                        [UploadedAt] DATETIME2 NOT NULL
                    );

                    CREATE INDEX [IX_OperacionesSeguimientoRegistros_FechaOperativa]
                        ON [dbo].[OperacionesSeguimientoRegistros] ([FechaOperativa]);
                END;

                IF OBJECT_ID(N'dbo.OperacionesSeguimientoCargas', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OperacionesSeguimientoCargas](
                        [Id] INT NOT NULL PRIMARY KEY,
                        [SourceFileName] NVARCHAR(260) NULL,
                        [FechaOperativa] DATETIME2 NULL,
                        [UpdatedAt] DATETIME2 NOT NULL
                    );
                END;

                IF COL_LENGTH(N'dbo.OperacionesSeguimientoRegistros', N'PesoSugerido') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[OperacionesSeguimientoRegistros]
                    ADD [PesoSugerido] DECIMAL(18,4) NOT NULL
                        CONSTRAINT [DF_OperacionesSeguimientoRegistros_PesoSugerido] DEFAULT(0);
                END;

                IF COL_LENGTH(N'dbo.OperacionesSeguimientoRegistros', N'Procedencia') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[OperacionesSeguimientoRegistros]
                    ADD [Procedencia] NVARCHAR(120) NOT NULL
                        CONSTRAINT [DF_OperacionesSeguimientoRegistros_Procedencia] DEFAULT('');
                END;
                """;

            _context.Database.ExecuteSqlRaw(sql);
        }

        private static OperacionesSeguimientoRegistro MapToEntity(
            SeguimientoToneladasRowViewModel row,
            DateTime defaultDate,
            DateTime uploadedAt)
        {
            var fechaOperativa = (row.FechaEvento ?? defaultDate).Date;

            return new OperacionesSeguimientoRegistro
            {
                FechaOperativa = fechaOperativa,
                FechaEvento = row.FechaEvento,
                Equipo = (row.Equipo ?? string.Empty).Trim(),
                Procedencia = (row.Procedencia ?? string.Empty).Trim(),
                Ruta = string.IsNullOrWhiteSpace(row.Ruta) ? "Sin ruta" : row.Ruta.Trim(),
                Toneladas = row.Toneladas,
                Viajes = row.Viajes <= 0 ? 1 : row.Viajes,
                CombustibleGalones = row.CombustibleLitros,
                PesoSugerido = row.PesoSugerido,
                Conductor = (row.Conductor ?? string.Empty).Trim(),
                Estado = (row.Estado ?? string.Empty).Trim(),
                Eficiencia = (row.Eficiencia ?? string.Empty).Trim(),
                UploadedAt = uploadedAt
            };
        }

        private static SeguimientoToneladasViewModel BuildToneladasModel(List<SeguimientoToneladasRowViewModel> rows)
        {
            var totalToneladas = rows.Sum(r => r.Toneladas);
            var totalViajes = rows.Sum(r => r.Viajes);
            var totalCombustibleGalones = rows.Sum(r => r.CombustibleLitros);
            var rowsConFecha = rows.Where(r => r.FechaEvento.HasValue).ToList();
            var fechaOperativa = rowsConFecha.Any()
                ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                : (DateTime?)null;
            var monthReference = fechaOperativa ?? DateTime.Now.Date;
            var toneladasAcumuladasMes = rows
                .Where(r =>
                {
                    if (!r.FechaEvento.HasValue)
                    {
                        return false;
                    }

                    var d = r.FechaEvento.Value.Date;
                    return d.Year == monthReference.Year && d.Month == monthReference.Month;
                })
                .Sum(r => r.Toneladas);

            var rowsBaseEstado = fechaOperativa.HasValue
                ? rowsConFecha.Where(r => r.FechaEvento!.Value.Date == fechaOperativa.Value).ToList()
                : rows;

            var estadoResumen = BuildEstadoResumen(rowsBaseEstado);
            var toneladasAlDia = rowsBaseEstado
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "FINALIZADO", StringComparison.Ordinal))
                .Sum(r => r.Toneladas);
            var equiposDescargados = estadoResumen.FirstOrDefault(x => x.Estado == "FINALIZADO")?.Equipos ?? 0;
            var equiposEnRuta = estadoResumen.FirstOrDefault(x => x.Estado == "EN RUTA")?.Equipos ?? 0;
            var toneladasEnRuta = rowsBaseEstado
                .Where(r => string.Equals(MapEstadoCategoria(r.Estado), "EN RUTA", StringComparison.Ordinal))
                .Sum(GetToneladasEnRuta);

            return new SeguimientoToneladasViewModel
            {
                Rows = rows,
                EstadoResumen = estadoResumen,
                TotalToneladas = totalToneladas,
                ToneladasAcumuladasMes = toneladasAcumuladasMes,
                TotalViajes = totalViajes,
                PromedioPorViaje = totalViajes > 0 ? Math.Round(totalToneladas / totalViajes, 1) : 0,
                TotalEquipos = rows
                    .Select(r => (r.Equipo ?? string.Empty).Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                ToneladasAlDia = toneladasAlDia,
                TotalCombustibleLitros = totalCombustibleGalones,
                EquiposDescargados = equiposDescargados,
                EquiposEnRuta = equiposEnRuta,
                ToneladasEnRuta = toneladasEnRuta,
                FechaOperativa = fechaOperativa
            };
        }

        private static List<SeguimientoToneladasEstadoResumenViewModel> BuildEstadoResumen(IEnumerable<SeguimientoToneladasRowViewModel> rows)
        {
            return rows
                .Select(r => new
                {
                    Row = r,
                    Categoria = MapEstadoCategoria(r.Estado)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Categoria))
                .GroupBy(x => x.Categoria)
                .Select(g => new SeguimientoToneladasEstadoResumenViewModel
                {
                    Estado = g.Key,
                    Equipos = g
                        .Select(x => (x.Row.Equipo ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Toneladas = g.Sum(x => x.Row.Toneladas),
                    Viajes = g.Sum(x => x.Row.Viajes),
                    EquiposDetalle = string.Join(", ",
                        g.Select(x => (x.Row.Equipo ?? string.Empty).Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase))
                })
                .OrderBy(x => x.Estado == "FINALIZADO" ? 0 : 1)
                .ToList();
        }

        private static string MapEstadoCategoria(string? rawEstado)
        {
            if (string.IsNullOrWhiteSpace(rawEstado))
            {
                return string.Empty;
            }

            var normalized = Normalize(rawEstado);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.Contains("finalizad", StringComparison.Ordinal) ||
                normalized.Contains("descargad", StringComparison.Ordinal) ||
                normalized.Contains("completad", StringComparison.Ordinal) ||
                normalized.Contains("terminad", StringComparison.Ordinal) ||
                normalized.Contains("cerrad", StringComparison.Ordinal))
            {
                return "FINALIZADO";
            }

            if (normalized.Contains("enruta", StringComparison.Ordinal) ||
                normalized.Contains("entrada", StringComparison.Ordinal) ||
                normalized.Contains("encamino", StringComparison.Ordinal) ||
                normalized.Contains("ruta", StringComparison.Ordinal) ||
                normalized.Contains("transito", StringComparison.Ordinal) ||
                normalized.Contains("proceso", StringComparison.Ordinal) ||
                normalized.Contains("pendiente", StringComparison.Ordinal))
            {
                return "EN RUTA";
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static decimal GetToneladasEnRuta(SeguimientoToneladasRowViewModel row)
        {
            return row.PesoSugerido > 0 ? row.PesoSugerido : row.Toneladas;
        }
    }
}
