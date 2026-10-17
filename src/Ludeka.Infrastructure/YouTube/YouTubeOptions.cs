namespace Ludeka.Infrastructure.YouTube;

public class YouTubeOptions
{
    public const string SectionName = "YouTube";

    /// <summary>
    /// Clave oficial de Google Cloud para YouTube Data API v3 (lectura pública).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// URL base de la API de YouTube v3.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.googleapis.com/youtube/v3/";

    /// <summary>
    /// URL base de subida resumable de YouTube Data API v3.
    /// </summary>
    public string UploadBaseUrl { get; set; } = "https://www.googleapis.com/upload/youtube/v3/videos";

    /// <summary>
    /// Endpoint OAuth 2.0 de Google para intercambio de refresh_token.
    /// </summary>
    public string TokenEndpointUrl { get; set; } = "https://oauth2.googleapis.com/token";

    /// <summary>
    /// Client ID de OAuth 2.0 para subida de vídeos al canal de Ludeka.
    /// </summary>
    public string? OAuthClientId { get; set; }

    /// <summary>
    /// Client Secret de OAuth 2.0 para subida de vídeos al canal de Ludeka.
    /// </summary>
    public string? OAuthClientSecret { get; set; }

    /// <summary>
    /// Refresh Token de larga duración autorizado por la cuenta propietaria del canal de YouTube de Ludeka.
    /// </summary>
    public string? OAuthRefreshToken { get; set; }

    /// <summary>
    /// Estado de privacidad por defecto al subir Shorts ("public", "unlisted" o "private").
    /// </summary>
    public string DefaultPrivacyStatus { get; set; } = "public";

    /// <summary>
    /// Categoría de YouTube por defecto ("20" = Gaming).
    /// </summary>
    public string DefaultCategoryId { get; set; } = "20";

    /// <summary>
    /// Si es true o si ApiKey está vacía, opera en modo de salvaguardas (dataset mock).
    /// Si es false y existe ApiKey, consulta YouTube en vivo.
    /// </summary>
    public bool Simulate { get; set; } = false;

    /// <summary>
    /// Determina si debe simular (solo si se fuerza Simulate o si no se ha provisto ApiKey).
    /// </summary>
    public bool ShouldSimulate => Simulate || string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Determina si las credenciales OAuth 2.0 para subida de Shorts están configuradas y activas.
    /// </summary>
    public bool IsUploadConfigured =>
        !Simulate &&
        !string.IsNullOrWhiteSpace(OAuthClientId) &&
        !string.IsNullOrWhiteSpace(OAuthClientSecret) &&
        !string.IsNullOrWhiteSpace(OAuthRefreshToken);
}
