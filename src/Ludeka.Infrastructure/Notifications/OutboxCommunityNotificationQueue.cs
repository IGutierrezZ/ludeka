using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Notifications;

/// <summary>
/// Implementación real de <see cref="ICommunityNotificationQueue"/> sobre el outbox persistente
/// (INC-47, R4b, diseño §6.2, tasks.md 6.10). Ámbito (Scoped): consume
/// <see cref="INotificationOutboxRepository"/>, que a su vez usa el <c>LudekaDbContext</c> con
/// ámbito. <see cref="InMemoryCommunityNotificationQueue"/> se conserva aparte, solo para
/// desarrollo local (<c>OutboxOptions.UseInMemoryQueueForLocalDev</c>).
/// </summary>
public sealed class OutboxCommunityNotificationQueue : ICommunityNotificationQueue
{
    private readonly INotificationOutboxRepository _repository;
    private readonly ILogger<OutboxCommunityNotificationQueue> _logger;

    public OutboxCommunityNotificationQueue(
        INotificationOutboxRepository repository,
        ILogger<OutboxCommunityNotificationQueue> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var fieldsJson = message.Fields is { Count: > 0 }
                ? JsonSerializer.Serialize(message.Fields)
                : "{}";

            var outboxMessage = new NotificationOutboxMessage(
                message.EventType,
                message.Title,
                message.Description,
                message.TargetUrl,
                message.ImageUrl,
                fieldsJson,
                message.TargetChannel);

            await _repository.EnqueueAsync(outboxMessage, ct);
        }
        catch (Exception ex)
        {
            // Resuelve C2 (diseño §2): FoundingVerdictService.cs:219-222 y RuleQAService.cs:201-204
            // (ninguno de los dos se toca) tragan esta excepción en su catch vacío. El rastro
            // queda aquí, en el log del outbox, antes de relanzarla.
            _logger.LogError(ex, "Fallo al persistir la notificación en el outbox: {Title}", message.Title);
            throw;
        }
    }
}
