using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public UserService(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateUserAsync(UserCreateViewModel model)
        {
            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            // Validación preventiva (capa aplicación)
            if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail))
                return (false, "El correo electrónico ya está registrado.");

            var user = new User
            {
                FullName = model.FullName,
                Email = normalizedEmail,
                Phone = model.Phone,
                Department = model.Department,
                Role = model.Role,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Protección ante concurrencia (índice único en BD)
                return (false, "El correo electrónico ya está registrado.");
            }

            return (true, null);
        }
    }
}