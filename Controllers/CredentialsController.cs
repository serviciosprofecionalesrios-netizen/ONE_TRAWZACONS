using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,Technician,GerenciaGeneral")]
    public class CredentialsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CredentialsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? type, bool includeInactive = false)
        {
            IQueryable<Credential> query = _context.Credentials.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term) ||
                    c.Username.Contains(term) ||
                    (c.EnvironmentOrLocation != null && c.EnvironmentOrLocation.Contains(term)) ||
                    (c.AccessUrlOrHost != null && c.AccessUrlOrHost.Contains(term)) ||
                    (c.Notes != null && c.Notes.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(c => c.Type == type);
            }

            var items = await query
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToListAsync();

            await PopulateTypeOptionsAsync(type);
            ViewBag.Search = search;
            ViewBag.IncludeInactive = includeInactive;
            ViewBag.CanManageCredentials = CanManageCredentials();

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var credential = await _context.Credentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (credential == null)
            {
                return NotFound();
            }

            ViewBag.CanManageCredentials = CanManageCredentials();
            return View(credential);
        }

        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> Create()
        {
            await PopulateTypeOptionsAsync(null);
            return View(new Credential { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> Create(Credential credential)
        {
            NormalizeCredential(credential);
            ValidateCredentialBusinessRules(credential);

            if (await _context.Credentials.AnyAsync(c => c.Name == credential.Name && c.Type == credential.Type))
            {
                ModelState.AddModelError(nameof(Credential.Name), "Ya existe una credencial con ese nombre y tipo.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateTypeOptionsAsync(credential.Type);
                return View(credential);
            }

            credential.CreatedAt = DateTime.UtcNow;
            credential.UpdatedAt = DateTime.UtcNow;

            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> Edit(int id)
        {
            var credential = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == id);
            if (credential == null)
            {
                return NotFound();
            }

            await PopulateTypeOptionsAsync(credential.Type);
            return View(credential);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> Edit(int id, Credential credential)
        {
            if (id != credential.Id)
            {
                return NotFound();
            }

            if (credential.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia de la credencial.");
                await PopulateTypeOptionsAsync(credential.Type);
                return View(credential);
            }

            var dbCredential = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == id);
            if (dbCredential == null)
            {
                return NotFound();
            }

            NormalizeCredential(credential);
            ValidateCredentialBusinessRules(credential);

            if (await _context.Credentials.AnyAsync(c => c.Id != credential.Id && c.Name == credential.Name && c.Type == credential.Type))
            {
                ModelState.AddModelError(nameof(Credential.Name), "Ya existe una credencial con ese nombre y tipo.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateTypeOptionsAsync(credential.Type);
                return View(credential);
            }

            _context.Entry(dbCredential).Property(c => c.RowVersion).OriginalValue = credential.RowVersion;

            dbCredential.Name = credential.Name;
            dbCredential.Type = credential.Type;
            dbCredential.EnvironmentOrLocation = credential.EnvironmentOrLocation;
            dbCredential.AccessUrlOrHost = credential.AccessUrlOrHost;
            dbCredential.Port = credential.Port;
            dbCredential.Username = credential.Username;
            dbCredential.Secret = credential.Secret;
            dbCredential.Notes = credential.Notes;
            dbCredential.IsActive = credential.IsActive;
            dbCredential.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "La credencial fue modificada por otro usuario. Recarga e intenta de nuevo.");
                credential.RowVersion = dbCredential.RowVersion;
                await PopulateTypeOptionsAsync(credential.Type);
                return View(credential);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var credential = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == id);
            if (credential == null)
            {
                return NotFound();
            }

            credential.IsActive = !credential.IsActive;
            credential.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician")]
        public async Task<IActionResult> Delete(int id)
        {
            var credential = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == id);
            if (credential == null)
            {
                return NotFound();
            }

            _context.Credentials.Remove(credential);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool CanManageCredentials()
        {
            return User.IsInRole(nameof(UserRole.Administrator)) ||
                   User.IsInRole(nameof(UserRole.Technician));
        }

        private static void NormalizeCredential(Credential credential)
        {
            credential.Name = credential.Name.Trim();
            credential.Type = credential.Type.Trim();
            credential.EnvironmentOrLocation = NormalizeNullable(credential.EnvironmentOrLocation);
            credential.AccessUrlOrHost = NormalizeNullable(credential.AccessUrlOrHost);
            credential.Port = NormalizeNullable(credential.Port);
            credential.Username = credential.Username.Trim();
            credential.Secret = credential.Secret.Trim();
            credential.Notes = NormalizeNullable(credential.Notes);
        }

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private void ValidateCredentialBusinessRules(Credential credential)
        {
            if (string.IsNullOrWhiteSpace(credential.Port))
            {
                return;
            }

            var portValue = credential.Port.Trim();
            if (!portValue.All(char.IsDigit))
            {
                return;
            }

            if (!int.TryParse(portValue, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
            {
                ModelState.AddModelError(nameof(Credential.Port), "El puerto numerico debe estar entre 1 y 65535.");
            }
        }

        private async Task PopulateTypeOptionsAsync(string? selectedType)
        {
            var baseTypes = new List<string>
            {
                "Servidor",
                "Correo",
                "CCTV",
                "Base de Datos",
                "Firewall",
                "Router",
                "Switch",
                "VPN",
                "Sistema Interno",
                "Otro"
            };

            var dbTypes = await _context.Credentials
                .AsNoTracking()
                .Where(c => !string.IsNullOrWhiteSpace(c.Type))
                .Select(c => c.Type.Trim())
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var types = baseTypes
                .Concat(dbTypes.Where(t => !baseTypes.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(t => new SelectListItem(
                    text: t,
                    value: t,
                    selected: !string.IsNullOrWhiteSpace(selectedType) && t.Equals(selectedType, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.TypeOptions = types;
        }
    }
}
