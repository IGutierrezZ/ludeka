using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record GameFilterCriteria(
    string? SearchTerm = null,
    int? PlayerCount = null,
    GameStyle? Style = null,
    ConfrontationType? Confrontation = null,
    int? MaxDurationMinutes = null,
    bool EspecialParejas = false,
    bool MesaFamiliar = false,
    bool SoloTop = false,
    GameType? TypeFilter = null,
    TableFootprint? Footprint = null
);
