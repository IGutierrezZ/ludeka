using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de negocio para la gestión y consulta de clasificaciones públicas con anonimato y opt-in estricto.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>
    /// Obtiene la clasificación mensual de jugadores según partidas registradas en mesa.
    /// Solo incluye usuarios con consentimiento explícito (LeaderboardOptIn == true).
    /// </summary>
    Task<MonthlyLeaderboardDto> GetMonthlyLeaderboardAsync(int? year = null, int? month = null, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado de participación y seudónimo actual del usuario especificado.
    /// </summary>
    Task<LeaderboardParticipationDto> GetUserParticipationAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza el consentimiento de participación, la preferencia de anonimato y el seudónimo opcional.
    /// </summary>
    Task SetUserParticipationAsync(string userId, bool optIn, bool anonymous, string? customPseudonym = null, CancellationToken ct = default);
}
