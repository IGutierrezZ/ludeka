using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para la consulta de las tendencias de juegos de mesa y cálculo de deltas de posición.
/// </summary>
public interface ITrendingService
{
    /// <summary>
    /// Obtiene la comparativa completa de las 50 tendencias de la fecha más reciente respecto al día previo disponible.
    /// </summary>
    Task<TrendingComparisonDto> GetTrendingComparisonAsync(CancellationToken ct = default);
}
