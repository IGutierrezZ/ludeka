using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato del servicio para sincronizar y enriquecer medios de imágenes comunitarias (portada, contraportada, mesa)
/// para los juegos más relevantes de BoardGameGeek (Top 3.000).
/// </summary>
public interface IBggImagesSyncService
{
    /// <summary>
    /// Sincroniza un lote de juegos ordenados por BggRank ascendente a partir de un cursor de rango.
    /// </summary>
    /// <param name="afterRank">Último BggRank procesado (0 para comenzar desde el #1).</param>
    /// <param name="batchSize">Cantidad máxima de juegos a procesar en el lote.</param>
    /// <param name="maxRank">Límite superior de rango BGG a considerar (ej. 3000).</param>
    /// <param name="delayMs">Pausa de cortesía en milisegundos entre consultas a la API de GeekDo.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<BggImagesSyncResultDto> SyncTopRankedImagesBatchAsync(
        int afterRank = 0,
        int batchSize = 25,
        int maxRank = 3000,
        int delayMs = 800,
        CancellationToken ct = default);
}
