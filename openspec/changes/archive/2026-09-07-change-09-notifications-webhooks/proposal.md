# Propuesta: change-09-notifications-webhooks (Incremento 9: Sistema de Notificaciones y Webhooks de Comunidad)

## 1. Resumen Ejecutivo y Motivación

En Ludeka, la comunidad lúdica es el corazón del proyecto. Sin embargo, en la web moderna, los usuarios no refrescan compulsivamente un sitio para ver si hay un nuevo sorteo o si ya se resolvió una duda sobre las reglas de un juego complejo. La conversación y el pulso diario ocurren en **Discord** y **Telegram**.

El **Incremento 9** dota a Ludeka de un **motor de difusión multicanal automatizado** que conecta los hitos clave de la plataforma con servidores de Discord y canales de Telegram mediante webhooks y bots oficiales, garantizando:
- **Cero latencia de cara al usuario:** Desacoplamiento total mediante colas asíncronas en memoria (`System.Threading.Channels.Channel<T>`) y un despachador `BackgroundService`.
- **Estética Editorial Enriquecida ("Cero AI Slop"):** Mensajes cuidadosamente maquetados con la paleta de color de Ludeka (terracota `#E05A38`), portadas oficiales de juegos, formato Markdown/HTML limpio y enlaces directos con llamadas a la acción.
- **Resiliencia y Modo Simulado (Dry-Run):** Aislamiento ante caídas de red o límites de peticiones (Rate Limit 429) de Discord/Telegram, con auditoría en base de datos SQLite y reintento con 1 clic.
- **Control Total en UI para Fundadores:** Panel de monitorización y pruebas con disparo manual de boletines y verificación de conectividad.

---

## 2. Decisiones de Arquitectura y Modelo de Datos

### 2.1 Dominio (`Ludeka.Core`)

#### 1. Enums Tipados
- `NotificationChannel`:
  - `Discord` (0)
  - `Telegram` (1)
- `NotificationEventType`:
  - `GiveawayExpiring` (0): Alerta de sorteo que finaliza en 24h.
  - `FridayReleasesSummary` (1): Boletín de novedades en tiendas de los viernes.
  - `FoundingVerdictPublished` (2): Publicación de un nuevo análisis de la casa con sello de recomendación.
  - `RuleQuestionAnswered` (3): Respuesta aceptada a una duda de reglas.
  - `CustomTestPing` (4): Notificación de prueba técnica de conectividad.
- `NotificationStatus`:
  - `Queued` (0): Encolado en el despachador.
  - `Sent` (1): Entregado exitosamente a la API externa.
  - `Failed` (2): Fallo en la llamada HTTP (con error registrado).
  - `DryRun` (3): Simulado en entorno local/test sin emisión HTTP real.

#### 2. Entidad `CommunityNotificationLog`
Registro auditable de cada mensaje emitido por el sistema:
- `Guid Id` (PK)
- `NotificationEventType EventType`
- `NotificationChannel Channel`
- `string Title`
- `string Summary`
- `string? TargetUrl`
- `string? ImageUrl`
- `NotificationStatus Status`
- `string? ErrorDetails`
- `DateTimeOffset CreatedAt`
- `DateTimeOffset? SentAt`
- Métodos de dominio: `MarkAsSent()`, `MarkAsFailed(string error)`, `MarkAsDryRun()`.

#### 3. Objeto de Mensaje `CommunityNotificationMessage`
Representa el payload de notificación transferido por la cola interna:
- `NotificationEventType EventType`
- `string Title`
- `string Description`
- `string? TargetUrl`
- `string? ImageUrl`
- `Dictionary<string, string>? Fields`
- `NotificationChannel? TargetChannel` (nulo si se emite a todos los canales activos)

---

### 2.2 Capa de Aplicación (`Ludeka.Application`)

#### 1. Configuración (`CommunityNotificationOptions`)
Secciones configurables en `appsettings.json`:
- `bool Enabled` (interruptor maestro general)
- `bool DryRun` (modo simulado para tests y desarrollo offline)
- `string? DiscordWebhookUrl` (URL del webhook de Discord)
- `bool DiscordEnabled`
- `string? TelegramBotToken` (Token del bot emitido por BotFather)
- `string? TelegramChatId` (Identificador del chat o canal de Telegram)
- `bool TelegramEnabled`

#### 2. Interfaces de Cola y Servicios
- `ICommunityNotificationQueue`:
  - Encola mensajes instantáneamente: `ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default);`
  - Consumo en streaming: `IAsyncEnumerable<CommunityNotificationMessage> ReadAllAsync(CancellationToken ct = default);`
- `ICommunityNotificationRepository`:
  - Persistencia y consulta de historial: `GetRecentLogsAsync`, `GetByIdAsync`, `AddLogAsync`, `UpdateLogAsync`.
- `ICommunityNotificationService`:
  - `Task<IReadOnlyList<NotificationDispatchResult>> BroadcastAsync(CommunityNotificationMessage message, CancellationToken ct = default);`
  - `Task<NotificationDispatchResult> SendToDiscordAsync(CommunityNotificationMessage message, CancellationToken ct = default);`
  - `Task<NotificationDispatchResult> SendToTelegramAsync(CommunityNotificationMessage message, CancellationToken ct = default);`
  - `Task<IReadOnlyList<CommunityNotificationLogDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);`
  - `Task<NotificationDispatchResult> RetryFailedNotificationAsync(Guid logId, CancellationToken ct = default);`
  - `Task TriggerExpiringGiveawaysScanAsync(CancellationToken ct = default);`
  - `Task TriggerFridayReleasesBulletinAsync(CancellationToken ct = default);`
  - `Task SendTestPingAsync(NotificationChannel? channel = null, CancellationToken ct = default);`

---

### 2.3 Capa de Infraestructura (`Ludeka.Infrastructure`)

1. **Clientes HTTP Tipados:**
   - `DiscordWebhookClient`: Transforma el mensaje a payload JSON de Discord con Embed rico (color `0xE05A38`, autor, thumbnail, campos y pie "Ludeka Comunidad").
   - `TelegramBotClient`: Transforma el mensaje a HTML limpio de Telegram con soporte de fotos (`sendPhoto` si hay `ImageUrl` o `sendMessage` con parse mode HTML).
2. **Cola en Memoria con Channels:**
   - `InMemoryCommunityNotificationQueue`: Implementación basada en `System.Threading.Channels.Channel.CreateBounded<CommunityNotificationMessage>(1000)` garantizando baja memoria, backpressure controlado y cero asignaciones pesadas.
3. **Servicio en Segundo Plano (`BackgroundService`):**
   - `CommunityNotificationDispatcherHostedService`: Consume de forma continua el canal y despacha mediante `ICommunityNotificationService` dentro de un scope de DI seguro.
   - Disparador programado ligero integrado para verificar cada hora si hay sorteos a 24 horas de expirar o si es viernes para el boletín.
4. **Persistencia SQLite:**
   - `SqliteCommunityNotificationRepository` integrado en `LudekaDbContext` con tabla `NotificationLogs` e índices en `Status`, `Channel` y `CreatedAt`.

---

### 2.4 Interfaz de Usuario y Experiencia Blazor (`Ludeka.Web`)

1. **Panel de Notificaciones y Webhooks (`AdminNotifications.razor` en `/notificaciones` o `/admin/notificaciones`):**
   - Visible y accesible para usuarios con rol `FoundingTeam` o `Moderator`.
   - **Tarjetas de Estado de Canales:**
     - 🟣 **Discord Webhook:** Estado (Activo / Simulado / Inactivo), URL ofuscada y botón de prueba.
     - 🔵 **Telegram Bot:** Estado (Activo / Simulado / Inactivo), Chat ID y botón de prueba.
   - **Barra de Acciones Inmediatas:**
     - `[ 🔔 Enviar Notificación de Prueba ]`
     - `[ ⏳ Disparar Alerta de Sorteos 24h ]`
     - `[ 🛍️ Publicar Boletín de Viernes ]`
   - **Historial de Despachos:**
     - Tabla con badges de canal, evento, estado (Verde = Enviado, Amarillo = Simulado/DryRun, Rojo = Fallo con tooltip del error).
     - Botón `[ 🔄 Reintentar ]` en filas con estado fallido.
2. **Navegación en `MainLayout.razor`:**
   - Acceso directo `🔔 Webhooks` en la cabecera junto a `🎬 Moderar` cuando el usuario tiene rol de la mesa fundadora.

---

## 3. Formato Editorial de los Mensajes ("Cero AI Slop")

### A. Alerta de Sorteo a punto de expirar (24h)
- **Discord Embed:**
  - Título: `⚠️ ¡ÚLTIMAS 24 HORAS! Sorteo en marcha: [Título del Sorteo]`
  - Color: `#F59E0B` (Ámbar de urgencia lúdica)
  - Campos:
    - *Organizador:* `@editorial_es`
    - *Juego:* `Wingspan (Maldito Games)`
    - *Cierre:* `Hoy a las 23:59h`
  - Enlace: Botón / URL directa al sorteo y a la ficha en Ludeka.
- **Telegram HTML:**
  - `⚠️ <b>¡ÚLTIMAS 24H DE SORTEO!</b>`
  - `🎲 <b>Juego:</b> Wingspan`
  - `🏷️ <b>Organiza:</b> @editorial_es`
  - `🔗 <a href="...">Participar en el sorteo</a> | <a href="...">Ver ficha en Ludeka</a>`

### B. Boletín de Lanzamientos de Viernes
- **Discord Embed:**
  - Título: `🛍️ NOVEDADES EN TIENDAS — Boletín del Viernes`
  - Color: `#E05A38` (Terracota corporativo Ludeka)
  - Descripción: *"Estos son los juegos de mesa y expansiones que llegan a las tiendas especializadas este fin de semana:"*
  - Campos detallados por cada título (Nombre, Editorial, PVP estimado y si es reimpresión).
- **Telegram HTML:**
  - Resumen visual con emojis de caja de juego, precios recomendados y enlace al catálogo completo en Ludeka.

### C. Nuevo Veredicto de la Mesa Fundadora
- **Discord Embed / Telegram:**
  - Título: `🛡️ NUEVO VEREDICTO FUNDADOR: [Nombre del Juego]`
  - Sello: `🟢 IMPRESCINDIBLE` / `🟡 RECOMENDADO CON ADAPTACIONES` / `🔴 PRESCINDIBLE`
  - Frase destacada del análisis oficial.
  - Enlace directo a la ficha del juego.

### D. Duda de Reglas Resuelta
- **Discord Embed / Telegram:**
  - Título: `💡 DUDA DE REGLAS RESUELTA: [Nombre del Juego]`
  - Pregunta: *"¿Se puede colocar un meeple en una ciudad ya cerrada?"*
  - Respuesta aceptada destacada con autor y enlace al consultorio de reglas en Ludeka.

---

## 4. Plan de Pruebas y Criterios de Aceptación

1. **Pruebas de Dominio (`CommunityNotificationLogTests.cs`):**
   - Transiciones de estado (`Queued -> Sent`, `Queued -> Failed`, `Queued -> DryRun`).
   - Validaciones de campos obligatorios y URLs.
2. **Pruebas de Cola Asíncrona (`CommunityNotificationQueueTests.cs`):**
   - Comportamiento no bloqueante con `Channel<T>`.
   - Lectura secuencial y respeto de orden FIFO.
3. **Pruebas de Servicio y Formateo (`CommunityNotificationServiceTests.cs`):**
   - Generación de payloads correctos para Discord (Embeds, colores, footer).
   - Generación de payloads correctos para Telegram (HTML válido, fotos).
   - Modo `DryRun`: sin llamadas de red reales, registrando estado `DryRun`.
   - Comportamiento ante fallos HTTP: captura de errores y registro de log `Failed`.
   - Escaneo de sorteos próximos a expirar (filtro de fecha `< 24h` y no expirados).
   - Boletín de viernes (agrupación de lanzamientos de la semana).
4. **Pruebas de Persistencia (`SqliteCommunityNotificationRepositoryTests.cs`):**
   - Inserción y consulta de logs con SQLite en memoria.
   - Filtrado por estado y límite de resultados.
5. **Meta de Verificación:** Mantener el 100% de los 170 tests actuales en verde y sumar al menos 12 tests nuevos para alcanzar ~182 tests en verde.
