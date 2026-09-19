using System;

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

    /// <summary>Retroceso exponencial acotado para el siguiente intento (diseño §5.4):
    /// <c>ahora + RetryBackoffSeconds × RetryBackoffMultiplier^(attempts-1)</c>, con techo en
    /// <c>LeaseSeconds × 12</c>. Lo usan tanto la reclamación del mensaje (<c>attempts</c> =
    /// intentos de reclamación) como la sub-entrega por canal (<c>attempts</c> = intentos de
    /// entrega).</summary>
    public DateTimeOffset ComputeNextAttempt(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);
        var delaySeconds = RetryBackoffSeconds * Math.Pow(RetryBackoffMultiplier, exponent);
        var cappedSeconds = Math.Min(delaySeconds, LeaseSeconds * 12);
        return DateTimeOffset.UtcNow.AddSeconds(cappedSeconds);
    }
}
