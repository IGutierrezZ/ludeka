using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Concesión de ejecución por ventana temporal, genérica para los cuatro trabajos de fondo
/// (INC-47, R3b, diseño §7, decisión D4). La restricción única (JobName, WindowKey) es la que
/// hace que un reintento del planificador externo choque contra la base de datos, nunca contra
/// una comprobación en memoria.
/// </summary>
public class JobExecutionLease
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string JobName { get; private set; } = string.Empty;
    public string WindowKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Running";
    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset HeartbeatAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public int ProcessedCount { get; private set; }
    public int FailedCount { get; private set; }
    public long? DurationMs { get; private set; }
    public string? HostIdentifier { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Constructor privado para EF Core
    private JobExecutionLease() { }

    public JobExecutionLease(string jobName, string windowKey, string? hostIdentifier = null)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("El nombre del trabajo no puede estar vacío.", nameof(jobName));

        if (string.IsNullOrWhiteSpace(windowKey))
            throw new ArgumentException("La clave de ventana no puede estar vacía.", nameof(windowKey));

        JobName = jobName.Trim();
        WindowKey = windowKey.Trim();
        HostIdentifier = string.IsNullOrWhiteSpace(hostIdentifier) ? null : hostIdentifier.Trim();
        Status = "Running";
        StartedAt = DateTimeOffset.UtcNow;
        HeartbeatAt = StartedAt;
    }

    /// <summary>Actualiza el latido de una ejecución en curso (diseño §7.3): se llama en cada
    /// frontera de fase de la unidad de trabajo, nunca desde un temporizador en un hilo aparte.</summary>
    public void Touch()
    {
        HeartbeatAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marca la concesión como completada con éxito (código de salida 0).</summary>
    public void MarkCompleted(int processedCount, int failedCount, long durationMs)
    {
        Status = "Completed";
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessedCount = processedCount;
        FailedCount = failedCount;
        DurationMs = durationMs;
        ErrorMessage = null;
    }

    /// <summary>Marca la concesión como fallida (código de salida distinto de cero): retomable
    /// por una ejecución posterior mediante el intercambio condicional de §7.3.</summary>
    public void MarkFailed(string? errorMessage, int processedCount, int failedCount, long durationMs)
    {
        Status = "Failed";
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessedCount = processedCount;
        FailedCount = failedCount;
        DurationMs = durationMs;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Error no especificado durante la ejecución del trabajo."
            : errorMessage.Trim();
    }
}
