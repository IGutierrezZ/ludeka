using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Compositor editorial de carátulas para el radar de sorteos en proporción horizontal 16:9,
/// adaptando fotos verticales u oscuras con fondo difuminado y recorte inteligente de barras móviles.
/// </summary>
public interface IGiveawayCoverComposer
{
    byte[] ComposeHorizontalCover(
        byte[] originalImageBytes,
        NormalizedBoundingBoxDto? cropBox = null,
        int targetWidth = 1280,
        int targetHeight = 720);
}
