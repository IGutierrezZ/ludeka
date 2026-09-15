namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para el radar de precios, detección de ofertas y sondeo periódico.
/// </summary>
public class PriceRadarOptions
{
    public const string SectionName = "PriceRadar";

    /// <summary>
    /// Habilita o deshabilita el servicio en segundo plano del radar de precios.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Intervalo en horas entre escaneos periódicos en segundo plano. Por defecto 6 horas.
    /// </summary>
    public int CheckIntervalHours { get; set; } = 6;

    /// <summary>
    /// Umbral mínimo de porcentaje de rebaja para calificar como oferta destacada (chollo). Por defecto 10.0%.
    /// </summary>
    public double MinDiscountPercentage { get; set; } = 10.0;

    /// <summary>
    /// Cantidad máxima de juegos muestreados en cada ciclo de sondeo desatendido.
    /// </summary>
    public int MaxGamesPerScan { get; set; } = 25;

    /// <summary>
    /// Modo simulado para pruebas sin llamadas HTTP salientes.
    /// </summary>
    public bool SimulateInDevelopment { get; set; } = false;
}
