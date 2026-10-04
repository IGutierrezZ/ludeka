using System.Text.Json;
using Ludeka.Application.Features.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggExpansionFilterTests
{
    [Theory]
    [InlineData("Root: The Riverfolk Expansion", false)]
    [InlineData("Root: The Underground Expansion", false)]
    [InlineData("Catan: Ciudades y Caballeros", false)]
    [InlineData("Wingspan: Expansión Oceanía", false)]
    [InlineData("Blood Rage: Mystics of Midgard", false)]
    [InlineData("Prometheus: Rise of the Titans", false)]
    [InlineData("The Promotion", false)]
    [InlineData("Terraforming Mars: Promo Cards", true)]
    [InlineData("Terraforming Mars: Big Box Promo Pack", true)]
    [InlineData("7 Wonders: Leaders – Stevie Promo Card", true)]
    [InlineData("Catan: Oil Springs Promo Hex", true)]
    [InlineData("Everdell: Extra! Extra! (Promo Cards)", true)]
    [InlineData("Scythe: Metal Coins", true)]
    [InlineData("Scythe: Realistic Resources Bonus Pack", true)]
    [InlineData("Blood Rage: Wildboar Clan (Kickstarter Promo)", true)]
    [InlineData("Dice Throne: Season 1 Rerolled Dice Set", true)]
    [InlineData("Dune: Imperium – Deluxe Upgrade Pack", true)]
    [InlineData("Ark Nova: Card Sleeves", true)]
    [InlineData("Heat: Pedal to the Metal – Playmat", true)]
    [InlineData("Anachrony: Acrylic Tokens", true)]
    public void IsProbablePromoOrAccessory_ClassifiesTitlesCorrectly(string title, bool expectedIsPromo)
    {
        bool actual = BggRawSnapshotParser.IsProbablePromoOrAccessory(title);
        Assert.Equal(expectedIsPromo, actual);
    }

    [Fact]
    public void ExtractCommunityStatsFromJson_ExtractsUsersRatedAndOwnedAccurately()
    {
        string json = """
        {
            "item": {
                "@id": "5000",
                "@type": "boardgameexpansion",
                "statistics": {
                    "ratings": {
                        "usersrated": { "@value": "125" },
                        "owned": { "@value": "450" }
                    }
                }
            }
        }
        """;

        var (usersRated, owned) = BggRawSnapshotParser.ExtractCommunityStatsFromJson(json);

        Assert.Equal(125, usersRated);
        Assert.Equal(450, owned);
    }

    [Fact]
    public void MeetsExpansionCommunityThresholdFromJson_WhenHasSpanishPublisher_AcceptsRegardlessOfLowRatings()
    {
        string json = """
        {
            "item": {
                "@id": "5001",
                "@type": "boardgameexpansion",
                "statistics": {
                    "ratings": {
                        "usersrated": { "@value": "8" },
                        "owned": { "@value": "15" }
                    }
                },
                "versions": {
                    "item": {
                        "name": { "@value": "Expansión en Español" },
                        "link": [
                            { "@type": "language", "@value": "Spanish" },
                            { "@type": "boardgamepublisher", "@value": "Devir" }
                        ]
                    }
                }
            }
        }
        """;

        bool result = BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(json, minUsersRated: 30, minOwned: 100);

        Assert.True(result);
    }

    [Fact]
    public void MeetsExpansionCommunityThresholdFromJson_WhenRatingsAboveUsersRatedThreshold_Accepts()
    {
        string json = """
        {
            "item": {
                "@id": "5002",
                "@type": "boardgameexpansion",
                "statistics": {
                    "ratings": {
                        "usersrated": { "@value": "35" },
                        "owned": { "@value": "60" }
                    }
                }
            }
        }
        """;

        bool result = BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(json, minUsersRated: 30, minOwned: 100);

        Assert.True(result);
    }

    [Fact]
    public void MeetsExpansionCommunityThresholdFromJson_WhenOwnedAboveThreshold_Accepts()
    {
        string json = """
        {
            "item": {
                "@id": "5003",
                "@type": "boardgameexpansion",
                "statistics": {
                    "ratings": {
                        "usersrated": { "@value": "15" },
                        "owned": { "@value": "120" }
                    }
                }
            }
        }
        """;

        bool result = BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(json, minUsersRated: 30, minOwned: 100);

        Assert.True(result);
    }

    [Fact]
    public void MeetsExpansionCommunityThresholdFromJson_WhenBelowBothThresholdsAndNoSpanishEdition_Rejects()
    {
        string json = """
        {
            "item": {
                "@id": "5004",
                "@type": "boardgameexpansion",
                "statistics": {
                    "ratings": {
                        "usersrated": { "@value": "12" },
                        "owned": { "@value": "40" }
                    }
                }
            }
        }
        """;

        bool result = BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(json, minUsersRated: 30, minOwned: 100);

        Assert.False(result);
    }

    [Fact]
    public void MeetsExpansionCommunityThresholdFromJson_WhenNoStatisticsSection_AcceptsConditionally()
    {
        string json = """
        {
            "item": {
                "@id": "5005",
                "@type": "boardgameexpansion",
                "name": { "@value": "Expansion Without Stats" }
            }
        }
        """;

        bool result = BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(json);

        Assert.True(result);
    }
}
