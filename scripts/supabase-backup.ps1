#Requires -Version 5.1
<#
.SYNOPSIS
    Script automatizado de copias de seguridad para PostgreSQL en Supabase.

.DESCRIPTION
    Realiza un volcado completo de la base de datos de producción de Supabase
    en formato comprimido (.sql.gz), con marca temporal y política de retención
    para purgar automáticamente copias con más de N días de antigüedad.

.PARAMETER ConnectionString
    Cadena de conexión de PostgreSQL (Host=... o postgresql://...).
    Si se omite, se intentará leer de la variable de entorno SUPABASE_DB_URL
    o ConnectionStrings__DefaultConnection.

.PARAMETER OutputDir
    Directorio donde se almacenarán las copias de seguridad. Predeterminado: './backups'.

.PARAMETER RetentionDays
    Número de días durante los cuales se conservarán las copias antes de ser purgadas. Predeterminado: 7.

.EXAMPLE
    .\scripts\supabase-backup.ps1 -OutputDir "C:\backups\ludeka" -RetentionDays 14
#>
param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$ConnectionString = $env:SUPABASE_DB_URL,

    [Parameter(Position = 1, Mandatory = $false)]
    [string]$OutputDir = "./backups",

    [Parameter(Position = 2, Mandatory = $false)]
    [int]$RetentionDays = 7
)

$ErrorActionPreference = 'Stop'

function Write-Info { param($msg) Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "[AVISO] $msg" -ForegroundColor Yellow }
function Write-Err  { param($msg) Write-Host "[ERROR] $msg" -ForegroundColor Red }

# 1. Resolver cadena de conexión
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:ConnectionStrings__DefaultConnection
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Err "No se ha proporcionado una cadena de conexión. Pasa -ConnectionString o define la variable de entorno SUPABASE_DB_URL."
    exit 1
}

# 2. Asegurar directorio de destino
$resolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path -LiteralPath $resolvedOutputDir)) {
    New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null
    Write-Info "Creado directorio de copias de seguridad: $resolvedOutputDir"
}

# 3. Localizar binario pg_dump
$pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
if (-not $pgDump) {
    # Búsqueda común en Program Files de Windows
    $possiblePaths = @(
        "C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
        "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
        "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
    )
    foreach ($path in $possiblePaths) {
        if (Test-Path -LiteralPath $path) {
            $pgDump = $path
            break
        }
    }
}

$timestamp = (Get-Date).ToString("yyyy-MM-dd_HHmmss")
$backupFileName = "ludeka_prod_$timestamp.sql"
$backupFilePath = Join-Path -Path $resolvedOutputDir -ChildPath $backupFileName
$gzFilePath = "$backupFilePath.gz"

Write-Info "Iniciando copia de seguridad de Supabase en: $timestamp"

if ($pgDump) {
    Write-Info "Utilizando herramienta: $pgDump"
    try {
        # Ejecutar volcado con pg_dump
        & $pgDump --dbname="$ConnectionString" --no-owner --no-privileges --file="$backupFilePath"
        
        if ($LASTEXITCODE -ne 0) {
            throw "pg_dump finalizó con código de error $LASTEXITCODE."
        }

        # Comprimir a .gz utilizando .NET nativo
        Write-Info "Comprimiendo volcado a GZip..."
        $inputStream = [System.IO.File]::OpenRead($backupFilePath)
        $outputStream = [System.IO.File]::Create($gzFilePath)
        $gzipStream = New-Object System.IO.Compression.GZipStream($outputStream, [System.IO.Compression.CompressionMode]::Compress)
        
        $inputStream.CopyTo($gzipStream)
        $gzipStream.Close()
        $outputStream.Close()
        $inputStream.Close()

        # Eliminar archivo SQL sin comprimir
        Remove-Item -LiteralPath $backupFilePath -Force

        $fileSize = (Get-Item $gzFilePath).Length / 1MB
        Write-Ok ("Copia de seguridad generada con éxito: {0} ({1:N2} MB)" -f $gzFilePath, $fileSize)
    }
    catch {
        Write-Err "Fallo al ejecutar el volcado: $_"
        exit 1
    }
}
else {
    Write-Warn "No se encontró 'pg_dump' instalado en el sistema local."
    Write-Warn "Para backups automáticos en local o CI, instala las utilidades cliente de PostgreSQL (PostgreSQL Client Tools)."
    Write-Info "En entornos Docker o servidores Linux, puedes ejecutar:"
    Write-Info "  pg_dump `"$ConnectionString`" | gzip > `"$gzFilePath`""
}

# 4. Política de retención: purgar copias de más de $RetentionDays días
Write-Info "Aplicando política de retención ($RetentionDays días)..."
$cutoffDate = (Get-Date).AddDays(-$RetentionDays)
$purgedCount = 0

Get-ChildItem -Path $resolvedOutputDir -Filter "ludeka_prod_*.sql.gz" | ForEach-Object {
    if ($_.LastWriteTime -lt $cutoffDate) {
        Write-Info "Purgando copia antigua: $($_.Name) ($($_.LastWriteTime.ToString('yyyy-MM-dd')))"
        Remove-Item -LiteralPath $_.FullName -Force
        $purgedCount++
    }
}

Write-Ok "Mantenimiento finalizado. Copias antiguas purgadas: $purgedCount."
