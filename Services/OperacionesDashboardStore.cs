using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services
{
    public sealed class OperacionesDashboardStore : IOperacionesDashboardStore
    {
        private readonly object _syncRoot = new();
        private OperacionesDashboardViewModel? _latest;

        public void Save(OperacionesDashboardViewModel model)
        {
            lock (_syncRoot)
            {
                _latest = Clone(model);
            }
        }

        public OperacionesDashboardViewModel? Get()
        {
            lock (_syncRoot)
            {
                return _latest is null ? null : Clone(_latest);
            }
        }

        private static OperacionesDashboardViewModel Clone(OperacionesDashboardViewModel source)
        {
            return new OperacionesDashboardViewModel
            {
                DespachosDelDia = source.DespachosDelDia,
                ToneladasMovilizadas = source.ToneladasMovilizadas,
                DieselConsumidoGalones = source.DieselConsumidoGalones,
                DieselConsumidoLitros = source.DieselConsumidoLitros,
                CumplimientoPlanPorcentaje = source.CumplimientoPlanPorcentaje,
                MetaDiaria = source.MetaDiaria,
                ToneladasEnRutaActual = source.ToneladasEnRutaActual,
                PrediccionToneladasManana = source.PrediccionToneladasManana,
                CumplimientoEstimadoMananaPorcentaje = source.CumplimientoEstimadoMananaPorcentaje,
                TopConductores = source.TopConductores
                    .Select(x => new OperacionesDashboardRankingItemViewModel
                    {
                        Nombre = x.Nombre,
                        Toneladas = x.Toneladas
                    }).ToList(),
                TopEquipos = source.TopEquipos
                    .Select(x => new OperacionesDashboardRankingItemViewModel
                    {
                        Nombre = x.Nombre,
                        Toneladas = x.Toneladas
                    }).ToList(),
                DistribucionProcedencia = source.DistribucionProcedencia
                    .Select(x => new OperacionesDashboardDistribucionItemViewModel
                    {
                        Categoria = x.Categoria,
                        Toneladas = x.Toneladas
                    }).ToList(),
                TendenciaDiaria = source.TendenciaDiaria
                    .Select(x => new OperacionesDashboardTendenciaItemViewModel
                    {
                        Fecha = x.Fecha,
                        Toneladas = x.Toneladas
                    }).ToList(),
                IsFromUpload = source.IsFromUpload,
                SourceFileName = source.SourceFileName,
                LastUpdatedAt = source.LastUpdatedAt
            };
        }
    }
}
