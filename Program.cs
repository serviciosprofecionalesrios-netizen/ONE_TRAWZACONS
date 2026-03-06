using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// SERVICIOS
// ======================================================

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ======================================================
// MIGRACIÓN + ADMIN INICIAL
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ApplicationDbContext>();
    var hasher = services.GetRequiredService<IPasswordHasher<User>>();

    context.Database.Migrate();

    // Crear admin solo si no existe
    if (!context.Users.Any(u => u.Email == "admin@transan.com"))
    {
        var admin = new User
        {
            FullName = "Administrador",
            Email = "admin@transan.com",

            // ✅ CORRECCIÓN REAL AQUÍ
            Role = UserRole.Administrator,

            Department = "IT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        admin.PasswordHash = hasher.HashPassword(admin, "Admin123*");

        context.Users.Add(admin);
        context.SaveChanges();
    }
}

// ======================================================
// PIPELINE
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}"
);

app.Run();