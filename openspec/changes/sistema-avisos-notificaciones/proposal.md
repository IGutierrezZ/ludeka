# Propuesta SDD: Sistema de Avisos, Notificaciones de Usuario, Web Push (PWA/Navegador) y Contador Real de Moderación (INC-149)

## 1. Intención y Propósito
Dotar a Ludeka de un sistema integral de avisos personalizados in-app y notificaciones nativas **Web Push** (W3C Push API + VAPID para móvil/PWA y navegador de escritorio) que avise a los jugadores cuando sucedan eventos clave en los juegos que les interesan o en la comunidad:
1. **Bajada de precio ($\ge 15\%$ sobre PVP base):** Al marcar un juego como «Lo quiero» (`WantToBuy`), se le pregunta al usuario si desea recibir un aviso de oferta cuando la web detecte una rebaja de al menos un 15% respecto al PVP base (además de contar con un botón independiente «Avísame de oferta» en la ficha).
2. **Reimpresión:** Botón independiente «Avísame de reimpresión» en la ficha del juego para avisar cuando se detecte o apruebe una reimpresión (`WeeklyRelease` con `IsReprint = true`).
3. **Reestock:** Botón independiente «Avísame de reestock» en el bloque de tiendas de la ficha cuando un juego esté agotado en todas las tiendas (`InStock == false`), notificando en cuanto vuelva a detectarse stock.
4. **Nuevos sorteos:** Interruptor en el panel de cuenta (`/cuenta`) para suscribirse a avisos de nuevos sorteos publicados en la web.
5. **Experiencia en el menú de usuario y página dedicada (`/cuenta/notificaciones`):**
   - Cuando el menú de usuario (`AccountMenu.razor`) está cerrado, el botón del avatar muestra un círculo rojo con el número de notificaciones sin leer (o pendientes de gestión para administradores).
   - Dentro del menú desplegado figura la opción de **Notificaciones** con icono de campana (`bell`) y contador, que dirige a la página dedicada `/cuenta/notificaciones`.
   - En `/cuenta/notificaciones`, cada notificación permanece sin leer hasta que el usuario pulsa sobre ella (marcándola como leída y navegando opcionalmente al recurso), además de permitir gestionar sus suscripciones activas y activar/desactivar las notificaciones Push del dispositivo.
6. **Sistema de avisos de administración (Staff):** El contador del «Panel de gestión» en `AccountMenu.razor` y `AdminLayout.razor` reflejará en tiempo real la suma real de tareas pendientes de resolver en todas las bandejas operativas (ingesta social pendiente, reportes comunitarios abiertos, novedades editoriales en espera de moderación y vídeos pendientes), eliminando el valor estático `"21"` de respaldo.

---

## 2. Alcance y Áreas Afectadas

### 2.1. Dominio (`src/Ludeka.Core`)
- **`UserNotification`:** Entidad para cada aviso emitido a un usuario (`Id`, `UserId`, `Type`, `Title`, `Message`, `TargetUrl`, `GameId`, `IsRead`, `CreatedAtUtc`, `ReadAtUtc`). Invariante: no se marca como leída automáticamente al listar, sino mediante acción explícita (`MarkAsRead()`).
- **`UserGameAlertSubscription`:** Suscripción de un usuario a un juego concreto y un tipo de alerta (`UserId`, `GameId`, `AlertType`: `PriceDrop`, `Reprint`, `Restock`, `CreatedAtUtc`, `LastNotifiedAtUtc`, `LastNotifiedValue`).
- **`UserPushSubscription`:** Credenciales Web Push (W3C Push API) por dispositivo del usuario (`Id`, `UserId`, `Endpoint`, `P256dh`, `Auth`, `UserAgent`, `CreatedAtUtc`).
- **`UserPreference`:** Nueva propiedad booleana `NotifyNewGiveaways` (por defecto `false`) para la suscripción general a sorteos desde `/cuenta`.
- **`Game`:** Propiedad opcional `ReferencePvp` (`decimal?`) o resolución canónica del PVP base (a partir del PVP oficial de editorial / máximo histórico verificado) para evaluar con precisión descuentos $\ge 15\%$.

### 2.2. Aplicación (`src/Ludeka.Application`)
- **`IUserNotificationService` / `UserNotificationService`:**
  - Consulta de notificaciones y contador de no leídas (`GetUnreadCountAsync`, `GetUserNotificationsAsync`).
  - Marcado individual como leída (`MarkAsReadAsync`) al pulsar sobre la notificación.
  - Gestión de suscripciones por juego (`SubscribeGameAlertAsync`, `UnsubscribeGameAlertAsync`, `GetGameSubscriptionsAsync`) con controles separados para Oferta ($\ge 15\%$), Reimpresión y Reestock.
  - Emisión de avisos in-app + encolado Web Push ante:
    1. Detección de bajada $\ge 15\%$ sobre PVP base en `PriceRadarService` / ingesta de feeds.
    2. Detección de reestock (paso de 0 tiendas con stock a $\ge 1$ tienda con stock).
    3. Publicación/aprobación de `WeeklyRelease` con `IsReprint == true`.
    4. Publicación/aprobación de nuevo `Giveaway`.
- **`IAdminPendingSummaryService` / `AdminPendingSummaryService`:**
  - Cálculo agregado y cacheado brevemente del total de elementos pendientes de resolver en administración (Ingesta Social + Reportes de Catálogo + Novedades Pendientes + Multimedia Pendiente).
- **`IWebPushNotificationSender`:** Contrato para envío de payloads Web Push cifrados con claves VAPID.

### 2.3. Infraestructura (`src/Ludeka.Infrastructure`)
- Persistencia EF Core dual (SQLite + PostgreSQL) para `UserNotifications`, `UserGameAlertSubscriptions`, `UserPushSubscriptions` y columna `NotifyNewGiveaways` en `UserPreferences`, incluyendo reconciliación en `SqliteSchemaMigrator` y migración EF Core.
- Implementación de envío Web Push (ECDH P-256 + HKDF + AES-128-GCM + JWT VAPID nativo en .NET 10 sin dependencias de pago) con limpieza automática de endpoints caducados (`HTTP 404/410`).

### 2.4. Frontend Web y PWA (`src/Ludeka.Web`)
- **`AccountMenu.razor`:**
  - Círculo rojo (`bg-rose-600 text-white`) en la esquina superior derecha del botón de usuario cuando el menú está cerrado mostrando el número de avisos sin leer (y/o pendientes de administración para staff).
  - Enlace con icono de campana (`bell`) y badge numérico dentro del menú desplegado hacia `/cuenta/notificaciones`.
  - Contador real de elementos pendientes junto a «Panel de gestión» para administradores/moderadores.
- **Página dedicada `/cuenta/notificaciones` (`Notifications.razor`):**
  - Diseño editorial «Revista Lúdica» con listado de avisos recibidos. Cada aviso no leído muestra indicador visual destacado y, al pulsar sobre él, se marca como leído (y permite ir a la ficha o sorteo).
  - Pestaña o sección de «Mis alertas activas» (juegos vigilados por oferta, reimpresión o reestock, y toggle de sorteos) y botón para activar notificaciones nativas en el móvil (PWA) o navegador.
- **`GameDetail.razor` y `StoreOffersCard.razor`:**
  - Modal de confirmación al pulsar «Lo quiero» preguntando: *«¿Quieres que te avisemos si baja al menos un 15% sobre su PVP base?»*.
  - Botones separados en la ficha: **«Avísame de oferta (-15%)»**, **«Avísame de reimpresión»** y, cuando todas las tiendas estén sin stock, **«Avísame de reestock»**.
- **`Account.razor`:**
  - Sección de avisos con interruptor para **«Avisarme de nuevos sorteos»** y activación de notificaciones Push en el dispositivo actual.
- **`service-worker.js` y `wwwroot/js/ludeka-push.js`:**
  - Manejo de eventos `push` (mostrando la notificación nativa del sistema operativo en móvil/escritorio con icono de Ludeka) y `notificationclick` (abriendo la URL de destino en la app/navegador).

---

## 3. Riesgos y Mitigaciones
- **Saturación de notificaciones por oscilaciones de precio o stock:** Se aplicará una ventana de enfriamiento (*cooldown*) en `UserGameAlertSubscription.LastNotifiedAtUtc` para no enviar avisos repetidos del mismo evento en un corto periodo, y en reestock se desactivará o marcará como cumplida tras avisar (o se mantendrá con enfriamiento de 7 días).
- **Compatibilidad Web Push en iOS / Android / Escritorio:** En iOS requiere que el usuario haya añadido la web a la pantalla de inicio (PWA instalada, iOS 16.4+). Si el navegador no soporta Push o el usuario está en iOS sin instalar la PWA, la interfaz mostrará una indicación clara mientras los avisos in-app en la campana funcionan siempre al 100%.

---

## 4. Criterio de Aprobación
Por favor, confirma si estás de acuerdo con esta propuesta para el **INC-149** para pasar a detallar la especificación/diseño y comenzar la implementación.
