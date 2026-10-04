namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para la auto-ingesta periódica y gradual de vídeos de YouTube en juegos del catálogo prioritario.
/// </summary>
public class YouTubeAutoIngestOptions
{
    public const string SectionName = "YouTubeAutoIngest";

    /// <summary>
    /// Indica si el proceso de auto-ingesta está habilitado.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Límite diario de juegos a evaluar y auto-ingestar (por defecto 60 juegos/día, consumiendo 6.000 unidades de cuota de YouTube).
    /// </summary>
    public int DailyGamesLimit { get; set; } = 60;

    /// <summary>
    /// Rango máximo de ranking BGG para priorizar el catálogo clave (por defecto Top 4.000).
    /// </summary>
    public int MaxBggRank { get; set; } = 4000;

    /// <summary>
    /// Determina si los vídeos ingestados quedan en estado Approved directamente (por defecto true).
    /// </summary>
    public bool AutoApprove { get; set; } = true;

    /// <summary>
    /// Pausa en milisegundos entre juegos procesados para dosificar llamadas a la API de YouTube.
    /// </summary>
    public int DelayBetweenGamesMs { get; set; } = 100;
}
