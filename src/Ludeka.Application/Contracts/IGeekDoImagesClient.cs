using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Cliente para la API interna de imágenes comunitarias de GeekDo (BoardGameGeek).
/// </summary>
public interface IGeekDoImagesClient
{
    /// <summary>
    /// Obtiene las 3 fotos comunitarias más votadas para un juego dado (portada, trasera y foto en mesa).
    /// </summary>
    Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default);
}
