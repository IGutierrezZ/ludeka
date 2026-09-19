using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Contracts;

/// <summary>
/// INC-47 (R4a/R4b, diseño §6.1): reducida a <see cref="EnqueueAsync"/>. <c>ReadAllAsync</c> se
/// retiró de la interfaz (tasks.md 6.7) porque el consumo infinito que exigía
/// (<c>CommunityNotificationDispatcherHostedService.ProcessQueueAsync</c>, hoy reescrito en
/// tasks.md 7.9) es incompatible con un despachador que reclama lotes acotados del outbox.
/// Sobrevive como método público, sin contrato, en
/// <see cref="Ludeka.Infrastructure.Notifications.InMemoryCommunityNotificationQueue"/>, para
/// desarrollo local y pruebas.
/// </summary>
public interface ICommunityNotificationQueue
{
    ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default);
}
