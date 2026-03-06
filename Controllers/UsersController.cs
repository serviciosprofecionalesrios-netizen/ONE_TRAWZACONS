using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Users;

namespace ITServiceDeskApp.Controllers
{
    // 🔐 SOLO Administrator y CoordinadorIT pueden administrar usuarios
    [Authorize(Roles = "Administrator,CoordinadorIT")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public UsersController(
            ApplicationDbContext context,
            IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        // ===============================
        // LISTADO
        // ===============================
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .OrderByDescending(u => u.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(users);
        }

        // ===============================
        // CREAR (GET)
        // ===============================
        public IActionResult Create()
        {
            return View();
        }

        // ===============================
        // CREAR (POST)
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _userService.CreateUserAsync(model);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Error al crear usuario.");
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // EDITAR (GET)
        // ===============================
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            var model = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Department = user.Department,
                Role = user.Role,
                IsActive = user.IsActive
            };

            return View(model);
        }

        // ===============================
        // EDITAR (POST)
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users.FindAsync(model.Id);

            if (user == null)
                return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email.Trim().ToLowerInvariant();
            user.Phone = model.Phone;
            user.Department = model.Department;
            user.Role = model.Role;
            user.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // ELIMINAR (Soft Delete)
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            user.IsActive = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}