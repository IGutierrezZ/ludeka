# Ludeka / Ludist — Reglas Maestras del Monorepo y Guía de Agentes

> **Proyecto:** Ludist / Ludeka ("Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.")  
> **Arquitectura:** .NET 10 (C# 13), Blazor Web App (SSR + Interactivo), Clean Architecture / Vertical Slices, Tailwind CSS + Componentes Editoriales, Engram Persistent Memory.

---

## 0. REGLA SUPREMA: IDIOMA OBLIGATORIO — ESPAÑOL (CASTELLANO)

- **TODO EN ESPAÑOL:** Todas las respuestas del agente, mensajes de chat, explicaciones, resúmenes, razonamientos dirigidos al usuario y **TODOS los artefactos de SDD** (`proposal.md`, `spec.md`, `design.md`, `tasks.md`, `verification-report.md`, `walkthrough.md`, etc.) DEBEN generarse y redactarse estrictamente en **español (castellano)**.
- **PROHIBIDO EL INGLÉS EN DOCUMENTACIÓN Y ARTEFACTOS:** Queda terminantemente prohibido generar propuestas, especificaciones o diseños en inglés. La única excepción son los identificadores técnicos de código (nombres de clases, métodos, interfaces y variables en C#) y palabras clave de frameworks.
- **PRECEDENCIA:** Si cualquier skill, prompt o plantilla externa menciona "default to English", esta regla del proyecto TIENE PRECEDENCIA ABSOLUTA y la sobreescribe: genera SIEMPRE el contenido en español castellano.
- **REGISTRO NEUTRO:** El chat con el usuario se redacta en castellano neutro profesional (tuteo), sin voseo rioplatense ni regionalismos (che, dale, posta, boludo). Se mantiene el tono cálido, directo y exigente del persona.

---

## 1. Filosofía de Desarrollo: Spec-Driven Development (SDD)

Este proyecto se construye bajo la metodología **Spec-Driven Development (SDD)** de Gentle-AI.
**REGLA DE ORO:** Nunca implementar directamente en código cambios arquitectónicos o funcionalidades mayores sin pasar por las fases del ciclo SDD:

```
[ sdd-explore ] ➔ [ sdd-propose ] ➔ [ sdd-spec ] ➔ [ sdd-design ] ➔ [ sdd-tasks ] ➔ [ sdd-apply ] ➔ [ sdd-verify ] ➔ [ sdd-archive ]
```

### Principios de la Máquina de Estados de SDD
1. **File-System como Fuente de la Verdad:** El estado de las fases reside en `openspec/` y `docs/specs/`. No confiar en la memoria volátil del chat. `openspec/` (sin punto) es el único almacén canónico: es el `planning_home` que resuelve `gentle-ai sdd-status`.
2. **Lossless Blocking Prompts:** Antes de pasar de `sdd-propose` a `sdd-spec` o de `sdd-design` a `sdd-apply`, presentar la propuesta o diseño al usuario en español y esperar aprobación explícita.
3. **Delegación con Subagentes:** Usar la primitiva de delegación de la plataforma para delegar exploraciones profundas, investigación externa y verificaciones independientes, recordando siempre el idioma español (en OpenCode: herramienta `task` con los subagentes `sdd-*` del orquestador de Gentle AI; en Antigravity: `invoke_subagent`).
4. **Presupuestos y CAS (Compare-And-Swap):** En `sdd-apply`, implementar exclusivamente contra los requerimientos acordados en la especificación y tareas definidas.
5. **Sincronización Continua de Roadmap:** Todo incremento debe figurar y mantenerse actualizado en los registros centrales de roadmap: `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`. Al proponer/iniciar pasa a `⏳ En progreso`, y al archivar a `✅ Archivado`.
6. **Volcado Obligatorio a la Especificación Viva del Sistema (`sdd-archive`):** Al finalizar y verificar cada incremento, en la fase `sdd-archive` es terminantemente obligatorio:
   - Volcar toda la información funcional, de dominio, arquitectura, persistencia, flujos y componentes en `docs/specs/sistema/` (creando `NN-nombre-modulo.md` o actualizando los módulos existentes impactados).
   - Actualizar el índice maestro `docs/specs/sistema/README.md` incorporando el enlace al módulo y el nuevo total de pruebas automáticas verificadas.
   - Trasladar el documento de incremento de `docs/increments/inc-XX.md` a `docs/increments/archive/inc-XX.md`.

---

## 1-bis. Flujo de Incrementos con Worktrees (Rama → PR)

Todo incremento se desarrolla en un **worktree propio** sobre una **rama nueva**, y se integra a `main` exclusivamente vía **Pull Request**. Este flujo es obligatorio para cualquier herramienta o agente que trabaje en este repositorio.

1. **Inicio del incremento:** ejecutar `scripts/sdd-worktree.ps1 new <slug>`: crea el worktree en `C:\repos\ludeka-wt\<slug>` y la rama `inc/<slug>` desde `main` actualizado. El slug va en kebab-case y minúsculas (ej. `portada-creadores`).
2. **Trabajo aislado:** todos los agentes de implementación, verificación y revisión trabajan con ese worktree como directorio de trabajo. Un solo escritor por worktree.
3. **Artefactos en la rama:** el código, los tests y TODOS los artefactos del incremento (`docs/specs/`, `openspec/`, `docs/increments/ROADMAP.md`) viven en la rama `inc/<slug>` y entran al PR.
4. **Cierre y Apertura de PR:** con la verificación en verde, ejecutar `scripts/sdd-worktree.ps1 pr <slug>`: pushea la rama y abre el PR a `main` (automático con `gh` CLI; si no está autenticado, imprime la URL para abrirlo a mano).
5. **Espera Obligatoria de CI (PR):** tras abrir el PR, el agente o ejecutor **NUNCA debe mergear ni cerrar de inmediato**. DEBE esperar activamente a que el pipeline de GitHub Actions (`Ludeka CI/CD Pipeline`) finalice al 100% en verde (`gh pr checks <numero_o_rama> --watch` o consultando con `gh pr checks`). Si algún test falla, el fallo se diagnostica y resuelve en el worktree antes de cualquier merge.
6. **Merge y Espera Obligatoria de CI/CD (main):** con el PR verificado en verde en GitHub Actions, se ejecuta el merge a `main` (ej. `gh pr merge <numero> --squash`). A continuación, el agente **DEBE monitorizar y esperar a que el pipeline de CI/CD en `main` finalice con éxito** (`gh run list -b main -L 1` / `gh run view <id>`), verificando que tanto la suite completa como el despliegue automático a producción (`Deploy to Google Cloud Run`) concluyan en verde. Queda terminantemente prohibido dar la tarea por finalizada sin esta verificación.
7. **Post-merge y Cleanup:** con el pipeline de `main` en verde, ejecutar `scripts/sdd-worktree.ps1 done <slug>`: elimina el worktree y la rama local, y actualiza `main` local.
8. **Orquestador SDD:** al lanzar `sdd-apply` y `sdd-verify` (y cualquier subagente que escriba código), el workdir DEBE ser el worktree del incremento; `sdd-archive` incluye la custodia obligatoria de los pasos 4 a 7 (PR + espera CI + merge + espera CD + cleanup).
9. **Prohibido:** pushear directamente a `main` (además bloqueado por el ruleset de GitHub del repositorio) o mergear sin PR. Si el PR excede las 400 líneas, dividir en ramas apiladas desde el mismo worktree (PRs encadenados).
10. **Paralelismo:** pueden coexistir N incrementos activos, cada uno en su worktree / rama / PR (ver `docs/increments/ROADMAP.md`, sección "Incrementos en Curso"). Los conflictos entre PRs se resuelven al mergear en orden.
11. **Mecanismo único, sin excepciones por plataforma:** los pasos 1, 4 y 7 se ejecutan SIEMPRE con `scripts/sdd-worktree.ps1`, igual en OpenCode, Gemini CLI, Antigravity y Claude Code. En Claude Code queda **prohibido** sustituirlo por sus herramientas nativas `EnterWorktree` / `ExitWorktree`: crean el worktree en `.claude/worktrees/` (dentro del repositorio) con una rama que el script no reconoce, dejan el paso 4 sin ejecutor, no actualizan `main` tras el merge y solo pueden eliminar worktrees creados en la misma sesión. El requisito del proyecto es un flujo que sobreviva **entre sesiones y entre aplicaciones**, y eso solo lo cumple el script. En Windows se invoca con `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sdd-worktree.ps1 <verbo> <slug>`, porque `pwsh` no está instalado.

---

## 2. Protocolo de Memoria Persistente (Engram MCP)

El proyecto cuenta con el servidor MCP de **Engram** conectado en `.tools/bin/engram.exe`.
- **Cuándo guardar (`mem_save`):** Inmediatamente tras resolver un bug, tomar una decisión de diseño, aprender una regla de negocio o establecer un patrón.
- **Cuándo consultar (`mem_context` / `mem_search`):** Al inicio de sesión o al retomar una funcionalidad para no perder el contexto previo.
- **Cierre de sesión (`mem_session_summary`):** Obligatorio antes de finalizar la sesión de trabajo.

---

## 3. Principios de UI/UX: Anti-Plantillas y Cero "AI Slop"

- **Identidad Propia:** Ludeka no es una base de datos corporativa ni un clon genérico con gradientes púrpura o tarjetas estándar. Debe respirar la pasión de los juegos de mesa (estilo editorial moderno, tipografía nítida con contraste, badges compactos de 3 segundos, microtextos con personalidad lúdica).
- **Mobile-First Radical:** Barra de acciones al alcance del pulgar (`Tengo`, `Jugado`, `Deseado`, `Prestar`), fichas por pestañas horizontales limpias, tiempo de carga instantáneo.
- **Componentes Gratuitos y Abiertos:** Uso de Tailwind CSS con utilidades bien estructuradas, componentes Razor propios e iconografía abierta (Lucide Icons). Cumplimiento estricto de WCAG 2.2 AA (accesibilidad).

---

## 4. Estándares Técnicos (.NET 10 & C# 13)

- **Solución y Capas:**
  - `src/Ludeka.Web`: Frontend Blazor Web App (SSR estático donde sea posible, interactividad por componentes, Streaming Rendering).
  - `src/Ludeka.Core`: Entidades de dominio y reglas de negocio puras (cero dependencias de framework).
  - `src/Ludeka.Application`: Casos de uso, interfaces, validaciones (FluentValidation o data annotations) y DTOs.
  - `src/Ludeka.Infrastructure`: Integración BGG XMLAPI2, APIs de YouTube/Instagram, persistencia y autenticación OAuth.
  - `tests/Ludeka.UnitTests`: Pruebas unitarias con xUnit y pruebas de componentes/integración.
- **Async/Await:** Emplear `ValueTask` cuando proceda, pasar siempre `CancellationToken`, evitar `.Result` o `.Wait()`.
- **Commits Convencionales:** Formato `feat:`, `fix:`, `refactor:`, `test:`, `docs:`. Prohibido añadir atribuciones "Co-Authored-By" de IA.

---

## 5. Entorno Local de Pruebas con Réplica de Producción (PostgreSQL en Docker)

Para verificar cambios *in situ* contra el catálogo real sin riesgo para producción ni esperas de despliegue:
- **Contenedor PostgreSQL local:** Servicio `postgres:17-alpine` definido en `docker-compose.yml` (puerto `5432:5432`, base `ludeka`, usuario `postgres`, contraseña `postgrespassword`, volumen persistente `postgres_data`).
  - Arrancar: `docker compose up -d postgres`.
- **Sincronización / Refresco desde producción:**
  - Script automatizado: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sync-prod-db.ps1`.
  - Extrae el volcado por *streaming* SSH seguro desde la VM de base de datos en Google Cloud (`ludeka-db`), descargándolo a local y restaurándolo en el contenedor sin almacenar archivos en Google Cloud.
- **Secretos y configuración local (`dotnet user-secrets`):**
  - Compartido entre `src/Ludeka.Web` y `src/Ludeka.Jobs` mediante `<UserSecretsId>ludeka-web-user-secrets-2026</UserSecretsId>`.
  - Cadena: `ConnectionStrings:DefaultConnection = Host=localhost;Port=5432;Database=ludeka;Username=postgres;Password=postgrespassword;`
  - Proveedor: `Database:Provider = PostgreSql` y `Database:SeedDemoData = false`.
  - Lotes acotados para Jobs: `NightlyCataloging:DailyCatalogingLimit = 100` (permite verificar ejecuciones de ingesta o sincronización con 100 juegos en vez de procesar el catálogo entero de 18.000+).
- **Ejecución de verificación local:**
  - Web: `dotnet run --project src/Ludeka.Web --launch-profile http` (disponible en `http://localhost:5081`).
  - Jobs: `dotnet run --project src/Ludeka.Jobs -- <nombre-job>` (ej. `nightly-cataloging`, `devir-images-backfill`).

<!-- axiom:skills-index -->
## Skills

| Skill | Trigger / description | Scope | Path |
| --- | --- | --- | --- |
| `accessibility` | Audit and improve web accessibility following WCAG 2.2 guidelines. Use when asked to "improve accessibility", "a11y audit", "WCAG compliance", "screen reader support", "keyboard navigation", or "make accessible". | project | `.agents/skills/accessibility/SKILL.md` |
| `aspnet-core` | Build, review, refactor, or architect ASP.NET Core web applications using current official guidance for .NET web development. Use when working on Blazor Web Apps, Razor Pages, MVC, Minimal APIs, controller-based Web APIs, SignalR, gRPC, middleware, dependency injection, configuration, authentication, authorization, testing, performance, deployment, or ASP.NET Core upgrades. | project | `.agents/skills/aspnet-core/SKILL.md` |
| `aspnet-minimal-api-openapi` | Create ASP.NET Minimal API endpoints with proper OpenAPI documentation | project | `.agents/skills/aspnet-minimal-api-openapi/SKILL.md` |
| `chained-pr` | Trigger: PRs over 400 lines, stacked PRs, review slices. Split oversized changes into chained PRs that protect review focus. | project | `.claude/skills/chained-pr/SKILL.md` |
| `cognitive-doc-design` | Design docs that reduce cognitive load. Trigger: writing guides, READMEs, RFCs, onboarding, architecture, or review-facing docs. | project | `.claude/skills/cognitive-doc-design/SKILL.md` |
| `csharp-async` | Get best practices for C# async programming | project | `.agents/skills/csharp-async/SKILL.md` |
| `csharp-xunit` | Get best practices for XUnit unit testing, including data-driven tests | project | `.agents/skills/csharp-xunit/SKILL.md` |
| `dotnet-best-practices` | Ensure .NET/C# code meets best practices for the solution/project. | project | `.agents/skills/dotnet-best-practices/SKILL.md` |
| `dotnet-design-pattern-review` | Review the C#/.NET code for design pattern implementation and suggest improvements. | project | `.agents/skills/dotnet-design-pattern-review/SKILL.md` |
| `fluentui-blazor` | Guide for using the Microsoft Fluent UI Blazor component library (Microsoft.FluentUI.AspNetCore.Components NuGet package) in Blazor applications. Use this when the user is building a Blazor app with Fluent UI components, setting up the library, using FluentUI components like FluentButton, FluentDataGrid, FluentDialog, FluentToast, FluentNavMenu, FluentTextField, FluentSelect, FluentAutocomplete, FluentDesignTheme, or any component prefixed with "Fluent". Also use when troubleshooting missing providers, JS interop issues, or theming. | project | `.agents/skills/fluentui-blazor/SKILL.md` |
| `frontend-design` | Create distinctive, production-grade frontend interfaces with high design quality. Use this skill when the user asks to build web components, pages, artifacts, posters, or applications (examples include websites, landing pages, dashboards, React components, HTML/CSS layouts, or when styling/beautifying any web UI). Generates creative, polished code and UI design that avoids generic AI aesthetics. | project | `.agents/skills/frontend-design/SKILL.md` |
| `go-testing` | Trigger: Go tests, go test coverage, Bubbletea teatest, golden files. Apply focused Go testing patterns. | project | `.claude/skills/go-testing/SKILL.md` |
| `judgment-day` | Trigger: judgment day, dual review, adversarial review, juzgar. Run explicit blind dual review with at most two scoped fix/re-judgment rounds. | project | `.claude/skills/judgment-day/SKILL.md` |
| `skill-creator` | Trigger: new skills, agent instructions, documenting AI usage patterns. Create LLM-first skills with valid frontmatter. | project | `.claude/skills/skill-creator/SKILL.md` |
| `skill-improver` | Trigger: improve skills, audit skills, refactor skills, skill quality. Audit and upgrade existing LLM-first skills. | project | `.claude/skills/skill-improver/SKILL.md` |
| `tailwind-css-patterns` | Provides comprehensive Tailwind CSS utility-first styling patterns including responsive design, layout utilities, flexbox, grid, spacing, typography, colors, and modern CSS best practices. Use when styling React/Vue/Svelte components, building responsive layouts, implementing design systems, or optimizing CSS workflow. | project | `.agents/skills/tailwind-css-patterns/SKILL.md` |
| `web-perf` | Analyzes web performance using Chrome DevTools MCP. Measures Core Web Vitals (LCP, INP, CLS) and supplementary metrics (FCP, TBT, Speed Index), identifies render-blocking resources, network dependency chains, layout shifts, caching issues, and accessibility gaps. Use when asked to audit, profile, debug, or optimize page load performance, Lighthouse scores, or site speed. Biases towards retrieval from current documentation over pre-trained knowledge. | project | `.agents/skills/web-perf/SKILL.md` |
| `work-unit-commits` | Plan commits as reviewable work units. Trigger: implementation, commit splitting, chained PRs, or keeping tests and docs with code. | project | `.claude/skills/work-unit-commits/SKILL.md` |
<!-- /axiom:skills-index -->
