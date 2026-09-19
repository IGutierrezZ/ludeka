namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración del outbox de notificaciones (INC-47, diseño §5.4). El registro en
/// el contenedor de dependencias se hace en R4a (tarea 6.11), que es quien primero las consume.
/// </summary>
public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 20;
    public int MaxDeliveryAttempts { get; set; } = 5;
    public int MaxClaimAttempts { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 300;
    public int RetryBackoffSeconds { get; set; } = 60;
    public int RetryBackoffMultiplier { get; set; } = 4;
    public int HealthPendingDepthDegraded { get; set; } = 100;
    public int HealthOldestPendingDegradedMinutes { get; set; } = 30;
    public int HealthQueryTimeoutSeconds { get; set; } = 2;
}
