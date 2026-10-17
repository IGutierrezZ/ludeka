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

    [Fact]
    public async Task SuggestMatchAsync_WhenPrimaryModelFails_CascadesToNextCandidateFromResolver()
    {
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "real-key",
            Model = "auto"
        });

        var resolver = new StubResolver(["gemini-3.5-flash-lite", "gemini-3.8-flash"]);
        var responseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\"suggestedBggId\": 367209, \"suggestedTitle\": \"Galactic Cruise\", \"reasoning\": \"Edición en español de Maldito Games.\"}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("gemini-3.5-flash-lite"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Overloaded")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new GeminiReleaseMatcherService(
            new HttpClient(handler),
            options,
            NullLogger<GeminiReleaseMatcherService>.Instance,
            resolver);

        var result = await service.SuggestMatchAsync("Crucero Estelar Inédito", "Maldito Games", 55m, null);

        Assert.NotNull(result);
        Assert.Equal(367209, result.SuggestedBggId);
        Assert.Equal("Galactic Cruise", result.SuggestedTitle);
        Assert.Contains("gemini-3.5-flash-lite", resolver.FailedModels);
        Assert.Contains("gemini-3.8-flash", resolver.SucceededModels);
    }

    private sealed class StubResolver(System.Collections.Generic.IReadOnlyList<string> models) : IGeminiModelResolver
    {
        public System.Collections.Generic.List<string> FailedModels { get; } = [];
        public System.Collections.Generic.List<string> SucceededModels { get; } = [];

        public Task<System.Collections.Generic.IReadOnlyList<string>> GetCandidateModelsAsync(System.Threading.CancellationToken ct = default)
            => Task.FromResult(models);

        public void ReportModelFailure(string model, System.Net.HttpStatusCode? statusCode)
            => FailedModels.Add(model);

        public void ReportModelSuccess(string model)
            => SucceededModels.Add(model);
    }

    private sealed class FakeHandler(System.Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
