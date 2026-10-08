#Requires -Version 5.1
<#
.SYNOPSIS
    Sincroniza la base de datos de producción (GCP VM) a tu contenedor PostgreSQL local.
.DESCRIPTION
    Extrae un volcado de PostgreSQL mediante streaming SSH desde la VM en Google Cloud,
    lo guarda en tu equipo local (sin ocupar almacenamiento en Google) y lo restaura
    en el contenedor Docker local 'ludeka-postgres'.
.PARAMETER VmIp
    Dirección IP pública de la máquina virtual en Google Cloud (ludeka-db).
.PARAMETER User
    Usuario SSH en la máquina virtual (predeterminado: lujambrio).
.EXAMPLE
    .\scripts\sync-prod-db.ps1 -VmIp 34.xxx.xxx.xxx
#>
param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$VmIp = "34.53.221.156",

    [Parameter(Position = 1, Mandatory = $false)]
    [string]$User = "lujambrio",

    [Parameter(Position = 2, Mandatory = $false)]
    [string]$RemoteDbUser = "ludeka_admin"
)

$ErrorActionPreference = 'Stop'

function Write-Info { param($msg) Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "[AVISO] $msg" -ForegroundColor Yellow }
function Write-Err  { param($msg) Write-Host "[ERROR] $msg" -ForegroundColor Red }

# 1. Validar o solicitar IP
if ([string]::IsNullOrWhiteSpace($VmIp)) {
    $VmIp = Read-Host "Introduce la IP pública de la VM en Google Cloud (ludeka-db)"
}

if ([string]::IsNullOrWhiteSpace($VmIp)) {
    Write-Err "Debes proporcionar una dirección IP válida."
    exit 1
}

# 2. Verificar que Docker local y el contenedor 'ludeka-postgres' están activos
Write-Info "Comprobando contenedor local 'ludeka-postgres'..."
$containerRunning = docker ps --filter "name=ludeka-postgres" --filter "status=running" -q
if (-not $containerRunning) {
    Write-Info "Levantando contenedor local 'ludeka-postgres' con Docker Compose..."
    docker compose up -d postgres
    Start-Sleep -Seconds 3
}

# 3. Preparar directorio local de copias
$backupDir = Join-Path -Path $PSScriptRoot -ChildPath "..\backups"
if (-not (Test-Path -LiteralPath $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

$timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
$localFile = Join-Path -Path $backupDir -ChildPath "ludeka_prod_$timestamp.sql"
$keyPath = Join-Path -Path $env:USERPROFILE -ChildPath ".ssh\id_ed25519"

Write-Info "Extrayendo volcado desde $User@$VmIp (streaming directo a local, 0 MB en Google)..."

# Configurar codificación UTF-8 para la tubería
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

try {
    # Ejecutar pg_dump por SSH con el rol remoto real (ludeka_admin) y neutralizando propietarios
    & ssh -o StrictHostKeyChecking=accept-new -i $keyPath "${User}@${VmIp}" "docker exec ludeka-postgres pg_dump -U $RemoteDbUser --clean --if-exists --no-owner --no-privileges ludeka" | Out-File -FilePath $localFile -Encoding utf8
    
    if (-not (Test-Path $localFile) -or (Get-Item $localFile).Length -lt 1000) {
        throw "El archivo de volcado descargado está vacío o incompleto. Revisa la IP, la clave SSH o el contenedor remoto."
    }
    
    $fileMb = [math]::Round(((Get-Item $localFile).Length / 1MB), 2)
    Write-Ok "Volcado guardado en tu equipo local: $localFile ($fileMb MB)"
}
catch {
    Write-Err "Error al conectar o extraer el volcado por SSH: $_"
    exit 1
}

# 4. Copiar al contenedor local y restaurar limpiamente
Write-Info "Restaurando base de datos en el contenedor local 'ludeka-postgres'..."
try {
    docker cp $localFile "ludeka-postgres:/tmp/restore.sql"
    docker exec ludeka-postgres psql -U postgres -d ludeka -f /tmp/restore.sql --quiet
    docker exec ludeka-postgres rm /tmp/restore.sql
    Write-Ok "¡Base de datos de producción restaurada con éxito en tu PostgreSQL local!"
}
catch {
    Write-Err "Error al restaurar en Docker local: $_"
    exit 1
}
