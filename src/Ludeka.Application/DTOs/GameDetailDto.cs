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
    string? TableImageUrl = null
)
{
    public bool IsExpansion => Type == GameType.Expansion || Type == GameType.StandaloneExpansion;

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
        g.Scalability.AsReadOnly(),
        g.Sleeves.AsReadOnly(),
        g.PurchaseLinks.AsReadOnly(),
        g.Type,
        g.BaseGameId,
        parentGame,
        g.ExpansionNecessity,
        g.ImpactTags.AsReadOnly(),
        g.WhatItBringsSummary,
        g.ExtraPlayerCount,
        g.ExtraDurationMinutes,
        g.BackCoverImageUrl,
        g.TableImageUrl
    );
}
