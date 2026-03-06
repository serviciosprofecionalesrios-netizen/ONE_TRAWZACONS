param(
    [Parameter(Mandatory = $true)]
    [string]$UserId,

    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = 'Stop'

$env:DB_USER = $UserId
$env:DB_PASSWORD = $Password

$testScript = Join-Path $PSScriptRoot 'test-sql-connection.ps1'
if (-not (Test-Path $testScript)) {
    throw "No se encontró el script de prueba: $testScript"
}

Write-Host "Probando conexión SQL antes de iniciar la app..."
powershell -ExecutionPolicy Bypass -File $testScript -UserId $UserId -Password $Password

if ($LASTEXITCODE -ne 0) {
    throw "La prueba de conexión falló. No se iniciará la aplicación."
}

Write-Host "Iniciando app con SQL Authentication para usuario: $UserId"
dotnet run
