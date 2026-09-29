using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SeedGamesExporter
{
    private static readonly int[] BaseGameBggIds =
    [
        13,     // Catan
        822,    // Carcassonne
        266192, // Wingspan
        167791, // Terraforming Mars
        173346, // 7 Wonders Duel
        342942, // Ark Nova
        230802, // Azul
        295947, // Cascadia
        316554, // Dune: Imperium
        199792, // Everdell
        174430, // Gloomhaven
        366013, // Heat: Pedal to the Metal
        169786, // Scythe
        162886, // Spirit Island
        148228, // Splendor
        324856, // The Crew: Misión Mar Profundo
        224517, // Brass: Birmingham
        201808, // Clank!
        30549,  // Pandemic
        237182, // Root
        163412, // Patchwork
        9209,   // Ticket to Ride
        138076, // Concordia
        129622, // Love Letter
        180263, // Viticulture
        31260,  // Agrícola
        312484, // Las Ruinas Perdidas de Arnak
        39856,  // Dixit
        178900, // Código Secreto
        193738, // Great Western Trail
        271629  // Los Castillos de Borgoña
    ];

    [Fact]
    public void ExportSeedGamesJson_Generates30BaseGamesWithAllMetadata()
    {
        var games = BaseGameBggIds.Select(id => BggSimulationDataset.CreateGameInstance(id)!).ToList();

        Assert.Equal(31, games.Count);
        Assert.All(games, Assert.NotNull);

        var models = games.Select(g => new
        {
            g.BggId,
            g.OriginalTitle,
            g.SpanishTitle,
            g.Designer,
            g.Publisher,
            g.YearPublished,
            g.CoverImageUrl,
            g.ThumbnailUrl,
            g.Description,
            g.BggRating,
            g.BggRank,
            g.LudistRating,
            Confrontation = g.Confrontation.ToString(),
            Style = g.Style.ToString(),
            g.IsOfficialSolo,
            BoxAge = g.Age.BoxAge,
            CommunityAge = g.Age.CommunityAge,
            Language = g.Language.ToString(),
            Footprint = g.Footprint.ToString(),
            MinMinutes = g.Duration.MinMinutes,
            MaxMinutes = g.Duration.MaxMinutes,
            EstimatedPerPlayerMinutes = g.Duration.EstimatedPerPlayerMinutes,
            Scalability = g.Scalability.Select(s => new
            {
                s.PlayerCount,
                s.DisplayCount,
                Status = s.Status.ToString(),
                s.BestVotes,
                s.RecommendedVotes,
                s.NotRecommendedVotes
            }),
            Sleeves = g.Sleeves.Select(sl => new
            {
                sl.FormatName,
                sl.WidthMm,
                sl.HeightMm,
                sl.CardCount,
                sl.AffiliateUrl
            }),
            PurchaseLinks = g.PurchaseLinks.Select(p => new
            {
                p.StoreName,
                p.AffiliateUrl,
                p.Price,
                p.Currency,
                p.InStock,
                p.Badge,
                p.AffiliateTag
            })
        }).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(models, options);

        string tempPath = Path.Combine(Path.GetTempPath(), $"ludeka-seed-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempPath, json);
            Assert.True(File.Exists(tempPath));
            string readBack = File.ReadAllText(tempPath);
            Assert.Contains("\"BggId\": 13", readBack);
            Assert.Contains("\"BggId\": 193738", readBack);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
