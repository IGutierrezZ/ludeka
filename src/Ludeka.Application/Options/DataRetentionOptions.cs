namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para las políticas de retención, purga de entidades caducadas y liberación de almacenamiento de imágenes.
/// </summary>
public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    /// <summary>
    /// Habilita o deshabilita la purga automática de datos vencidos. Por defecto true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Período de gracia en días tras la fecha límite de un sorteo antes de su eliminación física. Por defecto 7 días.
    /// </summary>
    public int GiveawayGracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Período de gracia en días tras la fecha fin de un evento antes de su eliminación física. Por defecto 7 días.
    /// </summary>
    public int EventGracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Tiempo máximo en días de permanencia de una novedad editorial en la plataforma si su fecha ha vencido o no tiene fecha. Por defecto 60 días.
    /// </summary>
    public int ReleaseRetentionDays { get; set; } = 60;

    /// <summary>
    /// Hora UTC de ejecución diaria programada (0..23). Por defecto 3 (03:00 UTC).
    /// </summary>
    public int ExecutionHourUtc { get; set; } = 3;
}
