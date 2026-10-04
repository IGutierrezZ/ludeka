using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.DTOs;

public record GameDetailDto(
    Guid Id,
    int BggId,
    string Slug,
    string SpanishTitle,
    string OriginalTitle,
    string Designer,
    string Publisher,
    int YearPublished,
    string? CoverImageUrl,
    string? ThumbnailUrl,
    string? Description,
    double BggRating,
    int? BggRank,
    double LudistRating,
    string IdealPlayerCountText,
    ConfrontationType Confrontation,
    GameStyle Style,
    bool IsOfficialSolo,
    AgeRating Age,
    LanguageDependence Language,
    TableFootprint Footprint,
    GameDuration Duration,
    IReadOnlyList<ScalabilityEntry> Scalability,
    IReadOnlyList<SleeveItem> Sleeves,
    IReadOnlyList<GamePurchaseLink>? PurchaseLinks = null,
    GameType Type = GameType.BaseGame,
    Guid? BaseGameId = null,
    ParentGameSummaryDto? BaseGame = null,
    ExpansionNecessity? ExpansionNecessity = null,
    IReadOnlyList<ExpansionImpactTag>? ImpactTags = null,
    string? WhatItBringsSummary = null,
    int? ExtraPlayerCount = null,
    int? ExtraDurationMinutes = null,
    string? BackCoverImageUrl = null,
    string? TableImageUrl = null,
    string? SpanishPublisher = null,
    IReadOnlyList<RegionalPublisherEntry>? RegionalPublishers = null,
    IReadOnlyList<LocalizedTitleEntry>? LocalizedTitles = null,
    string? Ean = null,
    IReadOnlyList<string>? AdditionalBarcodes = null
)
{
    public bool IsExpansion => Type == GameType.Expansion || Type == GameType.StandaloneExpansion;

    public string GetPublisherForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("ES", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(SpanishPublisher))
                return SpanishPublisher;
        }

        if (!string.IsNullOrWhiteSpace(countryCode) && RegionalPublishers != null && RegionalPublishers.Count > 0)
        {
            var match = RegionalPublishers.FirstOrDefault(r => string.Equals(r.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.PublisherName))
                return match.PublisherName;
        }

        return Publisher;
    }

    public string GetTitleForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("ES", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(SpanishTitle))
                return SpanishTitle;
        }

        if (!string.IsNullOrWhiteSpace(countryCode) && LocalizedTitles != null && LocalizedTitles.Count > 0)
        {
            var match = LocalizedTitles.FirstOrDefault(l => string.Equals(l.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.Title))
                return match.Title;
        }

        return !string.IsNullOrWhiteSpace(SpanishTitle) ? SpanishTitle : OriginalTitle;
    }

    public static GameDetailDto FromEntity(Game g) => FromEntity(g, null);

    public static GameDetailDto FromEntity(Game g, ParentGameSummaryDto? parentGame) => new(
        g.Id,
        g.BggId,
        g.Slug,
        g.SpanishTitle,
        g.OriginalTitle,
        g.Designer,
        g.Publisher,
        g.YearPublished,
        g.CoverImageUrl,
        g.ThumbnailUrl,
        g.Description,
        g.BggRating,
        g.BggRank,
        g.LudistRating,
        g.IdealPlayerCountText,
        g.Confrontation,
        g.Style,
        g.IsOfficialSolo,
        g.Age,
        g.Language,
        g.Footprint,
        g.Duration,
        (g.Scalability ?? new List<ScalabilityEntry>()).AsReadOnly(),
        (g.Sleeves ?? new List<SleeveItem>()).AsReadOnly(),
        (g.PurchaseLinks ?? new List<GamePurchaseLink>()).AsReadOnly(),
        g.Type,
        g.BaseGameId,
        parentGame,
        g.ExpansionNecessity,
        (g.ImpactTags ?? new List<ExpansionImpactTag>()).AsReadOnly(),
        g.WhatItBringsSummary,
        g.ExtraPlayerCount,
        g.ExtraDurationMinutes,
        g.BackCoverImageUrl,
        g.TableImageUrl,
        g.SpanishPublisher,
        (g.RegionalPublishers ?? new List<RegionalPublisherEntry>()).AsReadOnly(),
        (g.LocalizedTitles ?? new List<LocalizedTitleEntry>()).AsReadOnly(),
        g.Ean,
        (g.AdditionalBarcodes ?? new List<string>()).AsReadOnly()
    );
}
