using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de agregación de elementos pendientes de resolver en el área de gestión para administradores y moderadores (INC-149).
/// </summary>
public interface IAdminPendingSummaryService
{
    Task<AdminPendingSummaryDto> GetPendingSummaryAsync(CancellationToken ct = default);
}
