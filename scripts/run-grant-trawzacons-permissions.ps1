param(
    [string]$Server = "DESKTOP-5QF4PAU",
    [string]$Database = "master",
    [string]$UserId = "",
    [string]$Password = ""
)

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot "sql\grant_trawzacons_permissions.sql"
if (-not (Test-Path $scriptPath)) {
    throw "No se encontró el script SQL: $scriptPath"
}

$sqlText = Get-Content $scriptPath -Raw
$batches = [System.Text.RegularExpressions.Regex]::Split(
    $sqlText,
    '(?im)^\s*GO\s*$(\r?\n)?'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Add-Type -AssemblyName System.Data

if ([string]::IsNullOrWhiteSpace($UserId) -or [string]::IsNullOrWhiteSpace($Password)) {
    $connectionString = "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"
    Write-Host "Modo de conexión: Windows Integrated"
}
else {
    $connectionString = "Server=$Server;Database=$Database;User ID=$UserId;Password=$Password;Encrypt=False;TrustServerCertificate=True;"
    Write-Host "Modo de conexión: SQL Authentication"
}

$conn = New-Object System.Data.SqlClient.SqlConnection $connectionString

try {
    Write-Host "Conectando a SQL Server: $Server"
    $conn.Open()

    foreach ($batch in $batches) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 120
        $cmd.CommandText = $batch
        [void]$cmd.ExecuteNonQuery()
    }

    Write-Host "Script ejecutado correctamente."
}
finally {
    if ($conn.State -ne 'Closed') {
        $conn.Close()
    }
}

