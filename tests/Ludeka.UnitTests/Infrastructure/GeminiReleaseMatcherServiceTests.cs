using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiReleaseMatcherServiceTests
{
    private readonly GeminiReleaseMatcherService _service;

    public GeminiReleaseMatcherServiceTests()
    {
        var options = Options.Create(new GeminiOptions
        {
            Simulate = true,
            ApiKey = null
        });

        _service = new GeminiReleaseMatcherService(
            new HttpClient(),
            options,
            NullLogger<GeminiReleaseMatcherService>.Instance);
    }

    [Theory]
    [InlineData("Crucero Galáctico", "Galactic Cruise", 367209)]
    [InlineData("Crucero Galactico (Edición en español)", "Galactic Cruise", 367209)]
    [InlineData("Los 12 trabajos de Hércules", "12 Labours of Hercules", 374945)]
    [InlineData("El Valle de los Molinos", "Windmill Valley", 416560)]
    [InlineData("The Hanging Gardens", "The Hanging Gardens", 32679)]
    [InlineData("Lacrimosa", "Lacrimosa", 348450)]
    [InlineData("Amphipolis", "Amphipolis", 396860)]
    public async Task SuggestMatchAsync_WithKnownTranslations_ReturnsCanonicalBggDetails(
        string rawTitle,
        string expectedTitle,
        int expectedBggId)
    {
        var result = await _service.SuggestMatchAsync(rawTitle, "Maldito Games", 45m, "Preventa");

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.SuggestedTitle);
        Assert.Equal(expectedBggId, result.SuggestedBggId);
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    [Fact]
    public async Task SuggestMatchAsync_WithUnknownTitle_ReturnsCleanFallback()
    {
        var result = await _service.SuggestMatchAsync("Juego Misterioso Desconocido", "Editorial X", 20m, null);

        Assert.NotNull(result);
        Assert.Equal("Juego Misterioso Desconocido", result.SuggestedTitle);
        Assert.Null(result.SuggestedBggId);
        Assert.Contains("Editorial X", result.Reasoning);
    }

    [Fact]
    public async Task SuggestMatchAsync_WithEmptyTitle_ReturnsEmptyResult()
    {
        var result = await _service.SuggestMatchAsync("   ", "Maldito Games", null, null);

        Assert.NotNull(result);
        Assert.Null(result.SuggestedTitle);
        Assert.Null(result.SuggestedBggId);
    }
}
