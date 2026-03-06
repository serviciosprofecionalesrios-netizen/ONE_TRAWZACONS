using ITServiceDeskApp.ViewModels.Users;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IUserService
    {
        Task<(bool Success, string? ErrorMessage)> CreateUserAsync(UserCreateViewModel model);
    }
}