using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Core.Entities;

public partial class Game
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int BggId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string OriginalTitle { get; private set; } = string.Empty;
    public string SpanishTitle { get; private set; } = string.Empty;
    public string Designer { get; private set; } = string.Empty;
    public string Publisher { get; private set; } = string.Empty;
    public int YearPublished { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? BackCoverImageUrl { get; private set; }
    public string? TableImageUrl { get; private set; }
    public string? Description { get; private set; }
    public double BggRating { get; private set; }
    public int? BggRank { get; private set; }
    public double LudistRating { get; private set; }
    public ConfrontationType Confrontation { get; private set; }
    public GameStyle Style { get; private set; }
    public bool IsOfficialSolo { get; private set; }
    public AgeRating Age { get; private set; } = null!;
    public LanguageDependence Language { get; private set; }
    public TableFootprint Footprint { get; private set; }
    public GameDuration Duration { get; private set; } = null!;
    public List<ScalabilityEntry> Scalability { get; private set; } = [];
    public List<SleeveItem> Sleeves { get; private set; } = [];
    public List<GamePurchaseLink> PurchaseLinks { get; private set; } = [];
    public string? SpanishPublisher { get; private set; }
    public List<RegionalPublisherEntry> RegionalPublishers { get; private set; } = [];
    public List<LocalizedTitleEntry> LocalizedTitles { get; private set; } = [];
    public string? Ean { get; private set; }
    public List<string> AdditionalBarcodes { get; private set; } = [];
    public string? Asin { get; private set; }

    // --- Soporte de Expansiones y Ecosistema (Incremento 8) ---
    public GameType Type { get; private set; } = GameType.BaseGame;
    public Guid? BaseGameId { get; private set; }
    public Game? BaseGame { get; private set; }
    public List<Game> Expansions { get; private set; } = [];
    public ExpansionNecessity? ExpansionNecessity { get; private set; }
    public List<ExpansionImpactTag> ImpactTags { get; private set; } = [];
    public string? WhatItBringsSummary { get; private set; }
    public int? ExtraPlayerCount { get; private set; }
    public int? ExtraDurationMinutes { get; private set; }

    // --- Síntesis Inteligente con IA (Incremento 13) ---
    public AiGameSummary? AiSummary { get; private set; }

    public bool IsExpansion => Type == GameType.Expansion || Type == GameType.StandaloneExpansion;

    // Constructor privado para EF Core
    private Game() { }

    public Game(
        int bggId,
        string originalTitle,
        string spanishTitle,
        string designer,
        string publisher,
        int yearPublished,
        string? coverImageUrl,
        string? thumbnailUrl,
        string? description,
        double bggRating,
        int? bggRank,
        double ludistRating,
        ConfrontationType confrontation,
        GameStyle style,
        bool isOfficialSolo,
        AgeRating age,
        LanguageDependence language,
        TableFootprint footprint,
        GameDuration duration,
        IEnumerable<ScalabilityEntry>? scalability = null,
        IEnumerable<SleeveItem>? sleeves = null,
        IEnumerable<GamePurchaseLink>? purchaseLinks = null,
        string? customSlug = null,
        GameType type = GameType.BaseGame,
        Guid? baseGameId = null,
        ExpansionNecessity? expansionNecessity = null,
        IEnumerable<ExpansionImpactTag>? impactTags = null,
        string? whatItBringsSummary = null,
        int? extraPlayerCount = null,
        int? extraDurationMinutes = null,
        string? backCoverImageUrl = null,
        string? tableImageUrl = null,
        string? spanishPublisher = null,
        IEnumerable<RegionalPublisherEntry>? regionalPublishers = null,
        IEnumerable<LocalizedTitleEntry>? localizedTitles = null,
        string? ean = null,
        IEnumerable<string>? additionalBarcodes = null,
        string? asin = null)
    {
        if (bggId <= 0) throw new ArgumentOutOfRangeException(nameof(bggId), "El BggId debe ser positivo.");
        if (string.IsNullOrWhiteSpace(originalTitle)) throw new ArgumentException("El título original no puede estar vacío.", nameof(originalTitle));

        BggId = bggId;
        OriginalTitle = originalTitle.Trim();
        SpanishTitle = string.IsNullOrWhiteSpace(spanishTitle) ? OriginalTitle : spanishTitle.Trim();
        Designer = designer?.Trim() ?? string.Empty;
        Publisher = publisher?.Trim() ?? string.Empty;
        YearPublished = yearPublished;
        CoverImageUrl = coverImageUrl?.Trim();
        ThumbnailUrl = thumbnailUrl?.Trim();
        BackCoverImageUrl = backCoverImageUrl?.Trim();
        TableImageUrl = tableImageUrl?.Trim();
        Description = description?.Trim();
        BggRating = Math.Clamp(bggRating, 0.0, 10.0);
        BggRank = bggRank;
        LudistRating = Math.Clamp(ludistRating, 0.0, 10.0);
        Confrontation = confrontation;
        Style = style;
        IsOfficialSolo = isOfficialSolo;
        Age = age ?? throw new ArgumentNullException(nameof(age));
        Language = language;
        Footprint = footprint;
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));

        if (scalability != null) Scalability.AddRange(scalability);
        if (sleeves != null) Sleeves.AddRange(sleeves);
        if (purchaseLinks != null) PurchaseLinks.AddRange(purchaseLinks);
        SpanishPublisher = string.IsNullOrWhiteSpace(spanishPublisher) ? null : spanishPublisher.Trim();
        if (regionalPublishers != null) RegionalPublishers.AddRange(regionalPublishers);
        if (localizedTitles != null) LocalizedTitles.AddRange(localizedTitles);
        UpdateEan(ean);
        UpdateAdditionalBarcodes(additionalBarcodes);
        SetAsin(asin);

        Slug = string.IsNullOrWhiteSpace(customSlug)
            ? GenerateSlug(SpanishTitle)
            : GenerateSlug(customSlug);

        Type = type;
        BaseGameId = baseGameId;
        ExpansionNecessity = expansionNecessity;
        if (impactTags != null) ImpactTags.AddRange(impactTags);
        WhatItBringsSummary = whatItBringsSummary?.Trim();
        ExtraPlayerCount = extraPlayerCount;
        ExtraDurationMinutes = extraDurationMinutes;
    }

    public string IdealPlayerCountText => CalculateIdealPlayerCountText();

    public string CalculateIdealPlayerCountText()
    {
        var bestEntries = Scalability
            .Where(s => s.Status == ScalabilityStatus.MustPlay)
            .OrderBy(s => s.PlayerCount)
            .ToList();

        if (bestEntries.Count == 0)
        {
            // Fallback al mejor recomendado si no hay MustPlay
            bestEntries = Scalability
                .Where(s => s.Status == ScalabilityStatus.Recommended)
                .OrderBy(s => s.PlayerCount)
                .ToList();
        }

        if (bestEntries.Count == 0)
        {
            return "Sin datos ideales";
        }

        if (bestEntries.Count == 1)
        {
            return bestEntries[0].PlayerCount >= 7
                ? "Ideal: 7+ jugadores"
                : $"Ideal: {bestEntries[0].PlayerCount} jugadores";
        }

        // Si son consecutivos (ej. 2 y 3)
        int min = bestEntries.First().PlayerCount;
        int max = bestEntries.Last().PlayerCount;
        if (max - min == bestEntries.Count - 1)
        {
            return $"Ideal: {min}-{max} jugadores";
        }

        // Si son dispersos
        string counts = string.Join(", ", bestEntries.Select(e => e.DisplayCount));
        return $"Ideal: {counts} jugadores";
    }

    public void UpdateLudistRating(double newAverageRating)
    {
        LudistRating = Math.Clamp(Math.Round(newAverageRating, 1), 0.0, 10.0);
    }

    public void UpdateBggId(int bggId)
    {
        if (bggId <= 0) throw new ArgumentOutOfRangeException(nameof(bggId), "El BggId debe ser positivo.");
        BggId = bggId;
    }

    public void UpdateImages(string? coverImageUrl, string? thumbnailUrl = null)
    {
        CoverImageUrl = coverImageUrl?.Trim();
        ThumbnailUrl = thumbnailUrl?.Trim();
    }

    public void UpdateMediaUrls(
        string? coverImageUrl,
        string? thumbnailUrl = null,
        string? backCoverImageUrl = null,
        string? tableImageUrl = null)
    {
        CoverImageUrl = coverImageUrl?.Trim();
        ThumbnailUrl = thumbnailUrl?.Trim();
        BackCoverImageUrl = backCoverImageUrl?.Trim();
        TableImageUrl = tableImageUrl?.Trim();
    }

    public void UpdateCatalogInformation(
        string spanishTitle,
        string originalTitle,
        string designer,
        string publisher,
        int yearPublished,
        string? description,
        ConfrontationType confrontation,
        GameStyle style,
        bool isOfficialSolo,
        AgeRating age,
        LanguageDependence language,
        TableFootprint footprint,
        GameDuration duration,
        int minPlayers,
        int maxPlayers)
    {
        if (string.IsNullOrWhiteSpace(spanishTitle))
            throw new ArgumentException("El título en español no puede estar vacío.", nameof(spanishTitle));
        if (string.IsNullOrWhiteSpace(originalTitle))
            throw new ArgumentException("El título original no puede estar vacío.", nameof(originalTitle));
        int maxAllowedYear = DateTime.UtcNow.Year + 10;
        if (yearPublished < -5000 || yearPublished > maxAllowedYear)
            throw new ArgumentOutOfRangeException(nameof(yearPublished), $"El año de publicación debe situarse entre -5000 y {maxAllowedYear}.");
        if (minPlayers <= 0)
            throw new ArgumentOutOfRangeException(nameof(minPlayers), "El número mínimo de jugadores debe ser mayor a 0.");
        if (maxPlayers < minPlayers)
            throw new ArgumentException("El número máximo de jugadores no puede ser menor al mínimo.", nameof(maxPlayers));

        SpanishTitle = spanishTitle.Trim();
        OriginalTitle = originalTitle.Trim();
        Designer = designer?.Trim() ?? string.Empty;
        Publisher = publisher?.Trim() ?? string.Empty;
        YearPublished = yearPublished;
        Description = description?.Trim();
        Confrontation = confrontation;
        Style = style;
        IsOfficialSolo = isOfficialSolo;
        Age = age ?? throw new ArgumentNullException(nameof(age));
        Language = language;
        Footprint = footprint;
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));

        // INC-77: Solo ajustar la escalabilidad sintética si la entidad carece de datos comunitarios con votos reales
        if (!Scalability.Any(s => s.TotalVotes > 0))
        {
            AdjustScalability(minPlayers, maxPlayers);
        }
    }

    public void UpdateDna(GameStyle style, ConfrontationType confrontation, bool isOfficialSolo)
    {
        Style = style;
        Confrontation = confrontation;
        IsOfficialSolo = isOfficialSolo;
    }

    public void UpdateStyle(GameStyle style)
    {
        Style = style;
    }

    public void AdjustScalability(int minPlayers, int maxPlayers)
    {
        if (minPlayers <= 0 || maxPlayers < minPlayers) return;

        var existingDict = Scalability.ToDictionary(s => s.PlayerCount);
        var updated = new List<ScalabilityEntry>();

        for (int p = minPlayers; p <= maxPlayers; p++)
        {
            if (existingDict.TryGetValue(p, out var existingEntry))
            {
                updated.Add(existingEntry);
            }
            else
            {
                string display = p >= 7 ? "7+" : p.ToString();
                updated.Add(new ScalabilityEntry(p, display, ScalabilityStatus.Recommended));
            }
        }

        Scalability.Clear();
        Scalability.AddRange(updated);
    }

    public void ConfigureExpansion(
        Guid baseGameId,
        ExpansionNecessity necessity,
        IEnumerable<ExpansionImpactTag> impactTags,
        string whatItBringsSummary,
        int? extraPlayerCount = null,
        int? extraDurationMinutes = null)
    {
        if (baseGameId == Guid.Empty) throw new ArgumentException("El BaseGameId no puede estar vacío.", nameof(baseGameId));

        Type = GameType.Expansion;
        BaseGameId = baseGameId;
        ExpansionNecessity = necessity;
        ImpactTags.Clear();
        if (impactTags != null) ImpactTags.AddRange(impactTags);
        WhatItBringsSummary = whatItBringsSummary?.Trim();
        ExtraPlayerCount = extraPlayerCount;
        ExtraDurationMinutes = extraDurationMinutes;
    }

    public void SetBaseGameId(Guid baseGameId)
    {
        if (baseGameId == Guid.Empty) throw new ArgumentException("El BaseGameId no puede estar vacío.", nameof(baseGameId));
        BaseGameId = baseGameId;
        Type = GameType.Expansion;
    }

    public void SetGameType(GameType type)
    {
        Type = type;
    }

    public void SetExpansionAporte(
        ExpansionNecessity necessity,
        IEnumerable<ExpansionImpactTag>? impactTags,
        string whatItBringsSummary,
        int? extraPlayerCount = null,
        int? extraDurationMinutes = null)
    {
        Type = GameType.Expansion;
        ExpansionNecessity = necessity;
        ImpactTags.Clear();
        if (impactTags != null) ImpactTags.AddRange(impactTags);
        WhatItBringsSummary = whatItBringsSummary?.Trim();
        ExtraPlayerCount = extraPlayerCount;
        ExtraDurationMinutes = extraDurationMinutes;
    }

    public void AddPurchaseLink(GamePurchaseLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        PurchaseLinks.Add(link);
    }

    public void UpdatePurchaseLinks(IEnumerable<GamePurchaseLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        PurchaseLinks.Clear();
        PurchaseLinks.AddRange(links);
    }

    public void ClearPurchaseLinks()
    {
        PurchaseLinks.Clear();
    }

    public void AddSleeve(SleeveItem sleeve)
    {
        ArgumentNullException.ThrowIfNull(sleeve);
        Sleeves.Add(sleeve);
    }

    public void UpdateSleeves(IEnumerable<SleeveItem> sleeves)
    {
        ArgumentNullException.ThrowIfNull(sleeves);
        Sleeves.Clear();
        Sleeves.AddRange(sleeves);
    }

    public void ClearSleeves()
    {
        Sleeves.Clear();
    }

    public void UpdateScalability(IEnumerable<ScalabilityEntry> scalability)
    {
        ArgumentNullException.ThrowIfNull(scalability);
        Scalability.Clear();
        Scalability.AddRange(scalability.Where(s => s.PlayerCount > 0));
    }

    public void UpdateDuration(GameDuration duration)
    {
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
    }

    public void UpdateFootprint(TableFootprint footprint)
    {
        Footprint = footprint;
    }

    public void UpdateSpanishPublisher(string? spanishPublisher)
    {
        SpanishPublisher = string.IsNullOrWhiteSpace(spanishPublisher) ? null : spanishPublisher.Trim();
    }

    public void UpdateRegionalPublishers(IEnumerable<RegionalPublisherEntry>? regionalPublishers)
    {
        RegionalPublishers.Clear();
        if (regionalPublishers != null)
        {
            RegionalPublishers.AddRange(regionalPublishers);
        }
    }

    public void UpdateLocalizedTitles(IEnumerable<LocalizedTitleEntry>? localizedTitles)
    {
        LocalizedTitles.Clear();
        if (localizedTitles != null)
        {
            LocalizedTitles.AddRange(localizedTitles);
        }
    }

    public void UpdateEan(string? ean)
    {
        if (string.IsNullOrWhiteSpace(ean))
        {
            Ean = null;
            return;
        }

        if (!BarcodeValidator.TryNormalizeEan13(ean, out var normalized))
        {
            throw new ArgumentException($"El código de barras '{ean}' no es un EAN-13 o UPC válido con dígito de control correcto.", nameof(ean));
        }

        Ean = normalized;
    }

    public void SetAsin(string? asin)
    {
        if (string.IsNullOrWhiteSpace(asin))
        {
            Asin = null;
            return;
        }

        var normalized = asin.Trim().ToUpperInvariant();
        if (normalized.Length > 20)
        {
            normalized = normalized[..20];
        }

        Asin = normalized;
    }

    public void UpdateSpanishTitle(string spanishTitle)
    {
        if (string.IsNullOrWhiteSpace(spanishTitle))
            throw new ArgumentException("El título en español no puede estar vacío.", nameof(spanishTitle));

        SpanishTitle = spanishTitle.Trim();

        // Sincronizar o insertar la entrada en LocalizedTitles para ES
        var existingEs = LocalizedTitles.FirstOrDefault(l => string.Equals(l.CountryCode, "ES", StringComparison.OrdinalIgnoreCase));
        if (existingEs != null)
        {
            LocalizedTitles.Remove(existingEs);
        }
        LocalizedTitles.Insert(0, new LocalizedTitleEntry("ES", SpanishTitle));
    }

    public void UpdateAdditionalBarcodes(IEnumerable<string>? barcodes)
    {
        AdditionalBarcodes.Clear();
        if (barcodes == null) return;

        foreach (var raw in barcodes)
        {
            if (BarcodeValidator.TryNormalizeEan13(raw, out var normalized))
            {
                if (normalized != Ean && !AdditionalBarcodes.Contains(normalized))
                {
                    AdditionalBarcodes.Add(normalized);
                }
            }
        }
    }

    public bool MatchesBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return false;
        if (!BarcodeValidator.TryNormalizeEan13(barcode, out var normalized)) return false;

        return normalized == Ean || AdditionalBarcodes.Contains(normalized);
    }

    public string GetPublisherForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("ES", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(SpanishPublisher))
                return SpanishPublisher;
        }

        if (!string.IsNullOrWhiteSpace(countryCode) && RegionalPublishers.Count > 0)
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

        if (!string.IsNullOrWhiteSpace(countryCode) && LocalizedTitles.Count > 0)
        {
            var match = LocalizedTitles.FirstOrDefault(l => string.Equals(l.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.Title))
                return match.Title;
        }

        return !string.IsNullOrWhiteSpace(SpanishTitle) ? SpanishTitle : OriginalTitle;
    }

    public void SetAiSummary(AiGameSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        AiSummary = summary;
    }

    public void SetSlug(string newSlug)
    {
        if (string.IsNullOrWhiteSpace(newSlug))
            throw new ArgumentException("El slug no puede estar vacío.", nameof(newSlug));
        Slug = GenerateSlug(newSlug);
    }

    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Descomposición canónica para separar tildes y diacríticos
        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (char c in normalized)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        string cleanText = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // 2. Reemplazar caracteres no alfanuméricos por guiones
        cleanText = Regex.Replace(cleanText, @"[^a-z0-9\s-]", "");

        // 3. Reemplazar espacios y guiones múltiples por un único guión
        cleanText = Regex.Replace(cleanText, @"[\s-]+", "-").Trim('-');

        return cleanText;
    }
}
