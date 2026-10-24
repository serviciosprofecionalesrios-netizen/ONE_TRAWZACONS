param([Parameter(Mandatory=$true)][string]$CsvPath)
$ErrorActionPreference = 'Stop'
# The export contains multiline quoted fields; parse the complete text.
$text = Get-Content -LiteralPath $CsvPath -Raw -Encoding ansi
$newline = $text.IndexOf("`n")
$headers = $text.Substring(0, $newline).TrimEnd("`r").Split(';')
$rows = @(ConvertFrom-Csv -InputObject $text.Substring($newline + 1) -Delimiter ';' -Header $headers)
$destination = Join-Path $PSScriptRoot '../Data/Operaciones/ControlDocumentosPersonas.json'
$people = @()
if (Test-Path $destination) { $people = @(Get-Content $destination -Raw | ConvertFrom-Json -AsHashtable) }
$byId = @{}
foreach ($row in $rows) { if ($row.'#') { $byId[$row.'#'] = $row } }
$imported = 0
$trainingDates = 0
foreach ($root in @($rows | Where-Object { $_.Tipo -eq '1.Solicitud Ingreso El Limón' } | Sort-Object Actualizado)) {
    $name = $root.'Nombres y Apellidos'.Trim()
    if (!$name) { $name = $root.Asunto.Trim() }
    if (!$name) { continue }
    $person = $people | Where-Object { $_.Nombre.Trim().ToUpperInvariant() -eq $name.ToUpperInvariant() } | Select-Object -First 1
    if (!$person) {
        $person = @{ Nombre=$name; Fechas=@{}; Datos=@{}; CreatedAt=$root.Creado; UpdatedAt=$root.Actualizado }
        $people += $person
    }
    if (!$person.Datos) { $person.Datos = @{} }
    $related = @($rows | Where-Object { $_.'Tarea padre' -eq $root.'#' } | Sort-Object Actualizado)
    foreach ($record in @($root) + $related) {
        foreach ($header in $headers[23..($headers.Count-1)]) {
            $value = [string]$record.$header
            if (![string]::IsNullOrWhiteSpace($value)) { $person.Datos[$header] = $value.Trim() }
        }
        $person.Datos['Estado ' + $record.Tipo] = $record.Estado.Trim()
        foreach ($header in $headers[59..75]) {
            $value = [string]$record.$header
            if ($value -match '^\d{4}-\d{2}-\d{2}$') {
                $person.Fechas[$header] = [datetime]::ParseExact($value,'yyyy-MM-dd',$null).ToString('dd/MM/yyyy')
                $trainingDates++
            }
        }
    }
    $person.Datos['Solicitud de origen'] = $root.'#'
    $person.Datos['Estado de solicitud'] = $root.Estado.Trim()
    if ($person.Datos['Fecha Inicio Contrato']) {
        $person.Fechas['Contrato de Trabajo'] = ([datetime]$person.Datos['Fecha Inicio Contrato']).ToString('dd/MM/yyyy')
    }
    $imported++
}
foreach ($person in $people) {
    foreach ($field in @('CreatedAt','UpdatedAt')) {
        $person[$field] = ([datetime]$person[$field]).ToString('yyyy-MM-ddTHH:mm:ss')
    }
}
New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
if (Test-Path $destination) { Copy-Item $destination ($destination + '.before-import.bak') -ErrorAction Stop }
ConvertTo-Json -InputObject @($people | Sort-Object Nombre) -Depth 8 | Set-Content -LiteralPath $destination -Encoding utf8
Write-Output "Solicitudes: $imported; personas conservadas: $($people.Count); fechas de capacitacion: $trainingDates"
