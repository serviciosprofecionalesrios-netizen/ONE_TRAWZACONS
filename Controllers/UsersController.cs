using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,GerenciaGeneral")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IWebHostEnvironment _environment;

        public UsersController(
            ApplicationDbContext context,
            IUserService userService,
            IPasswordHasher<User> passwordHasher,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userService = userService;
            _passwordHasher = passwordHasher;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var bytes = UsersPdfReportService.GenerateUsersReportPdf(users, _environment.WebRootPath);
            var fileName = $"ReporteUsuarios_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Create()
        {
            await PopulateDepartmentOptionsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDepartmentOptionsAsync(model.Department);
                return View(model);
            }

            var result = await _userService.CreateUserAsync(model);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Error al crear usuario.");
                await PopulateDepartmentOptionsAsync(model.Department);
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var model = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Department = user.Department,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            await PopulateDepartmentOptionsAsync(model.Department);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDepartmentOptionsAsync(model.Department);
                return View(model);
            }

            var user = await _context.Users.FindAsync(model.Id);

            if (user == null)
            {
                return NotFound();
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();
            var emailInUse = await _context.Users
                .AnyAsync(u => u.Id != model.Id && u.Email == normalizedEmail);

            if (emailInUse)
            {
                ModelState.AddModelError(nameof(model.Email), "El correo electrónico ya está registrado.");
                await PopulateDepartmentOptionsAsync(model.Department);
                return View(model);
            }

            user.FullName = model.FullName;
            user.Email = normalizedEmail;
            user.Phone = model.Phone;
            user.Department = model.Department;
            user.Role = model.Role;
            user.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDepartmentOptionsAsync(string? selectedDepartment = null)
        {
            var baseDepartments = new List<string>
            {
                "IT",
                "Operaciones",
                "Administracion",
                "Compras",
                "Contabilidad"
            };

            var dbDepartments = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Department))
                .Select(t => t.Department!.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var departments = baseDepartments
                .Concat(dbDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedDepartment) &&
                departments.All(d => !d.Equals(selectedDepartment, StringComparison.OrdinalIgnoreCase)))
            {
                departments.Insert(0, selectedDepartment);
            }

            ViewBag.DepartmentOptions = departments
                .Select(d => new SelectListItem(
                    text: d,
                    value: d,
                    selected: !string.IsNullOrWhiteSpace(selectedDepartment) && d.Equals(selectedDepartment, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
