using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record GameFilterCriteria(
    string? SearchTerm = null,
    int? PlayerCount = null,
    IReadOnlyList<int>? PlayerCounts = null,
    bool PlayerCountsMatchAll = false,
    GameStyle? Style = null,
    IReadOnlyList<GameStyle>? Styles = null,
    ConfrontationType? Confrontation = null,
    IReadOnlyList<ConfrontationType>? Confrontations = null,
    int? MaxDurationMinutes = null,
    IReadOnlyList<int>? MaxDurations = null,
    bool EspecialParejas = false,
    bool MesaFamiliar = false,
    bool SoloTop = false,
    GameType? TypeFilter = null,
    IReadOnlyList<GameType>? Types = null,
    TableFootprint? Footprint = null,
    IReadOnlyList<TableFootprint>? Footprints = null,
    IReadOnlyList<GameComplexity>? Complexities = null,
    GameSortOrder SortBy = GameSortOrder.Rank,
    LanguageDependence? Language = null,
    IReadOnlyList<LanguageDependence>? Languages = null,
    int? MinYear = null,
    int? MaxYear = null
);
