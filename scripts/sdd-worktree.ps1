#Requires -Version 5.1
<#
.SYNOPSIS
    Gestion de worktrees por incremento para Ludeka (Rama -> PR -> cleanup).

.DESCRIPTION
    Un incremento = un worktree propio + una rama inc/<slug> creada desde main
    actualizado. El incremento se cierra con un Pull Request hacia main y,
    tras el merge, se limpian el worktree y la rama local.

.USAGE
    scripts/sdd-worktree.ps1 new <slug>   # Crea el worktree y la rama inc/<slug>
    scripts/sdd-worktree.ps1 pr <slug>    # Pushea la rama y abre el PR a main
    scripts/sdd-worktree.ps1 done <slug>  # Tras el merge: limpia worktree y rama
#>
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('new', 'pr', 'done')]
    [string]$Verb,

    [Parameter(Position = 1, Mandatory = $true)]
    [string]$Slug
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent $scriptDir

# La raiz de worktrees vive junto al checkout PRINCIPAL del repo. Al ejecutar
# desde un worktree, el padre de $repoRoot ya es la carpeta de worktrees, asi
# que se deriva la raiz principal desde el git common dir (siempre apunta al
# checkout principal).
$commonDir = git -C $repoRoot rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commonDir)) {
    $cd = (git -C $repoRoot rev-parse --git-common-dir).Trim()
    $commonDir = (Resolve-Path -LiteralPath (Join-Path $repoRoot $cd)).Path
}
$commonDir = $commonDir.Trim()
$mainRoot  = Split-Path -Parent $commonDir
$wtRoot    = Join-Path -Path (Split-Path -Parent $mainRoot) -ChildPath 'ludeka-wt'
$branch    = "inc/$Slug"
$wtPath    = Join-Path -Path $wtRoot -ChildPath $Slug

function Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
function Ok   { param($msg) Write-Host $msg -ForegroundColor Green }
function Fail { param($msg) Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# --- Validaciones comunes ---
if ($Slug -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$') {
    Fail "Slug invalido '$Slug'. Usa kebab-case en minusculas (ej. portada-creadores)."
}

git -C $repoRoot rev-parse --is-inside-work-tree >$null 2>&1
if ($LASTEXITCODE -ne 0) { Fail "No es un repositorio git: $repoRoot" }

switch ($Verb) {

    'new' {
        if (Test-Path -LiteralPath $wtPath) {
            Fail "Ya existe un worktree en: $wtPath"
        }

        git -C $repoRoot show-ref --verify --quiet "refs/heads/$branch"
        if ($LASTEXITCODE -eq 0) {
            Fail "La rama $branch ya existe localmente. Elige otro slug o elimina la rama."
        }

        Info "Actualizando origin/main..."
        git -C $repoRoot fetch origin main
        if ($LASTEXITCODE -ne 0) {
            Info "AVISO: el fetch fallo (sin red?); se usa el main local como base."
        }

        $base = 'origin/main'
        git -C $repoRoot show-ref --verify --quiet 'refs/remotes/origin/main'
        if ($LASTEXITCODE -ne 0) { $base = 'main' }

        New-Item -ItemType Directory -Force -Path $wtRoot | Out-Null
        git -C $repoRoot worktree add -b $branch $wtPath $base
        if ($LASTEXITCODE -ne 0) {
            Fail "git worktree add fallo. Revisa el error anterior."
        }

        Ok "Worktree creado: $wtPath"
        Ok "Rama: $branch (base: $base)"
        Info "Trabaja dentro de ese directorio. Al verificar, ejecuta: scripts/sdd-worktree.ps1 pr $Slug"
    }

    'pr' {
        if (-not (Test-Path -LiteralPath $wtPath)) {
            Fail "No existe el worktree: $wtPath"
        }

        $dirty = git -C $wtPath status --porcelain
        if ($dirty) {
            Fail "El worktree tiene cambios sin commitear. Haz commit de todo antes de crear el PR."
        }

        Info "Pusheando $branch a origin..."
        git -C $wtPath push -u origin $branch
        if ($LASTEXITCODE -ne 0) {
            Fail "git push fallo. Revisa tu conexion y permisos."
        }

        $remoteUrl = (git -C $wtPath remote get-url origin).Trim()
        $remoteUrl = $remoteUrl -replace '\.git$', ''
        $compareUrl = "$remoteUrl/compare/main...$branch" + '?expand=1'

        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($gh) {
            gh auth status 2>$null
            if ($LASTEXITCODE -eq 0) {
                Info "Creando el PR con gh..."
                Push-Location $wtPath
                try {
                    & gh pr create --base main --fill
                    if ($LASTEXITCODE -eq 0) {
                        Ok "PR creado correctamente."
                        Write-Host ""
                        Info "==> PASO OBLIGATORIO DE CI (PR): Espera a que el pipeline concluya en verde:"
                        Info "    gh pr checks $branch --watch"
                        Info "    Tras mergear en main, vigila el despliegue en produccion:"
                        Info "    gh run list -b main -L 1"
                        return
                    }
                }
                finally {
                    Pop-Location
                }
            }
            else {
                Info "gh esta instalado pero sin autenticar (ejecuta 'gh auth login'). Abre el PR manualmente:"
            }
        }
        else {
            Info "gh CLI no esta disponible. Abre el PR manualmente:"
        }
        Info $compareUrl
    }

    'done' {
        # Validacion proactiva de CI/CD en main si gh CLI esta disponible
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($gh) {
            try {
                $rawJson = gh run list -b main -L 1 --json status,conclusion,name,databaseId 2>$null
                if ($rawJson) {
                    $latestRuns = $rawJson | ConvertFrom-Json
                    if ($latestRuns -and $latestRuns.Count -gt 0) {
                        $run = $latestRuns[0]
                        if ($run.status -ne 'completed') {
                            Info "AVISO: El pipeline de CI/CD en 'main' ($($run.name)) aun esta en curso (estado: $($run.status))."
                            Info "Espera a que finalice con: gh run watch $($run.databaseId)"
                        } elseif ($run.conclusion -ne 'success') {
                            Info "AVISO CRITICO: La ultima ejecucion de CI/CD en 'main' no concluyo con exito (resultado: $($run.conclusion))."
                            Info "Revisa los registros con: gh run view $($run.databaseId)"
                        } else {
                            Ok "CI/CD en 'main' verificado en verde ($($run.conclusion))."
                        }
                    }
                }
            } catch {
                # Ignorar fallos de red
            }
        }

        $currentBranch = git -C $repoRoot rev-parse --abbrev-ref HEAD
        if ($currentBranch -eq $branch) {
            Fail "La rama $branch esta chequeada en el checkout principal. Cambia a main primero."
        }

        if (Test-Path -LiteralPath $wtPath) {
            Info "Eliminando el worktree $wtPath..."
            git -C $repoRoot worktree remove $wtPath
            if ($LASTEXITCODE -ne 0) {
                Fail "No se pudo eliminar el worktree (cambios sin commitear?). Revisalo o fuerza con: git worktree remove --force $wtPath"
            }
        }
        else {
            Info "El worktree $wtPath no existe; se continua con la limpieza de la rama."
        }

        $currentBranch = git -C $repoRoot rev-parse --abbrev-ref HEAD
        if ($currentBranch -eq 'main') {
            git -C $repoRoot fetch origin main
            git -C $repoRoot merge --ff-only origin main
            if ($LASTEXITCODE -eq 0) {
                Ok "main local actualizado."
            }
            else {
                Info "AVISO: main local no se pudo actualizar (cambios locales?). Haz pull manual."
            }
        }

        git -C $repoRoot branch -d $branch
        if ($LASTEXITCODE -ne 0) {
            Info "AVISO: no se pudo borrar $branch localmente (merge no visible). Haz pull en main y reintenta, o fuerza con: git branch -D $branch"
        }

        git -C $repoRoot worktree prune
        Ok "Incremento '$Slug' finalizado y limpio."
    }
}
