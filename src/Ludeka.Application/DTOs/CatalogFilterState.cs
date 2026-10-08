using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Primitives;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa un chip visual de filtro activo que puede ser eliminado individualmente.
/// </summary>
public record ActiveFilterChip(string Category, string Key, string Value, string Label);

/// <summary>
/// Modelo de estado unificado para el catálogo híbrido (INC-116).
/// Centraliza la sincronización bidireccional con la URL, la frase conversacional reactiva mad-lib,
/// los 8 grupos taxonómicos del panel drawer y los chips activos.
/// </summary>
public class CatalogFilterState
{
    public string SearchTerm { get; set; } = string.Empty;
    public HashSet<int> PlayerCounts { get; set; } = [];
    public HashSet<GameComplexity> Complexities { get; set; } = [];
    public HashSet<GameStyle> Styles { get; set; } = [];
    public HashSet<int> MaxDurations { get; set; } = [];
    public HashSet<GameType> Types { get; set; } = [];
    public HashSet<TableFootprint> Footprints { get; set; } = [];
    public HashSet<ConfrontationType> Confrontations { get; set; } = [];
    public HashSet<LanguageDependence> Languages { get; set; } = [];
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public GameSortOrder SortBy { get; set; } = GameSortOrder.Rank;
    public int CurrentPage { get; set; } = 1;
    public string ViewMode { get; set; } = "grid";
    public string? ActivePreset { get; set; }

    /// <summary>
    /// Total de filtros activos aplicados (excluyendo término de búsqueda y paginación).
    /// </summary>
    public int ActiveFilterCount =>
        PlayerCounts.Count +
        Complexities.Count +
        Styles.Count +
        MaxDurations.Count +
        Types.Count +
        Footprints.Count +
        Confrontations.Count +
        Languages.Count +
        (MinYear.HasValue ? 1 : 0) +
        (MaxYear.HasValue ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(ActivePreset) && !string.Equals(ActivePreset, "todos", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

    /// <summary>
    /// Indica si hay algún criterio o término de búsqueda activo.
    /// </summary>
    public bool HasActiveFilters => ActiveFilterCount > 0 || !string.IsNullOrWhiteSpace(SearchTerm);

    /// <summary>
    /// Texto reactivo para el hueco de comensales en la frase conversacional.
    /// </summary>
    public string PlayersSentenceLabel
    {
        get
        {
            if (PlayerCounts.Count == 0) return "cualquier número";

            var ordered = PlayerCounts.OrderBy(x => x).Select(x => x >= 7 ? "7+" : x.ToString()).ToList();
            if (ordered.Count == 1)
            {
                var val = ordered[0];
                return val == "1" ? "1 jugador" : $"{val} jugadores";
            }

            return JoinWithOr(ordered) + " jugadores";
        }
    }

    /// <summary>
    /// Texto reactivo para el hueco de dureza / complejidad en la frase conversacional.
    /// </summary>
    public string WeightSentenceLabel
    {
        get
        {
            if (Complexities.Count == 0) return "cualquiera";

            var labels = Complexities.OrderBy(x => (int)x).Select(c => c switch
            {
                GameComplexity.Light => "ligera",
                GameComplexity.Medium => "media",
                GameComplexity.Heavy => "dura",
                _ => c.ToString().ToLowerInvariant()
            }).ToList();

            return JoinWithOr(labels);
        }
    }

    /// <summary>
    /// Texto reactivo para el hueco de categoría / estilo en la frase conversacional.
    /// </summary>
    public string StyleSentenceLabel
    {
        get
        {
            if (Styles.Count == 0) return "cualquiera";

            var labels = Styles.OrderBy(x => (int)x).Select(s => s switch
            {
                GameStyle.Eurogame => "euro",
                GameStyle.Ameritrash => "temático",
                GameStyle.PartyGame => "party",
                GameStyle.FillerAbstract => "filler",
                GameStyle.NarrativeCampaign => "narrativo",
                _ => s.ToString().ToLowerInvariant()
            }).ToList();

            return JoinWithOr(labels);
        }
    }

    /// <summary>
    /// Texto reactivo para el hueco de duración máxima en la frase conversacional.
    /// </summary>
    public string TimeSentenceLabel
    {
        get
        {
            if (MaxDurations.Count == 0) return "lo que haga falta";

            var max = MaxDurations.Max();
            return $"hasta {max} min";
        }
    }

    public bool TogglePlayerCount(int count)
    {
        CurrentPage = 1;
        if (!PlayerCounts.Add(count))
        {
            PlayerCounts.Remove(count);
            return false;
        }
        return true;
    }

    public bool ToggleComplexity(GameComplexity complexity)
    {
        CurrentPage = 1;
        if (!Complexities.Add(complexity))
        {
            Complexities.Remove(complexity);
            return false;
        }
        return true;
    }

    public bool ToggleStyle(GameStyle style)
    {
        CurrentPage = 1;
        if (!Styles.Add(style))
        {
            Styles.Remove(style);
            return false;
        }
        return true;
    }

    public bool ToggleMaxDuration(int minutes)
    {
        CurrentPage = 1;
        if (!MaxDurations.Add(minutes))
        {
            MaxDurations.Remove(minutes);
            return false;
        }
        return true;
    }

    public bool ToggleType(GameType type)
    {
        CurrentPage = 1;
        if (!Types.Add(type))
        {
            Types.Remove(type);
            return false;
        }
        return true;
    }

    public bool ToggleFootprint(TableFootprint footprint)
    {
        CurrentPage = 1;
        if (!Footprints.Add(footprint))
        {
            Footprints.Remove(footprint);
            return false;
        }
        return true;
    }

    public bool ToggleConfrontation(ConfrontationType confrontation)
    {
        CurrentPage = 1;
        if (!Confrontations.Add(confrontation))
        {
            Confrontations.Remove(confrontation);
            return false;
        }
        return true;
    }

    public bool ToggleLanguage(LanguageDependence language)
    {
        CurrentPage = 1;
        if (!Languages.Add(language))
        {
            Languages.Remove(language);
            return false;
        }
        return true;
    }

    public void SetYearRange(int? minYear, int? maxYear)
    {
        MinYear = minYear;
        MaxYear = maxYear;
        CurrentPage = 1;
    }

    public void SetSortOrder(GameSortOrder order)
    {
        SortBy = order;
        CurrentPage = 1;
    }

    public void Reset()
    {
        SearchTerm = string.Empty;
        PlayerCounts.Clear();
        Complexities.Clear();
        Styles.Clear();
        MaxDurations.Clear();
        Types.Clear();
        Footprints.Clear();
        Confrontations.Clear();
        Languages.Clear();
        MinYear = null;
        MaxYear = null;
        SortBy = GameSortOrder.Rank;
        CurrentPage = 1;
        ActivePreset = null;
    }

    public IReadOnlyList<ActiveFilterChip> GetActiveChips()
    {
        var chips = new List<ActiveFilterChip>();

        foreach (var p in PlayerCounts.OrderBy(x => x))
        {
            var label = p >= 7 ? "7+ jugadores" : $"{p} jugadores";
            chips.Add(new ActiveFilterChip("players", "players", p.ToString(), label));
        }

        foreach (var c in Complexities.OrderBy(x => (int)x))
        {
            var label = c switch
            {
                GameComplexity.Light => "Dureza ligera",
                GameComplexity.Medium => "Dureza media",
                GameComplexity.Heavy => "Dureza alta",
                _ => c.ToString()
            };
            chips.Add(new ActiveFilterChip("weight", "dureza", c.ToString(), label));
        }

        foreach (var s in Styles.OrderBy(x => (int)x))
        {
            var label = s switch
            {
                GameStyle.Eurogame => "Eurogame",
                GameStyle.Ameritrash => "Temático",
                GameStyle.PartyGame => "Party",
                GameStyle.FillerAbstract => "Filler",
                GameStyle.NarrativeCampaign => "Narrativo",
                _ => s.ToString()
            };
            chips.Add(new ActiveFilterChip("style", "estilos", s.ToString(), label));
        }

        foreach (var d in MaxDurations.OrderBy(x => x))
        {
            chips.Add(new ActiveFilterChip("time", "duracion", d.ToString(), $"Hasta {d} min"));
        }

        foreach (var t in Types.OrderBy(x => (int)x))
        {
            var label = t switch
            {
                GameType.BaseGame => "Juego base",
                GameType.Expansion => "Expansión",
                GameType.StandaloneExpansion => "Autojugable",
                _ => t.ToString()
            };
            chips.Add(new ActiveFilterChip("type", "tipos", t.ToString(), label));
        }

        foreach (var fp in Footprints.OrderBy(x => (int)x))
        {
            var label = fp switch
            {
                TableFootprint.SmallTable => "Mesa pequeña",
                TableFootprint.StandardTable => "Mesa estándar",
                TableFootprint.TableMonster => "Mesa grande",
                _ => fp.ToString()
            };
            chips.Add(new ActiveFilterChip("footprint", "mesa", fp.ToString(), label));
        }

        foreach (var conf in Confrontations.OrderBy(x => (int)x))
        {
            var label = conf switch
            {
                ConfrontationType.Competitive => "Competitivo",
                ConfrontationType.Cooperative => "Cooperativo",
                ConfrontationType.HiddenRolesOrTeams => "Roles ocultos",
                ConfrontationType.SemiCooperative => "Semicooperativo",
                _ => conf.ToString()
            };
            chips.Add(new ActiveFilterChip("confrontation", "confrontacion", conf.ToString(), label));
        }

        foreach (var lang in Languages.OrderBy(x => (int)x))
        {
            var label = lang switch
            {
                LanguageDependence.None => "Sin texto (Nula)",
                LanguageDependence.Low => "Poca lectura (Baja)",
                LanguageDependence.High => "Dependiente (Alta)",
                _ => lang.ToString()
            };
            chips.Add(new ActiveFilterChip("language", "idiomas", lang.ToString(), label));
        }

        if (MinYear.HasValue && MaxYear.HasValue && MinYear == MaxYear)
        {
            chips.Add(new ActiveFilterChip("year", "ano", MinYear.Value.ToString(), $"Año {MinYear.Value}"));
        }
        else
        {
            if (MinYear.HasValue)
            {
                chips.Add(new ActiveFilterChip("year", "ano_desde", MinYear.Value.ToString(), $"Desde {MinYear.Value}"));
            }
            if (MaxYear.HasValue)
            {
                chips.Add(new ActiveFilterChip("year", "ano_hasta", MaxYear.Value.ToString(), $"Hasta {MaxYear.Value}"));
            }
        }

        return chips;
    }

    public void RemoveChip(ActiveFilterChip chip)
    {
        CurrentPage = 1;
        switch (chip.Category)
        {
            case "players":
                if (int.TryParse(chip.Value, out var p)) PlayerCounts.Remove(p);
                break;
            case "weight":
                if (Enum.TryParse<GameComplexity>(chip.Value, ignoreCase: true, out var c)) Complexities.Remove(c);
                break;
            case "style":
                if (Enum.TryParse<GameStyle>(chip.Value, ignoreCase: true, out var s)) Styles.Remove(s);
                break;
            case "time":
                if (int.TryParse(chip.Value, out var d)) MaxDurations.Remove(d);
                break;
            case "type":
                if (Enum.TryParse<GameType>(chip.Value, ignoreCase: true, out var t)) Types.Remove(t);
                break;
            case "footprint":
                if (Enum.TryParse<TableFootprint>(chip.Value, ignoreCase: true, out var fp)) Footprints.Remove(fp);
                break;
            case "confrontation":
                if (Enum.TryParse<ConfrontationType>(chip.Value, ignoreCase: true, out var conf)) Confrontations.Remove(conf);
                break;
            case "language":
                if (Enum.TryParse<LanguageDependence>(chip.Value, ignoreCase: true, out var lang)) Languages.Remove(lang);
                break;
            case "year":
                if (chip.Key == "ano_desde" || chip.Key == "ano") MinYear = null;
                if (chip.Key == "ano_hasta" || chip.Key == "ano") MaxYear = null;
                break;
        }
    }

    public Dictionary<string, string?> ToQueryDictionary()
    {
        var q = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            q["q"] = SearchTerm;
        }

        if (CurrentPage > 1)
        {
            q["page"] = CurrentPage.ToString();
        }

        if (string.Equals(ViewMode, "list", StringComparison.OrdinalIgnoreCase))
        {
            q["view"] = "list";
        }

        if (PlayerCounts.Count > 0)
        {
            q["jugadores"] = string.Join(",", PlayerCounts.OrderBy(x => x));
        }

        if (Complexities.Count > 0)
        {
            q["dureza"] = string.Join(",", Complexities.OrderBy(x => (int)x).Select(c => c.ToString().ToLowerInvariant()));
        }

        if (Styles.Count > 0)
        {
            q["estilos"] = string.Join(",", Styles.OrderBy(x => (int)x).Select(s => s.ToString().ToLowerInvariant()));
        }

        if (MaxDurations.Count > 0)
        {
            q["duracion"] = string.Join(",", MaxDurations.OrderBy(x => x));
        }

        if (Types.Count > 0)
        {
            q["tipos"] = string.Join(",", Types.OrderBy(x => (int)x).Select(t => t.ToString().ToLowerInvariant()));
        }

        if (Footprints.Count > 0)
        {
            q["mesa"] = string.Join(",", Footprints.OrderBy(x => (int)x).Select(f => f switch
            {
                TableFootprint.SmallTable => "pequena",
                TableFootprint.TableMonster => "monstruo",
                _ => "estandar"
            }));
        }

        if (Confrontations.Count > 0)
        {
            q["confrontacion"] = string.Join(",", Confrontations.OrderBy(x => (int)x).Select(c => c.ToString().ToLowerInvariant()));
        }

        if (Languages.Count > 0)
        {
            q["idiomas"] = string.Join(",", Languages.OrderBy(x => (int)x).Select(l => l.ToString().ToLowerInvariant()));
        }

        if (MinYear.HasValue)
        {
            q["ano_desde"] = MinYear.Value.ToString();
        }

        if (MaxYear.HasValue)
        {
            q["ano_hasta"] = MaxYear.Value.ToString();
        }

        if (SortBy != GameSortOrder.Rank)
        {
            q["orden"] = SortBy switch
            {
                GameSortOrder.RatingDesc => "rating",
                GameSortOrder.ComplexityAsc => "dureza-asc",
                GameSortOrder.ComplexityDesc => "dureza-desc",
                GameSortOrder.DurationAsc => "duracion-asc",
                GameSortOrder.DurationDesc => "duracion-desc",
                GameSortOrder.YearDesc => "novedad",
                GameSortOrder.TitleAsc => "alfabetico",
                _ => null
            };
        }

        if (!string.IsNullOrWhiteSpace(ActivePreset) && !string.Equals(ActivePreset, "todos", StringComparison.OrdinalIgnoreCase))
        {
            q["preset"] = ActivePreset.ToLowerInvariant();
        }

        return q;
    }

    public void FromQueryDictionary(IDictionary<string, StringValues> query)
    {
        if (query.TryGetValue("q", out var qVal) && !string.IsNullOrWhiteSpace(qVal))
        {
            SearchTerm = qVal.ToString();
        }
        else if (query.TryGetValue("search", out var searchVal) && !string.IsNullOrWhiteSpace(searchVal))
        {
            SearchTerm = searchVal.ToString();
        }
        else
        {
            SearchTerm = string.Empty;
        }

        if (query.TryGetValue("page", out var pVal) && int.TryParse(pVal, out var pageNum) && pageNum >= 1)
        {
            CurrentPage = pageNum;
        }
        else
        {
            CurrentPage = 1;
        }

        if (query.TryGetValue("view", out var vVal) && string.Equals(vVal.ToString(), "list", StringComparison.OrdinalIgnoreCase))
        {
            ViewMode = "list";
        }
        else
        {
            ViewMode = "grid";
        }

        PlayerCounts.Clear();
        var jTokens = query.TryGetValue("jugadores", out var jVal) ? jVal.ToString()
            : (query.TryGetValue("players", out var pVal2) ? pVal2.ToString() : null);
        if (!string.IsNullOrWhiteSpace(jTokens))
        {
            foreach (var item in jTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(item, out var num) && num >= 1)
                {
                    PlayerCounts.Add(num);
                }
            }
        }

        Complexities.Clear();
        var durezaTokens = query.TryGetValue("dureza", out var durVal) ? durVal.ToString()
            : (query.TryGetValue("weight", out var wVal) ? wVal.ToString() : null);
        if (!string.IsNullOrWhiteSpace(durezaTokens))
        {
            foreach (var item in durezaTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<GameComplexity>(item, ignoreCase: true, out var c))
                {
                    Complexities.Add(c);
                }
                else if (string.Equals(item, "ligero", StringComparison.OrdinalIgnoreCase))
                {
                    Complexities.Add(GameComplexity.Light);
                }
                else if (string.Equals(item, "medio", StringComparison.OrdinalIgnoreCase))
                {
                    Complexities.Add(GameComplexity.Medium);
                }
                else if (string.Equals(item, "duro", StringComparison.OrdinalIgnoreCase) || string.Equals(item, "experto", StringComparison.OrdinalIgnoreCase))
                {
                    Complexities.Add(GameComplexity.Heavy);
                }
            }
        }

        Styles.Clear();
        var estilosTokens = query.TryGetValue("estilos", out var estVal) ? estVal.ToString()
            : (query.TryGetValue("style", out var stVal) ? stVal.ToString() : null);
        if (!string.IsNullOrWhiteSpace(estilosTokens))
        {
            foreach (var item in estilosTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<GameStyle>(item, ignoreCase: true, out var s))
                {
                    Styles.Add(s);
                }
            }
        }

        MaxDurations.Clear();
        var dTokens = query.TryGetValue("duracion", out var dVal) ? dVal.ToString()
            : (query.TryGetValue("duration", out var durVal2) ? durVal2.ToString() : null);
        if (!string.IsNullOrWhiteSpace(dTokens))
        {
            foreach (var item in dTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(item, out var num) && num > 0)
                {
                    MaxDurations.Add(num);
                }
            }
        }

        Types.Clear();
        var tiposTokens = query.TryGetValue("tipos", out var tipVal) ? tipVal.ToString()
            : (query.TryGetValue("tipo", out var tSingleVal) ? tSingleVal.ToString() : null);
        if (!string.IsNullOrWhiteSpace(tiposTokens))
        {
            foreach (var item in tiposTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<GameType>(item, ignoreCase: true, out var t))
                {
                    Types.Add(t);
                }
                else if (string.Equals(item, "base", StringComparison.OrdinalIgnoreCase))
                {
                    Types.Add(GameType.BaseGame);
                }
                else if (string.Equals(item, "expansion", StringComparison.OrdinalIgnoreCase))
                {
                    Types.Add(GameType.Expansion);
                }
            }
        }

        Footprints.Clear();
        var mesaTokens = query.TryGetValue("mesa", out var mesaVal) ? mesaVal.ToString()
            : (query.TryGetValue("footprint", out var fpVal) ? fpVal.ToString() : null);
        if (!string.IsNullOrWhiteSpace(mesaTokens))
        {
            foreach (var item in mesaTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var fp = item.ToLowerInvariant() switch
                {
                    "pequena" or "small" => TableFootprint.SmallTable,
                    "estandar" or "standard" => TableFootprint.StandardTable,
                    "monstruo" or "monster" or "grande" => TableFootprint.TableMonster,
                    _ => (TableFootprint?)null
                };
                if (fp.HasValue) Footprints.Add(fp.Value);
            }
        }

        Confrontations.Clear();
        var confTokens = query.TryGetValue("confrontacion", out var cVal) ? cVal.ToString()
            : (query.TryGetValue("confrontation", out var cfVal) ? cfVal.ToString() : null);
        if (!string.IsNullOrWhiteSpace(confTokens))
        {
            foreach (var item in confTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ConfrontationType>(item, ignoreCase: true, out var c))
                {
                    Confrontations.Add(c);
                }
            }
        }

        Languages.Clear();
        var langTokens = query.TryGetValue("idiomas", out var lVal) ? lVal.ToString()
            : (query.TryGetValue("idioma", out var lVal2) ? lVal2.ToString() : null);
        if (!string.IsNullOrWhiteSpace(langTokens))
        {
            foreach (var item in langTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<LanguageDependence>(item, ignoreCase: true, out var lang))
                {
                    Languages.Add(lang);
                }
                else if (string.Equals(item, "nula", StringComparison.OrdinalIgnoreCase) || string.Equals(item, "none", StringComparison.OrdinalIgnoreCase))
                {
                    Languages.Add(LanguageDependence.None);
                }
                else if (string.Equals(item, "baja", StringComparison.OrdinalIgnoreCase) || string.Equals(item, "low", StringComparison.OrdinalIgnoreCase))
                {
                    Languages.Add(LanguageDependence.Low);
                }
                else if (string.Equals(item, "alta", StringComparison.OrdinalIgnoreCase) || string.Equals(item, "high", StringComparison.OrdinalIgnoreCase))
                {
                    Languages.Add(LanguageDependence.High);
                }
            }
        }

        MinYear = null;
        if (query.TryGetValue("ano_desde", out var minYearVal) && int.TryParse(minYearVal, out var yMin))
        {
            MinYear = yMin;
        }

        MaxYear = null;
        if (query.TryGetValue("ano_hasta", out var maxYearVal) && int.TryParse(maxYearVal, out var yMax))
        {
            MaxYear = yMax;
        }

        if (query.TryGetValue("ano", out var exactYearVal) && int.TryParse(exactYearVal, out var yExact))
        {
            MinYear = yExact;
            MaxYear = yExact;
        }

        if (query.TryGetValue("orden", out var oVal) && !string.IsNullOrWhiteSpace(oVal))
        {
            var oNormalized = oVal.ToString().ToLowerInvariant();
            SortBy = oNormalized switch
            {
                "rating" => GameSortOrder.RatingDesc,
                "dureza-asc" or "ligero" => GameSortOrder.ComplexityAsc,
                "dureza-desc" or "duro" => GameSortOrder.ComplexityDesc,
                "duracion-asc" or "rapido" => GameSortOrder.DurationAsc,
                "duracion-desc" or "largo" => GameSortOrder.DurationDesc,
                "novedad" or "ano" or "year" => GameSortOrder.YearDesc,
                "alfabetico" or "titulo" or "az" => GameSortOrder.TitleAsc,
                _ => GameSortOrder.Rank
            };
        }
        else
        {
            SortBy = GameSortOrder.Rank;
        }

        if (query.TryGetValue("preset", out var presetVal))
        {
            ActivePreset = presetVal.ToString();
        }
        else if (query.ContainsKey("parejas"))
        {
            ActivePreset = "parejas";
        }
        else if (query.ContainsKey("familiar"))
        {
            ActivePreset = "familiar";
        }
        else if (query.ContainsKey("expansiones"))
        {
            ActivePreset = "expansiones";
        }
        else if (query.ContainsKey("solo"))
        {
            ActivePreset = "solo";
        }
        else if (query.ContainsKey("rapidas"))
        {
            ActivePreset = "rapidas";
        }
        else
        {
            ActivePreset = null;
        }
    }

    public GameFilterCriteria ToCriteria(int pageSize = 24)
    {
        GameType? presetTypeFilter = ActivePreset?.ToLowerInvariant() switch
        {
            "base" => GameType.BaseGame,
            "expansiones" => GameType.Expansion,
            _ => null
        };

        var typesToFilter = Types.Count > 0
            ? Types.ToList()
            : (presetTypeFilter.HasValue ? new List<GameType> { presetTypeFilter.Value } : null);

        return new GameFilterCriteria(
            SearchTerm: SearchTerm,
            PlayerCounts: PlayerCounts.Count > 0 ? PlayerCounts.ToList() : null,
            PlayerCountsMatchAll: true,
            Footprints: Footprints.Count > 0 ? Footprints.ToList() : null,
            Complexities: Complexities.Count > 0 ? Complexities.ToList() : null,
            Styles: Styles.Count > 0 ? Styles.ToList() : null,
            Confrontations: Confrontations.Count > 0 ? Confrontations.ToList() : null,
            Types: typesToFilter,
            Languages: Languages.Count > 0 ? Languages.ToList() : null,
            MinYear: MinYear,
            MaxYear: MaxYear,
            MaxDurations: MaxDurations.Count > 0 ? MaxDurations.ToList() : null,
            MaxDurationMinutes: MaxDurations.Count > 0 ? MaxDurations.Max() : (ActivePreset == "rapidas" ? 45 : null),
            EspecialParejas: ActivePreset == "parejas",
            MesaFamiliar: ActivePreset == "familiar",
            SoloTop: ActivePreset == "solo",
            TypeFilter: Types.Count == 0 ? presetTypeFilter : null,
            SortBy: SortBy
        );
    }

    private static string JoinWithOr(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return string.Empty;
        if (items.Count == 1) return items[0];
        return string.Join(", ", items.Take(items.Count - 1)) + " o " + items.Last();
    }
}
