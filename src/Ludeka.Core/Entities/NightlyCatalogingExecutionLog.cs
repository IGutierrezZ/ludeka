using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ludeka.Core.Entities;

/// <summary>
/// Bitácora estructurada que registra cada ejecución nocturna o bajo demanda
/// del orquestador inteligente de catalogación y descubrimiento.
/// </summary>
public class NightlyCatalogingExecutionLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public int QueueProcessedCount { get; private set; }
    public int NewsDiscoveryCount { get; private set; }
    public int BggDiscoveryCount { get; private set; }
    public int TopBackfillCount { get; private set; }
    public int TotalCatalogedCount { get; private set; }
    public int FailedCount { get; private set; }
    public string CatalogedTitlesJson { get; private set; } = "[]";
    public string Status { get; private set; } = "Running";
    public string? ErrorMessage { get; private set; }

    private NightlyCatalogingExecutionLog() { }

    public NightlyCatalogingExecutionLog(DateTimeOffset startedAt)
    {
        StartedAt = startedAt;
        Status = "Running";
        CatalogedTitlesJson = "[]";
    }

    public void Complete(
        int queueProcessed,
        int newsDiscovery,
        int topBackfill,
        int totalCataloged,
        int failed,
        IEnumerable<string> titles,
        int bggDiscovery = 0)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        QueueProcessedCount = queueProcessed;
        NewsDiscoveryCount = newsDiscovery;
        BggDiscoveryCount = bggDiscovery;
        TopBackfillCount = topBackfill;
        TotalCatalogedCount = totalCataloged;
        FailedCount = failed;
        CatalogedTitlesJson = JsonSerializer.Serialize(titles ?? []);
        Status = "Completed";
        ErrorMessage = null;
    }

    public void Fail(string error)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        Status = "Failed";
        ErrorMessage = string.IsNullOrWhiteSpace(error) ? "Error no especificado durante el proceso nocturno." : error.Trim();
    }
}
