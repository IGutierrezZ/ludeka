# Diseño Técnico: change-09-notifications-webhooks (Incremento 9)

## 1. Diagrama de Arquitectura y Flujo de Datos

```
[ Usuario / Evento del Sistema ]
     │
     ▼ (síncrono, 0ms retardo)
[ ICommunityNotificationQueue.EnqueueAsync ]
     │
     ▼
[ System.Threading.Channels.Channel<CommunityNotificationMessage> ]
     │
     ▼ (asíncrono en segundo plano)
[ CommunityNotificationDispatcherHostedService ]
     │
     ├───► [ ICommunityNotificationService ]
     │          │
     │          ├───► [ DiscordWebhookClient (HTTP POST) ]
     │          ├───► [ TelegramBotClient (HTTP POST) ]
     │          │
     │          ▼
     └───► [ SqliteCommunityNotificationRepository ]
                │
                ▼
          [ LudekaDbContext.NotificationLogs (SQLite) ]
                │
                ▲ (consulta y reintento)
          [ AdminNotifications.razor (/admin/notificaciones) ]
```

---

## 2. Definición de Modelos e Interfaces

### 2.1 Dominio (`Ludeka.Core`)
```csharp
namespace Ludeka.Core.Enums;

public enum NotificationChannel { Discord = 0, Telegram = 1 }

public enum NotificationEventType
{
    GiveawayExpiring = 0,
    FridayReleasesSummary = 1,
    FoundingVerdictPublished = 2,
    RuleQuestionAnswered = 3,
    CustomTestPing = 4
}

public enum NotificationStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    DryRun = 3
}

public class CommunityNotificationLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? TargetUrl { get; private set; }
    public string? ImageUrl { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? ErrorDetails { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; private set; }

    public void MarkAsSent() { Status = NotificationStatus.Sent; SentAt = DateTimeOffset.UtcNow; ErrorDetails = null; }
    public void MarkAsFailed(string error) { Status = NotificationStatus.Failed; ErrorDetails = error; }
    public void MarkAsDryRun() { Status = NotificationStatus.DryRun; SentAt = DateTimeOffset.UtcNow; ErrorDetails = null; }
}

public record CommunityNotificationMessage(
    NotificationEventType EventType,
    string Title,
    string Description,
    string? TargetUrl = null,
    string? ImageUrl = null,
    Dictionary<string, string>? Fields = null,
    NotificationChannel? TargetChannel = null);
```

### 2.2 Contratos de Aplicación (`Ludeka.Application`)
```csharp
public interface ICommunityNotificationQueue
{
    ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    IAsyncEnumerable<CommunityNotificationMessage> ReadAllAsync(CancellationToken ct = default);
}

public interface ICommunityNotificationRepository
{
    Task<IReadOnlyList<CommunityNotificationLog>> GetRecentLogsAsync(int take = 50, CancellationToken ct = default);
    Task<CommunityNotificationLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddLogAsync(CommunityNotificationLog log, CancellationToken ct = default);
    Task UpdateLogAsync(CommunityNotificationLog log, CancellationToken ct = default);
}

public interface ICommunityNotificationService
{
    Task<IReadOnlyList<NotificationDispatchResult>> BroadcastAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    Task<NotificationDispatchResult> SendToDiscordAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    Task<NotificationDispatchResult> SendToTelegramAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityNotificationLogDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<NotificationDispatchResult> RetryFailedNotificationAsync(Guid logId, CancellationToken ct = default);
    Task TriggerExpiringGiveawaysScanAsync(CancellationToken ct = default);
    Task TriggerFridayReleasesBulletinAsync(CancellationToken ct = default);
    Task SendTestPingAsync(NotificationChannel? channel = null, CancellationToken ct = default);
}
```

---

## 3. Formato y Payloads de Integración

### 3.1 Discord Webhook Payload
```json
{
  "embeds": [
    {
      "title": "⚠️ ¡ÚLTIMAS 24H DE SORTEO! Wingspan: Oceanía",
      "description": "El sorteo organizado por @malditogames está a punto de concluir.",
      "url": "https://ludeka.es/radar",
      "color": 14703160,
      "thumbnail": { "url": "https://ludeka.es/images/games/wingspan.webp" },
      "fields": [
        { "name": "Organizador", "value": "@malditogames", "inline": true },
        { "name": "Plataforma", "value": "Instagram", "inline": true }
      ],
      "footer": { "text": "Ludeka Comunidad • El Letterboxd de los juegos de mesa" }
    }
  ]
}
```

### 3.2 Telegram Bot Payload (`parse_mode: HTML`)
```html
⚠️ <b>¡ÚLTIMAS 24H DE SORTEO!</b>
<b>Wingspan: Oceanía</b>

El sorteo organizado por <b>@malditogames</b> en Instagram finaliza en menos de 24 horas.

🔗 <a href="https://ludeka.es/radar">Ver en el Radar de Ludeka</a>
```
