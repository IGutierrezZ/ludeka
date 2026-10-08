using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.DTOs;

public record GameSummaryDto(
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
    double BggRating,
    int? BggRank,
    double LudistRating,
    string IdealPlayerCountText,
    ConfrontationType Confrontation,
    GameStyle Style,
    bool IsOfficialSolo,
    bool IsAccessibleEarlier,
    int CommunityAge,
    int BoxAge,
    LanguageDependence Language,
    TableFootprint Footprint,
    int EstimatedPerPlayerMinutes,
    GameType Type = GameType.BaseGame,
    string? BaseGameTitle = null,
    string? SpanishPublisher = null,
    IReadOnlyList<RegionalPublisherEntry>? RegionalPublishers = null,
    IReadOnlyList<LocalizedTitleEntry>? LocalizedTitles = null,
    int? TrendingRank = null,
    double? BggWeight = null
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

    public static GameSummaryDto FromEntity(Game g) => FromEntity(g, null);

    public static GameSummaryDto FromEntity(Game g, string? baseGameTitle) => new(
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
        g.BggRating,
        g.BggRank,
        g.LudistRating,
        g.IdealPlayerCountText,
        g.Confrontation,
        g.Style,
        g.IsOfficialSolo,
        g.Age.IsAccessibleEarlier,
        g.Age.CommunityAge,
        g.Age.BoxAge,
        g.Language,
        g.Footprint,
        g.Duration.EstimatedPerPlayerMinutes,
        g.Type,
        baseGameTitle,
        g.SpanishPublisher,
        (g.RegionalPublishers ?? new List<RegionalPublisherEntry>()).AsReadOnly(),
        (g.LocalizedTitles ?? new List<LocalizedTitleEntry>()).AsReadOnly(),
        null,
        g.BggWeight
    );
}
