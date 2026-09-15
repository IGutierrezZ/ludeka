using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa una publicación, vídeo o entrada descubierta en un canal social o feed público.
/// </summary>
public record DiscoveredSocialPostDto
{
    public string SourceUrl { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string AuthorOrChannel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsVideo { get; init; }
    public SocialPlatform Platform { get; init; }

    public DiscoveredSocialPostDto() { }

    public DiscoveredSocialPostDto(
        string sourceUrl,
        string title,
        string authorOrChannel,
        string description,
        string? thumbnailUrl,
        DateTimeOffset publishedAt,
        bool isVideo,
        SocialPlatform platform)
    {
        SourceUrl = sourceUrl;
        Title = title;
        AuthorOrChannel = authorOrChannel;
        Description = description;
        ThumbnailUrl = thumbnailUrl;
        PublishedAt = publishedAt;
        IsVideo = isVideo;
        Platform = platform;
    }
}

/// <summary>
/// Resumen de recolección de una cuenta o canal monitorizado concreto.
/// </summary>
public record SocialCollectorAccountSummaryDto
{
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public SocialPlatform Platform { get; init; }
    public int DiscoveredCount { get; init; }
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public string? ErrorMessage { get; init; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}

/// <summary>
/// Resultado consolidado de un ciclo de ejecución del recolector de canales sociales.
/// </summary>
public record SocialCollectorRunResultDto
{
    public int AccountsScanned { get; init; }
    public int ItemsDiscovered { get; init; }
    public int ItemsImported { get; init; }
    public int ItemsSkippedDuplicates { get; init; }
    public int ErrorsCount { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<SocialCollectorAccountSummaryDto> AccountSummaries { get; init; } = [];

    public string SummaryText =>
        $"{AccountsScanned} canales sondeados: {ItemsImported} importados, {ItemsSkippedDuplicates} omitidos (duplicados), {ErrorsCount} errores en {Duration.TotalSeconds:F1}s.";
}
