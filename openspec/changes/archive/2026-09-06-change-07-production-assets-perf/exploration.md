# Exploración: change-07-production-assets-perf (Incremento 7: Compilación de Producción, Optimización de Assets y Rendimiento Web)

## 1. Estado Actual del Monorepo y la Solución

### 1.1 Arquitectura y Proyectos (.NET 10 y C# 13)
- **Solución `Ludeka.slnx`:** 4 capas en Clean Architecture (`Ludeka.Core`, `Ludeka.Application`, `Ludeka.Infrastructure`, `Ludeka.Web`) y proyecto de pruebas `Ludeka.UnitTests`.
- **146 pruebas unitarias e integración pasando en verde al 100%**:
  - Incremento 1 (`change-01-core-catalog`): Catálogo base, ficha inteligente, ADN lúdico, semáforo dinámico de escalabilidad y cliente BGG XMLAPI2.
  - Incremento 2 (`change-02-library-loans`): Ludoteca personal en 4 estados, cuaderno de préstamos y reseñas comunitarias.
  - Incremento 3 (`change-03-founding-verdict`): Veredicto oficial de la Mesa Fundadora, galería fotográfica y reemplazo de síntesis IA.
  - Incremento 4 (`change-04-multimedia-hub`): Hub multimedia segregado (tutoriales, partidas y redes) con panel móvil de moderación.
  - Incremento 5 (`change-05-bgg-importer`): Importador BGG en 1 clic, cola comunitaria y buscador asistido en tiempo real.
  - Incremento 6 (`change-06-automation-community`): Automatización omnicanal (carteles 1:1 para Instagram), radar de sorteos, novedades semanales, consultorio Q&A y manifiesto de transparencia.

### 1.2 Diagnóstico Técnico de Rendimiento, Assets y Accesibilidad

Tras una auditoría exhaustiva del código fuente, hojas de estilo y componentes Blazor, se han identificado las siguientes oportunidades de mejora crítica:

1. **Pipeline de Tailwind CSS y Assets Estáticos:**
   - `src/Ludeka.Web/wwwroot/app.css` contiene actualmente 274 líneas con estilos base personalizados y selectores concretos (`.badge-pill`, `.traffic-chip`, `.game-card-editorial`), pero no cuenta con un paso de purga ni compilación automatizada de las utilidades de Tailwind (`p-6`, `rounded-2xl`, `bg-slate-900/70`, `grid`, etc.).
   - `@import url('https://fonts.googleapis.com/css2?...')` se encuentra en la cabecera de `app.css`. En el navegador, los `@import` bloquean la construcción del CSSOM y desencadenan una cascada de red secuencial en lugar de paralela, retrasando severamente el LCP (Largest Contentful Paint).
   - Node.js (v20.18.0) y npm/npx están disponibles en el entorno local, permitiendo utilizar `tailwindcss` (v3.4.17) para purgar clases no utilizadas, minificar el archivo y generar un bundle de peso ultra-reducido.

2. **Core Web Vitals (LCP, CLS, INP):**
   - **Cumulative Layout Shift (CLS):** La mayoría de elementos `<img>` (`GameCard.razor`, `GameDetail.razor`, `MyLibrary.razor`, `MultimediaHub.razor`) carecen de atributos explícitos `width`/`height` o clases contenedoras con `aspect-ratio` rígido, provocando saltos de página (layout shift) durante la carga asíncrona de imágenes de BGG y fotos de mesa.
   - **Largest Contentful Paint (LCP):** Las carátulas de juego en el hero de `GameDetail.razor` y en la primera tarjeta de `Home.razor` no cuentan con `fetchpriority="high"`, y el navegador no establece preconexiones DNS/TLS tempranas hacia `cf.geekdo-images.com` ni `fonts.gstatic.com`.
   - **Interaction to Next Paint (INP) y Fugas de Memoria:** En `CatalogSearchBar.razor`, el temporizador `System.Threading.Timer` para debounce de búsqueda no implementa `@implements IDisposable`, lo que provoca fugas de timers al navegar entre rutas.

3. **Estrategia de Caché Avanzada:**
   - Actualmente, todas las consultas a `CatalogService.GetCatalogAsync` y `GetGameBySlugAsync` golpean la base de datos SQLite en cada navegación o recarga, sin capa de caché en memoria ni invalidación por tags.
   - No está configurado `Output Caching` (`builder.Services.AddOutputCache()` / `app.UseOutputCache()`) en ASP.NET Core para servir instantáneamente HTML prerenderizado en rutas de lectura intensiva (`/`, `/catalogo`, `/radar`, `/transparencia`).

4. **Optimización Multimedia:**
   - La mayoría de imágenes remotas no cuentan con atributos `loading="lazy"` ni `decoding="async"`.
   - No hay fallbacks defensivos consistentes (`onerror`) ante carátulas borradas o fallos de red en dominios externos de BGG.

5. **Accesibilidad WCAG 2.2 Nivel AA:**
   - En `App.razor`, la etiqueta raíz es `<html lang="en">` en lugar de `<html lang="es">`, lo que induce a los lectores de pantalla a pronunciar en inglés el contenido en español.
   - Falta un enlace de salto de navegación accesible (*Skip to main content* / *Saltar al contenido principal*) antes del encabezado.
   - Múltiples modales (`MediaEmbedModal`, `SocialCardModal`, `LoanModal`, `FoundingVerdictModal`, `ReviewBottomSheet`, `Radar.razor`) carecen de atributos ARIA esenciales: `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, y botones de cierre con `aria-label="Cerrar modal"`.
   - Varios selectores de pestañas (`MultimediaHub.razor`, `Radar.razor`, `MyLibrary.razor`) no implementan la semántica `role="tablist"`, `role="tab"` y `aria-selected`.
   - Formularios con inputs huérfanos sin `<label for="...">` o `aria-label`.
   - Microtextos de error en `MainLayout.razor` en inglés ("An unhandled error has occurred. Reload").

---

## 2. Requerimientos Técnicos del Incremento 7

Bajo la hoja de ruta de `ROADMAP_MVP_SLICES.md`, el Incremento 7 aborda 5 áreas clave:

### 2.1 Pipeline de Assets y Tailwind CSS Optimizado
- Configurar un flujo de compilación de Tailwind CSS mediante `npx tailwindcss` con archivo de configuración `tailwind.config.js` y `input.css` enriquecido.
- Escanear todos los componentes `.razor` y `.html` para purgar clases no utilizadas, produciendo un archivo `app.min.css` (o `app.css` optimizado) con minificación para producción.
- Eliminar `@import` de fuentes remotas en el CSS; migrar la carga de fuentes a `<head>` en `App.razor` con `<link rel="preconnect">` hacia Google Fonts y los servidores de imágenes de BGG (`cf.geekdo-images.com`).

### 2.2 Auditoría y Optimización Core Web Vitals
- **LCP (< 1.2s en móvil):** Inyectar `fetchpriority="high"` en la carátula principal del hero de `GameDetail.razor` y en las primeras carátulas destacadas. Preconexión anticipada a CDN de carátulas BGG.
- **CLS (Objetivo: 0.00):** Asignar ratios de aspecto explícitos (`aspect-square`, `aspect-video`) y dimensiones `width`/`height` a todos los contenedores e imágenes para reservar el espacio antes de la descarga del asset.
- **INP e Interactividad:** Corregir la disposición del temporizador en `CatalogSearchBar.razor` implementando `IDisposable`. Asegurar que las interacciones del teclado y renderizado de componentes Blazor Server sean instantáneas.

### 2.3 Estrategia de Caché en Memoria y Output Caching
- **Servicio Decorador de Caché en Memoria (`CachedCatalogService`):**
  - Implementar un servicio que envuelva a `ICatalogService` utilizando `IMemoryCache`.
  - Cachear listados de catálogo filtrados y fichas detalladas por slug con expiración deslizante (ej. 10 minutos).
  - Permitir invalidación controlada del caché cuando se registran veredictos fundadores o se actualiza la cola de BGG.
- **Configuración de Output Caching y Response Caching en ASP.NET Core 10:**
  - Registrar `builder.Services.AddOutputCache()` y `app.UseOutputCache()`.
  - Definir políticas de caché con tags (`tag-catalog`, `tag-radar`, `tag-static`) y tiempos de vida diferenciados (5 min para radar, 10 min para catálogo, 1 hora para transparencia).
  - Añadir cabeceras de `Cache-Control` en assets estáticos a través de `MapStaticAssets()`.

### 2.4 Optimización Multimedia
- Atributos `loading="lazy"` y `decoding="async"` en todas las imágenes secundarias, miniaturas de tutoriales, partidas, reseñas y fotografías de mesa real.
- Sustitución elegante de imágenes no disponibles mediante placeholder local SVG o contenedor estilizado sin rotura de maquetación.

### 2.5 Accesibilidad WCAG 2.2 AA
- Declaración de idioma correcta: `<html lang="es">`.
- Añadir enlace accesible "Saltar al contenido principal" (`skip-link`) enfocado al pulsar `Tab`.
- Adición de roles ARIA semánticos en todos los modales (`role="dialog"`, `aria-modal="true"`, `aria-labelledby`) y botones con `aria-label` descriptivo en español.
- Semántica ARIA en pestañas horizontales (`role="tablist"`, `role="tab"`, `aria-selected="true/false"`).
- Atributos accesibles en campos de formulario y controles de rango.
- Traducción estricta de cadenas residuales en inglés en `MainLayout.razor`.

---

## 3. Estrategia de Pruebas y Validación

1. **Pruebas Unitarias Automatizadas en `Ludeka.UnitTests`:**
   - Nuevos tests para `CachedCatalogService`: verificación de aciertos de caché (*cache hit*), ausencias (*cache miss*), y correcta invalidación tras mutaciones.
   - Tests de políticas y configuración de caché en ASP.NET Core.
   - Tests de regresión asegurando que las 146 pruebas actuales se mantengan al 100% en verde.
2. **Auditoría de Marcado y Accesibilidad:**
   - Verificación estática de atributos `lang="es"`, `alt`, `aria-label`, `loading="lazy"`, `decoding="async"`, `fetchpriority="high"` y roles en los componentes Razor modificados.
3. **Compilación y Purga de Tailwind:**
   - Verificación de que el comando de generación de CSS compila sin errores y reduce el tamaño de los estilos garantizando la correcta visualización de todos los componentes.
