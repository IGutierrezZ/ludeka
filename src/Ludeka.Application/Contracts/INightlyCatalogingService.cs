using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el orquestador inteligente de catalogación nocturna asistida por BGG y Gemini.
/// </summary>
public interface INightlyCatalogingService
{
    /// <summary>
    /// Ejecuta el ciclo nocturno completo: extracción en novedades, procesamiento de cola prioritaria,
    /// relleno de cupo con Top de BGG y registro de bitácora de auditoría.
    /// </summary>
    Task<NightlyCatalogingResultDto> ExecuteNightlyCatalogingAsync(int? customLimit = null, CancellationToken ct = default);

    /// <summary>
    /// Ciclo de sistema del servicio hospedado (INC-46, W1): misma catalogación sin la guarda de la
    /// interfaz, para que el proceso programado no dependa de ninguna sesión.
    /// </summary>
    Task<NightlyCatalogingResultDto> RunScheduledCatalogingAsync(int? customLimit = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el historial de ejecuciones recientes registradas en la bitácora.
    /// </summary>
    Task<IReadOnlyList<NightlyCatalogingExecutionLogDto>> GetExecutionHistoryAsync(int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las opciones de configuración activas para la catalogación nocturna.
    /// </summary>
    Task<NightlyCatalogingOptions> GetOptionsAsync(CancellationToken ct = default);
}
