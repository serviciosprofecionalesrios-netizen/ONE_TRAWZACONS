using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IOperacionesSeguimientoStore
    {
        void Save(SeguimientoToneladasViewModel model);
        SeguimientoToneladasViewModel? Get();
    }
}
