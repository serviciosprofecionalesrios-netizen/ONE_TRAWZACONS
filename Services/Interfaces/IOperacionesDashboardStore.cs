using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IOperacionesDashboardStore
    {
        void Save(OperacionesDashboardViewModel model);
        OperacionesDashboardViewModel? Get();
    }
}
