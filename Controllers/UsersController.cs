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
        private const string AllValue = "all";

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

        public async Task<IActionResult> Index(
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page = 1,
            int pageSize = 25)
        {
            var model = await BuildUserListViewModelAsync(search, role, department, status, sortBy, sortDir, page, pageSize);

            if (TempData["UsersMessage"] is string message)
            {
                model.Message = message;
            }

            if (TempData["UsersError"] is string error)
            {
                model.Error = error;
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            string? search,
            string? role,
            string? department,
            string? status)
        {
            var query = BuildFilteredUsersQuery(search, role, department, status);
            var users = await query
                .OrderBy(u => u.FullName)
                .AsNoTracking()
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
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Phone = model.Phone?.Trim() ?? string.Empty;

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

            TempData["UsersMessage"] = "Usuario creado correctamente.";
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
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Phone = model.Phone?.Trim() ?? string.Empty;

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

            TempData["UsersMessage"] = "Usuario actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> CheckEmail(string? email, int? id)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json("El correo electrónico es requerido.");
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == normalizedEmail && (!id.HasValue || u.Id != id.Value));

            return exists
                ? Json("El correo electrónico ya está registrado.")
                : Json(true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> ResetPassword(
            int id,
            string? newPassword,
            string? confirmPassword,
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page = 1,
            int pageSize = 25)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["UsersError"] = "No se encontró el usuario para restablecer contraseña.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["UsersError"] = "La nueva contraseña debe tener al menos 8 caracteres.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                TempData["UsersError"] = "La confirmación de contraseña no coincide.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            if (!MeetsPasswordPolicy(newPassword))
            {
                TempData["UsersError"] = "La contraseña debe incluir mayúscula, minúscula y número.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            await _context.SaveChangesAsync();

            TempData["UsersMessage"] = $"Contraseña restablecida para {user.FullName}.";
            return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> ToggleStatus(
            int id,
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page = 1,
            int pageSize = 25)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                TempData["UsersError"] = "No se encontró el usuario.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            TempData["UsersMessage"] = user.IsActive
                ? $"Usuario {user.FullName} activado." : $"Usuario {user.FullName} desactivado.";

            return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Delete(
            int id,
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page = 1,
            int pageSize = 25)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                TempData["UsersError"] = "No se encontró el usuario.";
                return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["UsersMessage"] = $"Usuario {user.FullName} marcado como inactivo.";
            return RedirectToAction(nameof(Index), BuildRouteValues(search, role, department, status, sortBy, sortDir, page, pageSize));
        }

        private async Task<UserListViewModel> BuildUserListViewModelAsync(
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page,
            int pageSize)
        {
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var normalizedRole = NormalizeFilter(role);
            var normalizedDepartment = NormalizeFilter(department);
            var normalizedStatus = NormalizeStatus(status);
            var normalizedSortBy = NormalizeSortBy(sortBy);
            var normalizedSortDir = NormalizeSortDir(sortDir);
            var normalizedPageSize = pageSize switch
            {
                <= 10 => 10,
                <= 25 => 25,
                <= 50 => 50,
                _ => 100
            };

            var query = BuildFilteredUsersQuery(normalizedSearch, normalizedRole, normalizedDepartment, normalizedStatus);
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)normalizedPageSize));
            var normalizedPage = Math.Clamp(page, 1, totalPages);

            query = ApplySort(query, normalizedSortBy, normalizedSortDir);

            var users = await query
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(u => new UserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Department = u.Department,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            var roleOptions = new List<UserListOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" }
            };

            roleOptions.AddRange(Enum.GetValues<UserRole>()
                .Select(r => new UserListOptionViewModel
                {
                    Value = r.ToString(),
                    Label = r.ToString()
                }));

            var departments = await _context.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrWhiteSpace(u.Department))
                .Select(u => u.Department.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var departmentOptions = new List<UserListOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" }
            };
            departmentOptions.AddRange(departments.Select(d => new UserListOptionViewModel { Value = d, Label = d }));

            var statusOptions = new List<UserListOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = "active", Label = "Activo" },
                new() { Value = "inactive", Label = "Inactivo" }
            };

            return new UserListViewModel
            {
                Search = normalizedSearch,
                SelectedRole = normalizedRole,
                SelectedDepartment = normalizedDepartment,
                SelectedStatus = normalizedStatus,
                SortBy = normalizedSortBy,
                SortDir = normalizedSortDir,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Users = users,
                RoleOptions = roleOptions,
                DepartmentOptions = departmentOptions,
                StatusOptions = statusOptions
            };
        }

        private IQueryable<User> BuildFilteredUsersQuery(string? search, string? role, string? department, string? status)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.Phone.Contains(search) ||
                    u.Department.Contains(search));
            }

            var normalizedRole = NormalizeFilter(role);
            if (normalizedRole != AllValue && Enum.TryParse<UserRole>(normalizedRole, true, out var parsedRole))
            {
                query = query.Where(u => u.Role == parsedRole);
            }

            var normalizedDepartment = NormalizeFilter(department);
            if (normalizedDepartment != AllValue)
            {
                query = query.Where(u => u.Department == normalizedDepartment);
            }

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == "active")
            {
                query = query.Where(u => u.IsActive);
            }
            else if (normalizedStatus == "inactive")
            {
                query = query.Where(u => !u.IsActive);
            }

            return query;
        }

        private static IQueryable<User> ApplySort(IQueryable<User> query, string sortBy, string sortDir)
        {
            var ascending = sortDir == "asc";

            return sortBy switch
            {
                "name" => ascending ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
                "email" => ascending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "role" => ascending ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
                "department" => ascending ? query.OrderBy(u => u.Department) : query.OrderByDescending(u => u.Department),
                "status" => ascending ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
                _ => ascending ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt)
            };
        }

        private static object BuildRouteValues(
            string? search,
            string? role,
            string? department,
            string? status,
            string? sortBy,
            string? sortDir,
            int page,
            int pageSize)
        {
            return new
            {
                search,
                role,
                department,
                status,
                sortBy,
                sortDir,
                page,
                pageSize
            };
        }

        private static bool MeetsPasswordPolicy(string password)
        {
            return password.Any(char.IsUpper)
                && password.Any(char.IsLower)
                && password.Any(char.IsDigit);
        }

        private static string NormalizeFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? AllValue
                : value.Trim();
        }

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AllValue;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is "active" or "inactive" ? normalized : AllValue;
        }

        private static string NormalizeSortBy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "created";
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is "name" or "email" or "role" or "department" or "status" or "created"
                ? normalized
                : "created";
        }

        private static string NormalizeSortDir(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "desc";
            }

            return value.Trim().Equals("asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
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

            var userDepartments = await _context.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrWhiteSpace(u.Department))
                .Select(u => u.Department.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var departments = baseDepartments
                .Concat(dbDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Concat(userDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d)
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
    }
}
