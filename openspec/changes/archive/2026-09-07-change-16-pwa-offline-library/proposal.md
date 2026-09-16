# Propuesta: change-16-pwa-offline-library (Incremento 16: PWA y Modo Consulta Offline para Ludoteca)

## 1. Resumen Ejecutivo y Motivación
Los aficionados a los juegos de mesa frecuentan clubes, tiendas especializadas, sótanos de asociaciones y jornadas lúdicas donde la cobertura móvil o Wi-Fi es inestable o inexistente.
El objetivo del **Incremento 16** es convertir a Ludeka en una **Progressive Web App (PWA)** instalable en la pantalla de inicio de teléfonos móviles (iOS y Android) y ordenadores de escritorio, permitiendo consultar en cualquier momento y sin conexión a internet la colección personal de juegos, la lista de préstamos activos y los detalles esenciales de la ludoteca.

---

## 2. Objetivos Principales

1. **Instalabilidad Nativa (PWA):**
   - Incorporar un manifiesto web conforme a las especificaciones W3C (`manifest.webmanifest`), con iconos oficiales de Ludeka, nombre completo, nombre corto, color temático y modo `standalone`.
   - Permitir a los usuarios añadir Ludeka como icono independiente en sus dispositivos móviles sin barra de dirección del navegador.
2. **Service Worker Eficiente y Defensivo:**
   - Registrar un Service Worker que intercepte peticiones de red.
   - Cache-First para recursos estáticos: CSS (`app.css`), fuentes tipográficas de Google Fonts, iconos y placeholders.
   - Network-First con fallback a caché para vistas clave.
   - Página de cortesía [`offline.html`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/offline.html) con diseño editorial para navegación sin red cuando un recurso no esté cacheado.
3. **Instantánea Local de la Ludoteca (`ludeka_offline_library_v1`):**
   - Guardar en almacenamiento local del navegador (`localStorage`) una instantánea compacta y estructurada de la ludoteca del usuario en cada visita online.
   - Permitir que la vista `/mi-ludoteca` recupere y muestre la lista de títulos físicos, su estado y préstamos activos incluso cuando el servidor o la red no respondan.
4. **Indicador de Conexión en Tiempo Real (`OfflineIndicator.razor`):**
   - Avisar al usuario mediante una píldora visual en cabecera/pie cuando el dispositivo se encuentre desconectado («📡 Modo sin conexión»).
   - Proporcionar confirmación visual cuando la conectividad se restablezca («🟢 Conexión restablecida»).

---

## 3. Alcance Funcional Detallado

- **Manifiesto:** `src/Ludeka.Web/wwwroot/manifest.webmanifest`.
- **Iconografía PWA:** Iconos vectoriales y rasterizados en resoluciones estándar (192x192, 512x512 y maskable) en `src/Ludeka.Web/wwwroot/icons/`.
- **Service Worker:** `src/Ludeka.Web/wwwroot/service-worker.js`.
- **Página de Fallback Offline:** `src/Ludeka.Web/wwwroot/offline.html`.
- **Módulo JS de Persistencia Offline:** `src/Ludeka.Web/wwwroot/js/ludeka-offline.js`.
- **Componente Visual:** `src/Ludeka.Web/Components/Shared/OfflineIndicator.razor`.
- **Integración en Aplicación:** `App.razor`, `MainLayout.razor` y `MyLibrary.razor`.

---

## 4. No-Alcance (Límites de este Incremento)
- **Edición/mutación offline con sincronización diferida (Outbox):** En este incremento el modo offline es de **consulta y lectura segura**. Las acciones que mutan datos (crear préstamo, cambiar estado de juego, escribir reseña) seguirán requiriendo conexión activa o mostrarán un aviso claro indicando que se requiere red.
- **Cacheo masivo de todo el catálogo global:** Solo se cachean los recursos estáticos, la navegación reciente y la ludoteca del usuario activo.
