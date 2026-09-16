# Especificación: PWA y Modo Consulta Offline para Ludoteca

- **Módulo:** Progressive Web App (PWA), Disponibilidad Offline y Resiliencia en Cliente
- **Incremento:** 16 (`change-16-pwa-offline-library`)
- **Estado:** En Especificación (`sdd-spec`)

---

## 1. Requerimientos Funcionales

### RF-16.1: Manifiesto de Aplicación Web (`manifest.webmanifest`)
- El sistema debe servir un archivo de manifiesto web válido ubicado en `/manifest.webmanifest`.
- Debe contener:
  - `name`: "Ludeka — Tu Estantería de Juegos de Mesa"
  - `short_name`: "Ludeka"
  - `description`: "El Letterboxd de los juegos de mesa en español. Consulta tu ludoteca, préstamos y estadísticas sin conexión."
  - `start_url`: "/mi-ludoteca"
  - `scope`: "/"
  - `display`: "standalone"
  - `background_color`: "#18181b"
  - `theme_color`: "#d97706"
  - `icons`: Iconos en resoluciones 192x192 y 512x512 con soporte `purpose: "any maskable"`.
  - `lang`: "es"

### RF-16.2: Service Worker con Estrategia Mixta de Caché
- El sistema debe registrar un Service Worker en `/service-worker.js`.
- **Estrategia Cache-First:**
  - Debe pre-cachear durante el evento `install`:
    - `/` (Shell)
    - `/app.css`
    - `/offline.html`
    - `/manifest.webmanifest`
    - `/images/game-placeholder.svg`
    - `/images/expansion-placeholder.svg`
    - `/icons/icon-192.svg`
    - `/icons/icon-512.svg`
    - Fuentes tipográficas esenciales (`Plus Jakarta Sans`).
- **Estrategia Stale-While-Revalidate / Network-First con Fallback:**
  - Para peticiones de navegación (`request.mode === 'navigate'`):
    - Intentar responder desde la red.
    - Si falla o no hay conexión, servir la respuesta en caché si existe, o retornar `/offline.html`.
  - Para imágenes (`/images/games/*`):
    - Servir desde caché si está disponible y actualizar en segundo plano, o usar placeholder local si falla la red.
- **Ciclo de vida y limpieza:**
  - En el evento `activate`, debe purgar cachés obsoletas que no coincidan con la versión actual (`ludeka-cache-v1`).

### RF-16.3: Página de Cortesía Offline Editorial (`offline.html`)
- Debe disponer de diseño visual consistente con la identidad de Ludeka (papel, tonos carbón/ámbar, tipografía legible).
- Debe informar amigablemente al usuario de que se encuentra sin conexión a internet.
- Debe ofrecer un botón de *"Reintentar conexión"* (`window.location.reload()`) y un acceso directo a *"Abrir mi ludoteca guardada"* (`/mi-ludoteca`).

### RF-16.4: Almacenamiento Local e Instantánea de la Ludoteca (`ludeka-offline.js`)
- Cada vez que el usuario accede a `/mi-ludoteca` con conexión activa y se cargan los datos, el cliente debe guardar una instantánea compacta en `localStorage` bajo la clave `ludeka_offline_library_{userId}` (o `ludeka_offline_library_v1`).
- La instantánea contiene:
  - `timestamp`: Marca de tiempo de la última sincronización.
  - `userId`: Identificador del usuario.
  - `items`: Lista de juegos en posesión, carátula, slug, estado y préstamos activos.
  - `statsSummary`: Resumen de totales (posesión, jugados, prestados).
- Si la conexión falla al entrar a `/mi-ludoteca`, el componente Blazor o el script de soporte detecta la ausencia de red y carga los datos desde la instantánea local.

### RF-16.5: Indicador Visual de Conectividad en Tiempo Real (`OfflineIndicator.razor`)
- Debe monitorizar los eventos del navegador `window.addEventListener('online')` y `window.addEventListener('offline')`.
- En estado offline:
  - Muestra una píldora visual en la cabecera o barra superior con mensaje: *"📡 Modo sin conexión — Estás consultando tu ludoteca local"*.
- Al restablecer la conexión:
  - Muestra durante 3 segundos una píldora verde: *"🟢 Conexión restablecida — Sincronizando..."*.

### RF-16.6: Modo Consulta Offline en `MyLibrary.razor`
- Cuando la aplicación opera sin conexión a internet:
  - Presenta un banner informativo en la parte superior de la ludoteca indicando la fecha/hora de la última sincronización guardada.
  - Muestra los juegos en estantería y préstamos recuperados del almacenamiento local.
  - Deshabilita temporalmente los botones de mutación (ej. *"Importar BGG"*, *"Añadir por BGG"*, *"Registrar préstamo"*) mostrando un tooltip o mensaje explicativo: *"Esta acción requiere conexión a internet"*.

---

## 2. Requerimientos No Funcionales

- **Rendimiento:** El Service Worker no debe añadir más de 15 ms de latencia a las peticiones con red activa.
- **Tamaño de almacenamiento:** La instantánea serializada de la ludoteca en `localStorage` no debe superar los 250 KB para colecciones de hasta 500 juegos.
- **Accesibilidad:** El indicador offline debe contar con atributos `role="status"` y `aria-live="polite"` según WCAG 2.2 AA.
- **Compatibilidad:** Debe funcionar en Safari iOS (iOS 15+), Chrome/Edge en Android y navegadores de escritorio compatibles con la especificación de Service Workers y W3C Web App Manifest.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Detección y validación del manifiesto PWA
  Dado un navegador compatible con PWA
  Cuando solicita "/manifest.webmanifest"
  Entonces recibe un código de respuesta HTTP 200
  Y el Content-Type es "application/manifest+json"
  Y contiene "name", "icons", "start_url" y "display: standalone"

Escenario: Consulta de ludoteca en modo avión / sin conexión
  Dado un usuario que ha visitado previamente "/mi-ludoteca" estando online
  Cuando activa el modo avión en su dispositivo y visita "/mi-ludoteca"
  Entonces la aplicación carga la lista de juegos y préstamos desde la instantánea local
  Y muestra el indicador de "Modo sin conexión" con la fecha de la última sincronización

Escenario: Transición reactiva al restablecer conectividad
  Dado que el usuario navega con la advertencia de "Modo sin conexión"
  Cuando el dispositivo recupera la cobertura de red o Wi-Fi
  Entonces el indicador cambia a "Conexión restablecida"
  Y la aplicación refresca automáticamente los datos contra el servidor

Escenario: Carga de página offline de cortesía para rutas no cacheadas
  Dado un usuario sin conexión a internet
  Cuando intenta navegar a una ruta desconocida o no cacheada
  Entonces el Service Worker intercepta la petición y sirve "/offline.html"
  Y muestra un botón para reintentar la conexión
```
