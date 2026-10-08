using System.Collections.Generic;
using System.Linq;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class RegionalPublisherMatcherTests
{
    [Theory]
    [InlineData("Devir", "Devir Iberia")]
    [InlineData("Devir Iberia", "Devir Iberia")]
    [InlineData("Maldito Games", "Maldito Games")]
    [InlineData("MasQueOca", "Ediciones MasQueOca")]
    [InlineData("Ediciones MasQueOca", "Ediciones MasQueOca")]
    [InlineData("Asmodee", "Asmodee Ibérica")]
    [InlineData("Tranjis Games", "Tranjis Games")]
    [InlineData("SD Games", "SD Games")]
    [InlineData("Zacatrus", "Zacatrus!")]
    [InlineData("TCG Factory", "TCG Factory")]
    [InlineData("Gen-X Games", "Gen-X Games")]
    [InlineData("Lúdilo", "Lúdilo")]
    [InlineData("Ludilo", "Lúdilo")]
    [InlineData("Lúdilo Games", "Lúdilo")]
    public void Match_WithKnownSpanishPublishers_ReturnsCanonicalSpanishPublisher(string input, string expected)
    {
        var (spanish, regional) = RegionalPublisherMatcher.Match(new[] { input });
        Assert.Equal(expected, spanish);
        Assert.Contains(regional, r => r.CountryCode == "ES" && r.PublisherName == expected);
    }

    [Fact]
    public void Match_WithForeignPublishersOnly_ReturnsNullSpanishPublisher()
    {
        var (spanish, regional) = RegionalPublisherMatcher.Match(new[] { "Stonemaier Games", "Kosmos", "Days of Wonder", "Feuerland Spiele" });
        Assert.Null(spanish);
        Assert.Empty(regional);
    }

    [Fact]
    public void Match_WithNullOrEmpty_ReturnsNullAndEmpty()
    {
        var (s1, r1) = RegionalPublisherMatcher.Match(null);
        Assert.Null(s1);
        Assert.Empty(r1);

        var (s2, r2) = RegionalPublisherMatcher.Match(new List<string>());
        Assert.Null(s2);
        Assert.Empty(r2);
    }

    [Fact]
    public void Match_WithLatinAmericanPublishers_ReturnsAccurateEntries()
    {
        var publishers = new[]
        {
            "Stonemaier Games",
            "Maldito Games",
            "Buró de Juegos",
            "Ruibal Juegos",
            "Fractal Juegos",
            "El Troquel",
            "Matufia Juegos"
        };

        var (spanish, regional) = RegionalPublisherMatcher.Match(publishers);

        Assert.Equal("Maldito Games", spanish);
        Assert.NotEmpty(regional);
        Assert.Contains(regional, r => r.CountryCode == "ES");
        Assert.Contains(regional, r => r.CountryCode == "AR" && r.PublisherName == "Ruibal Juegos");
        Assert.Contains(regional, r => r.CountryCode == "CL" && r.PublisherName == "Fractal Juegos");
        Assert.Contains(regional, r => r.CountryCode == "MX" && r.PublisherName == "El Troquel");
        Assert.Contains(regional, r => r.CountryCode == "UY" && r.PublisherName == "Matufia Juegos");
    }

    [Fact]
    public void Match_WhenDevirIsPresent_ExpandsToLatinAmericanSubsidiaries()
    {
        var (spanish, regional) = RegionalPublisherMatcher.Match(new[] { "Devir", "Kosmos" });

        Assert.Equal("Devir Iberia", spanish);
        Assert.Contains(regional, r => r.CountryCode == "ES");
        Assert.Contains(regional, r => r.CountryCode == "MX" && r.PublisherName == "Devir México");
        Assert.Contains(regional, r => r.CountryCode == "CL" && r.PublisherName == "Devir Chile");
        Assert.Contains(regional, r => r.CountryCode == "CO" && r.PublisherName == "Devir Colombia");
        Assert.Contains(regional, r => r.CountryCode == "PE" && r.PublisherName == "Devir Perú");
    }
}
