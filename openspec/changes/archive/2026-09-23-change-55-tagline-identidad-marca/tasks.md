# Tareas de Implementación — INC-55: Retirada del Tagline de Marca y Unificación de la Identidad

- [x] 1. **Saneamiento de Frontend y PWA**
  - [x] 1.1 Actualizar `<PageTitle>` en `src/Ludeka.Web/Components/Pages/HomeDashboard.razor`.
  - [x] 1.2 Actualizar pie de página en `src/Ludeka.Web/Components/Layout/MainLayout.razor`.
  - [x] 1.3 Actualizar texto en `src/Ludeka.Web/Components/Pages/Login.razor`.
  - [x] 1.4 Actualizar vista previa en `src/Ludeka.Web/Components/Pages/InstagramModeration.razor`.
  - [x] 1.5 Actualizar descripción de tema en `src/Ludeka.Web/Components/Pages/MyLibrary.razor` y comentario en `src/Ludeka.Web/Styles/input.css`.
  - [x] 1.6 Actualizar `description` en `src/Ludeka.Web/wwwroot/manifest.webmanifest`.

- [x] 2. **Saneamiento de Infraestructura y Servicios**
  - [x] 2.1 Actualizar prompt del analista en `src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs` (líneas 366 y 517).
  - [x] 2.2 Actualizar User-Agent en `src/Ludeka.Infrastructure/Bgg/GeekDoImagesClient.cs` (línea 60).

- [x] 3. **Actualización de Especificaciones y Documentación Activa**
  - [x] 3.1 Actualizar cabecera de proyecto en `AGENTS.md` y `GEMINI.md`.
  - [x] 3.2 Actualizar declaraciones en `axiom.yaml` y `openspec/config.yaml`.
  - [x] 3.3 Actualizar `openspec/specs/social-card-generator/spec.md` y `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md`.
  - [x] 3.4 Armonizar menciones en `docs/specs/sistema/15-dashboard-inicio-editorial.md` y `docs/specs/sistema/22-portada-y-directorio-creadores.md`.

- [x] 4. **Verificación y Auditoría**
  - [x] 4.1 Ejecutar suite completa `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` asegurando cero fallos (1.639 pruebas superadas).
  - [x] 4.2 Ejecutar búsqueda grep/ripgrep para acreditar cero ocurrencias vivas de `Letterboxd`.
  - [x] 4.3 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
