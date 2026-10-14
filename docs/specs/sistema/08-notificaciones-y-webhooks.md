# 08. Notificaciones, Centro de Avisos de Usuario, Web Push y Webhooks de Comunidad

## 1. Visión General y Propósito
Este módulo comprende dos subsistemas complementarios:
1. **Difusión Multicanal Comunitaria (Discord y Telegram):** Avisos automatizados de eventos comunitarios clave empleando el patrón Outbox asíncrono para no penalizar la latencia del usuario.
2. **Sistema de Avisos Personales, Alertas por Juego/Sorteos, Web Push (PWA/Navegador) y Contador Administrativo Real (INC-149):** Centro de notificaciones en `/cuenta/notificaciones` con campana y badge rojo en `AccountMenu.razor`, lectura individual explícita al pulsar en cada notificación, alertas separadas por juego (bajada de precio $\ge 15\%$ sobre PVP base, reimpresión y reestock), suscripción a nuevos sorteos, notificaciones nativas W3C Web Push (VAPID) y contador administrativo real de elementos pendientes de moderación.

---

## 2. Centro de Avisos Personales, Alertas y Web Push (INC-149)

### 2.1 Entidades de Dominio (`src/Ludeka.Core`)
- [`UserNotification`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserNotification.cs): Aviso individual de un usuario (`Id`, `UserId`, `Type` de tipo `UserNotificationType`, `Title`, `Message`, `TargetUrl`, `GameId`, `ImageUrl`, `IsRead`, `CreatedAtUtc`, `ReadAtUtc`). Permanece con `IsRead = false` hasta que el usuario interactúa explícitamente con él (`MarkAsRead`).
- [`UserGameAlertSubscription`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserGameAlertSubscription.cs): Suscripción independiente por `(UserId, GameId, AlertType)` donde `GameAlertType` soporta tres valores separados:
  - `PriceDrop` (`1`): Alerta de bajada de precio de al menos un **15% sobre el PVP base** (`ReferenceBasePvp`).
  - `Reprint` (`2`): Alerta de reimpresión anunciada.
  - `Restock` (`3`): Alerta de reposición de stock en tiendas asociadas.
- [`UserPushSubscription`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserPushSubscription.cs): Suscripción W3C Push API (`Endpoint`, `P256dh`, `Auth`, `UserAgent`) vinculada al navegador o PWA móvil instalada del usuario.
- [`UserPreference.NotifyNewGiveaways`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserPreference.cs): Preferencia booleana en el perfil del usuario para recibir avisos cada vez que se publica un sorteo en Ludeka.

### 2.2 Servicios de Aplicación e Integración (`src/Ludeka.Application`)
- [`IUserNotificationService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IUserNotificationService.cs) y [`UserNotificationService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Account/UserNotificationService.cs):
  - Gestión de bandeja de notificaciones (`GetUserNotificationsAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`) con evento reactivo `OnNotificationsChanged`.
  - Gestión de alertas por juego (`GetUserGameAlertsAsync`, `SubscribeGameAlertAsync`, `UnsubscribeGameAlertAsync`, `ToggleGameAlertAsync`).
  - Evaluación de ofertas y stock (`EvaluatePriceAndStockAlertsAsync`): conectada a `PriceRadarService.RecordPriceObservationAsync`. Verifica que el descuento sea $\ge 15\%$ sobre el PVP base (`MinimumPriceDropRatio = 0.15m`) y evita duplicados si el precio no mejora respecto a `LastNotifiedPrice`. Para `Restock`, notifica cuando un juego pasa a estar en stock con enfriamiento de 24 horas.
  - Disparo de reimpresiones (`NotifyReprintDetectedAsync`): conectado a `WeeklyReleaseService` y `SocialIngestionService` cuando se publica o aprueba un lanzamiento con `IsReprint = true` y `GameId` vinculado.
  - Disparo de nuevos sorteos (`NotifyNewGiveawayPublishedAsync`): conectado a `GiveawayService` y `SocialIngestionService` al dar de alta o aprobar un sorteo.
- [`IAdminPendingSummaryService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IAdminPendingSummaryService.cs) y [`AdminPendingSummaryService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Admin/AdminPendingSummaryService.cs):
  - Calcula en tiempo real `AdminPendingSummaryDto` (`PendingSocialInboxCount`, `PendingIssueReportsCount`, `PendingWeeklyReleasesCount` y `TotalPendingCount`), reemplazando el valor estático `"21"` que existía en el menú de usuario.

### 2.3 Infraestructura y Web Push (`src/Ludeka.Infrastructure` y `wwwroot`)
- [`SqliteUserNotificationRepository`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Persistence/Repositories/SqliteUserNotificationRepository.cs): Persistencia dual compatible con SQLite y PostgreSQL (migración EF Core `20260416120000_AddUserNotificationsAndAlerts`, auto-migrador SQLite y `docs/database/supabase_schema.sql`).
- [`VapidWebPushNotificationSender`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Notifications/VapidWebPushNotificationSender.cs): Emisor Web Push mediante cifrado estándar ECDH P-256 + HKDF + AES-128-GCM (`aes128gcm`, RFC 8291) y firma JWT ES256 VAPID (RFC 8292) utilizando `System.Security.Cryptography` nativo de .NET 10 sin dependencias de terceros.
- [`wwwroot/service-worker.js`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/service-worker.js) y [`wwwroot/js/ludeka-push.js`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/js/ludeka-push.js): Eventos `push` y `notificationclick` en el Service Worker de la PWA para mostrar notificaciones nativas en Android, iOS (PWA instalada) y navegadores de escritorio.

### 2.4 Interfaz de Usuario (`src/Ludeka.Web`)
- [`AccountMenu.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AccountMenu.razor):
  - Cuando el menú está plegado, muestra un círculo rojo (`bg-rose-600 text-white`) sobre el botón del usuario con el número de notificaciones sin leer (`HeaderBadgeCount`).
  - Al desplegarse, incluye el elemento **«Notificaciones»** con icono de campana (`bell`) y contador de avisos sin leer, además del contador real de tareas pendientes en **«Panel de gestión»** para administradores y moderadores.
- [`AccountNotifications.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/AccountNotifications.razor) (`/cuenta/notificaciones`):
  - Página dedicada con cabecera editorial, resumen del área de gestión para staff, bandeja de avisos (donde cada notificación permanece sin leer hasta que el usuario pulsa sobre ella), tarjeta de activación de Web Push en el dispositivo actual, interruptor de avisos de sorteos y listado de alertas activas por juego.
- [`GameDetail.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor) y [`StoreOffersCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/StoreOffersCard.razor):
  - Tres botones separados en la ficha del juego: **«Avísame de oferta (-15% PVP)»**, **«Avísame de reimpresión»** y **«Avísame de reestock»** (también disponible directamente en `StoreOffersCard.razor` cuando no hay stock confirmado en tiendas).
  - Al marcar un juego como **«Lo quiero»** (`CollectionStatus.WantToBuy`), se muestra un modal preguntando al usuario si desea activar automáticamente el aviso de bajada de precio ($\ge 15\%$ sobre PVP base).
- [`Account.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Account.razor):
  - Sección `05 Avisos y notificaciones` con interruptor directo para suscribirse a nuevos sorteos y enlace a `/cuenta/notificaciones`.

---

## 3. Modelo de Dominio y Cola Outbox Comunitaria (Discord / Telegram)

### 3.1 Entidad `CommunityNotificationLog`
Ubicación: [`src/Ludeka.Core/Entities/CommunityNotificationLog.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/CommunityNotificationLog.cs)

- `Id` (Guid), `Channel` (`Discord`, `Telegram`), `EventType`, `Title`, `Message`, `TargetUrl`, `IsSuccess`, `ErrorMessage`, `SentAt`.

### 3.2 Desacoplamiento Asíncrono en Memoria
- Interfaz [`ICommunityNotificationQueue`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs) basada en `System.Threading.Channels.Channel<NotificationMessage>`.
- Las peticiones HTTP de los usuarios escriben en el canal sin esperar a la llamada de red hacia Discord o Telegram.
- Servicio en segundo plano: [`CommunityNotificationDispatcherHostedService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs).

---

## 4. Clientes de Integración Comunitaria

- **Discord Webhooks (`DiscordWebhookClient`):** Envía mensajes enriquecidos (*Embeds*) con color corporativo, título formateado, miniatura de carátula y enlaces directos.
- **Telegram Bot API (`TelegramBotClient`):** Envía mensajes formateados en HTML con botones inline (*InlineKeyboardMarkup*) para acceder a la web en 1 toque.
- **Modo Dry-Run:** Parámetro `CommunityNotifications:DryRun: true` en `appsettings.json` para entornos de prueba y desarrollo local.

---

## 5. Eventos Automatizados del Sistema

1. **Alerta 24h de Sorteo:** Notificación preventiva un día antes de que un sorteo activo finalice su plazo.
2. **Boletín de Lanzamientos de Viernes:** Difusión matinal de novedades que llegan a las tiendas especializadas.
3. **Nuevo Veredicto Fundador:** Publicación inmediata cuando la mesa fundadora califica un juego como *Imprescindible* o *Recomendado*.
4. **Regla de Juego Aclarada:** Difusión cuando una pregunta en el consultorio Q&A recibe una respuesta oficial aceptada.

---

## 6. Canales Comunitarios y Vías de Apoyo (`CommunityNotificationOptions`)

Además de los webhooks de salida automatizados, `CommunityNotificationOptions` centraliza las URLs públicas de presencia comunitaria y mecenazgo consumidas por la interfaz (`MainLayout.razor` y `Transparency.razor`):
- `DiscordInviteUrl`: Invitación al servidor oficial de Discord.
- `TelegramChannelUrl`: Enlace al canal oficial de Telegram.
- `KofiUrl`: Enlace de mecenazgo voluntario en Ko-fi (`https://ko-fi.com/ludeka` por defecto, configurable mediante `CommunityNotifications__KofiUrl` en `docker-compose.prod.yml` o `appsettings.json`).
