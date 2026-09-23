# 08. Notificaciones y Webhooks de Comunidad

## 1. Visión General y Propósito
Este módulo implementa la difusión multicanal automatizada hacia Discord y Telegram para avisar en tiempo real de eventos comunitarios clave (sorteos próximos a expirar, boletín de novedades de los viernes, nuevos veredictos fundadores y dudas de reglas resueltas), empleando el patrón Outbox asíncrono para no penalizar la latencia del usuario.

---

## 2. Modelo de Dominio y Cola Outbox

### 2.1 Entidad `CommunityNotificationLog`
Ubicación: [`src/Ludeka.Core/Entities/CommunityNotificationLog.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/CommunityNotificationLog.cs)

- `Id` (Guid), `Channel` (`Discord`, `Telegram`), `EventType`, `Title`, `Message`, `TargetUrl`, `IsSuccess`, `ErrorMessage`, `SentAt`.

### 2.2 Desacoplamiento Asíncrono en Memoria
- Interfaz [`ICommunityNotificationQueue`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs) basada en `System.Threading.Channels.Channel<NotificationMessage>`.
- Las peticiones HTTP de los usuarios escriben en el canal sin esperar a la llamada de red hacia Discord o Telegram.
- Servicio en segundo plano: [`CommunityNotificationDispatcherHostedService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs).

---

## 3. Clientes de Integración

- **Discord Webhooks (`DiscordWebhookClient`):** Envía mensajes enriquecidos (*Embeds*) con color corporativo, título formateado, miniatura de carátula y enlaces directos.
- **Telegram Bot API (`TelegramBotClient`):** Envía mensajes formateados en HTML con botones inline (*InlineKeyboardMarkup*) para acceder a la web en 1 toque.
- **Modo Dry-Run:** Parámetro `CommunityNotifications:DryRun: true` en `appsettings.json` para entornos de prueba y desarrollo local.

---

## 4. Eventos Automatizados del Sistema

1. **Alerta 24h de Sorteo:** Notificación preventiva un día antes de que un sorteo activo finalice su plazo.
2. **Boletín de Lanzamientos de Viernes:** Difusión matinal de novedades que llegan a las tiendas especializadas.
3. **Nuevo Veredicto Fundador:** Publicación inmediata cuando la mesa fundadora califica un juego como *Imprescindible* o *Recomendado*.
4. **Regla de Juego Aclarada:** Difusión cuando una pregunta en el consultorio Q&A recibe una respuesta oficial aceptada.

---

## 5. Canales Comunitarios y Vías de Apoyo (`CommunityNotificationOptions`)

Además de los webhooks de salida automatizados, `CommunityNotificationOptions` centraliza las URLs públicas de presencia comunitaria y mecenazgo consumidas por la interfaz (`MainLayout.razor` y `Transparency.razor`):
- `DiscordInviteUrl`: Invitación al servidor oficial de Discord.
- `TelegramChannelUrl`: Enlace al canal oficial de Telegram.
- `KofiUrl`: Enlace de mecenazgo voluntario en Ko-fi (`https://ko-fi.com/ludeka` por defecto, configurable mediante `CommunityNotifications__KofiUrl` en `docker-compose.prod.yml` o `appsettings.json`).

