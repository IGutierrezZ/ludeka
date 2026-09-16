# Propuesta: change-07-production-assets-perf (Incremento 7: Compilación de Producción, Optimización de Assets y Rendimiento Web)

## 1. Resumen Ejecutivo y Motivación

El **Incremento 7** da el pistoletazo de salida a la **Fase Post-MVP (Consolidación, Rendimiento y Operaciones)** de Ludeka (`ROADMAP_MVP_SLICES.md`). Una vez completados con éxito los 6 incrementos de funcionalidades clave (Catálogo, Colección/Préstamos, Veredicto Fundador, Hub Multimedia, Importador BGG y Comunidad/Sorteos/Q&A), este incremento garantiza que la plataforma ofrezca **una experiencia de carga instantánea, una accesibilidad universal impecable (WCAG 2.2 AA) y una huella de red mínima** en dispositivos móviles.

### ¿Por qué este incremento es fundamental ahora?
1. **Rendimiento Móvil Radical y Core Web Vitals:** La mayoría de consultas lúdicas (buscar reglas en mitad de partida, consultar escalabilidad en tienda física o registrar préstamos) se realizan desde smartphones con conexiones móviles variables. Necesitamos un LCP < 1.2s, CLS = 0 y tiempos de respuesta de milisegundos mediante caché multinivel.
2. **Purga y Compilación de Tailwind CSS:** Actualmente los componentes Blazor usan decenas de clases de utilidad que no están compiladas en el CSS, o dependen de un archivo plano sin purga. Con Node.js y `tailwindcss` (v3.4.17) configurados en el proyecto, podemos generar un stylesheet minificado que descarte el 95% del CSS no usado.
3. **Caché en Memoria y Output Caching en ASP.NET Core 10:** Evita consultas redundantes a SQLite en cada renderizado de la página de inicio, catálogo, fichas y radar de sorteos, sirviendo peticiones directamente desde memoria RAM con políticas de invalidación granular.
4. **Cumplimiento Estricto de Accesibilidad (WCAG 2.2 Nivel AA):** Garantiza que usuarios con tecnologías de asistencia o navegación por teclado disfruten de la misma agilidad: lenguaje en español (`<html lang="es">`), navegación por salto (*skip link*), modales con foco atrapado y atributos `role="dialog"`, y pestañas con semántica ARIA.

---

## 2. Alcance Detallado de la Solución

### 2.1 Pipeline de Assets y Tailwind CSS
- **Configuración de Tailwind CLI (`tailwind.config.js` y `input.css`):**
  - Configurar `tailwind.config.js` en `src/Ludeka.Web` apuntando a todos los archivos de plantillas (`./Components/**/*.razor`, `./wwwroot/**/*.html`).
  - Definir la paleta cromática editorial oficial de Ludeka (`brand-primary: #FF5A36`, `bg-main: #0B0F17`, semáforo `#10B981`, `#F59E0B`, `#EF4444`).
  - Crear `src/Ludeka.Web/Styles/input.css` que combine directivas `@tailwind base; @tailwind components; @tailwind utilities;` con los componentes editoriales del monorepo (`.traffic-chip`, `.badge-pill`, `.game-card-editorial`, etc.).
  - Configurar script de compilación para generar `src/Ludeka.Web/wwwroot/app.min.css` (o purgar `app.css`) con compresión.
- **Optimización de Fuentes e Hiperenlaces Críticos en `<head>`:**
  - Extraer `@import` de fuentes remotas fuera del CSS.
  - Añadir en `App.razor` preconexiones anticipadas:
    ```html
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link rel="preconnect" href="https://cf.geekdo-images.com" crossorigin />
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@500;700&display=swap" />
    ```

### 2.2 Optimización Core Web Vitals (LCP, CLS, INP)
- **Eliminación de Cumulative Layout Shift (CLS):**
  - Dotar a todos los contenedores de carátulas y fotos (`GameCard`, `GameDetail`, `GiveawayCard`, `MultimediaHub`, `MyLibrary`) de ratios de aspecto explícitos (`aspect-square`, `aspect-video`) y atributos `width` y `height`.
  - Asegurar que la tipografía use `font-display: swap` con fuentes del sistema de respaldo bien emparejadas para evitar saltos tipográficos.
- **Aceleración de Largest Contentful Paint (LCP):**
  - En la ficha de juego (`GameDetail.razor`), marcar la carátula principal con `fetchpriority="high"` y `decoding="async"`.
  - En la página principal (`Home.razor`), la primera fila de tarjetas no retrasará la carga de imágenes.
- **Optimización de INP y Limpieza de Recursos:**
  - En `CatalogSearchBar.razor`, implementar `@implements IDisposable` para desechar rigurosamente el temporizador de debounce y evitar fugas de memoria y bloqueos de hilo UI.

### 2.3 Estrategia de Caché Avanzada (En Memoria y Output Caching)
- **Capa de Caché de Aplicación (`CachedCatalogService`):**
  - Crear un decorador `CachedCatalogService` que implemente `ICatalogService` e inyecte `IMemoryCache`.
  - Cachear resultados de catálogo frecuente (por filtros y páginas) y fichas de detalle (`GetGameBySlugAsync`) con política de expiración deslizante (10 min) y tamaño máximo.
  - Exponer método o mecanismo de invalidación cuando se añade o modifica un juego o veredicto.
- **Output Caching y Response Caching en ASP.NET Core 10:**
  - Habilitar `builder.Services.AddOutputCache()` y `app.UseOutputCache()`.
  - Definir políticas con tags:
    - Política `"CatalogCache"` (10 minutos, tag `tag-catalog`).
    - Política `"RadarCache"` (5 minutos, tag `tag-radar`).
    - Política `"StaticPages"` (60 minutos, tag `tag-static`).
  - Configurar cabeceras HTTP de caché eficientes para assets estáticos mediante `app.MapStaticAssets()`.

### 2.4 Optimización Multimedia
- Dotar a todas las imágenes secundarias de `loading="lazy"` y `decoding="async"`.
- Implementar manejo de fallbacks ante errores de carga de carátulas (por ejemplo, imágenes caídas en BGG).

### 2.5 Accesibilidad Universal (WCAG 2.2 Nivel AA)
- **Idioma y Metadatos:** Cambiar `<html lang="en">` por `<html lang="es">` en `App.razor`.
- **Enlace de Salto (Skip Link):** Añadir `<a href="#main-content" class="skip-link sr-only focus:not-sr-only ...">Saltar al contenido principal</a>` en `MainLayout.razor` y asociar `id="main-content"` en la etiqueta `<main>`.
- **Accesibilidad en Diálogos y Modales:**
  - Estandarizar todos los modales (`MediaEmbedModal`, `SocialCardModal`, `LoanModal`, `FoundingVerdictModal`, `ReviewBottomSheet`, modal de sorteos en `Radar.razor`) con:
    - `role="dialog"`
    - `aria-modal="true"`
    - `aria-labelledby="[id-del-titulo]"`
    - Botones de cierre con `aria-label="Cerrar modal"` en español.
- **Pestañas y Controles Semánticos:**
  - Dotar a los selectores de pestañas en `MultimediaHub`, `Radar` y `MyLibrary` de `role="tablist"`, `role="tab"`, `aria-selected="true/false"` y `aria-controls`.
  - Habilitar activación por teclado (Enter / Espacio) en tarjetas interactivas de vídeo.
- **Formularios e Inputs:**
  - Garantizar que todos los campos `<input>`, `<textarea>` y `<select>` cuenten con su etiqueta `<label for="...">` asociada o atributo `aria-label`.
- **Corrección Lingüística:**
  - Traducir los textos de error residuales en inglés de `MainLayout.razor` ("An unhandled error has occurred") al español castellano.

---

## 3. Plan de Cambios por Capa

### 3.1 Capa de Aplicación (`src/Ludeka.Application`)
- `Features/Catalog/CachedCatalogService.cs` (Nuevo decorador con `IMemoryCache` para `ICatalogService`).

### 3.2 Capa Web (`src/Ludeka.Web`)
- `tailwind.config.js` (Configuración de purga de Tailwind con rutas a `.razor` y tema Ludeka).
- `Styles/input.css` (Estilos base + utilidades + componentes propios).
- `wwwroot/app.css` (Hoja de estilos purgada y optimizada para producción).
- `Program.cs` (Registro de `AddOutputCache()`, `UseOutputCache()`, decorador `CachedCatalogService` y políticas de caché).
- `Components/App.razor` (Atributo `lang="es"`, preconexiones `<link rel="preconnect">` a Google Fonts y BGG, precarga de CSS).
- `Components/Layout/MainLayout.razor` (Skip link accesible, traducción de error UI, accesibilidad en botones de rol).
- `Components/Pages/Home.razor` (Optimización LCP de carátulas, accesibilidad en filtros).
- `Components/Pages/GameDetail.razor` (Carátula hero con `fetchpriority="high"`, `decoding="async"` y dimensiones anti-CLS).
- `Components/Pages/Radar.razor` (Semántica ARIA en pestañas y atributos accesibles en el modal de sorteo).
- `Components/Pages/MyLibrary.razor` (Semántica ARIA en pestañas y dimensiones de imágenes).
- `Components/Shared/MultimediaHub.razor` (Semántica ARIA en pestañas, navegación por teclado en tarjetas y `loading="lazy"`).
- `Components/Shared/CatalogSearchBar.razor` (Implementación de `IDisposable` para el temporizador y atributos ARIA en SVG).
- Modales (`MediaEmbedModal`, `SocialCardModal`, `LoanModal`, `FoundingVerdictModal`, `ReviewBottomSheet`): Incorporación de `role="dialog"`, `aria-modal="true"`, `aria-labelledby` y `aria-label` en cierres.

### 3.3 Capa de Pruebas (`tests/Ludeka.UnitTests`)
- `Application/CachedCatalogServiceTests.cs` (Verificación de hits, misses, expiración y llamadas al repositorio subyacente).
- `Web/AccessibilityAndPerfTests.cs` (Pruebas unitarias que validan la correcta configuración de políticas de caché y marcado semántico).

---

## 4. Riesgos y Mitigaciones

| Riesgo Identificado | Impacto | Mitigación |
|---|---|---|
| Purga excesiva de clases dinámicas de Tailwind | Algún componente podría perder estilos en runtime | Incluir `safelist` en `tailwind.config.js` para clases generadas por helpers de C# (como badges de estados, semáforos y colores dinámicos). |
| Datos desactualizados en caché del catálogo | Usuarios podrían no ver inmediatamente cambios al sembrar o crear juegos | Tiempos de expiración razonables (10 min) + método de desalojo programático (`Invalidate()`). |
| Incompatibilidad de Output Cache con Blazor InteractiveServer | Posibles conflictos con WebSockets de SignalR | Aplicar Output Cache con precisión en peticiones GET de renderizado inicial y endpoints estáticos; la interactividad del circuito WebSocket no se altera. |

---

## 5. Pregunta para Aprobación del Usuario (Lossless Blocking Prompt)

¿Apruebas esta propuesta para dar comienzo a la fase **`sdd-spec`** (Especificación técnica detallada y criterios de aceptación Gherkin) del **Incremento 7: Compilación de Producción, Optimización de Assets y Rendimiento Web**?
