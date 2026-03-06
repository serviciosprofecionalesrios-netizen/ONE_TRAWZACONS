using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se encontró la cadena de conexión 'DefaultConnection'. Revisa appsettings.json o variables de entorno.");
}

var envDbUser = Environment.GetEnvironmentVariable("DB_USER");
var envDbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

var sqlAuthEnabled = builder.Configuration.GetValue<bool>("SqlAuth:Enabled");
var sqlAuthUser = builder.Configuration["SqlAuth:UserId"];
var sqlAuthPassword = builder.Configuration["SqlAuth:Password"];

string? selectedUser = null;
string? selectedPassword = null;

if (!string.IsNullOrWhiteSpace(envDbUser) && !string.IsNullOrWhiteSpace(envDbPassword))
{
    selectedUser = envDbUser;
    selectedPassword = envDbPassword;
}
else if (sqlAuthEnabled)
{
    if (string.IsNullOrWhiteSpace(sqlAuthUser) || string.IsNullOrWhiteSpace(sqlAuthPassword))
    {
        throw new InvalidOperationException(
            "SqlAuth está habilitado, pero faltan SqlAuth:UserId o SqlAuth:Password en la configuración.");
    }

    selectedUser = sqlAuthUser;
    selectedPassword = sqlAuthPassword;
}

if (!string.IsNullOrWhiteSpace(selectedUser) && !string.IsNullOrWhiteSpace(selectedPassword))
{
    var connBuilder = new SqlConnectionStringBuilder(connectionString)
    {
        IntegratedSecurity = false,
        UserID = selectedUser,
        Password = selectedPassword,
        Encrypt = false,
        TrustServerCertificate = true
    };

    connectionString = connBuilder.ConnectionString;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
    }));

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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();

        context.Database.Migrate();

        if (!context.Users.Any(u => u.Email == "admin@transan.com"))
        {
            var admin = new User
            {
                FullName = "Administrador",
                Email = "admin@transan.com",
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
    catch (SqlException ex) when (ex.Message.Contains("SSPI", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogError(ex,
            "Error SSPI al autenticarse con SQL Server. Si usas autenticación integrada, verifica tu sesión de Windows. " +
            "Alternativa: habilita SqlAuth en appsettings o define DB_USER y DB_PASSWORD.");
        Environment.ExitCode = 1;
        return;
    }
    catch (SqlException ex) when (ex.Message.Contains("encryption", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogError(ex,
            "Error de cifrado al conectar con SQL Server. Usa Encrypt=False o un certificado TLS válido en el servidor.");
        Environment.ExitCode = 1;
        return;
    }
    catch (SqlException ex)
    {
        logger.LogError(ex,
            "Error al conectar con SQL Server. Verifica instancia, credenciales y cadena de conexión.");
        Environment.ExitCode = 1;
        return;
    }
}

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
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
