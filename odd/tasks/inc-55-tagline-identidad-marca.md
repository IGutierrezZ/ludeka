# Documento Vivo ODD — INC-55: Retirada del Tagline de Marca y Unificación de la Identidad

> **Feature:** `tagline-identidad-marca`  
> **Fichero:** `odd/tasks/inc-55-tagline-identidad-marca.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-55-tagline-identidad-marca/`  
> **Rama:** `inc/tagline-identidad-marca`  
> **Worktree:** `C:\repos\ludeka-wt\tagline-identidad-marca`  
> **Creado:** 2026-09-23 · **Ruta:** rama `inc/tagline-identidad-marca` → PR a `main`  

---

## 1. Objetivo

Eliminar de forma exhaustiva y definitiva la frase *«El Letterboxd de los juegos de mesa en español»* y cualquier mención no autorizada a marcas ajenas (Letterboxd) en todas las superficies vivas del proyecto:
- Interfaz gráfica (UI web Razor y layout).
- Metadatos PWA (`manifest.webmanifest`).
- Prompts del servicio de inteligencia artificial (`GeminiGameSummaryService`).
- Cabeceras de cortesía y User-Agent (`GeekDoImagesClient`).
- Documentación viva del sistema, especificaciones activas y guías operativas (`AGENTS.md`, `GEMINI.md`, `axiom.yaml`, `openspec/config.yaml`, `docs/specs/sistema/`).

El objetivo es unificar la identidad propia de Ludeka como plataforma editorial y comunitaria para aficionados a los juegos de mesa en español, sin depender de marcas registradas de terceros ni arrastrar ambigüedades en prompts de IA.

---

## 2. Problema y Diagnóstico

El barrido del repositorio reveló **44 coincidencias** de la palabra `Letterboxd`. Tras aislar los registros históricos archivados (que por trazabilidad de cambios pasados no deben reescribirse), se identificaron las siguientes superficies **vivas** que vulneran la directriz del mantenedor:

1. **Configuraciones de Identidad y Agentes:**
   - `AGENTS.md:3`
   - `GEMINI.md:3`
   - `axiom.yaml:39`
   - `openspec/config.yaml:8`
2. **Interfaz de Usuario (Blazor Web / Razor):**
   - `src/Ludeka.Web/Components/Pages/HomeDashboard.razor:8` (`<PageTitle>`)
   - `src/Ludeka.Web/Components/Layout/MainLayout.razor:241` (Pie de página general)
   - `src/Ludeka.Web/Components/Pages/Login.razor:14` (Subtítulo de acceso)
   - `src/Ludeka.Web/Components/Pages/InstagramModeration.razor:198` (Pie en vista previa de tarjetas sociales)
   - `src/Ludeka.Web/Components/Pages/MyLibrary.razor:382` (Descripción del tema Terracota)
   - `src/Ludeka.Web/Styles/input.css:7` (Comentario de cabecera de paleta)
3. **Plataforma y PWA:**
   - `src/Ludeka.Web/wwwroot/manifest.webmanifest:11` (`description`)
4. **Infraestructura e Integraciones:**
   - `src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs:366, 517` (El claim viaja dentro del prompt del sistema a Google Gemini, condicionando el sesgo de la síntesis)
   - `src/Ludeka.Infrastructure/Bgg/GeekDoImagesClient.cs:60` (Identificador User-Agent enviado a los servidores de BoardGameGeek)
5. **Especificación Activa y No Archivada:**
   - `openspec/specs/social-card-generator/spec.md:18`
   - `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md:13`
   - `docs/specs/sistema/15-dashboard-inicio-editorial.md:8`
   - `docs/specs/sistema/22-portada-y-directorio-creadores.md:12, 27`

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- Sustitución/retirada limpia en todas las superficies vivas identificadas.
- Actualización de prompts de IA en `GeminiGameSummaryService` para definir el rol de analista sin referencias externas.
- Normalización del User-Agent en `GeekDoImagesClient` a `Ludeka/1.0 (Comunidad de juegos de mesa en español; contacto@ludeka.com)`.
- Coherencia en `<PageTitle>` y descripciones de interfaz (ej. *«Ludeka — Juegos de mesa en español»* o similar sobrio y descriptivo).
- Actualización de `ROADMAP.md` y `ROADMAP_MVP_SLICES.md` reflejando el incremento en progreso.
- Mantenimiento del 100% de la suite de pruebas unitarias y de integración en verde.

### Fuera de Alcance:
- Reescribir commits pasados o archivos en `docs/increments/archive/` y `openspec/changes/archive/` (son registros inmutables de versiones anteriores).
- Cambios de diseño, layout o funcionalidad en las páginas afectadas más allá de la cadena de texto/etiquetas de identidad.
- Alteraciones en la lógica de negocio de catalogación, BGG o IA.

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Adopción del nuevo lema propio | *«Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.»* sustituye el antiguo claim informal en UI, PWA y pie, dotando a Ludeka de voz cálida y propia sin marcas ajenas. |
| **D-02** | User-Agent formal para BGG | El protocolo de BGG exige identificar la aplicación; se usa `Ludeka/1.0 (Bienvenido a tu mesa; contacto@ludeka.com)`. |
| **D-03** | Prompt de Gemini neutral y riguroso | En lugar de pedirle que imite a otra plataforma, se instruye: *"Eres un crítico y analista experto de juegos de mesa para Ludeka («Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa»)."* |
| **D-04** | PWA y Meta descripciones autónomas | La descripción pasa a centrarse en la propuesta de valor: *"Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa. Consulta tu colección, préstamos y estadísticas sin conexión."* |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Interfaz Web y PWA (Frontend)**
  - [x] 1.1 Limpiar `<PageTitle>` en `src/Ludeka.Web/Components/Pages/HomeDashboard.razor`.
  - [x] 1.2 Limpiar pie de página en `src/Ludeka.Web/Components/Layout/MainLayout.razor`.
  - [x] 1.3 Limpiar texto en `src/Ludeka.Web/Components/Pages/Login.razor`.
  - [x] 1.4 Limpiar texto en vista previa de `src/Ludeka.Web/Components/Pages/InstagramModeration.razor`.
  - [x] 1.5 Limpiar descripción del tema en `src/Ludeka.Web/Components/Pages/MyLibrary.razor` y comentario en `input.css`.
  - [x] 1.6 Actualizar `description` en `src/Ludeka.Web/wwwroot/manifest.webmanifest`.
- [x] **ODD-2 — Servicios de Infraestructura (Backend / Clientes)**
  - [x] 2.1 Actualizar prompt de sistema en `src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs` (líneas 366 y 517).
  - [x] 2.2 Actualizar cabecera User-Agent en `src/Ludeka.Infrastructure/Bgg/GeekDoImagesClient.cs` (línea 60).
- [x] **ODD-3 — Especificaciones y Documentación Activa**
  - [x] 3.1 Actualizar `AGENTS.md`, `GEMINI.md`, `axiom.yaml` y `openspec/config.yaml`.
  - [x] 3.2 Actualizar `openspec/specs/social-card-generator/spec.md` y `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md`.
  - [x] 3.3 Actualizar `docs/specs/sistema/15-dashboard-inicio-editorial.md` y `docs/specs/sistema/22-portada-y-directorio-creadores.md`.
- [x] **ODD-4 — Sincronización del Roadmap y Metadatos SDD**
  - [x] 4.1 Actualizar estado en `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 4.2 Establecer cambio formal en `openspec/changes/change-55-tagline-identidad-marca/`.
- [x] **ODD-5 — Verificación Automatizada y Auditoría de Cero Matches**
  - [x] 5.1 Ejecutar `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` asegurando 0 regresiones (1.639 superadas, 0 fallos).
  - [x] 5.2 Ejecutar auditoría ripgrep/grep sobre archivos vivos comprobando cero ocurrencias de `Letterboxd`.

---

## 6. Criterios de Aceptación

1. **Cero coincidencias en código y UI:** La búsqueda sensible/insensible a mayúsculas de `letterboxd` en `src/` no devuelve ningún resultado.
2. **Cero coincidencias en docs vivos:** La búsqueda en `AGENTS.md`, `GEMINI.md`, `axiom.yaml`, `openspec/config.yaml` y `docs/specs/sistema/` no devuelve referencias al antiguo tagline con la marca ajena.
3. **Consistencia en PWA y UI:** El título y descripción de la aplicación comunican su propósito funcional sin inconsistencias.
4. **Verificación verde:** La suite completa de pruebas unitarias (`dotnet test`) se ejecuta de principio a fin sin fallos.
