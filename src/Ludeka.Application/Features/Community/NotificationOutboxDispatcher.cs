using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Community;

/// <summary>
/// Despachador real del outbox de notificaciones comunitarias (INC-47, R4b, diseño §6.1/§6.5).
/// Un ciclo acotado: reclama un lote, deriva el conjunto de canales de CADA mensaje releyendo
/// <see cref="CommunityNotificationOptions"/> en el momento del despacho — nunca lo fija al
/// encolar (decisión 4 del maintainer) —, asegura la sub-entrega por canal y entrega. La
/// cadencia de sondeo la gobierna <c>CommunityNotificationDispatcherHostedService</c> (R4b,
/// tasks.md 7.9), no esta clase.
/// </summary>
public class NotificationOutboxDispatcher : INotificationOutboxDispatcher
{
    private readonly INotificationOutboxRepository _repository;
    private readonly ICommunityNotificationService _notificationService;
    private readonly CommunityNotificationOptions _channelOptions;
    private readonly OutboxOptions _outboxOptions;
    private readonly ILogger<NotificationOutboxDispatcher> _logger;

    public NotificationOutboxDispatcher(
        INotificationOutboxRepository repository,
        ICommunityNotificationService notificationService,
        IOptions<CommunityNotificationOptions> channelOptions,
        IOptions<OutboxOptions> outboxOptions,
        ILogger<NotificationOutboxDispatcher> logger)
    {
        _repository = repository;
        _notificationService = notificationService;
        _channelOptions = channelOptions.Value;
        _outboxOptions = outboxOptions.Value;
        _logger = logger;
    }

    public async Task<OutboxDispatchResultDto> DispatchPendingAsync(CancellationToken ct = default)
    {
        if (!_outboxOptions.Enabled)
        {
            return new OutboxDispatchResultDto(0, 0, 0, 0);
        }

        var claimedBy = $"{Environment.MachineName}:{Environment.ProcessId}";
        var lease = TimeSpan.FromSeconds(_outboxOptions.LeaseSeconds);
        var claims = await _repository.ClaimPendingAsync(_outboxOptions.BatchSize, claimedBy, lease, ct);

        int completed = 0, retried = 0, dead = 0;

        foreach (var claim in claims)
        {
            var channels = ResolveChannels(claim);

            if (channels.Count == 0)
            {
                if (await ReleaseOrKillAsync(claim, "Ningún canal habilitado para este mensaje.", ct))
                {
                    dead++;
                }
                else
                {
                    retried++;
                }
                continue;
            }

            var message = ToMessage(claim);
            var allTerminal = true;
            string? lastError = null;

            foreach (var channel in channels)
            {
                var delivery = await _repository.EnsureDeliveryAsync(claim.Id, channel, claim, ct);
                if (delivery.Status != NotificationStatus.Queued)
                {
                    // Ya terminal (Sent/DryRun/Failed): idempotente, no se reintenta.
                    continue;
                }

                var result = await _notificationService.DeliverAsync(delivery, message, ct);
                if (delivery.Status == NotificationStatus.Queued)
                {
                    allTerminal = false;
                    lastError = result.ErrorMessage;
                }
            }

            if (allTerminal)
            {
                await _repository.CompleteMessageAsync(claim.Id, ct);
                completed++;
            }
            else if (await ReleaseOrKillAsync(claim, lastError, ct))
            {
                dead++;
            }
            else
            {
                retried++;
            }
        }

        return new OutboxDispatchResultDto(claims.Count, completed, retried, dead);
    }

    /// <summary>Deriva el conjunto de canales AHORA, releyendo las opciones (diseño §6.5, paso
    /// 1) — misma rama que <c>CommunityNotificationService.BroadcastAsync</c>
    /// (:66,77,81,89,94), pero a partir de <see cref="OutboxClaim.TargetChannel"/> en vez de
    /// <c>CommunityNotificationMessage.TargetChannel</c>.</summary>
    private List<NotificationChannel> ResolveChannels(OutboxClaim claim)
    {
        var channels = new List<NotificationChannel>();

        if (!_channelOptions.Enabled)
        {
            return channels;
        }

        if (claim.TargetChannel.HasValue)
        {
            if (claim.TargetChannel.Value == NotificationChannel.Discord && _channelOptions.DiscordEnabled)
            {
                channels.Add(NotificationChannel.Discord);
            }
            else if (claim.TargetChannel.Value == NotificationChannel.Telegram && _channelOptions.TelegramEnabled)
            {
                channels.Add(NotificationChannel.Telegram);
            }

            return channels;
        }

        if (_channelOptions.DiscordEnabled) channels.Add(NotificationChannel.Discord);
        if (_channelOptions.TelegramEnabled) channels.Add(NotificationChannel.Telegram);
        return channels;
    }

    private static CommunityNotificationMessage ToMessage(OutboxClaim claim)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(claim.FieldsJson);
        return new CommunityNotificationMessage(
            claim.EventType, claim.Title, claim.Summary, claim.TargetUrl, claim.ImageUrl, fields, claim.TargetChannel);
    }

    /// <summary>Libera el mensaje con retroceso exponencial o lo agota a <c>Dead</c> si ya
    /// alcanzó <c>MaxClaimAttempts</c> (diseño §6.5, paso 2). Devuelve <see langword="true"/>
    /// si el mensaje quedó agotado.</summary>
    private async Task<bool> ReleaseOrKillAsync(OutboxClaim claim, string? reason, CancellationToken ct)
    {
        if (claim.Attempts >= _outboxOptions.MaxClaimAttempts)
        {
            _logger.LogWarning(
                "Mensaje del outbox {MessageId} agotó los intentos de reclamación: {Reason}", claim.Id, reason);
            await _repository.MarkMessageDeadAsync(claim.Id, reason ?? "Se agotaron los intentos de reclamación.", ct);
            return true;
        }

        await _repository.ReleaseMessageAsync(claim.Id, _outboxOptions.ComputeNextAttempt(claim.Attempts), reason, ct);
        return false;
    }
}
