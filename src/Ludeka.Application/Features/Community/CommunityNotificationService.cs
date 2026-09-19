using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Community;

public class CommunityNotificationService : ICommunityNotificationService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanManageNotifications' para gestionar las notificaciones comunitarias.";

    private readonly CommunityNotificationOptions _options;
    private readonly ICommunityNotificationRepository _repository;
    private readonly IDiscordWebhookClient _discordClient;
    private readonly ITelegramBotClient _telegramClient;
    private readonly IGiveawayRepository _giveawayRepository;
    private readonly IWeeklyReleaseRepository _weeklyReleaseRepository;
    private readonly ILogger<CommunityNotificationService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;
    private readonly OutboxOptions _outboxOptions;

    public CommunityNotificationService(
        IOptions<CommunityNotificationOptions> options,
        ICommunityNotificationRepository repository,
        IDiscordWebhookClient discordClient,
        ITelegramBotClient telegramClient,
        IGiveawayRepository giveawayRepository,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        ILogger<CommunityNotificationService> logger,
        ISessionPermissionGuard? permissionGuard = null,
        IOptions<OutboxOptions>? outboxOptions = null)
    {
        _options = options.Value;
        _repository = repository;
        _discordClient = discordClient;
        _telegramClient = telegramClient;
        _giveawayRepository = giveawayRepository;
        _weeklyReleaseRepository = weeklyReleaseRepository;
        _logger = logger;
        _permissionGuard = permissionGuard;
        // INC-47 (R4b, diseño §6.6, tasks.md 7.8): parámetro opcional para no romper la firma
        // existente (mismo patrón que permissionGuard) — DI lo resuelve solo en producción,
        // donde ya está registrado (tasks.md 4b.3). Solo lo consume DeliverAsync.
        _outboxOptions = outboxOptions?.Value ?? new OutboxOptions();
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1). Solo las acciones
    /// manuales del panel pasan por aquí; el despachador programado usa las rutas de sistema.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanManageNotifications, DenialMessage, ct);

    public async Task<IReadOnlyList<NotificationDispatchResult>> BroadcastAsync(
        CommunityNotificationMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Las notificaciones comunitarias están deshabilitadas globalmente.");
            return [];
        }

        var results = new List<NotificationDispatchResult>();

        // Si se especificó un canal destino exclusivo
        if (message.TargetChannel.HasValue)
        {
            if (message.TargetChannel.Value == NotificationChannel.Discord && _options.DiscordEnabled)
            {
                results.Add(await SendToDiscordAsync(message, ct));
            }
            else if (message.TargetChannel.Value == NotificationChannel.Telegram && _options.TelegramEnabled)
            {
                results.Add(await SendToTelegramAsync(message, ct));
            }
            return results;
        }

        // Difusión multicanal
        if (_options.DiscordEnabled)
        {
            results.Add(await SendToDiscordAsync(message, ct));
        }

        if (_options.TelegramEnabled)
        {
            results.Add(await SendToTelegramAsync(message, ct));
        }

        return results;
    }

    public async Task<NotificationDispatchResult> SendToDiscordAsync(
        CommunityNotificationMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var log = new CommunityNotificationLog(
            message.EventType,
            NotificationChannel.Discord,
            message.Title,
            message.Description,
            message.TargetUrl,
            message.ImageUrl,
            NotificationStatus.Queued);

        await _repository.AddLogAsync(log, ct);

        var result = await SendOnChannelAsync(NotificationChannel.Discord, message, ct);

        if (result.Status == NotificationStatus.DryRun) log.MarkAsDryRun();
        else if (result.Success) log.MarkAsSent();
        else log.MarkAsFailed(result.ErrorMessage ?? "Error desconocido en Discord Webhook.");

        await _repository.UpdateLogAsync(log, ct);
        return result;
    }

    public async Task<NotificationDispatchResult> SendToTelegramAsync(
        CommunityNotificationMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var log = new CommunityNotificationLog(
            message.EventType,
            NotificationChannel.Telegram,
            message.Title,
            message.Description,
            message.TargetUrl,
            message.ImageUrl,
            NotificationStatus.Queued);

        await _repository.AddLogAsync(log, ct);

        var result = await SendOnChannelAsync(NotificationChannel.Telegram, message, ct);

        if (result.Status == NotificationStatus.DryRun) log.MarkAsDryRun();
        else if (result.Success) log.MarkAsSent();
        else log.MarkAsFailed(result.ErrorMessage ?? "Error desconocido en Telegram Bot API.");

        await _repository.UpdateLogAsync(log, ct);
        return result;
    }

    /// <summary>
    /// Envía por <c>delivery.Channel</c> y actualiza ESA sub-entrega del outbox (Attempts,
    /// Status, NextAttemptAt); no crea ninguna fila nueva (INC-47, R4b, diseño §6.6). Exclusivo
    /// del despachador (<see cref="NotificationOutboxDispatcher"/>): los métodos públicos de
    /// arriba crean su propia fila antes de enviar, este opera sobre una que ya existe.
    /// </summary>
    public async Task<NotificationDispatchResult> DeliverAsync(
        CommunityNotificationLog delivery,
        CommunityNotificationMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(message);

        var result = await SendOnChannelAsync(delivery.Channel, message, ct);

        if (result.Status == NotificationStatus.DryRun)
        {
            delivery.MarkAsDryRun();
        }
        else if (result.Success)
        {
            delivery.MarkAsSent();
        }
        else
        {
            var error = result.ErrorMessage ?? "Error desconocido al reintentar la entrega.";
            delivery.RegisterFailedAttempt(error, _outboxOptions.ComputeNextAttempt(delivery.Attempts + 1));

            if (delivery.Attempts >= _outboxOptions.MaxDeliveryAttempts)
            {
                delivery.MarkAsPermanentlyFailed(error);
            }
        }

        await _repository.UpdateLogAsync(delivery, ct);
        return result;
    }

    /// <summary>
    /// Mecánica pura de envío por canal, sin ninguna persistencia (INC-47, R4b, diseño §6.6).
    /// Los tres métodos de envío de arriba delegan aquí: los dos históricos crean su propia fila
    /// antes de llamar y la actualizan después; <see cref="DeliverAsync"/> opera sobre una
    /// sub-entrega del outbox que ya existe.
    /// </summary>
    private async Task<NotificationDispatchResult> SendOnChannelAsync(
        NotificationChannel channel,
        CommunityNotificationMessage message,
        CancellationToken ct)
    {
        if (channel == NotificationChannel.Discord)
        {
            if (_options.DryRun || !_options.IsDiscordConfigured)
            {
                _logger.LogInformation("[DryRun] Simulación de emisión a Discord: {Title}", message.Title);
                return new NotificationDispatchResult(true, NotificationChannel.Discord, NotificationStatus.DryRun);
            }

            try
            {
                return await _discordClient.SendAsync(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar webhook a Discord: {Message}", ex.Message);
                return new NotificationDispatchResult(false, NotificationChannel.Discord, NotificationStatus.Failed, ex.Message);
            }
        }

        if (_options.DryRun || !_options.IsTelegramConfigured)
        {
            _logger.LogInformation("[DryRun] Simulación de emisión a Telegram: {Title}", message.Title);
            return new NotificationDispatchResult(true, NotificationChannel.Telegram, NotificationStatus.DryRun);
        }

        try
        {
            return await _telegramClient.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar mensaje a Telegram: {Message}", ex.Message);
            return new NotificationDispatchResult(false, NotificationChannel.Telegram, NotificationStatus.Failed, ex.Message);
        }
    }

    public async Task<IReadOnlyList<CommunityNotificationLogDto>> GetHistoryAsync(
        int limit = 50,
        CancellationToken ct = default)
    {
        var logs = await _repository.GetRecentLogsAsync(limit, ct);
        return logs.Select(l => new CommunityNotificationLogDto(
            l.Id,
            l.EventType,
            l.Channel,
            l.Title,
            l.Summary,
            l.TargetUrl,
            l.ImageUrl,
            l.Status,
            l.ErrorDetails,
            l.CreatedAt,
            l.SentAt)).ToList();
    }

    public Task<IReadOnlyList<NotificationChannelStatusDto>> GetChannelStatusesAsync(CancellationToken ct = default)
    {
        var discordPreview = _options.IsDiscordConfigured
            ? MaskSecret(_options.DiscordWebhookUrl!)
            : "No configurado";

        var telegramPreview = _options.IsTelegramConfigured
            ? $"ChatId: {_options.TelegramChatId} (Token: {MaskSecret(_options.TelegramBotToken!)})"
            : "No configurado";

        var list = new List<NotificationChannelStatusDto>
        {
            new(NotificationChannel.Discord, _options.DiscordEnabled, _options.IsDiscordConfigured, _options.DryRun, discordPreview),
            new(NotificationChannel.Telegram, _options.TelegramEnabled, _options.IsTelegramConfigured, _options.DryRun, telegramPreview)
        };

        return Task.FromResult<IReadOnlyList<NotificationChannelStatusDto>>(list);
    }

    public async Task<NotificationDispatchResult> RetryFailedNotificationAsync(Guid logId, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var log = await _repository.GetByIdAsync(logId, ct);
        if (log == null)
        {
            return new NotificationDispatchResult(false, NotificationChannel.Discord, NotificationStatus.Failed, "Registro de notificación no encontrado.");
        }

        var message = new CommunityNotificationMessage(
            log.EventType,
            log.Title,
            log.Summary,
            log.TargetUrl,
            log.ImageUrl,
            TargetChannel: log.Channel);

        if (log.Channel == NotificationChannel.Discord)
        {
            if (_options.DryRun || !_options.IsDiscordConfigured)
            {
                log.MarkAsDryRun();
                await _repository.UpdateLogAsync(log, ct);
                return new NotificationDispatchResult(true, NotificationChannel.Discord, NotificationStatus.DryRun);
            }

            var result = await _discordClient.SendAsync(message, ct);
            if (result.Success) log.MarkAsSent();
            else log.MarkAsFailed(result.ErrorMessage ?? "Fallo en reintento.");
            await _repository.UpdateLogAsync(log, ct);
            return result;
        }
        else
        {
            if (_options.DryRun || !_options.IsTelegramConfigured)
            {
                log.MarkAsDryRun();
                await _repository.UpdateLogAsync(log, ct);
                return new NotificationDispatchResult(true, NotificationChannel.Telegram, NotificationStatus.DryRun);
            }

            var result = await _telegramClient.SendAsync(message, ct);
            if (result.Success) log.MarkAsSent();
            else log.MarkAsFailed(result.ErrorMessage ?? "Fallo en reintento.");
            await _repository.UpdateLogAsync(log, ct);
            return result;
        }
    }

    public async Task TriggerExpiringGiveawaysScanAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        await RunExpiringGiveawaysScanAsync(ct);
    }

    /// <inheritdoc />
    public async Task RunExpiringGiveawaysScanAsync(CancellationToken ct = default)
    {
        var activeGiveaways = await _giveawayRepository.GetGiveawaysAsync(includeExpired: false, ct);
        var threshold = DateTimeOffset.UtcNow.AddHours(24);

        var expiring = activeGiveaways
            .Where(g => g.DeadlineAt <= threshold && g.DeadlineAt > DateTimeOffset.UtcNow)
            .ToList();

        _logger.LogInformation("Escaneo de sorteos: {Count} sorteos finalizan en las próximas 24h.", expiring.Count);

        foreach (var giveaway in expiring)
        {
            var remainingHours = Math.Max(1, (int)Math.Round((giveaway.DeadlineAt - DateTimeOffset.UtcNow).TotalHours));
            var message = new CommunityNotificationMessage(
                NotificationEventType.GiveawayExpiring,
                $"⚠️ ¡ÚLTIMAS {remainingHours} HORAS! Sorteo: {giveaway.Title}",
                $"El sorteo organizado por {giveaway.FormattedOrganizer} finaliza muy pronto. ¡Participa antes del cierre de plazo!",
                TargetUrl: giveaway.Url,
                ImageUrl: giveaway.ThumbnailUrl,
                Fields: new Dictionary<string, string>
                {
                    { "Organizador", giveaway.FormattedOrganizer },
                    { "Plataforma", giveaway.Platform.ToString() },
                    { "Juego", giveaway.GameTitle ?? "Juego de mesa" }
                });

            await BroadcastAsync(message, ct);
        }
    }

    public async Task TriggerFridayReleasesBulletinAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        await RunFridayReleasesBulletinAsync(ct);
    }

    /// <inheritdoc />
    public async Task RunFridayReleasesBulletinAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int diff = (7 + ((int)today.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        var endOfWeek = startOfWeek.AddDays(6);

        var weeklyReleases = await _weeklyReleaseRepository.GetReleasesAsync(fromDate: startOfWeek, ct);

        if (weeklyReleases.Count == 0)
        {
            _logger.LogInformation("Boletín de Viernes: No hay lanzamientos registrados para la semana del {Date}.", startOfWeek);
            return;
        }

        var count = weeklyReleases.Count;
        var summaryLines = weeklyReleases
            .Take(5)
            .Select(r => $"• **{r.Title}** ({r.Publisher}){(r.EstimatedPvp.HasValue ? $" — ~{r.EstimatedPvp:F2}€" : "")}{(r.IsReprint ? " [Reimpresión]" : "")}");

        var description = $"Estos son los {count} juegos de mesa y expansiones que llegan a tiendas especializadas este fin de semana:\n\n" +
                          string.Join("\n", summaryLines) +
                          (count > 5 ? $"\n\n...y {count - 5} títulos más disponibles en el catálogo." : "");

        var firstWithImage = weeklyReleases.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.CoverImageUrl));

        var message = new CommunityNotificationMessage(
            NotificationEventType.FridayReleasesSummary,
            $"🛍️ NOVEDADES EN TIENDAS — Boletín del Viernes ({today:dd/MM/yyyy})",
            description,
            TargetUrl: "https://ludeka.es/radar",
            ImageUrl: firstWithImage?.CoverImageUrl,
            Fields: new Dictionary<string, string>
            {
                { "Lanzamientos", $"{count} títulos" },
                { "Semana", $"{startOfWeek:dd/MM} al {endOfWeek:dd/MM}" }
            });

        await BroadcastAsync(message, ct);
    }

    public async Task SendTestPingAsync(NotificationChannel? channel = null, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var message = new CommunityNotificationMessage(
            NotificationEventType.CustomTestPing,
            "🔔 Ping de Prueba de Notificaciones — Ludeka",
            "Este es un mensaje de prueba técnica emitido desde el panel de administración de Ludeka para verificar la conectividad de los webhooks comunitarios.",
            TargetUrl: "https://ludeka.es",
            Fields: new Dictionary<string, string>
            {
                { "Entorno", "Ludeka Core (.NET 10)" },
                { "Fecha", DateTimeOffset.UtcNow.ToString("dd/MM/yyyy HH:mm:ss 'UTC'") }
            },
            TargetChannel: channel);

        await BroadcastAsync(message, ct);
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 10)
            return "****";

        return $"{secret[..6]}...{secret[^4..]}";
    }
}
