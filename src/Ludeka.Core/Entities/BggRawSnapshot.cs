namespace Ludeka.Core.Entities;

/// <summary>
/// Snapshot inmutable/versionado del payload bruto devuelto por BGG XMLAPI2.
/// Tabla satélite desacoplada del catálogo caliente para preservar histórico e información íntegra sin penalizar el I/O.
/// </summary>
public class BggRawSnapshot
{
    public int BggId { get; private set; }
    public string RawJson { get; private set; } = string.Empty;
    public int ApiVersion { get; private set; } = 2;
    public DateTimeOffset FetchedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    // Constructor protegido para EF Core
    protected BggRawSnapshot() { }

    public BggRawSnapshot(int bggId, string rawJson, int apiVersion = 2, DateTimeOffset? fetchedAt = null)
    {
        if (bggId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bggId), "El BGG ID debe ser positivo.");

        BggId = bggId;
        RawJson = rawJson ?? string.Empty;
        ApiVersion = apiVersion;
        FetchedAtUtc = fetchedAt ?? DateTimeOffset.UtcNow;
    }

    public void UpdatePayload(string rawJson, int apiVersion = 2, DateTimeOffset? updatedAt = null)
    {
        RawJson = rawJson ?? string.Empty;
        ApiVersion = apiVersion;
        UpdatedAtUtc = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
