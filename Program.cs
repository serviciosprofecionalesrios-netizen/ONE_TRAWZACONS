using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using System.Data.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// The existing SQL Server model stores DateTime without timezone information
// and the application contains both local and UTC values. Keep that established
// behavior when Npgsql writes to PostgreSQL's timestamp-without-time-zone columns.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.WebHost.ConfigureKestrel(options =>
{
    // Avoid 400 responses when the browser sends large localhost cookie headers.
    options.Limits.MaxRequestHeadersTotalSize = 128 * 1024;
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("PublishedInventory", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<PublishedInventoryService>();

var databaseProvider = builder.Configuration["Database:Provider"]?.Trim() ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se encontró la cadena de conexión 'DefaultConnection'. Revisa appsettings.json o variables de entorno.");
}

if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
    databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    connectionString = NormalizePostgresConnectionString(connectionString);
    builder.Services.AddDbContext<ApplicationDbContext, PostgresApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, postgresOptions =>
        {
            postgresOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            postgresOptions.CommandTimeout(30);
        }));
}
else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var envDbUser = Environment.GetEnvironmentVariable("DB_USER");
    var envDbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
    var sqlAuthEnabled = builder.Configuration.GetValue<bool>("SqlAuth:Enabled");
    var selectedUser = !string.IsNullOrWhiteSpace(envDbUser)
        ? envDbUser
        : sqlAuthEnabled ? builder.Configuration["SqlAuth:UserId"] : null;
    var selectedPassword = !string.IsNullOrWhiteSpace(envDbPassword)
        ? envDbPassword
        : sqlAuthEnabled ? builder.Configuration["SqlAuth:Password"] : null;

    if (sqlAuthEnabled && (string.IsNullOrWhiteSpace(selectedUser) || string.IsNullOrWhiteSpace(selectedPassword)))
    {
        throw new InvalidOperationException(
            "SqlAuth está habilitado, pero faltan SqlAuth:UserId o SqlAuth:Password en la configuración.");
    }

    if (!string.IsNullOrWhiteSpace(selectedUser) && !string.IsNullOrWhiteSpace(selectedPassword))
    {
        var connBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            IntegratedSecurity = false,
            UserID = selectedUser,
            Password = selectedPassword,
            Encrypt = true,
            TrustServerCertificate = builder.Environment.IsDevelopment()
        };
        connectionString = connBuilder.ConnectionString;
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            sqlOptions.CommandTimeout(30);
        }));
}
else
{
    throw new InvalidOperationException(
        $"Proveedor de base de datos no válido: '{databaseProvider}'. Usa 'SqlServer' o 'Postgres'.");
}

var dataProtectionPath = builder.Configuration["Storage:DataProtectionKeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    Directory.CreateDirectory(dataProtectionPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
        .SetApplicationName("ITServiceDeskApp");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IWhatsAppNotificationService, WhatsAppNotificationService>();
builder.Services.AddSingleton<IOperacionesDashboardStore, OperacionesDashboardStore>();
builder.Services.AddScoped<IOperacionesSeguimientoStore, OperacionesSeguimientoStore>();
builder.Services.AddScoped<IOperacionesIngresoPersonalTraceStore, OperacionesIngresoPersonalTraceStore>();
builder.Services.AddScoped<IOperacionesIngresoPersonalRequestStore, OperacionesIngresoPersonalRequestStore>();
builder.Services.AddScoped<IOperacionesIngresoEquipoGondolaTraceStore, OperacionesIngresoEquipoGondolaTraceStore>();
builder.Services.AddScoped<IOperacionesIngresoEquipoGondolaRequestStore, OperacionesIngresoEquipoGondolaRequestStore>();
builder.Services.AddHttpClient("TwilioWhatsApp", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

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
var allowStartupWithoutDatabase = app.Environment.IsDevelopment();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();

        context.Database.Migrate();

        var bootstrapEmail = builder.Configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant()
            ?? "admin@trawzacons.com";

        if (!context.Users.Any(u => u.Email == bootstrapEmail))
        {
            var admin = new User
            {
                FullName = "Administrador",
                Email = bootstrapEmail,
                Role = UserRole.Administrator,
                Department = "IT",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var bootstrapPassword = builder.Configuration["BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(bootstrapPassword))
            {
                if (!app.Environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Falta BootstrapAdmin__Password para crear el primer administrador.");
                }

                bootstrapPassword = "Admin123*";
            }

            admin.PasswordHash = hasher.HashPassword(admin, bootstrapPassword);

            context.Users.Add(admin);
            context.SaveChanges();
        }
    }
    catch (DbException ex)
    {
        logger.LogError(ex,
            "Error al conectar con {DatabaseProvider}. Verifica credenciales, red y cadena de conexión.",
            databaseProvider);
        if (!allowStartupWithoutDatabase)
        {
            Environment.ExitCode = 1;
            return;
        }

        logger.LogWarning("La aplicación continuará en Development sin inicializar la base de datos.");
    }
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (ApplicationDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ok", database = "connected" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Operaciones}/{action=Dashboard}/{id?}");

app.Run();

static string NormalizePostgresConnectionString(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
    {
        return value;
    }

    var credentials = uri.UserInfo.Split(':', 2);
    if (credentials.Length != 2)
    {
        throw new InvalidOperationException("La URL de PostgreSQL no contiene usuario y contraseña.");
    }

    var connectionBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = Npgsql.SslMode.Prefer,
        Pooling = true,
        MaxPoolSize = 20,
        Timeout = 15
    };

    return connectionBuilder.ConnectionString;
}






