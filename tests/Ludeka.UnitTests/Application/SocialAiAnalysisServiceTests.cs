using System;
using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class SocialAiAnalysisServiceTests
{
    private readonly GeminiSocialAnalysisService _service;

    public SocialAiAnalysisServiceTests()
    {
        var options = Options.Create(new GeminiOptions
        {
            Simulate = true,
            ApiKey = null
        });

        _service = new GeminiSocialAnalysisService(
            new HttpClient(),
            options,
            NullLogger<GeminiSocialAnalysisService>.Instance);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenTextDescribesGiveaway_ClassifiesAsGiveaway()
    {
        // Arrange
        var text = "¡Gran sorteo de «Ark Nova» con Maldito Games! Participa hasta el 25 de octubre mencionando a dos amigos.";

        // Act
        var result = await _service.AnalyzeTextAsync(text, "Maldito Games");

        // Assert
        Assert.Equal(SocialSubmissionType.Giveaway, result.DetectedType);
        Assert.Equal("Ark Nova", result.SuggestedGameTitle);
        Assert.Equal("Maldito Games", result.OrganizerOrAuthor);
        Assert.NotNull(result.EventOrReleaseDate);
        Assert.Equal(10, result.EventOrReleaseDate.Value.Month);
        Assert.Equal(25, result.EventOrReleaseDate.Value.Day);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenTextDescribesEvent_ClassifiesAsEventWithDates()
    {
        // Arrange
        var text = "Ven a las Jornadas «InterOcio» en Madrid del 15 al 17 de marzo. ¡Entradas ya a la venta!";

        // Act
        var result = await _service.AnalyzeTextAsync(text, "InterOcio Oficial");

        // Assert
        Assert.Equal(SocialSubmissionType.BoardGameEvent, result.DetectedType);
        Assert.Equal("InterOcio", result.SuggestedGameTitle);
        Assert.Equal("Madrid", result.Location);
        Assert.NotNull(result.EventOrReleaseDate);
        Assert.NotNull(result.EventEndDate);
        Assert.Equal(15, result.EventOrReleaseDate.Value.Day);
        Assert.Equal(17, result.EventEndDate.Value.Day);
        Assert.Equal(3, result.EventOrReleaseDate.Value.Month);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenTextDescribesWeeklyRelease_ClassifiesAsRelease()
    {
        // Arrange
        var text = "¡Nueva novedad editorial de Devir! Ya en tiendas «Cascadia», el aclamado juego de fauna y naturaleza.";

        // Act
        var result = await _service.AnalyzeTextAsync(text, "Devir Iberia");

        // Assert
        Assert.Equal(SocialSubmissionType.WeeklyRelease, result.DetectedType);
        Assert.Equal("Cascadia", result.SuggestedGameTitle);
        Assert.Equal("Devir Iberia", result.OrganizerOrAuthor);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenTextDescribesVideo_DetectsCategoryAndBadge()
    {
        // Arrange
        var text = "Hoy os traemos una completa partida a 2 jugadores de «Dune Imperium» con nuestra opinión final.";

        // Act
        var result = await _service.AnalyzeTextAsync(text, "MeepleMania");

        // Assert
        Assert.Equal(SocialSubmissionType.MediaItem, result.DetectedType);
        Assert.Equal("Dune Imperium", result.SuggestedGameTitle);
        Assert.Equal(MediaCategory.Gameplay, result.MediaCategory);
        Assert.Equal("Partida a 2", result.PlayerCountBadge);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenTextMentionsCollaborator_ExtractsCollaborator()
    {
        // Arrange
        var text = "Sorteo del juego «Wingspan» en colaboración con @zacatrus para toda la comunidad.";

        // Act
        var result = await _service.AnalyzeTextAsync(text, "Asociación Lúdica");

        // Assert
        Assert.Equal("zacatrus", result.Collaborator);
        Assert.Equal("Wingspan", result.SuggestedGameTitle);
    }
}
