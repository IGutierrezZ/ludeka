namespace Ludeka.Core.Enums;

/// <summary>
/// Estado de la descarga de metadatos detallados de BGG (Thing XML) en staging.
/// </summary>
public enum StagingFetchStatus
{
    Pending = 0,
    InProgress = 1,
    Fetched = 2,
    Failed = 3
}

/// <summary>
/// Estado de la descarga, optimización y subida de imágenes a Cloudflare R2 en staging.
/// </summary>
public enum StagingImagesStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Failed = 4
}

/// <summary>
/// Estado del enriquecimiento con síntesis editorial generada por Gemini Flash en staging.
/// </summary>
public enum StagingAiStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    QuotaExceeded = 3,
    Skipped = 4,
    Failed = 5
}

/// <summary>
/// Estado de la promoción del registro de staging hacia el catálogo definitivo (Games).
/// </summary>
public enum StagingPromotionStatus
{
    Pending = 0,
    InProgress = 1,
    Promoted = 2,
    Failed = 3
}
