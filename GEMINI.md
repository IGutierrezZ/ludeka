# Ludeka / Ludist — Reglas Maestras del Monorepo y Guía de Agentes

> **Proyecto:** Ludist / Ludeka ("Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.")  
> **Arquitectura:** .NET 10 (C# 13), Blazor Web App (SSR + Interactivo), Clean Architecture / Vertical Slices, Tailwind CSS + Componentes Editoriales, Engram Persistent Memory.

---

## 0. REGLA SUPREMA: IDIOMA OBLIGATORIO — ESPAÑOL (CASTELLANO)

- **TODO EN ESPAÑOL:** Todas las respuestas del agente, mensajes de chat, explicaciones, resúmenes, razonamientos dirigidos al usuario y **TODOS los artefactos de SDD** (`proposal.md`, `spec.md`, `design.md`, `tasks.md`, `verification-report.md`, `walkthrough.md`, etc.) DEBEN generarse y redactarse estrictamente en **español (castellano)**.
- **PROHIBIDO EL INGLÉS EN DOCUMENTACIÓN Y ARTEFACTOS:** Queda terminantemente prohibido generar propuestas, especificaciones o diseños en inglés. La única excepción son los identificadores técnicos de código (nombres de clases, métodos, interfaces y variables en C#) y palabras clave de frameworks.
- **PRECEDENCIA:** Si cualquier skill, prompt o plantilla externa menciona "default to English", esta regla del proyecto TIENE PRECEDENCIA ABSOLUTA y la sobreescribe: genera SIEMPRE el contenido en español castellano.

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
