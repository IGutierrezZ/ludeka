using System;
using System.Collections.Generic;
using System.Text.Json;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa un elemento en la tabla intermedia de aislamiento y staging para la ingesta masiva de BGG.
/// Desacopla las fases de obtención de detalles, imágenes R2, síntesis con IA y promoción final a Game.
/// </summary>
public class BggCatalogStagingItem
{
    public int BggId { get; private set; }
    public string OriginalTitle { get; private set; } = string.Empty;
    public string? SpanishTitle { get; private set; }
    public int? YearPublished { get; private set; }
    public int? BggRank { get; private set; }
    public int UsersRated { get; private set; }
    public double BayesAverage { get; private set; }
    public double AverageRating { get; private set; }

    // Estados independientes del pipeline
    public StagingFetchStatus FetchStatus { get; private set; } = StagingFetchStatus.Pending;
    public StagingImagesStatus ImagesStatus { get; private set; } = StagingImagesStatus.Pending;
    public StagingAiStatus AiStatus { get; private set; } = StagingAiStatus.Pending;
    public StagingPromotionStatus PromotionStatus { get; private set; } = StagingPromotionStatus.Pending;

    // Caché de metadatos detallados de Thing XML
    public string? RawThingXml { get; private set; }
    public string? Designer { get; private set; }
    public string? Publisher { get; private set; }
    public string? SpanishPublisher { get; private set; }
    public string? Description { get; private set; }
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    public int PlayingTimeMinutes { get; private set; }
    public int MinPlayTimeMinutes { get; private set; }
    public int MaxPlayTimeMinutes { get; private set; }
    public int MinAge { get; private set; }
    public TableFootprint InferredFootprint { get; private set; } = TableFootprint.StandardTable;

    // Persistencia serializada JSON para tipos complejos en staging
    public string? ScalabilityJson { get; private set; }
    public string? SleevesJson { get; private set; }
    public string? RegionalPublishersJson { get; private set; }

    // URLs de medios en Cloudflare R2
    public string? CoverImageUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? BackCoverImageUrl { get; private set; }
    public string? TableImageUrl { get; private set; }

    // Síntesis editorial de Gemini Flash (JSON estructurado)
    public string? AiSummaryJson { get; private set; }

    // Control y Auditoría
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    private BggCatalogStagingItem() { }

    public BggCatalogStagingItem(
        int bggId,
        string originalTitle,
        int? yearPublished = null,
        int? bggRank = null,
        int usersRated = 0,
        double bayesAverage = 0.0,
        double averageRating = 0.0,
        string? spanishTitle = null)
    {
        if (bggId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bggId), "El BggId debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(originalTitle))
            throw new ArgumentException("El título original no puede estar vacío.", nameof(originalTitle));

        BggId = bggId;
        OriginalTitle = originalTitle.Trim();
        SpanishTitle = string.IsNullOrWhiteSpace(spanishTitle) ? null : spanishTitle.Trim();
        YearPublished = yearPublished;
        BggRank = bggRank;
        UsersRated = Math.Max(0, usersRated);
        BayesAverage = Math.Clamp(bayesAverage, 0.0, 10.0);
        AverageRating = Math.Clamp(averageRating, 0.0, 10.0);

        FetchStatus = StagingFetchStatus.Pending;
        ImagesStatus = StagingImagesStatus.Pending;
        AiStatus = StagingAiStatus.Pending;
        PromotionStatus = StagingPromotionStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateRankMetrics(int? bggRank, int usersRated, double bayesAverage, double averageRating)
    {
        BggRank = bggRank;
        UsersRated = Math.Max(0, usersRated);
        BayesAverage = Math.Clamp(bayesAverage, 0.0, 10.0);
        AverageRating = Math.Clamp(averageRating, 0.0, 10.0);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFetchInProgress()
    {
        FetchStatus = StagingFetchStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFetched(
        string? rawXml,
        string? spanishTitle,
        string? designer,
        string? publisher,
        string? description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        double? bggRating = null,
        string? scalabilityJson = null,
        string? sleevesJson = null,
        int minPlayTimeMinutes = 0,
        int maxPlayTimeMinutes = 0,
        TableFootprint inferredFootprint = TableFootprint.StandardTable,
        string? spanishPublisher = null,
        string? regionalPublishersJson = null)
    {
        RawThingXml = rawXml;
        if (!string.IsNullOrWhiteSpace(spanishTitle)) SpanishTitle = spanishTitle.Trim();
        Designer = designer?.Trim();
        Publisher = publisher?.Trim();
        if (!string.IsNullOrWhiteSpace(spanishPublisher)) SpanishPublisher = spanishPublisher.Trim();
        Description = description?.Trim();
        MinPlayers = Math.Max(1, minPlayers);
        MaxPlayers = Math.Max(MinPlayers, maxPlayers);
        PlayingTimeMinutes = Math.Max(0, playingTimeMinutes);
        MinPlayTimeMinutes = minPlayTimeMinutes > 0 ? minPlayTimeMinutes : PlayingTimeMinutes;
        MaxPlayTimeMinutes = maxPlayTimeMinutes > 0 ? maxPlayTimeMinutes : (MinPlayTimeMinutes > 0 ? MinPlayTimeMinutes : PlayingTimeMinutes);
        MinAge = Math.Max(0, minAge);
        if (bggRating.HasValue && bggRating.Value > 0)
        {
            AverageRating = Math.Clamp(bggRating.Value, 0.0, 10.0);
        }

        ScalabilityJson = string.IsNullOrWhiteSpace(scalabilityJson) ? null : scalabilityJson.Trim();
        SleevesJson = string.IsNullOrWhiteSpace(sleevesJson) ? null : sleevesJson.Trim();
        RegionalPublishersJson = string.IsNullOrWhiteSpace(regionalPublishersJson) ? null : regionalPublishersJson.Trim();
        InferredFootprint = inferredFootprint;

        FetchStatus = StagingFetchStatus.Fetched;
        UpdatedAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFetched(
        string? rawXml,
        string? spanishTitle,
        string? designer,
        string? publisher,
        string? description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        double? bggRating,
        int minPlayTimeMinutes,
        int maxPlayTimeMinutes,
        TableFootprint inferredFootprint,
        IEnumerable<ScalabilityEntry>? scalability = null,
        IEnumerable<SleeveItem>? sleeves = null,
        string? spanishPublisher = null,
        IEnumerable<RegionalPublisherEntry>? regionalPublishers = null)
    {
        string? scalJson = scalability != null ? JsonSerializer.Serialize(scalability, JsonOptions) : null;
        string? slvJson = sleeves != null ? JsonSerializer.Serialize(sleeves, JsonOptions) : null;
        string? regJson = regionalPublishers != null ? JsonSerializer.Serialize(regionalPublishers, JsonOptions) : null;

        MarkFetched(
            rawXml,
            spanishTitle,
            designer,
            publisher,
            description,
            minPlayers,
            maxPlayers,
            playingTimeMinutes,
            minAge,
            bggRating,
            scalabilityJson: scalJson,
            sleevesJson: slvJson,
            minPlayTimeMinutes: minPlayTimeMinutes,
            maxPlayTimeMinutes: maxPlayTimeMinutes,
            inferredFootprint: inferredFootprint,
            spanishPublisher: spanishPublisher,
            regionalPublishersJson: regJson
        );
    }

    public void UpdateInferredFootprint(TableFootprint footprint)
    {
        InferredFootprint = footprint;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSpanishPublisher(string? spanishPublisher)
    {
        SpanishPublisher = string.IsNullOrWhiteSpace(spanishPublisher) ? null : spanishPublisher.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateRegionalPublishers(string? regionalPublishersJson)
    {
        RegionalPublishersJson = string.IsNullOrWhiteSpace(regionalPublishersJson) ? null : regionalPublishersJson.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<ScalabilityEntry> GetScalability()
    {
        if (string.IsNullOrWhiteSpace(ScalabilityJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ScalabilityEntry>>(ScalabilityJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<SleeveItem> GetSleeves()
    {
        if (string.IsNullOrWhiteSpace(SleevesJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<SleeveItem>>(SleevesJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<RegionalPublisherEntry> GetRegionalPublishers()
    {
        if (string.IsNullOrWhiteSpace(RegionalPublishersJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<RegionalPublisherEntry>>(RegionalPublishersJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void MarkImagesInProgress()
    {
        ImagesStatus = StagingImagesStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkImagesCompleted(
        string? coverImageUrl,
        string? thumbnailUrl,
        string? backCoverImageUrl = null,
        string? tableImageUrl = null)
    {
        CoverImageUrl = coverImageUrl?.Trim();
        ThumbnailUrl = thumbnailUrl?.Trim();
        BackCoverImageUrl = backCoverImageUrl?.Trim();
        TableImageUrl = tableImageUrl?.Trim();

        ImagesStatus = StagingImagesStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    public void MarkImagesSkipped()
    {
        ImagesStatus = StagingImagesStatus.Skipped;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAiInProgress()
    {
        AiStatus = StagingAiStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAiCompleted(string summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
            throw new ArgumentException("El JSON de síntesis no puede estar vacío.", nameof(summaryJson));

        AiSummaryJson = summaryJson.Trim();
        AiStatus = StagingAiStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    public void MarkAiQuotaExceeded()
    {
        AiStatus = StagingAiStatus.QuotaExceeded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAiSkipped()
    {
        AiStatus = StagingAiStatus.Skipped;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetAiQuotaToPending()
    {
        if (AiStatus == StagingAiStatus.QuotaExceeded)
        {
            AiStatus = StagingAiStatus.Pending;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkPromotionInProgress()
    {
        PromotionStatus = StagingPromotionStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPromoted()
    {
        PromotionStatus = StagingPromotionStatus.Promoted;
        ProcessedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ProcessedAt.Value;
        ErrorMessage = null;
    }

    public void MarkStageFailed(string stage, string error)
    {
        ErrorMessage = $"[{stage}] {error}".Trim();
        RetryCount++;
        UpdatedAt = DateTimeOffset.UtcNow;

        switch (stage.ToLowerInvariant())
        {
            case "fetch":
                FetchStatus = StagingFetchStatus.Failed;
                break;
            case "images":
                ImagesStatus = StagingImagesStatus.Failed;
                break;
            case "ai":
                AiStatus = StagingAiStatus.Failed;
                break;
            case "promotion":
                PromotionStatus = StagingPromotionStatus.Failed;
                break;
        }
    }
}
