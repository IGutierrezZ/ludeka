using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Asistente semántico con Google Gemini Flash (y fallback heurístico en español)
/// para clasificar el contenido y estructurar datos de sorteos, novedades, eventos y vídeos.
/// </summary>
public interface ISocialAiAnalysisService
{
    Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default);
}
