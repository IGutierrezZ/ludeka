# Tareas de Implementación: change-07-production-assets-perf (Incremento 7: Compilación de Producción, Optimización de Assets y Rendimiento Web)

## Fase 1: Capa de Aplicación y Caché en Memoria (`src/Ludeka.Application`)
- [x] 1.1 Implementar el decorador `CachedCatalogService` en `Features/Catalog/CachedCatalogService.cs` con `IMemoryCache`, expiración deslizante (10m) y método `Invalidate()`.
- [x] 1.2 Implementar pruebas unitarias en `tests/Ludeka.UnitTests/Application/CachedCatalogServiceTests.cs` validando Cache Hit, Cache Miss e invalidación por slug.

## Fase 2: Pipeline de Tailwind CSS y Assets Estáticos (`src/Ludeka.Web`)
- [x] 2.1 Crear `src/Ludeka.Web/tailwind.config.js` con rutas a componentes Razor, tema de Ludeka y `safelist` para clases dinámicas.
- [x] 2.2 Crear `src/Ludeka.Web/Styles/input.css` incorporando `@tailwind base; @tailwind components; @tailwind utilities;` y los estilos de componentes editoriales (sin `@import` de fuentes).
- [x] 2.3 Compilar y purgar `src/Ludeka.Web/wwwroot/app.css` mediante Tailwind CLI (`npx -y tailwindcss@3.4.17 --minify`).

## Fase 3: Core Web Vitals, Preconexiones y Estabilidad Visual (Zero CLS)
- [x] 3.1 Actualizar `Components/App.razor`: preconexiones anticipadas (`fonts.googleapis.com`, `fonts.gstatic.com`, `cf.geekdo-images.com`) y carga optimizada de tipografías con `display=swap`.
- [x] 3.2 Actualizar `Components/Pages/GameDetail.razor`: carátula principal con `fetchpriority="high"`, `decoding="async"` y contenedor rígido `aspect-square`.
- [x] 3.3 Actualizar `Components/Shared/GameCard.razor`: contenedor `cover-wrapper` con `aspect-square`, `decoding="async"` y dimensiones explícitas.
- [x] 3.4 Actualizar `Components/Shared/CatalogSearchBar.razor`: implementar `@implements IDisposable` y liberación segura del temporizador `_debounceTimer`.
- [x] 3.5 Actualizar `Components/Layout/MainLayout.razor`: dimensiones fijas `width="36"` y `height="36"` en el logotipo para prevenir layout shifts.

## Fase 4: Auditoría y Accesibilidad Universal WCAG 2.2 Nivel AA
- [x] 4.1 Actualizar `Components/App.razor`: cambiar atributo raíz a `<html lang="es">`.
- [x] 4.2 Actualizar `Components/Layout/MainLayout.razor`:
  - Añadir enlace de salto (*Skip Link*) accesible: `<a href="#main-content" class="sr-only focus:not-sr-only ...">Saltar al contenido principal</a>`.
  - Añadir `id="main-content"` y `tabindex="-1"` a la etiqueta `<main>`.
  - Añadir `aria-label` descriptivo al botón de alternar rol de usuario.
  - Traducir al español las cadenas del bloque `blazor-error-ui`.
- [x] 4.3 Estandarizar modales con atributos ARIA (`role="dialog"`, `aria-modal="true"`, `aria-labelledby` y botón con `aria-label="Cerrar modal"`):
  - `Components/Shared/MediaEmbedModal.razor`
  - `Components/Shared/SocialCardModal.razor`
  - `Components/Shared/LoanModal.razor`
  - `Components/Shared/FoundingVerdictModal.razor`
  - `Components/Shared/ReviewBottomSheet.razor`
  - Modal de sorteo en `Components/Pages/Radar.razor`
- [x] 4.4 Semántica ARIA en pestañas horizontales (`role="tablist"`, `role="tab"`, `aria-selected`, `role="tabpanel"`):
  - `Components/Shared/MultimediaHub.razor`
  - `Components/Pages/Radar.razor`
  - `Components/Pages/MyLibrary.razor`
- [x] 4.5 Controles de formulario accesibles:
  - `Components/Shared/RuleQuestionsSection.razor` (asociación de `<label for="...">` con inputs).
  - `Components/Shared/ScalabilityTrafficLight.razor` (`role="img"` o `role="status"` en chips de escalabilidad).
  - Atributos `aria-hidden="true"` en iconos decorativos SVG.

## Fase 5: Configuración de Caching y Pipeline HTTP (`src/Ludeka.Web/Program.cs`)
- [x] 5.1 Registrar `AddMemoryCache()` e inyectar `CachedCatalogService` como implementación de `ICatalogService`.
- [x] 5.2 Configurar `AddOutputCache()` con políticas `CatalogCache`, `RadarCache` y `StaticPages`, y habilitar `app.UseOutputCache()`.
- [x] 5.3 Crear pruebas unitarias en `tests/Ludeka.UnitTests/Web/PerformanceAndAccessibilityTests.cs` verificando la configuración del middleware y políticas de caché.

## Fase 6: Verificación Integral, Cierre y Archivo SDD
- [x] 6.1 Ejecutar suite completa de pruebas unitarias (`dotnet test src/Ludeka.slnx`), asegurando 100% de tests en verde.
- [x] 6.2 Generar reporte de verificación `verify-report.md`.
- [x] 6.3 Archivar el incremento bajo `.openspec/changes/archive/2026-09-06-change-07-production-assets-perf/` y actualizar `ROADMAP_MVP_SLICES.md`.
