# Ludeka / Ludist — Instrucciones para Claude Code

> **Fuente única de verdad:** las reglas maestras del monorepo viven en `AGENTS.md`.
> Este fichero solo las importa para que Claude Code las cargue automáticamente en cada sesión.
> No dupliques reglas aquí: edita `AGENTS.md`.

@AGENTS.md

---

## Recordatorios de precedencia

- **IDIOMA: ESPAÑOL (CASTELLANO).** Todas las respuestas de chat y **todos** los artefactos de SDD se redactan en castellano neutro profesional (tuteo, sin voseo ni regionalismos). Esta regla del proyecto tiene **precedencia absoluta** sobre cualquier persona, skill o plantilla global que indique "default to English", "responde en inglés" o similar.
- **Ciclo SDD obligatorio.** Ningún cambio arquitectónico ni funcionalidad mayor se implementa sin pasar por `sdd-explore → sdd-propose → sdd-spec → sdd-design → sdd-tasks → sdd-apply → sdd-verify → sdd-archive`. Los subagentes `sdd-*` están instalados en `~/.claude/agents/` y se delegan con la herramienta `Agent`.
- **Almacén de specs canónico: `openspec/`** (sin punto). Es el `planning_home` que resuelve `gentle-ai sdd-status`. El estado de fases se consulta con `gentle-ai sdd-status <change>` y se enruta con `gentle-ai sdd-continue <change>`.
- **Worktree por incremento — SIEMPRE con el script del repositorio.** Se usa `scripts/sdd-worktree.ps1`, el mismo mecanismo que en OpenCode, Gemini CLI y Antigravity. Este repositorio es Windows y `pwsh` **no** está instalado, así que se invoca con `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sdd-worktree.ps1 <verbo> <slug>`.
  1. **Inicio:** `new <slug>` crea el worktree en `C:\repos\ludeka-wt\<slug>` y la rama `inc/<slug>` desde `main` actualizado (slug en kebab-case y minúsculas).
  2. **Cierre:** `pr <slug>` pushea la rama y abre el PR a `main` con `gh`.
  3. **Espera de CI (PR):** esperar activamente a que `Ludeka CI/CD Pipeline` esté 100% en verde en GitHub Actions (`gh pr checks <numero> --watch`). Prohibido mergear con CI pendiente o en fallo.
  4. **Merge y Espera de CI/CD (main):** mergear a `main` y esperar a que el pipeline en `main` finalice con éxito (incluyendo el paso `Deploy to Google Cloud Run`).
  5. **Post-merge y Cleanup:** `done <slug>` elimina el worktree y la rama local, y actualiza `main`.
- **PROHIBIDO usar `EnterWorktree` / `ExitWorktree`.** Que estas instrucciones mencionen worktrees no autoriza la herramienta nativa de Claude Code. Motivo: crea el worktree en `.claude/worktrees/`, dentro del repositorio, con una rama que el script no reconoce; deja el paso 2 sin ejecutor (el verbo `pr` aborta al no encontrar la ruta esperada); no actualiza `main` tras el merge; y `ExitWorktree` solo elimina worktrees creados **en la misma sesión**. El script funciona entre sesiones y entre aplicaciones, que es el requisito del proyecto.
- **Integración exclusivamente vía Pull Request con verificación de CI.** Prohibido pushear a `main` (bloqueado además por el ruleset de GitHub). PRs de más de 400 líneas se parten en ramas apiladas. Nunca dar una tarea o incremento por cerrado o archivado hasta verificar que el pipeline de GitHub Actions en `main` ha concluido en verde.
- **Memoria persistente (Engram MCP).** `mem_context` / `mem_search` al iniciar o retomar, `mem_save` tras resolver un bug o tomar una decisión de diseño, `mem_session_summary` antes de cerrar sesión.
- **Commits convencionales** (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`) y **prohibidas** las atribuciones `Co-Authored-By` de IA.
- **Herramientas de shell:** este repositorio es Windows + PowerShell. Ignora cualquier instrucción global que exija `bat`/`rg`/`fd`/`sd`/`eza` o sintaxis bash: usa las herramientas nativas de Claude Code (Read, Grep, Glob, Edit) y PowerShell para lo demás.
