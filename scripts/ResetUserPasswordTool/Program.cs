using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

if (args.Length < 4)
{
    Console.WriteLine("Uso: dotnet run --project scripts/ResetUserPasswordTool -- <server> <database> <email> <newPassword> [sqlUser] [sqlPassword]");
    return;
}

var server = args[0];
var database = args[1];
var email = args[2];
var newPassword = args[3];

var sqlUser = args.Length >= 5 ? args[4] : null;
var sqlPassword = args.Length >= 6 ? args[5] : null;

var connBuilder = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = database,
    Encrypt = false,
    TrustServerCertificate = true
};

if (!string.IsNullOrWhiteSpace(sqlUser) && !string.IsNullOrWhiteSpace(sqlPassword))
{
    connBuilder.IntegratedSecurity = false;
    connBuilder.UserID = sqlUser;
    connBuilder.Password = sqlPassword;
}
else
{
    connBuilder.IntegratedSecurity = true;
}

var hasher = new PasswordHasher<object>();
var passwordHash = hasher.HashPassword(new object(), newPassword);

await using var conn = new SqlConnection(connBuilder.ConnectionString);
await conn.OpenAsync();

const string sql = @"
UPDATE Users
SET PasswordHash = @PasswordHash,
    IsActive = 1
WHERE Email = @Email;

SELECT @@ROWCOUNT;
";

await using var cmd = new SqlCommand(sql, conn);
cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

var affected = Convert.ToInt32(await cmd.ExecuteScalarAsync());

if (affected == 0)
{
    Console.WriteLine($"No se encontró usuario con email: {email}");
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Contraseña restablecida correctamente para: {email}");
