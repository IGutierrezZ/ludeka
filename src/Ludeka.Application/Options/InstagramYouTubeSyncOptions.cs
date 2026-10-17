namespace Ludeka.Application.Options;

public class InstagramYouTubeSyncOptions
{
    public const string SectionName = "InstagramYouTubeSync";

    /// <summary>
    /// Máximo de Reels que se suben a YouTube Shorts en una misma ejecución diaria.
    /// Cada subida consume 1.600 unidades de la cuota gratuita de 10.000 uds/día (5 * 1.600 = 8.000 uds).
    /// </summary>
    public int MaxUploadsPerRun { get; set; } = 5;

    /// <summary>
    /// Número máximo de Reels a consultar en la cuenta de Instagram por ciclo.
    /// </summary>
    public int MaxFetchReels { get; set; } = 50;

    /// <summary>
    /// Número máximo de reintentos ante fallos transitorios por cada Reel.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
