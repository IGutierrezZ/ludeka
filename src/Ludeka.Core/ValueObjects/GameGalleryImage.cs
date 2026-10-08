using System;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa una imagen adicional de la galería fotográfica de una ficha de juego.
/// </summary>
public record GameGalleryImage
{
    public string Url { get; init; } = string.Empty;
    public string? Title { get; init; }

    // Constructor sin parámetros para deserialización JSON / EF Core
    public GameGalleryImage() { }

    public GameGalleryImage(string url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL de la imagen de galería no puede estar vacía.", nameof(url));

        Url = url.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        if (Title != null && Title.Length > 150)
            throw new ArgumentException("El título de la foto de galería no puede exceder los 150 caracteres.", nameof(title));
    }
}
