using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Infrastructure.Bgg;

public static class BggXmlParser
{
    public static Game? ParseItem(XElement item)
    {
        if (item == null) return null;

        int bggId = int.Parse(item.Attribute("id")?.Value ?? "0");
        if (bggId <= 0) return null;

        // Títulos
        var names = item.Elements("name").ToList();
        string originalTitle = names.FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value
            ?? names.FirstOrDefault()?.Attribute("value")?.Value ?? "Desconocido";

        string spanishTitle = ExtractSpanishTitle(names, originalTitle);

        // Metadatos básicos
        int yearPublished = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out int yr) ? yr : DateTime.UtcNow.Year;
        string? coverImageUrl = item.Element("image")?.Value?.Trim();
        string? thumbnailUrl = item.Element("thumbnail")?.Value?.Trim();
        string? rawDescription = item.Element("description")?.Value;
        string? description = string.IsNullOrWhiteSpace(rawDescription) ? null : WebUtility.HtmlDecode(rawDescription).Trim();

        // Autores y Editorial
        string designer = item.Elements("link")
            .FirstOrDefault(l => l.Attribute("type")?.Value == "boardgamedesigner")
            ?.Attribute("value")?.Value ?? "Varios";

        string publisher = item.Elements("link")
            .FirstOrDefault(l => l.Attribute("type")?.Value == "boardgamepublisher")
            ?.Attribute("value")?.Value ?? "Varios";

        // Jugadores
        int minPlayers = int.TryParse(item.Element("minplayers")?.Attribute("value")?.Value, out int minP) && minP > 0 ? minP : 1;
        int maxPlayers = int.TryParse(item.Element("maxplayers")?.Attribute("value")?.Value, out int maxP) && maxP >= minPlayers ? maxP : Math.Max(minPlayers, 4);

        // Tiempos reales
        int playTime = int.TryParse(item.Element("playingtime")?.Attribute("value")?.Value, out int pt) && pt > 0 ? pt : 0;
        int minTime = int.TryParse(item.Element("minplaytime")?.Attribute("value")?.Value, out int mt) && mt > 0 ? mt : (playTime > 0 ? playTime : 30);
        int maxTime = int.TryParse(item.Element("maxplaytime")?.Attribute("value")?.Value, out int xt) && xt > 0 ? xt : (playTime > 0 ? playTime : minTime);
        int avgPlayers = Math.Max(1, (minPlayers + maxPlayers) / 2);
        int estPerPlayer = Math.Max(10, (int)Math.Round((double)(minTime + maxTime) / (2 * avgPlayers)));

        // Edad de caja
        int boxAge = int.TryParse(item.Element("minage")?.Attribute("value")?.Value, out int ma) && ma > 0 ? ma : 10;

        // Encuesta de edad comunitaria
        int communityAge = ParseCommunityAge(item, boxAge);

        // Encuesta de dependencia del idioma
        var language = ParseLanguageDependence(item);

        // Ratings y Rankings
        var (bggRating, bggRank) = ParseStatistics(item);

        // Encuesta de escalabilidad (con fallback si no hay votos)
        var scalability = ParseScalability(item, minPlayers, maxPlayers);

        // ADN lúdico heurístico según enlaces y categorías (con huella en mesa)
        var (confrontation, style, isSolo, footprint) = InferGameDna(item, scalability, maxTime);

        // Fundas de cartas
        var sleeves = BggSleeveParser.ParseSleeves(item);

        return new Game(
            bggId: bggId,
            originalTitle: originalTitle,
            spanishTitle: spanishTitle,
            designer: designer,
            publisher: publisher,
            yearPublished: yearPublished,
            coverImageUrl: coverImageUrl,
            thumbnailUrl: thumbnailUrl,
            description: description,
            bggRating: bggRating,
            bggRank: bggRank,
            ludistRating: bggRating,
            confrontation: confrontation,
            style: style,
            isOfficialSolo: isSolo,
            age: new AgeRating(boxAge, communityAge),
            language: language,
            footprint: footprint,
            duration: new GameDuration(minTime, maxTime, estPerPlayer),
            scalability: scalability,
            sleeves: sleeves
        );
    }

    private static string ExtractSpanishTitle(List<XElement> names, string fallback)
    {
        foreach (var name in names.Where(n => n.Attribute("type")?.Value == "alternate"))
        {
            string val = name.Attribute("value")?.Value ?? string.Empty;
            if (val.Contains("español", StringComparison.OrdinalIgnoreCase) ||
                val.Contains("spanish", StringComparison.OrdinalIgnoreCase) ||
                val.Contains("castellano", StringComparison.OrdinalIgnoreCase))
            {
                // Limpiar sufijos como "(Edición en español)", "(Spanish edition)"
                string cleaned = Regex.Replace(val, @"\s*\([^)]*(español|spanish|castellano)[^)]*\)", "", RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }
        }
        return fallback;
    }

    private static int ParseCommunityAge(XElement item, int defaultAge)
    {
        var agePoll = item.Elements("poll").FirstOrDefault(p => p.Attribute("name")?.Value == "suggested_playerage");
        if (agePoll == null) return defaultAge;

        int highestVotes = -1;
        int bestAge = defaultAge;

        foreach (var r in agePoll.Descendants("result"))
        {
            if (int.TryParse(r.Attribute("numvotes")?.Value, out int votes) &&
                int.TryParse(r.Attribute("value")?.Value, out int age))
            {
                if (votes > highestVotes && votes > 0)
                {
                    highestVotes = votes;
                    bestAge = age;
                }
            }
        }

        return bestAge;
    }

    private static LanguageDependence ParseLanguageDependence(XElement item)
    {
        var poll = item.Elements("poll").FirstOrDefault(p => p.Attribute("name")?.Value == "language_dependence");
        if (poll == null) return LanguageDependence.None;

        int highestVotes = -1;
        int bestLevel = 1;

        foreach (var r in poll.Descendants("result"))
        {
            if (int.TryParse(r.Attribute("numvotes")?.Value, out int votes) &&
                int.TryParse(r.Attribute("level")?.Value, out int level))
            {
                if (votes > highestVotes && votes > 0)
                {
                    highestVotes = votes;
                    bestLevel = level;
                }
            }
        }

        return bestLevel switch
        {
            1 => LanguageDependence.None,
            2 => LanguageDependence.Low,
            _ => LanguageDependence.High
        };
    }

    private static (double Rating, int? Rank) ParseStatistics(XElement item)
    {
        var stats = item.Element("statistics")?.Element("ratings");
        if (stats == null) return (0.0, null);

        double rating = 0.0;
        if (double.TryParse(stats.Element("average")?.Attribute("value")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double avg))
        {
            rating = Math.Round(avg, 2);
        }

        int? rank = null;
        var rankElem = stats.Element("ranks")?.Elements("rank")
            .FirstOrDefault(r => r.Attribute("name")?.Value == "boardgame");

        if (rankElem != null && int.TryParse(rankElem.Attribute("value")?.Value, out int rk))
        {
            rank = rk;
        }

        return (rating, rank);
    }

    private static List<ScalabilityEntry> ParseScalability(XElement item, int minPlayers, int maxPlayers)
    {
        var entries = new List<ScalabilityEntry>();
        var poll = item.Elements("poll").FirstOrDefault(p => p.Attribute("name")?.Value == "suggested_numplayers");
        if (poll != null)
        {
            foreach (var results in poll.Elements("results"))
            {
                string numPlayersRaw = results.Attribute("numplayers")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(numPlayersRaw)) continue;

                bool isPlus = numPlayersRaw.EndsWith("+");
                string cleanNum = numPlayersRaw.TrimEnd('+');
                if (!int.TryParse(cleanNum, out int playerCount)) continue;

                int best = 0;
                int recommended = 0;
                int notRec = 0;

                foreach (var res in results.Elements("result"))
                {
                    string val = res.Attribute("value")?.Value ?? string.Empty;
                    int.TryParse(res.Attribute("numvotes")?.Value, out int votes);

                    if (val.Equals("Best", StringComparison.OrdinalIgnoreCase)) best = votes;
                    else if (val.Equals("Recommended", StringComparison.OrdinalIgnoreCase)) recommended = votes;
                    else if (val.Equals("Not Recommended", StringComparison.OrdinalIgnoreCase)) notRec = votes;
                }

                var status = ScalabilityCalculator.DetermineStatus(best, recommended, notRec);
                string display = isPlus ? $"{playerCount}J+" : $"{playerCount}J";

                entries.Add(new ScalabilityEntry(playerCount, display, status, best, recommended, notRec));
            }
        }

        // Deduplicar por PlayerCount si BGG devolviese múltiples
        var deduped = entries
            .GroupBy(e => e.PlayerCount)
            .Select(g => g.OrderByDescending(e => e.TotalVotes).First())
            .OrderBy(e => e.PlayerCount)
            .ToList();

        // Si la encuesta de BGG devolvió resultados con votos, devolverlos
        if (deduped.Count > 0 && deduped.Any(e => e.TotalVotes > 0))
        {
            return deduped;
        }

        // Fallback robusto cuando no hay votos en BGG: generar entradas para el rango oficial
        int start = Math.Max(1, minPlayers);
        int end = Math.Max(start, maxPlayers);
        var fallbackEntries = new List<ScalabilityEntry>();

        for (int p = start; p <= end; p++)
        {
            string display = $"{p}J";
            var status = (start == end)
                ? ScalabilityStatus.MustPlay
                : ScalabilityStatus.Recommended;

            fallbackEntries.Add(new ScalabilityEntry(p, display, status, BestVotes: 0, RecommendedVotes: 1, NotRecommendedVotes: 0));
        }

        return fallbackEntries;
    }

    private static (ConfrontationType Confrontation, GameStyle Style, bool IsSolo, TableFootprint Footprint) InferGameDna(
        XElement item, List<ScalabilityEntry> scalability, int maxTime)
    {
        var categories = item.Elements("link")
            .Where(l => l.Attribute("type")?.Value is "boardgamecategory" or "boardgamemechanic")
            .Select(l => l.Attribute("value")?.Value ?? string.Empty)
            .ToList();

        bool isCoop = categories.Any(c => c.Contains("Cooperative", StringComparison.OrdinalIgnoreCase));
        bool isTeams = categories.Any(c => c.Contains("Team-Based", StringComparison.OrdinalIgnoreCase) || c.Contains("Secret Identity", StringComparison.OrdinalIgnoreCase));
        bool isSemiCoop = categories.Any(c => c.Contains("Semi-Cooperative", StringComparison.OrdinalIgnoreCase));

        var confrontation = isCoop ? ConfrontationType.Cooperative
            : isSemiCoop ? ConfrontationType.SemiCooperative
            : isTeams ? ConfrontationType.HiddenRolesOrTeams
            : ConfrontationType.Competitive;

        bool isParty = categories.Any(c => c.Contains("Party Game", StringComparison.OrdinalIgnoreCase));
        bool isAbstract = categories.Any(c => c.Contains("Abstract Strategy", StringComparison.OrdinalIgnoreCase));
        bool isCampaign = categories.Any(c => c.Contains("Campaign", StringComparison.OrdinalIgnoreCase) || c.Contains("Legacy", StringComparison.OrdinalIgnoreCase));
        bool isThematic = categories.Any(c => c.Contains("Thematic", StringComparison.OrdinalIgnoreCase) || c.Contains("Wargame", StringComparison.OrdinalIgnoreCase));

        var style = isParty ? GameStyle.PartyGame
            : isCampaign ? GameStyle.NarrativeCampaign
            : isThematic ? GameStyle.Ameritrash
            : isAbstract ? GameStyle.FillerAbstract
            : GameStyle.Eurogame;

        bool isSolo = scalability.Any(s => s.PlayerCount == 1 && s.Status != ScalabilityStatus.NotRecommended);

        bool isSmallTable = categories.Any(c =>
            c.Contains("Card Game", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Party Game", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Dice", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Trivia", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Deduction", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Microgame", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Travel", StringComparison.OrdinalIgnoreCase))
            && maxTime <= 45;

        bool isMonsterTable = categories.Any(c =>
            c.Contains("Miniatures", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Wargame", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Civilization", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("4x", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Big Box", StringComparison.OrdinalIgnoreCase))
            || maxTime >= 150;

        TableFootprint footprint = isMonsterTable ? TableFootprint.TableMonster
            : (isSmallTable ? TableFootprint.SmallTable : TableFootprint.StandardTable);

        return (confrontation, style, isSolo, footprint);
    }

    public static IReadOnlyList<BggCollectionItemDto> ParseCollection(XDocument doc)
    {
        var items = new List<BggCollectionItemDto>();
        if (doc.Root == null) return items;

        foreach (var item in doc.Root.Elements("item"))
        {
            string subtype = item.Attribute("subtype")?.Value ?? "boardgame";
            if (subtype != "boardgame" && subtype != "boardgameexpansion")
                continue;

            if (!int.TryParse(item.Attribute("objectid")?.Value, out int bggId) || bggId <= 0)
                continue;

            string title = item.Element("name")?.Value?.Trim() ?? "Desconocido";
            int? year = int.TryParse(item.Element("yearpublished")?.Value, out int yr) ? yr : null;
            string? thumbnail = item.Element("thumbnail")?.Value?.Trim();
            string? image = item.Element("image")?.Value?.Trim();

            var statusEl = item.Element("status");
            bool isOwned = statusEl?.Attribute("own")?.Value == "1";
            bool isWishlist = statusEl?.Attribute("wishlist")?.Value == "1";
            bool isWantToBuy = statusEl?.Attribute("wanttobuy")?.Value == "1";
            int numPlays = int.TryParse(item.Element("numplays")?.Value, out int plays) ? plays : 0;

            items.Add(new BggCollectionItemDto(
                BggId: bggId,
                Title: WebUtility.HtmlDecode(title),
                YearPublished: year,
                ThumbnailUrl: thumbnail,
                CoverImageUrl: image,
                IsOwned: isOwned,
                IsWishlist: isWishlist,
                IsWantToBuy: isWantToBuy,
                NumPlays: numPlays
            ));
        }

        return items;
    }

    public static IReadOnlyList<BggSearchResultDto> ParseSearchResults(XDocument doc)
    {
        var results = new List<BggSearchResultDto>();
        if (doc.Root == null) return results;

        foreach (var item in doc.Root.Elements("item"))
        {
            if (!int.TryParse(item.Attribute("id")?.Value, out int bggId) || bggId <= 0)
                continue;

            var names = item.Elements("name").ToList();
            string title = names.FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value
                ?? names.FirstOrDefault()?.Attribute("value")?.Value
                ?? item.Element("name")?.Value
                ?? "Desconocido";

            int? year = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out int yr) ? yr : null;

            results.Add(new BggSearchResultDto(
                BggId: bggId,
                Title: WebUtility.HtmlDecode(title).Trim(),
                YearPublished: year
            ));
        }

        return results;
    }

    public static IReadOnlyList<BggTopGameDto> ParseHotGames(XDocument doc)
    {
        var results = new List<BggTopGameDto>();
        if (doc.Root == null) return results;

        foreach (var item in doc.Root.Elements("item"))
        {
            if (!int.TryParse(item.Attribute("id")?.Value, out int bggId) || bggId <= 0)
                continue;

            int? rank = int.TryParse(item.Attribute("rank")?.Value, out int rk) ? rk : null;
            string title = item.Element("name")?.Attribute("value")?.Value
                ?? item.Element("name")?.Value
                ?? "Desconocido";

            int? year = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out int yr) ? yr : null;
            string? thumb = item.Element("thumbnail")?.Attribute("value")?.Value
                ?? item.Element("thumbnail")?.Value;

            results.Add(new BggTopGameDto(
                BggId: bggId,
                Title: WebUtility.HtmlDecode(title).Trim(),
                BggRank: rank,
                YearPublished: year,
                ThumbnailUrl: string.IsNullOrWhiteSpace(thumb) ? null : thumb.Trim()
            ));
        }

        return results;
    }
}

