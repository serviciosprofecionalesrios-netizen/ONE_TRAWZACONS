param(
    [Parameter(Mandatory = $false)]
    [string]$Server = "DESKTOP-5QF4PAU",

    [Parameter(Mandatory = $false)]
    [string]$Database = "TrawzaconsDB",

    [Parameter(Mandatory = $true)]
    [string]$UserId,

    [Parameter(Mandatory = $false)]
    [SecureString]$Password
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'ITServiceDeskApp.csproj'

if (-not (Test-Path $projectFile)) {
    throw "No se encontró el proyecto en: $projectFile"
}

if (-not $Password) {
    $Password = Read-Host "Ingresa la contraseña SQL para $UserId" -AsSecureString
}

$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
}
finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($plainPassword)) {
    throw "La contraseña no puede quedar vacía."
}

$baseConnectionString = "Server=$Server;Database=$Database;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True"

Write-Host "Guardando secretos seguros de SQL (User Secrets)..." -ForegroundColor Cyan
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $baseConnectionString --project $projectFile | Out-Null
dotnet user-secrets set "SqlAuth:Enabled" "true" --project $projectFile | Out-Null
dotnet user-secrets set "SqlAuth:UserId" $UserId --project $projectFile | Out-Null
dotnet user-secrets set "SqlAuth:Password" $plainPassword --project $projectFile | Out-Null

Write-Host "Validando conexión SQL..." -ForegroundColor Cyan
$testScript = Join-Path $PSScriptRoot 'test-sql-connection.ps1'
powershell -ExecutionPolicy Bypass -File $testScript -Server $Server -Database $Database -UserId $UserId -Password $plainPassword

if ($LASTEXITCODE -ne 0) {
    throw "No se pudo validar la conexión SQL con los secretos configurados."
}

Write-Host "Configuración fija y segura completada. La app ya no necesita DB_USER/DB_PASSWORD." -ForegroundColor Green

