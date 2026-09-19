using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Despachador real del outbox de notificaciones comunitarias (INC-47, R4b, diseño §6.1/§6.5).
/// </summary>
public interface INotificationOutboxDispatcher
{
    /// <summary>Un ciclo acotado: reclama un lote, deriva los canales de CADA mensaje releyendo
    /// las opciones, asegura la sub-entrega por canal e intenta el envío. No entra en ningún
    /// bucle: la cadencia de sondeo la gobierna quien invoca este método.</summary>
    Task<OutboxDispatchResultDto> DispatchPendingAsync(CancellationToken ct = default);
}
