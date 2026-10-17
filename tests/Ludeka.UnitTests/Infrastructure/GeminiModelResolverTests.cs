using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiModelResolverTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public int RequestCount { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task GetCandidateModelsAsync_DiscoversFiltersAndOrdersModelsByCostTierAndVersion()
    {
        // Arrange: Respuesta realista de GET /v1beta/models con modelos válidos, deprecados (2.5, 3.1) y especializados (tts, live, image, pro, embedding)
        var modelsJson = """
        {
          "models": [
            {
              "name": "models/gemini-3.5-flash",
              "supportedGenerationMethods": ["generateContent", "countTokens"]
            },
            {
              "name": "models/gemini-3.8-flash-tts",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.8-live",
              "supportedGenerationMethods": ["bidiGenerateContent"]
            },
            {
              "name": "models/gemini-3.1-flash-lite",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-2.5-flash",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.6-flash",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.5-flash-lite",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.8-flash",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.7-flash",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.1-pro-preview",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-3.1-flash-image",
              "supportedGenerationMethods": ["generateContent"]
            },
            {
              "name": "models/gemini-embedding-001",
              "supportedGenerationMethods": ["embedContent"]
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("/v1beta/models?key=test-api-key", req.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
            };
        });

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance);

        // Act
        var candidates = await resolver.GetCandidateModelsAsync();

        // Assert:
        // 1º escalón más barato (flash-lite >= 3.5): gemini-3.5-flash-lite
        // 2º escalón (flash >= 3.5 de más nuevo a más antiguo): 3.8 -> 3.7 -> 3.6 -> 3.5
        Assert.Equal(
            [
                "gemini-3.5-flash-lite",
                "gemini-3.8-flash",
                "gemini-3.7-flash",
                "gemini-3.6-flash",
                "gemini-3.5-flash"
            ],
            candidates);
    }

    [Fact]
    public async Task GetCandidateModelsAsync_WhenFutureFlashLiteExists_PlacesNewestFlashLiteFirst()
    {
        // Arrange: Si Google lanza gemini-3.8-flash-lite y gemini-4.0-flash en el futuro
        var modelsJson = """
        {
          "models": [
            { "name": "models/gemini-4.0-flash", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.5-flash-lite", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.8-flash-lite", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.8-flash", "supportedGenerationMethods": ["generateContent"] }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
        });

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance);

        // Act
        var candidates = await resolver.GetCandidateModelsAsync();

        // Assert: Todos los flash-lite van primero (del más reciente al más antiguo), luego los flash
        Assert.Equal(
            [
                "gemini-3.8-flash-lite",
                "gemini-3.5-flash-lite",
                "gemini-4.0-flash",
                "gemini-3.8-flash"
            ],
            candidates);
    }

    [Fact]
    public async Task GetCandidateModelsAsync_CachesDiscoveredModelsWithinTtl()
    {
        var modelsJson = """
        {
          "models": [
            { "name": "models/gemini-3.5-flash-lite", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.8-flash", "supportedGenerationMethods": ["generateContent"] }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
        });

        var timeProvider = new FakeTimeProvider();
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance,
            timeProvider);

        // Primera llamada: consulta HTTP
        var first = await resolver.GetCandidateModelsAsync();
        // Segunda llamada 1 hora después: usa caché sin llamar a HTTP
        timeProvider.Advance(TimeSpan.FromHours(1));
        var second = await resolver.GetCandidateModelsAsync();

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(first, second);

        // Pasadas 13 horas: refresca caché
        timeProvider.Advance(TimeSpan.FromHours(12));
        await resolver.GetCandidateModelsAsync();
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ReportModelFailure_When503Or429_MovesModelToEndDuringCooldownAndRestoresAfterExpiry()
    {
        var modelsJson = """
        {
          "models": [
            { "name": "models/gemini-3.5-flash-lite", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.8-flash", "supportedGenerationMethods": ["generateContent"] }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
        });

        var timeProvider = new FakeTimeProvider();
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance,
            timeProvider);

        // Inicialmente gemini-3.5-flash-lite es el primero
        var initial = await resolver.GetCandidateModelsAsync();
        Assert.Equal(["gemini-3.5-flash-lite", "gemini-3.8-flash"], initial);

        // Reportamos saturación 503 en gemini-3.5-flash-lite
        resolver.ReportModelFailure("gemini-3.5-flash-lite", HttpStatusCode.ServiceUnavailable);

        // Durante el cooldown (5 min), gemini-3.8-flash pasa a ser el primero y 3.5-flash-lite queda al final como último recurso
        var duringCooldown = await resolver.GetCandidateModelsAsync();
        Assert.Equal(["gemini-3.8-flash", "gemini-3.5-flash-lite"], duringCooldown);

        // Tras expirar los 5 minutos de cooldown, gemini-3.5-flash-lite recupera la primera posición por ser el más barato
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        var afterCooldown = await resolver.GetCandidateModelsAsync();
        Assert.Equal(["gemini-3.5-flash-lite", "gemini-3.8-flash"], afterCooldown);
    }

    [Fact]
    public async Task ReportModelFailure_When404NotFound_ExcludesModelFromCandidates()
    {
        var modelsJson = """
        {
          "models": [
            { "name": "models/gemini-3.5-flash-lite", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.8-flash", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-3.6-flash", "supportedGenerationMethods": ["generateContent"] }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
        });

        var timeProvider = new FakeTimeProvider();
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance,
            timeProvider);

        await resolver.GetCandidateModelsAsync();
        resolver.ReportModelFailure("gemini-3.6-flash", HttpStatusCode.NotFound);

        var candidates = await resolver.GetCandidateModelsAsync();
        Assert.Equal(["gemini-3.5-flash-lite", "gemini-3.8-flash"], candidates);
    }

    [Fact]
    public async Task GetCandidateModelsAsync_WhenApiFails_ReturnsStaticFallbackChainGracefully()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            Model = "auto"
        });

        var resolver = new GeminiModelResolver(
            new HttpClient(handler),
            options,
            NullLogger<GeminiModelResolver>.Instance);

        var candidates = await resolver.GetCandidateModelsAsync();

        Assert.Equal(GeminiOptions.DefaultFallbackModels, candidates);
    }
}
