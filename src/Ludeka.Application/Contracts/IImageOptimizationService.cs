using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Dimensiones de ancho y alto de una imagen en píxeles.
/// </summary>
public record ImageDimensions(int Width, int Height);

/// <summary>
/// Servicio de procesamiento y optimización de imágenes en memoria (Zero-Disk).
/// </summary>
public interface IImageOptimizationService
{
    /// <summary>
    /// Convierte y redimensiona proporcionalmente un stream de imagen a formato WebP optimizado.
    /// Si el ancho de la imagen original es menor o igual a maxWidth, no se amplía (evita upscaling).
    /// </summary>
    Task<byte[]> ConvertToWebpAsync(
        Stream inputStream, 
        int maxWidth, 
        int quality = 82, 
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene las dimensiones originales (ancho y alto) de la imagen sin persistirla.
    /// </summary>
    Task<ImageDimensions> GetDimensionsAsync(
        Stream inputStream, 
        CancellationToken ct = default);
}
