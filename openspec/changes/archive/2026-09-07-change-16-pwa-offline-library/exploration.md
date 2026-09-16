# Exploración: change-16-pwa-offline-library (Incremento 16: PWA y Modo Consulta Offline para Ludoteca)

## 1. Estado Actual de la Solución y Análisis de Brecha

### 1.1 Estado Actual de la Web App
- **Arquitectura Blazor Web App (.NET 10):**
  - La aplicación corre sobre Blazor con componentes interactivos bajo `@rendermode InteractiveServer` y páginas renderizadas por SSR.
  - La colección personal se consulta a través de `IUserLibraryService` y se renderiza en [`MyLibrary.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MyLibrary.razor).
  - Si el usuario pierde la conexión (modo avión, sótano de una asociación, bar sin cobertura), el circuito SignalR se desconecta y el navegador muestra el modal de reconexión por defecto (`<ReconnectModal />`), impidiendo consultar los títulos de su ludoteca o préstamos activos.
- **Inexistencia de Manifiesto Web y Service Worker:**
  - Actualmente no existe `manifest.webmanifest`, meta-tags de app móvil ni Service Worker registrado en [`App.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/App.razor).
  - La aplicación no es instalable en la pantalla de inicio de Android (Chrome) ni iOS (Safari) como aplicación independiente (`display: standalone`).

---

## 2. Alternativas Técnicas y Retos Específicos de Blazor

### 2.1 El Desafío de Blazor Server / Interactive Server sin Conexión
En una aplicación puramente cliente (Blazor WebAssembly), el código C# se ejecuta en el navegador mediante WebAssembly. Sin embargo, en `InteractiveServer`, la lógica interactiva vive en el servidor ASP.NET Core conectado mediante un WebSocket de SignalR.
Cuando se corta la red:
- Las llamadas a endpoints y eventos interactivos en el servidor fallan.
- Si el usuario refresca la página o navega sin red, el servidor no puede responder.

### 2.2 Estrategia Híbrida Editorial para Ludeka
Para ofrecer una experiencia offline sólida en Ludeka sin requerir migrar todo a WebAssembly client-side, adoptamos una estrategia de 4 capas:
1. **Manifiesto PWA Completo (`manifest.webmanifest`):**
   - Nombre, iconos oficiales en SVG/PNG con resoluciones 192x192 y 512x512, color temático `#d97706`, `display: standalone` y modo horizontal/vertical adaptativo.
2. **Service Worker Ligero (`service-worker.js`):**
   - **Cache First:** Para assets estáticos inmutables (CSS compilado `app.css`, fuentes tipográficas de Google Fonts `Plus Jakarta Sans` y `JetBrains Mono`, iconos SVG, placeholders).
   - **Stale-While-Revalidate con límite de tamaño:** Para carátulas de juegos pre-cacheadas (`/images/games/*`).
   - **Network First con Fallback Offline:** Para rutas de navegación (`/mi-ludoteca`, `/`, etc.), sirviendo una página offline amigable [`offline.html`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/offline.html) o la versión cacheada si la red no responde.
3. **Caché Local de la Ludoteca del Usuario (`ludeka-offline.js`):**
   - Cada vez que el usuario carga `/mi-ludoteca` estando online, se almacena una instantánea compacta serializada (`ludeka_offline_library_v1`) en `localStorage`.
   - Incluye los títulos de su estantería, estados (`InCollection`, `Played`, etc.), carátulas locales/remotas y préstamos activos.
   - Si la app se inicia o recarga sin red, el cliente JS y el componente Razor pueden leer esta instantánea local y presentar la estantería completa con un aviso de *«Modo Consulta Offline»*.
4. **Indicador de Conexión y Estado Offline (`OfflineIndicator.razor`):**
   - Componente global en [`MainLayout.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Layout/MainLayout.razor) que monitoriza `navigator.onLine` y los eventos `online`/`offline` del navegador.
   - Píldora visual sutil y accesible (amarillo/ámbar en offline, transición a verde al recuperar red).

---

## 3. Impacto en el Monorepo y Archivos Afectados

- **Nuevos Archivos:**
  - `src/Ludeka.Web/wwwroot/manifest.webmanifest` (metadatos PWA).
  - `src/Ludeka.Web/wwwroot/service-worker.js` (lógica de cacheo y control de ciclo de vida).
  - `src/Ludeka.Web/wwwroot/service-worker.published.js` (para entornos de publicación).
  - `src/Ludeka.Web/wwwroot/offline.html` (pantalla de respaldo cuando no hay red ni caché de página).
  - `src/Ludeka.Web/wwwroot/js/ludeka-offline.js` (sincronización y lectura de instantáneas offline).
  - `src/Ludeka.Web/wwwroot/icons/` (iconos vectoriales y rasterizados 192x192 y 512x512).
  - `src/Ludeka.Web/Components/Shared/OfflineIndicator.razor` (indicador de estado en cabecera).
  - `src/Ludeka.Application/Contracts/IOfflineSyncService.cs` y `OfflineSyncDtos.cs`.
- **Archivos a Modificar:**
  - `src/Ludeka.Web/Components/App.razor` (etiquetas `<link rel="manifest">`, meta-tags móviles y registro del Service Worker).
  - `src/Ludeka.Web/Components/Layout/MainLayout.razor` (inclusión de `<OfflineIndicator />`).
  - `src/Ludeka.Web/Components/Pages/MyLibrary.razor` (soporte para hidratar desde instantánea offline si no hay conexión).
