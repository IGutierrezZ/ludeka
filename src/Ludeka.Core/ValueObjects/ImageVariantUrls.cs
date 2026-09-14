namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa el par de URLs públicas correspondientes a la versión principal y a la miniatura optimizadas de una imagen.
/// </summary>
public record ImageVariantUrls(string FullUrl, string ThumbUrl);
