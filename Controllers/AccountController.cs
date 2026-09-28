using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ITServiceDeskApp.Security;

namespace ITServiceDeskApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IWebHostEnvironment _environment;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            IWebHostEnvironment environment)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Debe ingresar email y contraseña.");
                return View();
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();

            User? user;

            try
            {
                user = await _context.Users
                    .Where(u => u.IsActive)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            }
            catch (SqlException) when (_environment.IsDevelopment())
            {
                user = new User
                {
                    Id = 0,
                    FullName = "Administrador Demo",
                    Email = "admin@trawzacons.com",
                    Role = UserRole.Administrator,
                    Department = "IT",
                    IsActive = true
                };
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
                return View();
            }

            var verificationResult = user.Id == 0 && _environment.IsDevelopment()
                ? PasswordVerificationResult.Success
                : _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
                return View();
            }

            var accessProfile = UserAccessProfiles.Resolve(user.Email);
            var effectiveRole = accessProfile is null ? user.Role.ToString() : UserRole.Administrator.ToString();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, effectiveRole),
                new Claim("UserId", user.Id.ToString())
            };

            if (accessProfile is not null)
            {
                claims.Add(new Claim(UserAccessProfiles.ClaimType, accessProfile));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            // Los perfiles configurados deben iniciar siempre en su módulo permitido,
            // aunque hayan llegado al login desde una URL que no les corresponde.
            if (accessProfile is null && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var home = UserAccessProfiles.Home(accessProfile);
            return RedirectToAction(home.Action, home.Controller);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied()
        {
            var profile = User.FindFirst(UserAccessProfiles.ClaimType)?.Value;
            var home = UserAccessProfiles.Home(profile);
            ViewData["HomeController"] = home.Controller;
            ViewData["HomeAction"] = home.Action;
            return View();
        }
    }
}
