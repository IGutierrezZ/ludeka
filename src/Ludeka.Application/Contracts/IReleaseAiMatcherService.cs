using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el servicio de asistencia de IA que deduce el título canónico en BGG y su correspondencia
/// a partir de lanzamientos anunciados por editoriales (como Maldito Games o Devir).
/// </summary>
public interface IReleaseAiMatcherService
{
    /// <summary>
    /// Infiere el título original en BGG, posible ID de BGG y razonamiento de coincidencia editorial.
    /// </summary>
    Task<AiReleaseMatchResultDto> SuggestMatchAsync(
        string rawTitle,
        string publisher,
        decimal? estimatedPvp,
        string? notes,
        CancellationToken ct = default);
}
