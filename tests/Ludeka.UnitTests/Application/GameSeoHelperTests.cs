using System.Collections.Generic;
using System.Text.Json;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Seo;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GameSeoHelperTests
{
    private static GameDetailDto CreateSampleGameDto()
    {
        var game = new Game(
            bggId: 266192,
            originalTitle: "Wingspan",
            spanishTitle: "Wingspan",
            designer: "Elizabeth Hargrave",
            publisher: "Stonemaier Games",
            yearPublished: 2019,
            coverImageUrl: "https://cf.geekdo-images.com/sample.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/sample_t.jpg",
            description: "Juego de mesa sobre creación de reservas de aves.",
            bggRating: 8.1,
            bggRank: 25,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(BoxAge: 14, CommunityAge: 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(40, 70, 25),
            scalability: new List<ScalabilityEntry>
            {
                new(1, "1J", ScalabilityStatus.Recommended, 20, 40, 5),
                new(2, "2J", ScalabilityStatus.MustPlay, 100, 10, 2),
                new(3, "3J", ScalabilityStatus.Recommended, 80, 20, 3),
                new(4, "4J", ScalabilityStatus.Recommended, 50, 30, 10),
                new(5, "5J", ScalabilityStatus.NotRecommended, 10, 20, 60)
            }
        );
        game.UpdateSpanishPublisher("Maldito Games");

        return GameDetailDto.FromEntity(game);
    }

    [Fact]
    public void BuildMetaDescription_ShouldIncludeEssentialGameInformation()
    {
        var dto = CreateSampleGameDto();

        var description = GameSeoHelper.BuildMetaDescription(dto);

        Assert.NotNull(description);
        Assert.Contains("Wingspan (2019)", description);
        Assert.Contains("40–70 min", description);
        Assert.Contains("1–5 jugadores", description);
        Assert.Contains("Elizabeth Hargrave", description);
        Assert.Contains("Maldito Games", description);
        Assert.True(description.Length <= 165, $"La descripción ({description.Length} caracteres) supera el límite ideal de 165 caracteres para Google.");
    }

    [Fact]
    public void BuildStructuredDataJson_ShouldContainBoardGameAndFAQPageEntities()
    {
        var dto = CreateSampleGameDto();
        var canonicalUrl = $"https://ludeka.es/juegos/{dto.Slug}";

        var json = GameSeoHelper.BuildStructuredDataJson(dto, canonicalUrl);

        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("https://schema.org", root.GetProperty("@context").GetString());

        var graph = root.GetProperty("@graph");
        Assert.Equal(2, graph.GetArrayLength());

        // Entidad 1: BoardGame
        var boardGame = graph[0];
        Assert.Equal("BoardGame", boardGame.GetProperty("@type").GetString());
        Assert.Equal("Wingspan", boardGame.GetProperty("name").GetString());
        Assert.Equal("https://cf.geekdo-images.com/sample.jpg", boardGame.GetProperty("image").GetString());
        Assert.Equal(14, boardGame.GetProperty("suggestedMinAge").GetInt32());

        var players = boardGame.GetProperty("numberOfPlayers");
        Assert.Equal(1, players.GetProperty("minValue").GetInt32());
        Assert.Equal(5, players.GetProperty("maxValue").GetInt32());

        // Entidad 2: FAQPage para capturar búsquedas de preguntas frecuentes
        var faqPage = graph[1];
        Assert.Equal("FAQPage", faqPage.GetProperty("@type").GetString());
        var questions = faqPage.GetProperty("mainEntity");
        Assert.True(questions.GetArrayLength() >= 3);

        var firstQ = questions[0];
        Assert.Equal("Question", firstQ.GetProperty("@type").GetString());
        Assert.Contains("Wingspan", firstQ.GetProperty("name").GetString());
        Assert.NotNull(firstQ.GetProperty("acceptedAnswer").GetProperty("text").GetString());
    }

    [Fact]
    public void BuildStructuredDataJson_FAQShouldAnswerTypicalSearchQueries()
    {
        var dto = CreateSampleGameDto();
        var canonicalUrl = $"https://ludeka.es/juegos/{dto.Slug}";

        var json = GameSeoHelper.BuildStructuredDataJson(dto, canonicalUrl);
        using var doc = JsonDocument.Parse(json);
        var faq = doc.RootElement.GetProperty("@graph")[1];
        var questions = faq.GetProperty("mainEntity");

        var questionsText = new List<string>();
        for (int i = 0; i < questions.GetArrayLength(); i++)
        {
            questionsText.Add(questions[i].GetProperty("name").GetString()!);
        }

        Assert.Contains(questionsText, q => q.Contains("¿Cuántos jugadores pueden jugar a"));
        Assert.Contains(questionsText, q => q.Contains("¿Cuánto dura una partida de"));
        Assert.Contains(questionsText, q => q.Contains("¿A partir de qué edad se recomienda jugar a"));
        Assert.Contains(questionsText, q => q.Contains("¿Quién diseñó"));
    }
}
