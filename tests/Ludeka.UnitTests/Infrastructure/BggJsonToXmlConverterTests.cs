using System;
using System.Linq;
using System.Xml.Linq;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggJsonToXmlConverterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"notFound\":true}")]
    [InlineData("{\"notFound\":true,\"bggId\":123}")]
    public void ConvertToItemElement_WhenInvalidOrNotFound_ReturnsNull(string? rawJson)
    {
        var result = BggJsonToXmlConverter.ConvertToItemElement(rawJson);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertToItemElement_RoundTrip_PreservesAllQualityAndDnaMetadata()
    {
        // XML original con todos los campos de calidad y ADN lúdico
        string originalXml = """
            <item type="boardgame" id="13">
                <name type="primary" sortindex="1" value="Catan" />
                <minplayers value="3" />
                <maxplayers value="4" />
                <playingtime value="75" />
                <minplaytime value="60" />
                <maxplaytime value="90" />
                <minage value="10" />
                <link type="boardgamesubdomain" id="5497" value="Strategy Games" />
                <link type="boardgamecategory" id="1026" value="Negotiation" />
                <link type="boardgamecategory" id="1021" value="Economic" />
                <link type="boardgamepublisher" id="29734" value="Devir" />
                <poll name="suggested_numplayers" title="User Suggested Number of Players" totalvotes="100">
                    <results numplayers="2">
                        <result value="Best" numvotes="5" />
                        <result value="Recommended" numvotes="15" />
                        <result value="Not Recommended" numvotes="80" />
                    </results>
                    <results numplayers="3">
                        <result value="Best" numvotes="40" />
                        <result value="Recommended" numvotes="50" />
                        <result value="Not Recommended" numvotes="10" />
                    </results>
                    <results numplayers="4">
                        <result value="Best" numvotes="85" />
                        <result value="Recommended" numvotes="12" />
                        <result value="Not Recommended" numvotes="3" />
                    </results>
                </poll>
            </item>
            """;

        var originalDoc = XDocument.Parse(originalXml);
        var originalItem = originalDoc.Root!;

        // 1. Convertir a JSON como hace BggXmlToJsonConverter
        string rawJson = BggXmlToJsonConverter.ConvertToJson(originalItem);
        Assert.False(string.IsNullOrWhiteSpace(rawJson));

        // 2. Reconstituir a XElement usando BggJsonToXmlConverter
        var reconstitutedItem = BggJsonToXmlConverter.ConvertToItemElement(rawJson);
        Assert.NotNull(reconstitutedItem);
        Assert.Equal("item", reconstitutedItem.Name.LocalName);
        Assert.Equal("13", reconstitutedItem.Attribute("id")?.Value);
        Assert.Equal("boardgame", reconstitutedItem.Attribute("type")?.Value);

        // 3. Evaluar metadatos de calidad sobre ambos y verificar equivalencia exacta
        var originalQuality = BggXmlParser.ParseQualityMetadata(originalItem);
        var reconstitutedQuality = BggXmlParser.ParseQualityMetadata(reconstitutedItem);

        Assert.Equal(originalQuality.MinPlayTime, reconstitutedQuality.MinPlayTime);
        Assert.Equal(originalQuality.MaxPlayTime, reconstitutedQuality.MaxPlayTime);
        Assert.Equal(originalQuality.PlayingTime, reconstitutedQuality.PlayingTime);
        Assert.Equal(originalQuality.MinPlayers, reconstitutedQuality.MinPlayers);
        Assert.Equal(originalQuality.MaxPlayers, reconstitutedQuality.MaxPlayers);
        Assert.Equal(originalQuality.Footprint, reconstitutedQuality.Footprint);
        Assert.Equal(originalQuality.SpanishPublisher, reconstitutedQuality.SpanishPublisher);
        Assert.Equal(originalQuality.Scalability.Count, reconstitutedQuality.Scalability.Count);

        for (int i = 0; i < originalQuality.Scalability.Count; i++)
        {
            var orig = originalQuality.Scalability[i];
            var recon = reconstitutedQuality.Scalability[i];
            Assert.Equal(orig.PlayerCount, recon.PlayerCount);
            Assert.Equal(orig.Status, recon.Status);
            Assert.Equal(orig.BestVotes, recon.BestVotes);
            Assert.Equal(orig.RecommendedVotes, recon.RecommendedVotes);
            Assert.Equal(orig.NotRecommendedVotes, recon.NotRecommendedVotes);
        }

        // 4. Evaluar ADN lúdico sobre ambos
        var originalDna = BggXmlParser.InferGameDna(originalItem, originalQuality.Scalability);
        var reconstitutedDna = BggXmlParser.InferGameDna(reconstitutedItem, reconstitutedQuality.Scalability);

        Assert.Equal(originalDna.Confrontation, reconstitutedDna.Confrontation);
        Assert.Equal(originalDna.Style, reconstitutedDna.Style);
        Assert.Equal(originalDna.IsSolo, reconstitutedDna.IsSolo);
    }
}
