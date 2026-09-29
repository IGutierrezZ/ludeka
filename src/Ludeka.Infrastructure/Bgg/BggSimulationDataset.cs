using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Provee el catálogo en memoria de 40 títulos canónicos del hobby (30 juegos base + 10 expansiones)
/// y colecciones de prueba representativas para simulación sin conexión a la API externa de BGG.
/// </summary>
public static class BggSimulationDataset
{
    private record GameBlueprint(
        int BggId,
        string OriginalTitle,
        string SpanishTitle,
        string Designer,
        string Publisher,
        int YearPublished,
        string CoverImageUrl,
        string ThumbnailUrl,
        string Description,
        double BggRating,
        int? BggRank,
        double LudistRating,
        ConfrontationType Confrontation,
        GameStyle Style,
        bool IsOfficialSolo,
        int BoxAge,
        int CommunityAge,
        LanguageDependence Language,
        TableFootprint Footprint,
        int MinMinutes,
        int MaxMinutes,
        int EstimatedPerPlayerMinutes,
        List<ScalabilityEntry> Scalability,
        List<SleeveItem> Sleeves,
        List<GamePurchaseLink> PurchaseLinks,
        GameType Type = GameType.BaseGame,
        string? CustomSlug = null,
        ExpansionNecessity? ExpansionNecessity = null,
        List<ExpansionImpactTag>? ImpactTags = null,
        string? WhatItBringsSummary = null,
        int ExtraPlayerCount = 0,
        int ExtraDurationMinutes = 0,
        string? BackCoverImageUrl = null,
        string? TableImageUrl = null
    );

    private static readonly List<GameBlueprint> Blueprints = [];

    static BggSimulationDataset()
    {
        InitializeBlueprints();
    }

    public static IReadOnlyList<int> GetAllBggIds() => Blueprints.Select(b => b.BggId).ToList();

    public static Game? CreateGameInstance(int bggId)
    {
        var b = Blueprints.FirstOrDefault(x => x.BggId == bggId);
        if (b == null) return null;

        var scalabilityCopies = b.Scalability.Select(s =>
            new ScalabilityEntry(s.PlayerCount, s.DisplayCount, s.Status, s.BestVotes, s.RecommendedVotes, s.NotRecommendedVotes)).ToList();

        var sleeveCopies = b.Sleeves.Select(s =>
            new SleeveItem(s.FormatName, s.WidthMm, s.HeightMm, s.CardCount, s.AffiliateUrl)).ToList();

        var linkCopies = b.PurchaseLinks.Select(p =>
            new GamePurchaseLink(p.StoreName, p.AffiliateUrl, p.Price, p.Currency, p.InStock, p.Badge, p.AffiliateTag)).ToList();

        return new Game(
            bggId: b.BggId,
            originalTitle: b.OriginalTitle,
            spanishTitle: b.SpanishTitle,
            designer: b.Designer,
            publisher: b.Publisher,
            yearPublished: b.YearPublished,
            coverImageUrl: b.CoverImageUrl,
            thumbnailUrl: b.ThumbnailUrl,
            description: b.Description,
            bggRating: b.BggRating,
            bggRank: b.BggRank,
            ludistRating: b.LudistRating,
            confrontation: b.Confrontation,
            style: b.Style,
            isOfficialSolo: b.IsOfficialSolo,
            age: new AgeRating(b.BoxAge, b.CommunityAge),
            language: b.Language,
            footprint: b.Footprint,
            duration: new GameDuration(b.MinMinutes, b.MaxMinutes, b.EstimatedPerPlayerMinutes),
            scalability: scalabilityCopies,
            sleeves: sleeveCopies,
            customSlug: b.CustomSlug,
            type: b.Type,
            baseGameId: null,
            expansionNecessity: b.ExpansionNecessity,
            impactTags: b.ImpactTags,
            whatItBringsSummary: b.WhatItBringsSummary,
            extraPlayerCount: b.ExtraPlayerCount,
            extraDurationMinutes: b.ExtraDurationMinutes,
            purchaseLinks: linkCopies,
            backCoverImageUrl: b.BackCoverImageUrl,
            tableImageUrl: b.TableImageUrl
        );
    }

    public static IReadOnlyList<BggSearchResultDto> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var normalizedQuery = NormalizeString(query.Trim());
        if (normalizedQuery.Length < 2) return [];

        var results = new List<BggSearchResultDto>();

        foreach (var b in Blueprints)
        {
            var matchSpanish = NormalizeString(b.SpanishTitle).Contains(normalizedQuery);
            var matchOriginal = NormalizeString(b.OriginalTitle).Contains(normalizedQuery);
            var matchDesigner = NormalizeString(b.Designer).Contains(normalizedQuery);

            if (matchSpanish || matchOriginal || matchDesigner)
            {
                results.Add(new BggSearchResultDto(
                    BggId: b.BggId,
                    Title: b.SpanishTitle,
                    YearPublished: b.YearPublished,
                    IsAlreadyCataloged: false
                ));
            }
        }

        return results;
    }

    public static IReadOnlyList<BggTopGameDto> GetTopGames(int limit = 50)
    {
        return Blueprints
            .OrderBy(b => b.BggRank ?? int.MaxValue)
            .ThenByDescending(b => b.BggRating)
            .Take(limit)
            .Select(b => new BggTopGameDto(
                BggId: b.BggId,
                Title: b.SpanishTitle,
                BggRank: b.BggRank,
                YearPublished: b.YearPublished,
                ThumbnailUrl: b.ThumbnailUrl
            ))
            .ToList();
    }

    public static IReadOnlyList<BggCollectionItemDto> GetUserCollection(string username)
    {
        var normalizedUser = username?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalizedUser switch
        {
            "ludeka_demo" => GetLudekaDemoCollection(),
            "pareja_jugona" => GetParejaJugonaCollection(),
            "maraton_euro" => GetMaratonEuroCollection(),
            _ => GetDefaultCollection()
        };
    }

    private static IReadOnlyList<BggCollectionItemDto> GetLudekaDemoCollection()
    {
        return
        [
            CreateItem(266192, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 18), // Wingspan
            CreateItem(13, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 32),     // Catán
            CreateItem(822, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 25),    // Carcassonne
            CreateItem(295947, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 12), // Cascadia
            CreateItem(230802, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 15), // Azul
            CreateItem(316554, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 8),  // Dune: Imperium
            CreateItem(366013, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 6),  // Heat: Pedal to the Metal
            CreateItem(324856, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 14), // The Crew: Misión Mar Profundo
            CreateItem(199792, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0),  // Everdell
            CreateItem(169786, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0),  // Scythe
            CreateItem(162886, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0),  // Spirit Island
            CreateItem(148228, isOwned: false, isWishlist: false, isWantToBuy: true, numPlays: 0)   // Splendor
        ];
    }

    private static IReadOnlyList<BggCollectionItemDto> GetParejaJugonaCollection()
    {
        return
        [
            CreateItem(173346, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 42), // 7 Wonders Duel
            CreateItem(163412, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 36), // Patchwork
            CreateItem(366161, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 19), // Wingspan: Expansión Asia
            CreateItem(295947, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 21), // Cascadia
            CreateItem(230802, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 18), // Azul
            CreateItem(822, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 14),    // Carcassonne
            CreateItem(39856, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0),   // Dixit
            CreateItem(324856, isOwned: false, isWishlist: false, isWantToBuy: true, numPlays: 0)  // The Crew
        ];
    }

    private static IReadOnlyList<BggCollectionItemDto> GetMaratonEuroCollection()
    {
        return
        [
            CreateItem(167791, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 28), // Terraforming Mars
            CreateItem(224517, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 19), // Brass: Birmingham
            CreateItem(342942, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 15), // Ark Nova
            CreateItem(193738, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 11), // Great Western Trail
            CreateItem(169786, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 14), // Scythe
            CreateItem(138076, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 9),  // Concordia
            CreateItem(31260, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 22),  // Agrícola
            CreateItem(316554, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 10), // Dune: Imperium
            CreateItem(180263, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0),  // Viticulture
            CreateItem(271629, isOwned: false, isWishlist: true, isWantToBuy: false, numPlays: 0)   // Los Castillos de Borgoña
        ];
    }

    private static IReadOnlyList<BggCollectionItemDto> GetDefaultCollection()
    {
        return
        [
            CreateItem(266192, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 10),
            CreateItem(13, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 20),
            CreateItem(822, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 15),
            CreateItem(173346, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 25),
            CreateItem(295947, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 8),
            CreateItem(230802, isOwned: true, isWishlist: false, isWantToBuy: false, numPlays: 12)
        ];
    }

    private static BggCollectionItemDto CreateItem(int bggId, bool isOwned, bool isWishlist, bool isWantToBuy, int numPlays)
    {
        var b = Blueprints.FirstOrDefault(x => x.BggId == bggId);
        string title = b?.SpanishTitle ?? $"Juego #{bggId}";
        int? year = b?.YearPublished;
        string? thumb = b?.ThumbnailUrl;
        string? cover = b?.CoverImageUrl;

        return new BggCollectionItemDto(
            BggId: bggId,
            Title: title,
            YearPublished: year,
            ThumbnailUrl: thumb,
            CoverImageUrl: cover,
            IsOwned: isOwned,
            IsWishlist: isWishlist,
            IsWantToBuy: isWantToBuy,
            NumPlays: numPlays
        );
    }

    private static string NormalizeString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static void InitializeBlueprints()
    {
        // ==========================================
        // 30 JUEGOS BASE
        // ==========================================

        // 1. Catan (13)
        AddBase(13, "Catan", "Catán", "Klaus Teuber", "Devir", 1995,
            "/images/games/catan.png", "/images/games/catan.png",
            "El clásico revolucionario de comercio, colonización y gestión de recursos en la isla de Catán.",
            7.14, 450, 7.3, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            10, 10, LanguageDependence.None, TableFootprint.StandardTable, 60, 90, 25,
            [
                new(3, "3J", ScalabilityStatus.MustPlay, 620, 310, 40),
                new(4, "4J", ScalabilityStatus.MustPlay, 890, 150, 25)
            ],
            [],
            [new("Zacatrus", "https://zacatrus.es/catan.html?ref=ludeka", 42.00m, "€", true, "Envío 24h", "Direct")]);

        // 2. Carcassonne (822)
        AddBase(822, "Carcassonne", "Carcassonne", "Klaus-Jürgen Wrede", "Devir", 2000,
            "/images/games/carcassonne.png", "/images/games/carcassonne.png",
            "Obra maestra de colocación de losetas donde creas el paisaje medieval del sur de Francia.",
            7.42, 210, 7.9, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            8, 8, LanguageDependence.None, TableFootprint.StandardTable, 30, 45, 10,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 850, 220, 15),
                new(3, "3J", ScalabilityStatus.Recommended, 410, 520, 40),
                new(4, "4J", ScalabilityStatus.Recommended, 300, 560, 65),
                new(5, "5J", ScalabilityStatus.Recommended, 150, 420, 120)
            ],
            [],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/carcassonne?ref=ludeka", 27.95m, "€", true, "Stock Real", "Direct")]);

        // 3. Wingspan (266192)
        AddBase(266192, "Wingspan", "Wingspan", "Elizabeth Hargrave", "Maldito Games", 2019,
            "/images/games/wingspan.png", "/images/games/wingspan.png",
            "Juego competitivo de construcción de motores impulsado por cartas ornitológicas con una producción excelsa.",
            8.05, 28, 8.4, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            10, 9, LanguageDependence.Low, TableFootprint.StandardTable, 40, 70, 25,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 210, 580, 95),
                new(2, "2J", ScalabilityStatus.MustPlay, 850, 390, 35),
                new(3, "3J", ScalabilityStatus.MustPlay, 920, 310, 20),
                new(4, "4J", ScalabilityStatus.Recommended, 420, 680, 90),
                new(5, "5J", ScalabilityStatus.NotRecommended, 85, 290, 480)
            ],
            [new("Chimera Standard", 57, 89, 212, "https://zacatrus.es/fundas-chimera.html?ref=ludeka")],
            [new("Zacatrus", "https://zacatrus.es/wingspan.html?ref=ludeka", 49.95m, "€", true, "Envío 24h", "Direct")]);

        // 4. Terraforming Mars (167791)
        AddBase(167791, "Terraforming Mars", "Terraforming Mars", "Jacob Fryxelius", "Maldito Games", 2016,
            "/images/games/terraforming-mars.png", "/images/games/terraforming-mars.png",
            "Gigantescas corporaciones compiten por transformar el planeta rojo haciéndolo habitable.",
            8.38, 7, 8.8, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 12, LanguageDependence.Low, TableFootprint.StandardTable, 90, 120, 30,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 320, 480, 70),
                new(2, "2J", ScalabilityStatus.Recommended, 540, 590, 60),
                new(3, "3J", ScalabilityStatus.MustPlay, 1100, 240, 15),
                new(4, "4J", ScalabilityStatus.Recommended, 450, 620, 110),
                new(5, "5J", ScalabilityStatus.NotRecommended, 90, 280, 520)
            ],
            [new("Standard Card Game", 63.5, 88, 208, null)],
            [new("Zacatrus", "https://zacatrus.es/terraforming-mars.html?ref=ludeka", 62.95m, "€", true, "Envío 24h", "Direct")]);

        // 5. 7 Wonders Duel (173346)
        AddBase(173346, "7 Wonders Duel", "7 Wonders: Duel", "Antoine Bauza, Bruno Cathala", "Repos Production", 2015,
            "/images/games/7-wonders-duel.png", "/images/games/7-wonders-duel.png",
            "Uno de los mejores juegos exclusivos para 2 personas: draft piramidal de cartas con 3 condiciones de victoria.",
            8.11, 19, 8.5, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            10, 10, LanguageDependence.None, TableFootprint.SmallTable, 30, 30, 15,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 2100, 110, 12)
            ],
            [new("Standard European", 59, 92, 73, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/7-wonders-duel?ref=ludeka", 24.95m, "€", true, "Stock Real", "Direct")]);

        // 6. Ark Nova (342942)
        AddBase(342942, "Ark Nova", "Ark Nova", "Mathias Wigge", "Maldito Games", 2021,
            "/images/games/ark-nova.png", "/images/games/ark-nova.png",
            "Planifica y construye un zoológico moderno gestionando recintos, programas de conservación y patrocinadores.",
            8.51, 4, 8.9, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            14, 13, LanguageDependence.Low, TableFootprint.StandardTable, 90, 150, 45,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 410, 510, 45),
                new(2, "2J", ScalabilityStatus.MustPlay, 1450, 310, 20),
                new(3, "3J", ScalabilityStatus.Recommended, 620, 680, 80),
                new(4, "4J", ScalabilityStatus.NotRecommended, 110, 320, 610)
            ],
            [new("Standard Card Game", 63.5, 88, 255, null)],
            [new("Zacatrus", "https://zacatrus.es/ark-nova.html?ref=ludeka", 67.50m, "€", true, "Envío 24h", "Direct")]);

        // 7. Azul (230802)
        AddBase(230802, "Azul", "Azul", "Michael Kiesling", "Asmodee", 2017,
            "/images/games/azul.png", "/images/games/azul.png",
            "Emula a los artesanos moriscos vistiendo los muros del Palacio Real de Évora con azulejos portugueses.",
            7.75, 72, 8.0, ConfrontationType.Competitive, GameStyle.FillerAbstract, false,
            8, 8, LanguageDependence.None, TableFootprint.SmallTable, 30, 45, 10,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 1200, 350, 25),
                new(3, "3J", ScalabilityStatus.MustPlay, 890, 410, 30),
                new(4, "4J", ScalabilityStatus.Recommended, 420, 610, 85)
            ],
            [],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/azul?ref=ludeka", 39.99m, "€", true, "Envío Rápido", "Direct")]);

        // 8. Cascadia (295947)
        AddBase(295947, "Cascadia", "Cascadia", "Randy Flynn", "Delirium Games", 2021,
            "/images/games/cascadia.png", "/images/games/cascadia.png",
            "Juego relajante y táctico de colocación de hábitats y patrones de fauna salvaje del Pacífico Noroeste.",
            7.96, 45, 8.3, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            10, 9, LanguageDependence.Low, TableFootprint.StandardTable, 30, 45, 12,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 310, 420, 30),
                new(2, "2J", ScalabilityStatus.MustPlay, 980, 240, 15),
                new(3, "3J", ScalabilityStatus.MustPlay, 850, 290, 20),
                new(4, "4J", ScalabilityStatus.Recommended, 410, 520, 45)
            ],
            [new("Tarot", 70, 120, 25, null)],
            [new("Zacatrus", "https://zacatrus.es/cascadia.html?ref=ludeka", 37.95m, "€", true, "Envío 24h", "Direct")]);

        // 9. Dune: Imperium (316554)
        AddBase(316554, "Dune: Imperium", "Dune: Imperium", "Paul Dennen", "Dire Wolf", 2020,
            "/images/games/dune-imperium.png", "/images/games/dune-imperium.png",
            "Construcción de mazos, colocación de trabajadores e intriga política y militar en el planeta Arrakis.",
            8.36, 12, 8.7, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            14, 13, LanguageDependence.Low, TableFootprint.StandardTable, 60, 120, 25,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 210, 390, 60),
                new(2, "2J", ScalabilityStatus.Recommended, 320, 480, 85),
                new(3, "3J", ScalabilityStatus.MustPlay, 950, 310, 25),
                new(4, "4J", ScalabilityStatus.MustPlay, 1350, 210, 20)
            ],
            [new("Standard Card Game", 63.5, 88, 162, null), new("Mini USA", 41, 63, 58, null)],
            [new("Zacatrus", "https://zacatrus.es/dune-imperium.html?ref=ludeka", 49.95m, "€", true, "Envío 24h", "Direct")]);

        // 10. Everdell (199792)
        AddBase(199792, "Everdell", "Everdell", "James A. Wilson", "Maldito Games", 2018,
            "/images/games/everdell.png", "/images/games/everdell.png",
            "Construye una encantadora ciudad de animalitos del bosque bajo las ramas del gran Árbol Perenne.",
            8.06, 35, 8.3, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            13, 10, LanguageDependence.Low, TableFootprint.TableMonster, 40, 80, 20,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 190, 380, 70),
                new(2, "2J", ScalabilityStatus.MustPlay, 820, 310, 35),
                new(3, "3J", ScalabilityStatus.MustPlay, 890, 280, 30),
                new(4, "4J", ScalabilityStatus.Recommended, 390, 520, 95)
            ],
            [new("Standard Card Game", 63.5, 88, 128, null), new("Mini USA", 41, 63, 30, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/everdell?ref=ludeka", 59.95m, "€", true, "Envío Rápido", "Direct")]);

        // 11. Gloomhaven (174430)
        AddBase(174430, "Gloomhaven", "Gloomhaven", "Isaac Childres", "Cephalofair Games", 2017,
            "/images/games/gloomhaven.png", "/images/games/gloomhaven.png",
            "Monstruosa campaña cooperativa de combate táctico eurogame en un mundo oscuro en constante evolución.",
            8.61, 3, 8.9, ConfrontationType.Cooperative, GameStyle.Ameritrash, true,
            14, 14, LanguageDependence.High, TableFootprint.TableMonster, 60, 150, 40,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 410, 480, 60),
                new(2, "2J", ScalabilityStatus.MustPlay, 1100, 320, 25),
                new(3, "3J", ScalabilityStatus.MustPlay, 1250, 280, 20),
                new(4, "4J", ScalabilityStatus.Recommended, 620, 510, 95)
            ],
            [new("Standard Card Game", 63.5, 88, 727, null), new("Mini European", 44, 68, 975, null)],
            [new("Zacatrus", "https://zacatrus.es/gloomhaven.html?ref=ludeka", 139.95m, "€", true, "Envío Gratis", "Direct")]);

        // 12. Heat: Pedal to the Metal (366013)
        AddBase(366013, "Heat: Pedal to the Metal", "Heat: Pedal to the Metal", "Asger Harding Granerud, Daniel Skjold Pedersen", "Days of Wonder", 2022,
            "/images/games/heat.png", "/images/games/heat.png",
            "Emocionantes carreras de bólidos de los años 60 donde gestionas el calor del motor y entras derrapando a las curvas.",
            8.12, 38, 8.5, ConfrontationType.Competitive, GameStyle.Ameritrash, true,
            10, 10, LanguageDependence.Low, TableFootprint.StandardTable, 45, 60, 12,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 220, 380, 40),
                new(2, "2J", ScalabilityStatus.Recommended, 310, 450, 60),
                new(3, "3J", ScalabilityStatus.Recommended, 420, 490, 30),
                new(4, "4J", ScalabilityStatus.MustPlay, 850, 210, 15),
                new(5, "5J", ScalabilityStatus.MustPlay, 920, 180, 10),
                new(6, "6J", ScalabilityStatus.MustPlay, 1050, 150, 10)
            ],
            [new("Standard Card Game", 63.5, 88, 330, null)],
            [new("Zacatrus", "https://zacatrus.es/heat.html?ref=ludeka", 64.95m, "€", true, "Envío 24h", "Direct")]);

        // 13. Scythe (169786)
        AddBase(169786, "Scythe", "Scythe", "Jamey Stegmaier", "Maldito Games", 2016,
            "/images/games/scythe.png", "/images/games/scythe.png",
            "Ucronía dieselpunk europea de los años 20: mecanoides agrícolas y militares en una tensa carrera por la prosperidad.",
            8.17, 16, 8.4, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            14, 12, LanguageDependence.Low, TableFootprint.TableMonster, 90, 120, 25,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 280, 420, 70),
                new(2, "2J", ScalabilityStatus.Recommended, 390, 510, 80),
                new(3, "3J", ScalabilityStatus.Recommended, 550, 490, 40),
                new(4, "4J", ScalabilityStatus.MustPlay, 1150, 280, 20),
                new(5, "5J", ScalabilityStatus.Recommended, 620, 480, 95)
            ],
            [new("Standard Card Game", 63.5, 88, 106, null), new("Mini USA", 41, 63, 54, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/scythe?ref=ludeka", 79.95m, "€", true, "Envío Rápido", "Direct")]);

        // 14. Spirit Island (162886)
        AddBase(162886, "Spirit Island", "Spirit Island", "R. Eric Reuss", "Arrakis Games", 2017,
            "/images/games/spirit-island.png", "/images/games/spirit-island.png",
            "Defiende una isla mágica poniéndote en la piel de espíritus ancestrales y expulsando a los invasores colonizadores.",
            8.34, 11, 8.8, ConfrontationType.Cooperative, GameStyle.Eurogame, true,
            14, 13, LanguageDependence.Low, TableFootprint.StandardTable, 90, 120, 35,
            [
                new(1, "1J", ScalabilityStatus.MustPlay, 980, 320, 25),
                new(2, "2J", ScalabilityStatus.MustPlay, 1420, 210, 15),
                new(3, "3J", ScalabilityStatus.Recommended, 620, 450, 50),
                new(4, "4J", ScalabilityStatus.Recommended, 310, 420, 95)
            ],
            [new("Standard Card Game", 63.5, 88, 119, null), new("Mini USA", 41, 63, 15, null)],
            [new("Zacatrus", "https://zacatrus.es/spirit-island.html?ref=ludeka", 74.95m, "€", true, "Envío 24h", "Direct")]);

        // 15. Splendor (148228)
        AddBase(148228, "Splendor", "Splendor", "Marc André", "Space Cowboys", 2014,
            "/images/games/splendor.png", "/images/games/splendor.png",
            "Elegante juego de gemas donde mercaderes renacentistas adquieren minas, rutas y la visita de nobles ilustres.",
            7.42, 195, 7.6, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            10, 8, LanguageDependence.None, TableFootprint.SmallTable, 30, 30, 10,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 820, 310, 30),
                new(3, "3J", ScalabilityStatus.MustPlay, 950, 250, 15),
                new(4, "4J", ScalabilityStatus.Recommended, 480, 520, 60)
            ],
            [new("Standard Card Game", 63.5, 88, 90, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/splendor?ref=ludeka", 32.95m, "€", true, "Stock Real", "Direct")]);

        // 16. The Crew: Misión Mar Profundo (324856)
        AddBase(324856, "The Crew: Mission Deep Sea", "La Tripulación: Misión Mar Profundo", "Thomas Sing", "Devir", 2021,
            "/images/games/the-crew.png", "/images/games/the-crew.png",
            "Juego cooperativo de bazas con comunicación limitada en una inmersión abisal inolvidable.",
            8.19, 36, 8.4, ConfrontationType.Cooperative, GameStyle.PartyGame, false,
            10, 9, LanguageDependence.Low, TableFootprint.SmallTable, 20, 20, 5,
            [
                new(3, "3J", ScalabilityStatus.Recommended, 410, 380, 40),
                new(4, "4J", ScalabilityStatus.MustPlay, 1120, 190, 15),
                new(5, "5J", ScalabilityStatus.Recommended, 380, 450, 70)
            ],
            [new("Standard European", 59, 92, 45, null), new("Mini European", 44, 68, 96, null)],
            [new("Zacatrus", "https://zacatrus.es/la-tripulacion-mision-mar-profundo.html?ref=ludeka", 15.00m, "€", true, "Envío 24h", "Direct")]);

        // 17. Brass: Birmingham (224517)
        AddBase(224517, "Brass: Birmingham", "Brass: Birmingham", "Gavan Brown, Matt Tolman, Martin Wallace", "Maldito Games", 2018,
            "/images/games/brass-birmingham.png", "/images/games/brass-birmingham.png",
            "La cúspide de la estrategia económica industrial en Inglaterra: canales, ferrocarriles, carbón, hierro y cerveza.",
            8.60, 1, 9.1, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            14, 13, LanguageDependence.None, TableFootprint.StandardTable, 60, 120, 30,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 450, 510, 50),
                new(3, "3J", ScalabilityStatus.MustPlay, 1200, 250, 15),
                new(4, "4J", ScalabilityStatus.MustPlay, 1850, 190, 10)
            ],
            [new("Chimera Standard", 57, 89, 70, null)],
            [
                new("Zacatrus", "https://zacatrus.es/brass-birmingham.html?ref=ludeka", 79.95m, "€", true, "Envío Gratis", "Direct"),
                new("Cuarto de Juegos", "https://cuartodejuegos.es/buscar?controller=search&s=Brass+Birmingham", 76.95m, "€", true, "Especializada", "Direct"),
                new("Dracotienda", "https://www.dracotienda.com/buscar?controller=search&s=Brass+Birmingham", 74.95m, "€", true, "Oferta", "Direct")
            ],
            backCover: "https://cf.geekdo-images.com/x3zxCWVIubAnNmHgHGaKLw__original/img/O8r1A0Kszn0Vj8l6x7xP41iL3zM=/0x0/filters:format(jpeg)/pic3968318.jpg",
            tableImg: "https://cf.geekdo-images.com/7k_nOxpO9OGIjhLqPaLnjA__original/img/E2pXGqG3B-yX3e0H8rXh0y8t7n8=/0x0/filters:format(jpeg)/pic4235948.jpg");

        // 18. Clank! (201808)
        AddBase(201808, "Clank!: A Deck-Building Adventure", "Clank!", "Paul Dennen", "Devir", 2016,
            "/images/games/clank.png", "/images/games/clank.png",
            "Saquea la mazmorra subterránea del dragón en una divertida carrera de construcción de mazos sin hacer ruido.",
            7.75, 78, 8.1, ConfrontationType.Competitive, GameStyle.Ameritrash, false,
            12, 10, LanguageDependence.Low, TableFootprint.StandardTable, 30, 60, 15,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 390, 480, 55),
                new(3, "3J", ScalabilityStatus.MustPlay, 890, 240, 20),
                new(4, "4J", ScalabilityStatus.MustPlay, 980, 210, 15)
            ],
            [new("Standard Card Game", 63.5, 88, 183, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/clank?ref=ludeka", 54.95m, "€", true, "Envío Rápido", "Direct")]);

        // 19. Pandemic (30549)
        AddBase(30549, "Pandemic", "Pandemic", "Matt Leacock", "Z-Man Games", 2008,
            "/images/games/pandemic.png", "/images/games/pandemic.png",
            "Coopera como equipo de especialistas de salud para contener y erradicar cuatro plagas globales mortales.",
            7.53, 130, 7.8, ConfrontationType.Cooperative, GameStyle.Ameritrash, false,
            8, 9, LanguageDependence.Low, TableFootprint.StandardTable, 45, 45, 12,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 420, 510, 45),
                new(3, "3J", ScalabilityStatus.MustPlay, 850, 280, 20),
                new(4, "4J", ScalabilityStatus.MustPlay, 920, 240, 25)
            ],
            [new("Standard Card Game", 63.5, 88, 118, null)],
            [new("Zacatrus", "https://zacatrus.es/pandemic.html?ref=ludeka", 39.95m, "€", true, "Envío 24h", "Direct")]);

        // 20. Root (237182)
        AddBase(237182, "Root", "Root", "Cole Wehrle", "2 Tomatoes Games", 2018,
            "/images/games/root.png", "/images/games/root.png",
            "Juego asimétrico de poder y guerra en el bosque: marqueses felinos, dinastías de aves y el astuto vagabundo.",
            8.07, 30, 8.6, ConfrontationType.Competitive, GameStyle.Ameritrash, true,
            14, 12, LanguageDependence.Low, TableFootprint.StandardTable, 60, 90, 25,
            [
                new(3, "3J", ScalabilityStatus.Recommended, 480, 520, 50),
                new(4, "4J", ScalabilityStatus.MustPlay, 1420, 210, 15)
            ],
            [new("Standard Card Game", 63.5, 88, 98, null)],
            [new("Zacatrus", "https://zacatrus.es/root.html?ref=ludeka", 59.95m, "€", true, "Envío 24h", "Direct")]);

        // 21. Patchwork (163412)
        AddBase(163412, "Patchwork", "Patchwork", "Uwe Rosenberg", "Maldito Games", 2014,
            "/images/games/patchwork.png", "/images/games/patchwork.png",
            "El rey de los duelos de retales: encaja piezas de tipo tetris y optimiza botones y tiempo en tu colcha.",
            7.61, 105, 8.0, ConfrontationType.Competitive, GameStyle.FillerAbstract, false,
            8, 8, LanguageDependence.None, TableFootprint.SmallTable, 30, 30, 15,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 1650, 120, 10)
            ],
            [],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/patchwork?ref=ludeka", 22.50m, "€", true, "Stock Real", "Direct")]);

        // 22. Ticket to Ride (9209)
        AddBase(9209, "Ticket to Ride", "¡Aventureros al Tren!", "Alan R. Moon", "Days of Wonder", 2004,
            "/images/games/ticket-to-ride.png", "/images/games/ticket-to-ride.png",
            "Reclama rutas ferroviarias conectando ciudades icónicas en una carrera familiar repleta de emoción.",
            7.40, 230, 7.5, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            8, 8, LanguageDependence.None, TableFootprint.StandardTable, 30, 60, 10,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 310, 480, 80),
                new(3, "3J", ScalabilityStatus.Recommended, 420, 510, 35),
                new(4, "4J", ScalabilityStatus.MustPlay, 980, 240, 15),
                new(5, "5J", ScalabilityStatus.MustPlay, 910, 280, 20)
            ],
            [new("Mini USA", 41, 63, 144, null)],
            [new("Zacatrus", "https://zacatrus.es/aventureros-al-tren.html?ref=ludeka", 44.95m, "€", true, "Envío 24h", "Direct")]);

        // 23. Concordia (138076)
        AddBase(138076, "Concordia", "Concordia", "Mac Gerdts", "Ediciones MasQueOca", 2013,
            "/images/games/concordia.png", "/images/games/concordia.png",
            "Elegante juego económico de cartas de acción y comercio pacífico en las provincias del Imperio Romano.",
            8.11, 22, 8.6, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            13, 11, LanguageDependence.None, TableFootprint.StandardTable, 100, 100, 25,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 390, 450, 60),
                new(3, "3J", ScalabilityStatus.MustPlay, 850, 280, 20),
                new(4, "4J", ScalabilityStatus.MustPlay, 1100, 210, 15),
                new(5, "5J", ScalabilityStatus.Recommended, 420, 490, 70)
            ],
            [new("Standard Card Game", 63.5, 88, 72, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/concordia?ref=ludeka", 59.95m, "€", true, "Stock Real", "Direct")]);

        // 24. Love Letter (129622)
        AddBase(129622, "Love Letter", "Love Letter", "Seiji Kanai", "Z-Man Games", 2012,
            "/images/games/love-letter.png", "/images/games/love-letter.png",
            "Roba una carta, juega una carta. Deducción y faroleo condensados en apenas 16 cartas en la corte de Tempest.",
            7.21, 350, 7.4, ConfrontationType.Competitive, GameStyle.PartyGame, false,
            10, 8, LanguageDependence.Low, TableFootprint.SmallTable, 20, 20, 5,
            [
                new(3, "3J", ScalabilityStatus.Recommended, 410, 390, 30),
                new(4, "4J", ScalabilityStatus.MustPlay, 1150, 180, 15)
            ],
            [new("Standard Card Game", 63.5, 88, 27, null)],
            [new("Zacatrus", "https://zacatrus.es/love-letter.html?ref=ludeka", 12.95m, "€", true, "Envío 24h", "Direct")]);

        // 25. Viticulture Essential Edition (180263)
        AddBase(180263, "Viticulture Essential Edition", "Viticulture", "Jamey Stegmaier, Alan Stone, Uwe Rosenberg", "Maldito Games", 2015,
            "/images/games/viticulture.png", "/images/games/viticulture.png",
            "Crea y gestiona tu propio viñedo en la Toscana cultivando vides, envejeciendo vino y atendiendo visitantes.",
            8.03, 40, 8.4, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            13, 11, LanguageDependence.Low, TableFootprint.StandardTable, 60, 90, 20,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 280, 390, 45),
                new(2, "2J", ScalabilityStatus.Recommended, 450, 480, 40),
                new(3, "3J", ScalabilityStatus.MustPlay, 910, 240, 15),
                new(4, "4J", ScalabilityStatus.MustPlay, 980, 220, 15),
                new(5, "5J", ScalabilityStatus.Recommended, 390, 490, 75)
            ],
            [new("Standard Card Game", 63.5, 88, 154, null), new("Mini European", 44, 68, 78, null)],
            [new("Zacatrus", "https://zacatrus.es/viticulture-essential.html?ref=ludeka", 59.95m, "€", true, "Envío 24h", "Direct")]);

        // 26. Agrícola (31260)
        AddBase(31260, "Agricola", "Agrícola", "Uwe Rosenberg", "Homoludicus / Lookout", 2007,
            "/images/games/agricola.png", "/images/games/agricola.png",
            "El clásico absoluto de colocación de trabajadores: gestiona tu granja familiar del siglo XVII y alimenta a los tuyos.",
            7.92, 42, 8.5, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 12, LanguageDependence.Low, TableFootprint.StandardTable, 30, 150, 30,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 350, 410, 40),
                new(2, "2J", ScalabilityStatus.Recommended, 480, 490, 35),
                new(3, "3J", ScalabilityStatus.MustPlay, 980, 220, 15),
                new(4, "4J", ScalabilityStatus.MustPlay, 1150, 190, 15)
            ],
            [new("Standard European", 59, 92, 120, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/agricola?ref=ludeka", 54.95m, "€", true, "Envío Rápido", "Direct")]);

        // 27. Las Ruinas Perdidas de Arnak (312484)
        AddBase(312484, "Lost Ruins of Arnak", "Las Ruinas Perdidas de Arnak", "Min & Elwen", "Devir", 2020,
            "/images/games/arnak.png", "/images/games/arnak.png",
            "Exploración de templos antiguos combinando colocación de trabajadores, construcción de mazos y gestión de recursos.",
            8.13, 29, 8.5, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 11, LanguageDependence.Low, TableFootprint.StandardTable, 30, 120, 25,
            [
                new(1, "1J", ScalabilityStatus.Recommended, 290, 380, 40),
                new(2, "2J", ScalabilityStatus.Recommended, 490, 480, 30),
                new(3, "3J", ScalabilityStatus.MustPlay, 950, 210, 15),
                new(4, "4J", ScalabilityStatus.Recommended, 410, 520, 60)
            ],
            [new("Standard Card Game", 63.5, 88, 110, null)],
            [new("Zacatrus", "https://zacatrus.es/las-ruinas-perdidas-de-arnak.html?ref=ludeka", 54.95m, "€", true, "Envío 24h", "Direct")]);

        // 28. Dixit (39856)
        AddBase(39856, "Dixit", "Dixit", "Jean-Louis Roubira", "Libellud", 2008,
            "/images/games/dixit.png", "/images/games/dixit.png",
            "Juego familiar y social de pistas poéticas y votación secreta con cartas de ilustración onírica.",
            7.21, 380, 7.3, ConfrontationType.Competitive, GameStyle.PartyGame, false,
            8, 8, LanguageDependence.None, TableFootprint.SmallTable, 30, 30, 10,
            [
                new(4, "4J", ScalabilityStatus.Recommended, 390, 480, 45),
                new(5, "5J", ScalabilityStatus.MustPlay, 890, 210, 15),
                new(6, "6J", ScalabilityStatus.MustPlay, 1050, 180, 10)
            ],
            [new("Tarot", 70, 120, 84, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/dixit?ref=ludeka", 32.95m, "€", true, "Stock Real", "Direct")]);

        // 29. Código Secreto (178900)
        AddBase(178900, "Codenames", "Código Secreto", "Vlaada Chvátil", "Devir", 2015,
            "/images/games/codigo-secreto.png", "/images/games/codigo-secreto.png",
            "Dos jefes de espías rivales conocen la identidad secreta de 25 agentes y dan pistas de una sola palabra.",
            7.56, 125, 7.8, ConfrontationType.Competitive, GameStyle.PartyGame, false,
            14, 10, LanguageDependence.High, TableFootprint.SmallTable, 15, 15, 5,
            [
                new(4, "4J", ScalabilityStatus.Recommended, 420, 490, 45),
                new(6, "6J", ScalabilityStatus.MustPlay, 1250, 180, 10),
                new(8, "8J+", ScalabilityStatus.MustPlay, 980, 240, 20)
            ],
            [new("Mini European", 44, 68, 200, null)],
            [new("Zacatrus", "https://zacatrus.es/codigo-secreto.html?ref=ludeka", 22.00m, "€", true, "Envío 24h", "Direct")]);

        // 30. Great Western Trail (193738)
        AddBase(193738, "Great Western Trail", "Great Western Trail", "Alexander Pfister", "Ediciones MasQueOca", 2016,
            "/images/games/great-western-trail.png", "/images/games/great-western-trail.png",
            "Conduce tu ganado de Texas a Kansas City mejorando tu mazo de reses, contratando vaqueros y construyendo paradas.",
            8.27, 15, 8.7, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 12, LanguageDependence.None, TableFootprint.StandardTable, 75, 150, 30,
            [
                new(2, "2J", ScalabilityStatus.Recommended, 480, 490, 35),
                new(3, "3J", ScalabilityStatus.MustPlay, 1150, 210, 15),
                new(4, "4J", ScalabilityStatus.Recommended, 520, 480, 65)
            ],
            [new("Mini USA", 41, 63, 120, null)],
            [new("Zacatrus", "https://zacatrus.es/great-western-trail.html?ref=ludeka", 59.95m, "€", true, "Envío 24h", "Direct")]);

        // 31. Los Castillos de Borgoña (271629)
        AddBase(271629, "The Castles of Burgundy", "Los Castillos de Borgoña", "Stefan Feld", "alea / Ravensburger", 2019,
            "/images/games/castles-of-burgundy.png", "/images/games/castles-of-burgundy.png",
            "El clásico de draft de dados de Stefan Feld: construye y expande tu principado en el valle del Loira del siglo XV con comercio y ganadería.",
            8.12, 17, 8.6, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            12, 12, LanguageDependence.None, TableFootprint.StandardTable, 70, 120, 25,
            [
                new(2, "2J", ScalabilityStatus.MustPlay, 1450, 120, 10),
                new(3, "3J", ScalabilityStatus.Recommended, 620, 480, 40),
                new(4, "4J", ScalabilityStatus.Recommended, 410, 510, 80)
            ],
            [],
            [new("Zacatrus", "https://zacatrus.es/los-castillos-de-borgona.html?ref=ludeka", 49.95m, "€", true, "Envío 24h", "Direct")]);

        // ==========================================
        // 10 EXPANSIONES OFICIALES
        // ==========================================

        // 31. Wingspan: Europa (290448)
        AddExpansion(290448, "Wingspan: European Expansion", "Wingspan: Expansión Europa", "Elizabeth Hargrave", "Maldito Games", 2019,
            "/images/games/wingspan.png", "/images/games/wingspan.png",
            "Añade 81 majestuosas aves europeas con poderes de fin de ronda, nuevos objetivos y huevos morados.",
            8.35, 78, 8.6, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            10, 10, LanguageDependence.Low, TableFootprint.StandardTable, 40, 70, 25,
            [new(2, "2J", ScalabilityStatus.MustPlay, 850, 210, 15)],
            [new("Chimera Standard", 57, 89, 81, null)],
            [new("Zacatrus", "https://zacatrus.es/wingspan-europa.html?ref=ludeka", 26.50m, "€", true, "Envío 24h", "Direct")],
            "wingspan-expansion-europa", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.FixesBalance, ExpansionImpactTag.ModularContent],
            "Introduce aves con poderes de final de ronda que potencian la interacción positiva y la variedad.",
            0, 5);

        // 32. Wingspan: Oceanía (300580)
        AddExpansion(300580, "Wingspan: Oceania Expansion", "Wingspan: Expansión Oceanía", "Elizabeth Hargrave", "Maldito Games", 2020,
            "/images/games/wingspan.png", "/images/games/wingspan.png",
            "Rebalancea los tableros de jugador, introduce el néctar como alimento comodín y aves no voladoras.",
            8.42, 55, 8.8, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            10, 10, LanguageDependence.Low, TableFootprint.StandardTable, 40, 75, 25,
            [new(2, "2J", ScalabilityStatus.MustPlay, 920, 180, 10)],
            [new("Chimera Standard", 57, 89, 95, null)],
            [new("Zacatrus", "https://zacatrus.es/wingspan-oceania.html?ref=ludeka", 29.95m, "€", true, "Envío 24h", "Direct")],
            "wingspan-expansion-oceania", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.FixesBalance, ExpansionImpactTag.ModularContent],
            "Corrige la estrategia dominante de puesta de huevos con nuevos tableros y el recurso néctar.",
            0, 10);

        // 33. Wingspan: Asia (366161)
        AddExpansion(366161, "Wingspan: Asia", "Wingspan: Expansión Asia", "Elizabeth Hargrave", "Maldito Games", 2022,
            "/images/games/wingspan.png", "/images/games/wingspan.png",
            "Autojugable para 1-2 jugadores con el Modo Dúo, y expansión para 6-7 jugadores con el modo Bandada.",
            8.38, 62, 8.7, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            10, 10, LanguageDependence.Low, TableFootprint.StandardTable, 40, 75, 25,
            [new(2, "2J", ScalabilityStatus.MustPlay, 1150, 120, 5)],
            [new("Chimera Standard", 57, 89, 90, null)],
            [new("Zacatrus", "https://zacatrus.es/wingspan-asia.html?ref=ludeka", 39.95m, "€", true, "Envío 24h", "Direct")],
            "wingspan-expansion-asia", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.ImprovesTwoPlayers, ExpansionImpactTag.AddsPlayers],
            "Incluye el aclamado Modo Dúo para dos jugadores en un mapa territorial táctico.",
            2, 15, GameType.StandaloneExpansion);

        // 34. Terraforming Mars: Preludio (247030)
        AddExpansion(247030, "Terraforming Mars: Prelude", "Terraforming Mars: Preludio", "Jacob Fryxelius", "Maldito Games", 2018,
            "/images/games/terraforming-mars.png", "/images/games/terraforming-mars.png",
            "Cartas de arranque acelerado que potencian la producción inicial y acortan la partida en 30-40 minutos.",
            8.64, 32, 8.9, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 12, LanguageDependence.Low, TableFootprint.StandardTable, 60, 100, 25,
            [new(3, "3J", ScalabilityStatus.MustPlay, 980, 180, 10)],
            [new("Standard Card Game", 63.5, 88, 35, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/prelude?ref=ludeka", 19.95m, "€", true, "Stock Real", "Direct")],
            "terraforming-mars-preludio", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.TightensTime, ExpansionImpactTag.FixesBalance],
            "Elimina el lento arranque de las primeras generaciones aportando cartas de preludio temáticas.",
            0, -30);

        // 35. Terraforming Mars: Hellas & Elysium (230914)
        AddExpansion(230914, "Terraforming Mars: Hellas & Elysium", "Terraforming Mars: Hellas & Elysium", "Jacob Fryxelius", "Maldito Games", 2017,
            "/images/games/terraforming-mars.png", "/images/games/terraforming-mars.png",
            "Doble tablero con dos nuevos mapas completos de Marte con hitos y recompensas totalmente inéditos.",
            8.21, 95, 8.5, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            12, 12, LanguageDependence.None, TableFootprint.StandardTable, 90, 120, 30,
            [new(3, "3J", ScalabilityStatus.MustPlay, 850, 210, 15)],
            [],
            [new("Zacatrus", "https://zacatrus.es/hellas-elysium.html?ref=ludeka", 19.95m, "€", true, "Envío 24h", "Direct")],
            "terraforming-mars-hellas-elysium", ExpansionNecessity.HighlyRecommended,
            [ExpansionImpactTag.NewMapOrFactions, ExpansionImpactTag.ModularContent],
            "Renueva la frescura de las partidas sobre la superficie marciana con metas competitivas nuevas.",
            0, 0);

        // 36. Carcassonne: Posadas y Catedrales (2993)
        AddExpansion(2993, "Carcassonne: Inns & Cathedrals", "Carcassonne: Posadas y Catedrales", "Klaus-Jürgen Wrede", "Devir", 2002,
            "/images/games/carcassonne.png", "/images/games/carcassonne.png",
            "Piezas para un 6º jugador, el meeple grande y losetas de posadas y catedrales con alto riesgo y recompensa.",
            7.91, 120, 8.4, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            8, 8, LanguageDependence.None, TableFootprint.StandardTable, 35, 60, 10,
            [new(6, "6J", ScalabilityStatus.MustPlay, 500, 150, 20)],
            [],
            [new("Zacatrus", "https://zacatrus.es/carcassonne-posadas-y-catedrales.html?ref=ludeka", 17.50m, "€", true, "Envío 24h", "Direct")],
            "carcassonne-posadas-y-catedrales", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.AddsPlayers, ExpansionImpactTag.ModularContent],
            "Introduce el meeple gigante (fuerza 2) y componentes para sumar a una sexta persona en la mesa.",
            1, 10);

        // 37. Carcassonne: Constructores y Comerciantes (8443)
        AddExpansion(8443, "Carcassonne: Traders & Builders", "Carcassonne: Constructores y Comerciantes", "Klaus-Jürgen Wrede", "Devir", 2003,
            "/images/games/carcassonne.png", "/images/games/carcassonne.png",
            "Meeple constructor para turnos dobles encadenados, cerdo para granjas y mercancías comerciales.",
            7.84, 135, 8.3, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            8, 8, LanguageDependence.None, TableFootprint.StandardTable, 40, 60, 10,
            [new(2, "2J", ScalabilityStatus.MustPlay, 450, 120, 15)],
            [],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/carcassonne-constructores?ref=ludeka", 17.50m, "€", true, "Stock Real", "Direct")],
            "carcassonne-constructores-y-comerciantes", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.TightensTime, ExpansionImpactTag.ModularContent],
            "Aporta dinamismo con turnos consecutivos gracias al constructor y recursos comerciales.",
            0, 5);

        // 38. 7 Wonders Duel: Pantheon (202976)
        AddExpansion(202976, "7 Wonders Duel: Pantheon", "7 Wonders Duel: Pantheon", "Antoine Bauza, Bruno Cathala", "Repos Production", 2016,
            "/images/games/7-wonders-duel.png", "/images/games/7-wonders-duel.png",
            "Introduce las divinidades mitológicas de 5 panteones (griego, romano, egipcio, mesopotámico y fenicio).",
            8.16, 68, 8.6, ConfrontationType.Competitive, GameStyle.Eurogame, false,
            10, 10, LanguageDependence.None, TableFootprint.SmallTable, 30, 30, 15,
            [new(2, "2J", ScalabilityStatus.MustPlay, 1450, 110, 10)],
            [new("Standard European", 59, 92, 16, null), new("Large / Tarot", 65, 100, 15, null)],
            [new("Zacatrus", "https://zacatrus.es/7-wonders-duel-pantheon.html?ref=ludeka", 22.95m, "€", true, "Envío 24h", "Direct")],
            "7-wonders-duel-pantheon", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.FixesBalance, ExpansionImpactTag.ModularContent],
            "Permite activar cartas divinas sin tomar cartas de la pirámide, rompiendo empates y bloqueos.",
            0, 5);

        // 39. Dune: Imperium - El Auge de Ix (342035)
        AddExpansion(342035, "Dune: Imperium – Rise of Ix", "Dune: Imperium: El Auge de Ix", "Paul Dennen", "Dire Wolf", 2022,
            "/images/games/dune-imperium.png", "/images/games/dune-imperium.png",
            "La confederación de Ix aporta innovaciones tecnológicas, poderosos acorazados de combate y nuevos líderes.",
            8.67, 25, 8.9, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            14, 13, LanguageDependence.Low, TableFootprint.StandardTable, 60, 120, 25,
            [new(3, "3J", ScalabilityStatus.MustPlay, 890, 150, 10), new(4, "4J", ScalabilityStatus.MustPlay, 1120, 120, 10)],
            [new("Standard Card Game", 63.5, 88, 65, null), new("Mini USA", 41, 63, 20, null)],
            [new("Zacatrus", "https://zacatrus.es/dune-imperium-rise-of-ix.html?ref=ludeka", 39.95m, "€", true, "Envío 24h", "Direct")],
            "dune-imperium-el-auge-de-ix", ExpansionNecessity.MustHave,
            [ExpansionImpactTag.FixesBalance, ExpansionImpactTag.ModularContent],
            "Sustituye las rutas espaciales de la CHOAM por un mercado tecnológico fascinante y acorazados estelares.",
            0, 10);

        // 40. Everdell: Bellfaire (265492)
        AddExpansion(265492, "Everdell: Bellfaire", "Everdell: Bellfaire", "James A. Wilson", "Maldito Games", 2019,
            "/images/games/everdell.png", "/images/games/everdell.png",
            "Módulo festivo que amplía el juego hasta 5-6 comensales, introduce habilidades asimétricas de especie y nuevo mercado.",
            7.95, 115, 8.2, ConfrontationType.Competitive, GameStyle.Eurogame, true,
            13, 10, LanguageDependence.Low, TableFootprint.TableMonster, 40, 90, 20,
            [new(4, "4J", ScalabilityStatus.MustPlay, 620, 210, 20), new(5, "5J", ScalabilityStatus.Recommended, 420, 310, 50)],
            [new("Standard Card Game", 63.5, 88, 40, null)],
            [new("Cuarto de Juegos", "https://cuartodejuegos.es/everdell-bellfaire?ref=ludeka", 39.95m, "€", true, "Stock Real", "Direct")],
            "everdell-bellfaire", ExpansionNecessity.HighlyRecommended,
            [ExpansionImpactTag.AddsPlayers, ExpansionImpactTag.AddsAsymmetry],
            "Añade componentes para jugar hasta 6 personas, un tablero plano sustituto del gran árbol y poderes asimétricos.",
            2, 15);
    }

    private static void AddBase(
        int bggId, string orig, string span, string designer, string pub, int year,
        string cover, string thumb, string desc, double bggRating, int? bggRank, double ludistRating,
        ConfrontationType confrontation, GameStyle style, bool solo,
        int boxAge, int commAge, LanguageDependence lang, TableFootprint footprint,
        int minMin, int maxMin, int perPlayer,
        List<ScalabilityEntry> scalability, List<SleeveItem> sleeves, List<GamePurchaseLink> links,
        string? backCover = null, string? tableImg = null)
    {
        Blueprints.Add(new GameBlueprint(
            BggId: bggId,
            OriginalTitle: orig,
            SpanishTitle: span,
            Designer: designer,
            Publisher: pub,
            YearPublished: year,
            CoverImageUrl: cover,
            ThumbnailUrl: thumb,
            Description: desc,
            BggRating: bggRating,
            BggRank: bggRank,
            LudistRating: ludistRating,
            Confrontation: confrontation,
            Style: style,
            IsOfficialSolo: solo,
            BoxAge: boxAge,
            CommunityAge: commAge,
            Language: lang,
            Footprint: footprint,
            MinMinutes: minMin,
            MaxMinutes: maxMin,
            EstimatedPerPlayerMinutes: perPlayer,
            Scalability: scalability,
            Sleeves: sleeves,
            PurchaseLinks: links,
            BackCoverImageUrl: backCover,
            TableImageUrl: tableImg
        ));
    }

    private static void AddExpansion(
        int bggId, string orig, string span, string designer, string pub, int year,
        string cover, string thumb, string desc, double bggRating, int? bggRank, double ludistRating,
        ConfrontationType confrontation, GameStyle style, bool solo,
        int boxAge, int commAge, LanguageDependence lang, TableFootprint footprint,
        int minMin, int maxMin, int perPlayer,
        List<ScalabilityEntry> scalability, List<SleeveItem> sleeves, List<GamePurchaseLink> links,
        string customSlug, ExpansionNecessity necessity, List<ExpansionImpactTag> tags,
        string whatItBrings, int extraPlayers, int extraMinutes, GameType type = GameType.Expansion,
        string? backCover = null, string? tableImg = null)
    {
        Blueprints.Add(new GameBlueprint(
            BggId: bggId,
            OriginalTitle: orig,
            SpanishTitle: span,
            Designer: designer,
            Publisher: pub,
            YearPublished: year,
            CoverImageUrl: cover,
            ThumbnailUrl: thumb,
            Description: desc,
            BggRating: bggRating,
            BggRank: bggRank,
            LudistRating: ludistRating,
            Confrontation: confrontation,
            Style: style,
            IsOfficialSolo: solo,
            BoxAge: boxAge,
            CommunityAge: commAge,
            Language: lang,
            Footprint: footprint,
            MinMinutes: minMin,
            MaxMinutes: maxMin,
            EstimatedPerPlayerMinutes: perPlayer,
            Scalability: scalability,
            Sleeves: sleeves,
            PurchaseLinks: links,
            Type: type,
            CustomSlug: customSlug,
            ExpansionNecessity: necessity,
            ImpactTags: tags,
            WhatItBringsSummary: whatItBrings,
            ExtraPlayerCount: extraPlayers,
            ExtraDurationMinutes: extraMinutes,
            BackCoverImageUrl: backCover,
            TableImageUrl: tableImg
        ));
    }
}
