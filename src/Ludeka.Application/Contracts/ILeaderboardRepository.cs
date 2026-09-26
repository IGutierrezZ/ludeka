using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Agregación de partidas jugadas por un usuario dentro de un rango temporal.
/// </summary>
public record UserMonthlyPlaysAggregate(
    string UserId,
    int PlayCount,
    DateTimeOffset FirstPlayDate
);

/// <summary>
/// Repositorio de consultas analíticas para clasificaciones de jugadores.
/// </summary>
public interface ILeaderboardRepository
{
    Task<List<UserMonthlyPlaysAggregate>> GetMonthlyPlaysAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default);
}
