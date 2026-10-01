namespace Ludeka.Application.DTOs;

/// <summary>
/// Resumen del proceso de retención y purga de entidades caducadas e imágenes asociadas.
/// </summary>
public sealed record DataRetentionResult(
    int PurgedGiveawaysCount,
    int PurgedEventsCount,
    int PurgedReleasesCount,
    int DeletedImagesCount,
    int FailedImagesCount)
{
    public int TotalPurgedEntities => PurgedGiveawaysCount + PurgedEventsCount + PurgedReleasesCount;
}
