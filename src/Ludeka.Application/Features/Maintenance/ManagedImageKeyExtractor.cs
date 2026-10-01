using System;

namespace Ludeka.Application.Features.Maintenance;

/// <summary>
/// Utilidad para identificar y extraer claves de objeto relativas de imágenes almacenadas en el sistema local o Cloudflare R2.
/// </summary>
public static class ManagedImageKeyExtractor
{
    private static readonly string[] ExternalHostKeywords =
    [
        "geekdo-images.com",
        "boardgamegeek.com",
        "instagram.com",
        "cdninstagram.com",
        "fbcdn.net",
        "unsplash.com",
        "imgur.com",
        "cloudinary.com"
    ];

    /// <summary>
    /// Intenta extraer la clave de objeto relativa de una URL si pertenece al almacenamiento gestionado de Ludeka.
    /// </summary>
    /// <param name="imageUrl">URL absoluta o relativa de la imagen.</param>
    /// <param name="objectKey">Clave relativa resultante apta para <see cref="Contracts.IImageStorageService.DeleteImageAsync"/>.</param>
    /// <returns>True si la imagen es gestionada por Ludeka; false en caso contrario.</returns>
    public static bool TryExtract(string? imageUrl, out string? objectKey)
    {
        objectKey = null;
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        var trimmed = imageUrl.Trim();

        // 1. Rutas relativas estándar de la aplicación: "/images/..." o "images/..."
        if (trimmed.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            objectKey = trimmed.Substring("/images/".Length).TrimStart('/');
            return !string.IsNullOrWhiteSpace(objectKey);
        }

        if (trimmed.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
        {
            objectKey = trimmed.Substring("images/".Length).TrimStart('/');
            return !string.IsNullOrWhiteSpace(objectKey);
        }

        // 2. URLs absolutas (CDN propio, Cloudflare R2 o storage)
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            foreach (var external in ExternalHostKeywords)
            {
                if (host.Contains(external, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            var path = uri.AbsolutePath.TrimStart('/');
            if (path.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            {
                objectKey = path.Substring("images/".Length).TrimStart('/');
                return !string.IsNullOrWhiteSpace(objectKey);
            }

            // Si es una ruta bajo carpetas canónicas conocidas de Ludeka
            if (path.StartsWith("events/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("community/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("games/", StringComparison.OrdinalIgnoreCase))
            {
                objectKey = path;
                return true;
            }
        }

        return false;
    }
}
