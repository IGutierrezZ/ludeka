# Diseño Técnico: change-16-pwa-offline-library (Incremento 16: PWA y Modo Consulta Offline para Ludoteca)

## 1. Arquitectura del Sistema y Flujo Offline

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                 Navegador del Usuario (PWA)                           │
│                                                                                        │
│  ┌───────────────────────┐         ┌────────────────────────┐                          │
│  │   App.razor           │         │  MainLayout.razor      │                          │
│  │  - manifest.webmanif. │         │  - <OfflineIndicator/> │                          │
│  │  - SW Registration    │         └───────────┬────────────┘                          │
│  └───────────┬───────────┘                     │                                       │
│              │                                 ▼                                       │
│              │                     ┌───────────────────────┐                           │
│              │                     │   MyLibrary.razor     │                           │
│              │                     │  (Online vs Offline)  │                           │
│              │                     └───────────┬───────────┘                           │
│              │                                 │                                       │
│              ▼                                 ▼                                       │
│  ┌───────────────────────┐         ┌───────────────────────┐                           │
│  │   service-worker.js   │         │   ludeka-offline.js   │                           │
│  │  (Caché estática HTTP)│         │ (localStorage Sync)   │                           │
│  └───────────┬───────────┘         └───────────┬───────────┘                           │
│              │                                 │                                       │
└──────────────┼─────────────────────────────────┼───────────────────────────────────────┘
               │                                 │
     (Network Fallback)                 (Snapshot JSON)
               │                                 │
               ▼                                 ▼
   ┌───────────────────────┐         ┌───────────────────────┐
   │     offline.html      │         │     localStorage      │
   │   (Página respaldo)   │         │ (ludeka_offline_lib)  │
   └───────────────────────┘         └───────────────────────┘
```

---

## 2. Manifiesto de Aplicación Web (`manifest.webmanifest`)

Ubicación: `src/Ludeka.Web/wwwroot/manifest.webmanifest`
```json
{
  "$schema": "https://json.schemastore.org/web-manifest-combined.json",
  "name": "Ludeka — Tu Estantería de Juegos de Mesa",
  "short_name": "Ludeka",
  "start_url": "/mi-ludoteca",
  "scope": "/",
  "display": "standalone",
  "orientation": "any",
  "theme_color": "#d97706",
  "background_color": "#18181b",
  "description": "El Letterboxd de los juegos de mesa en español. Consulta tu colección, préstamos y estadísticas sin conexión.",
  "lang": "es",
  "categories": ["entertainment", "games", "lifestyle"],
  "icons": [
    {
      "src": "/icons/icon-192.svg",
      "sizes": "192x192",
      "type": "image/svg+xml",
      "purpose": "any"
    },
    {
      "src": "/icons/icon-512.svg",
      "sizes": "512x512",
      "type": "image/svg+xml",
      "purpose": "any"
    },
    {
      "src": "/icons/icon-maskable.svg",
      "sizes": "512x512",
      "type": "image/svg+xml",
      "purpose": "maskable"
    }
  ]
}
```

---

## 3. Especificación del Service Worker (`service-worker.js`)

Ubicación: `src/Ludeka.Web/wwwroot/service-worker.js`

1. **Versión y Nombre de Caché:** `const CACHE_NAME = 'ludeka-v1';`
2. **Pre-cacheo en `install`:**
   - Recursos estáticos esenciales:
     - `/`
     - `/app.css`
     - `/offline.html`
     - `/manifest.webmanifest`
     - `/images/game-placeholder.svg`
     - `/images/expansion-placeholder.svg`
     - `/icons/icon-192.svg`
     - `/icons/icon-512.svg`
     - `/icons/icon-maskable.svg`
3. **Limpieza en `activate`:**
   - Itera sobre las llaves de `caches.keys()` y borra cualquier caché antigua que no sea `CACHE_NAME`.
4. **Estrategia en `fetch`:**
   - Si `request.mode === 'navigate'`:
     - Intenta resolver por red (`fetch(request)`).
     - Si falla, busca en caché.
     - Si no está en caché, sirve `/offline.html`.
   - Si la URL pertenece a `images/`:
     - Stale-While-Revalidate con fallback a placeholder si no hay red.
   - Si es recurso estático (`.css`, `.js`, `.woff2`, `.svg`):
     - Cache-First con fallback a red.

---

## 4. Persistencia Local e Interoperabilidad (`ludeka-offline.js`)

Ubicación: `src/Ludeka.Web/wwwroot/js/ludeka-offline.js`

El script expone utilidades accesibles tanto desde JavaScript nativo como mediante `IJSRuntime`:

```javascript
window.LudekaOffline = {
    // Guarda instantánea de la ludoteca
    saveLibrarySnapshot: function(userId, data) {
        try {
            var payload = {
                timestamp: new Date().toISOString(),
                userId: userId,
                data: data
            };
            localStorage.setItem('ludeka_offline_lib_' + userId, JSON.stringify(payload));
            return true;
        } catch (e) {
            return false;
        }
    },

    // Recupera la instantánea local guardada
    getLibrarySnapshot: function(userId) {
        try {
            var raw = localStorage.getItem('ludeka_offline_lib_' + userId);
            if (!raw) return null;
            return JSON.parse(raw);
        } catch (e) {
            return null;
        }
    },

    // Comprueba conectividad actual
    isOnline: function() {
        return navigator.onLine;
    },

    // Registra listener de cambios de conectividad con callback a DotNet
    initConnectivityListener: function(dotNetHelper) {
        function updateStatus() {
            dotNetHelper.invokeMethodAsync('OnConnectivityChanged', navigator.onLine);
        }
        window.addEventListener('online', updateStatus);
        window.addEventListener('offline', updateStatus);
        return navigator.onLine;
    }
};
```

---

## 5. DTOs de Sincronización Offline (`Ludeka.Application`)

```csharp
namespace Ludeka.Application.DTOs;

public record OfflineLibrarySnapshotDto(
    string UserId,
    DateTimeOffset Timestamp,
    int TotalInCollection,
    int TotalPlayed,
    int TotalWishlist,
    int TotalWantToBuy,
    int TotalActiveLoans,
    List<UserCollectionItemDto> Items,
    List<GameLoanDto> ActiveLoans
);
```

---

## 6. Componente Visual: `OfflineIndicator.razor`

Ubicación: `src/Ludeka.Web/Components/Shared/OfflineIndicator.razor`
- Inyectado en `MainLayout.razor` justo encima del contenido principal o en la barra de estado.
- Atributos ARIA: `role="status"` y `aria-live="polite"`.
- Estilos con Tailwind:
  - Cuando offline: fondo amarillo/ámbar sutil (`bg-amber-500/15 border-amber-500/30 text-amber-600 dark:text-amber-400`), icono `"📡"`, microtexto *"Modo sin conexión — Estás consultando datos locales"*.
  - Al recuperar conexión: transición breve (3 segundos) a verde esmeralda (`bg-emerald-500/15 text-emerald-600`), icono `"🟢"`, microtexto *"Conexión restablecida"*.

---

## 7. Plan de Pruebas Unitarias y Automatizadas

1. **Pruebas de Manifiesto Web y Assets PWA:**
   - `ManifestValidationTests`: valida que `manifest.webmanifest` es un JSON válido, contiene los campos requeridos (`name`, `icons`, `start_url`, `display: standalone`), y los iconos referenciados existen físicamente.
2. **Pruebas de Service Worker y Fallback:**
   - `ServiceWorkerSyntaxTests`: valida la sintaxis y existencia de `service-worker.js` y `offline.html`.
3. **Pruebas de DTOs y Mapeo de Instantáneas:**
   - `OfflineLibrarySnapshotTests`: valida serialización/deserialización, preservación de préstamos e integridad de campos.
4. **Verificación Global:**
   - Ejecución de `dotnet test` y confirmación de suite completa en verde.
