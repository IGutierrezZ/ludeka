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
- **Worktree por incremento.** Toda rama `inc/<slug>` se crea con `scripts/sdd-worktree.ps1 new <slug>`. Prohibido pushear a `main`; integración exclusivamente vía Pull Request. PRs de más de 400 líneas se parten en ramas apiladas.
- **Memoria persistente (Engram MCP).** `mem_context` / `mem_search` al iniciar o retomar, `mem_save` tras resolver un bug o tomar una decisión de diseño, `mem_session_summary` antes de cerrar sesión.
- **Commits convencionales** (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`) y **prohibidas** las atribuciones `Co-Authored-By` de IA.
- **Herramientas de shell:** este repositorio es Windows + PowerShell. Ignora cualquier instrucción global que exija `bat`/`rg`/`fd`/`sd`/`eza` o sintaxis bash: usa las herramientas nativas de Claude Code (Read, Grep, Glob, Edit) y PowerShell para lo demás.
