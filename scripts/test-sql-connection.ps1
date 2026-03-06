param(
    [string]$Server,
    [string]$Database,
    [string]$UserId,
    [string]$Password,
    [switch]$UseIntegrated
)

$ErrorActionPreference = 'Stop'

function Get-Settings {
    $settingsPath = Join-Path (Get-Location) 'appsettings.Development.json'
    if (-not (Test-Path $settingsPath)) {
        throw "No se encontró $settingsPath"
    }

    return Get-Content $settingsPath -Raw | ConvertFrom-Json
}

function Get-ConnectionDefaults {
    param([object]$Settings)

    $connString = $Settings.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($connString)) {
        throw "No existe ConnectionStrings:DefaultConnection en appsettings.Development.json"
    }

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connString

    return [pscustomobject]@{
        Server = $builder.DataSource
        Database = $builder.InitialCatalog
        SqlAuthEnabled = [bool]$Settings.SqlAuth.Enabled
        SqlAuthUser = [string]$Settings.SqlAuth.UserId
        SqlAuthPassword = [string]$Settings.SqlAuth.Password
    }
}

Add-Type -AssemblyName System.Data
# System.Data.SqlClient viene con el runtime de .NET

$settings = Get-Settings
$defaults = Get-ConnectionDefaults -Settings $settings

if ([string]::IsNullOrWhiteSpace($Server)) {
    $Server = $defaults.Server
}

if ([string]::IsNullOrWhiteSpace($Database)) {
    $Database = $defaults.Database
}

if (-not $UseIntegrated) {
    if ([string]::IsNullOrWhiteSpace($UserId) -and -not [string]::IsNullOrWhiteSpace($env:DB_USER)) {
        $UserId = $env:DB_USER
    }

    if ([string]::IsNullOrWhiteSpace($Password) -and -not [string]::IsNullOrWhiteSpace($env:DB_PASSWORD)) {
        $Password = $env:DB_PASSWORD
    }

    if ([string]::IsNullOrWhiteSpace($UserId) -and $defaults.SqlAuthEnabled) {
        $UserId = $defaults.SqlAuthUser
    }

    if ([string]::IsNullOrWhiteSpace($Password) -and $defaults.SqlAuthEnabled) {
        $Password = $defaults.SqlAuthPassword
    }
}

$useSqlAuth = -not $UseIntegrated -and -not [string]::IsNullOrWhiteSpace($UserId) -and -not [string]::IsNullOrWhiteSpace($Password)

if ($useSqlAuth -and $Password -eq 'CAMBIAR_AQUI_TU_PASSWORD_SQL') {
    throw 'Debes reemplazar la contraseña placeholder de SqlAuth antes de probar conexión.'
}

if ($useSqlAuth) {
    $connectionString = "Server=$Server;Database=$Database;User ID=$UserId;Password=$Password;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;"
    Write-Host "Modo: SQL Authentication" -ForegroundColor Cyan
}
else {
    $connectionString = "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;"
    Write-Host "Modo: Windows Integrated" -ForegroundColor Cyan
}

$conn = New-Object System.Data.SqlClient.SqlConnection $connectionString

try {
    Write-Host "Conectando a servidor: $Server" -ForegroundColor Yellow
    Write-Host "Base de datos: $Database" -ForegroundColor Yellow

    $conn.Open()

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    SYSTEM_USER AS SystemUser,
    SUSER_SNAME() AS LoginName,
    GETDATE() AS ServerTime,
    1 AS HealthCheck;
"@

    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Conexion OK" -ForegroundColor Green
        Write-Host "ServerName : $($reader['ServerName'])"
        Write-Host "Database   : $($reader['DatabaseName'])"
        Write-Host "SystemUser : $($reader['SystemUser'])"
        Write-Host "LoginName  : $($reader['LoginName'])"
        Write-Host "ServerTime : $($reader['ServerTime'])"
        Write-Host "Health     : $($reader['HealthCheck'])"
    }

    $reader.Close()
}
catch {
    Write-Host "Conexion fallida" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    if ($conn.State -ne 'Closed') {
        $conn.Close()
    }
}

