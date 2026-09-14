using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato unificado para almacenamiento, variantes deterministas y gestión de imágenes de Ludeka.
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Optimiza un stream de imagen a WebP y lo sube al bucket/almacén bajo la clave especificada.
    /// </summary>
    Task<string> UploadOptimizedImageAsync(
        Stream inputStream,
        string objectKey,
        int maxWidth = 1000,
        int quality = 82,
        CancellationToken ct = default);

    /// <summary>
    /// Genera y sube las variantes canónicas de imagen de un juego (ej. portada full + miniatura, o trasera, o en mesa).
    /// </summary>
    Task<ImageVariantUrls> UploadGameImageVariantsAsync(
        Stream rawImageStream,
        int bggId,
        string imageType,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina una imagen del almacenamiento por su clave de objeto.
    /// </summary>
    Task<bool> DeleteImageAsync(
        string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Retorna la URL pública accesible para una clave de objeto dada.
    /// </summary>
    string GetPublicUrl(string objectKey);

    // --- Métodos de compatibilidad existentes ---

    Task<GameImageUploadResult> SaveGameCoverAsync(
        string slug,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> SaveEventPosterAsync(
        string eventSlugOrId,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> SaveCommunityImageAsync(
        string subfolder,
        string identifier,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> ValidateCoverUrlAsync(
        string imageUrl,
        CancellationToken ct = default);
}
