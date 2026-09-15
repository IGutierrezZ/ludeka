namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para el worker desatendido de recolección de canales sociales.
/// </summary>
public class SocialCollectorOptions
{
    public const string SectionName = "SocialCollector";

    /// <summary>
    /// Indica si el worker en segundo plano está habilitado para ejecutarse periódicamente.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Intervalo de sondeo en minutos entre ejecuciones del lote desatendido.
    /// </summary>
    public int IntervalMinutes { get; set; } = 120;

    /// <summary>
    /// Número máximo de publicaciones recientes a extraer por cuenta en cada ciclo de sondeo.
    /// </summary>
    public int MaxItemsPerAccount { get; set; } = 5;

    /// <summary>
    /// Pausa inicial en segundos tras arrancar el servidor antes de lanzar el primer sondeo.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Modo simulado para pruebas unitarias y entornos de desarrollo sin conectividad externa.
    /// </summary>
    public bool Simulate { get; set; } = false;

    /// <summary>
    /// Plantilla de URL para puente RSS (ej. RSS-Bridge) para canales de Instagram.
    /// Formato con placeholder: https://rss-bridge.host/?action=display&bridge=Instagram&context=Username&u={username}&format=Atom
    /// </summary>
    public string? RssBridgeUrlTemplate { get; set; }

    /// <summary>
    /// Antigüedad máxima en días para importar publicaciones históricas (por defecto 14 días).
    /// </summary>
    public int MaxPostAgeDays { get; set; } = 14;

    /// <summary>
    /// Habilita o deshabilita la recolección de canales de YouTube.
    /// </summary>
    public bool YouTubeEnabled { get; set; } = true;

    /// <summary>
    /// Habilita o deshabilita la recolección de canales públicos de Telegram.
    /// </summary>
    public bool TelegramEnabled { get; set; } = true;

    /// <summary>
    /// Habilita o deshabilita la recolección de feeds RSS/Atom de blogs de editoriales.
    /// </summary>
    public bool RssBlogEnabled { get; set; } = true;

    /// <summary>
    /// Habilita o deshabilita la recolección de cuentas de Instagram.
    /// </summary>
    public bool InstagramEnabled { get; set; } = true;
}
