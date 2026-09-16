# Tareas de Implementación: change-16-pwa-offline-library (Incremento 16)

## Tareas

- [x] **1. Manifiesto e Iconografía PWA (`Ludeka.Web/wwwroot`)**
  - [x] 1.1 Crear directorio `src/Ludeka.Web/wwwroot/icons` y generar iconos vectoriales `icon-192.svg`, `icon-512.svg` e `icon-maskable.svg` con la identidad visual de Ludeka (dado editorial y color `#d97706`).
  - [x] 1.2 Crear archivo `src/Ludeka.Web/wwwroot/manifest.webmanifest` con metadatos completos, modo `standalone` e iconos adaptativos.
  - [x] 1.3 Crear página de cortesía offline `src/Ludeka.Web/wwwroot/offline.html` con estética editorial, botón de reintento y acceso a la ludoteca.

- [x] **2. Service Worker y Script de Persistencia Local (`Ludeka.Web/wwwroot`)**
  - [x] 2.1 Implementar `src/Ludeka.Web/wwwroot/service-worker.js` con estrategias Cache-First para estáticos, Network-First con fallback para navegación y ciclo de vida defensivo (`install`, `activate`, `fetch`).
  - [x] 2.2 Implementar script de interoperabilidad `src/Ludeka.Web/wwwroot/js/ludeka-offline.js` para almacenamiento local (`localStorage`) de la instantánea de ludoteca y listeners de conectividad (`online`/`offline`).

- [x] **3. Integración en el Shell de la Aplicación (`Ludeka.Web`)**
  - [x] 3.1 Actualizar `src/Ludeka.Web/Components/App.razor` con `<link rel="manifest">`, meta-tags de iOS/Android (`theme-color`, `apple-mobile-web-app-capable`) y registro del Service Worker.
  - [x] 3.2 Crear componente visual `src/Ludeka.Web/Components/Shared/OfflineIndicator.razor` para notificar al usuario el estado de conexión con feedback accesible (WCAG 2.2 AA).
  - [x] 3.3 Integrar `<OfflineIndicator />` en `src/Ludeka.Web/Components/Layout/MainLayout.razor`.
  - [x] 3.4 Actualizar `src/Ludeka.Web/Components/Pages/MyLibrary.razor` para sincronizar la instantánea en local tras cada carga exitosa y permitir visualización offline con aviso si se pierde la conexión.

- [x] **4. DTOs y Contratos de Dominio (`Ludeka.Application`)**
  - [x] 4.1 Crear `src/Ludeka.Application/DTOs/OfflineLibrarySnapshotDto.cs` para tipar y validar la instantánea de ludoteca.

- [x] **5. Pruebas Automatizadas y Verificación (`Ludeka.UnitTests`)**
  - [x] 5.1 Crear `tests/Ludeka.UnitTests/Infrastructure/PwaAndOfflineSyncTests.cs` para validar la estructura del manifiesto web, existencia de assets, sintaxis de Service Worker y serialización del DTO de instantánea.
  - [x] 5.2 Ejecutar suite completa `dotnet test` y verificar 100% de tests en verde.
  - [x] 5.3 Generar informe de verificación `verify-report.md`.
