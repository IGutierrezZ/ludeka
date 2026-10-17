using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiSocialAnalysisServiceTests
{
    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_SimulatedMode_ReturnsHeuristicMultimodal()
    {
        // Arrange
        var options = Options.Create(new GeminiOptions
        {
            Simulate = true,
            ApiKey = "fake-key"
        });

        var client = new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: "¡Gran sorteo de Ark Nova! Menciona a 2 amigos. Válido sólo en península.",
            basesImageBytes: [1, 2, 3],
            basesImageMimeType: "image/jpeg",
            coverImageBytes: [4, 5, 6],
            coverImageMimeType: "image/jpeg",
            authorOrChannel: "Maldito Games");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SocialSubmissionType.Giveaway, result.DetectedType);
        Assert.Equal("Maldito Games", result.OrganizerOrAuthor);
        Assert.Contains("Península", result.TerritorialScope ?? result.Location ?? string.Empty);
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_ValidGeminiApiResponse_ParsesBoundingBoxAndTerritorialScope()
    {
        // Arrange: Respuesta estructurada de Gemini con bounding box y ámbito territorial
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Dune Imperium Uprising",
          "organizerOrAuthor": "Asmodee España",
          "collaborator": "@zacatrus",
          "suggestedGameTitle": "Dune Imperium",
          "eventOrReleaseDateIso": "2026-10-15T23:59:59Z",
          "cropBoundingBox": [70, 40, 930, 960],
          "territorialScope": "España (Península y Baleares)",
          "notes": "Sorteo activo en Instagram"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "real-format-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var client = new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: "Bases del sorteo de Dune",
            basesImageBytes: [1, 2, 3],
            basesImageMimeType: "image/png",
            coverImageBytes: [4, 5, 6],
            coverImageMimeType: "image/png",
            authorOrChannel: "Asmodee");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SocialSubmissionType.Giveaway, result.DetectedType);
        Assert.Equal("Sorteo Dune Imperium Uprising", result.Title);
        Assert.Equal("Asmodee España", result.OrganizerOrAuthor);
        Assert.Equal("@zacatrus", result.Collaborator);
        Assert.Equal("España (Península y Baleares)", result.TerritorialScope);

        Assert.NotNull(result.CropBoundingBox);
        Assert.Equal(70, result.CropBoundingBox.YMin);
        Assert.Equal(40, result.CropBoundingBox.XMin);
        Assert.Equal(930, result.CropBoundingBox.YMax);
        Assert.Equal(960, result.CropBoundingBox.XMax);
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_ValidGeminiApiResponse_ParsesExtractedText()
    {
        // Arrange
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Nippon: Zaibatsu",
          "organizerOrAuthor": "Turol Games",
          "collaborator": "Maldito Games",
          "suggestedGameTitle": "Nippon: Zaibatsu",
          "eventOrReleaseDateIso": "2026-10-10T23:59:59Z",
          "extractedText": "¡Sorteamos una copia de Nippon: Zaibatsu!\nRequisitos:\n1. Seguir a @turolgames y @malditogames\n2. Mencionar a 2 amigos\nFin: 10 de octubre",
          "notes": "Sorteo conjunto"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "real-format-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var client = new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: null,
            basesImageBytes: [1, 2, 3],
            basesImageMimeType: "image/jpeg",
            authorOrChannel: "Turol Games");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("¡Sorteamos una copia de Nippon: Zaibatsu!\nRequisitos:\n1. Seguir a @turolgames y @malditogames\n2. Mencionar a 2 amigos\nFin: 10 de octubre", result.ExtractedText);
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_ApiReturnsHttpError_FallsBackToHeuristicWithoutThrowing()
    {
        // Arrange: API falla con 500 Internal Server Error
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var client = new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error")
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: "Sorteo semanal en marcha",
            basesImageBytes: [1, 2],
            basesImageMimeType: "image/jpeg",
            coverImageBytes: [3, 4],
            coverImageMimeType: "image/jpeg",
            authorOrChannel: "Canal Juegos");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SocialSubmissionType.Giveaway, result.DetectedType);
        Assert.Equal("Canal Juegos", result.OrganizerOrAuthor);
        Assert.Null(result.CropBoundingBox);
        Assert.Contains("No se pudo recortar", result.Notes);
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_WhenPrimaryModelReturns503_CascadesToSecondaryModelAndSucceeds()
    {
        // Arrange: Primer modelo devuelve 503 Overloaded, segundo modelo responde con éxito
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Asesinato en el Hotel",
          "organizerOrAuthor": "Casos Criminales",
          "suggestedGameTitle": "Asesinato en el Gran Hotel",
          "cropBoundingBox": [37, 314, 977, 685],
          "notes": "Sorteo verificado por visión"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        int callCount = 0;
        var client = new HttpClient(new FakeHttpHandler(req =>
        {
            callCount++;
            var url = req.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("gemini-3.5-flash-lite"))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"The model is overloaded. Please try again later.\"}}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
            };
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: "Sorteo del juego Asesinato",
            basesImageBytes: [1, 2],
            basesImageMimeType: "image/jpeg",
            coverImageBytes: [3, 4],
            coverImageMimeType: "image/jpeg",
            authorOrChannel: "Casos Criminales");

        // Assert
        Assert.NotNull(result);
        Assert.True(callCount >= 2);
        Assert.Equal("Sorteo Asesinato en el Hotel", result.Title);
        Assert.NotNull(result.CropBoundingBox);
        Assert.Equal(37, result.CropBoundingBox.YMin);
        Assert.Equal(314, result.CropBoundingBox.XMin);
        Assert.Equal(977, result.CropBoundingBox.YMax);
        Assert.Equal(685, result.CropBoundingBox.XMax);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenPrimaryModelFails_CascadesToSecondaryModelAndSucceeds()
    {
        // Arrange
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Trampitas",
          "organizerOrAuthor": "Trampitas y Rewines",
          "notes": "Sorteo 1000 seguidores"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-api-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        int callCount = 0;
        var client = new HttpClient(new FakeHttpHandler(req =>
        {
            callCount++;
            var url = req.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("gemini-3.5-flash-lite"))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"Overloaded\"}}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
            };
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeTextAsync("Sorteamos pack de juegos", "Trampitas y Rewines");

        // Assert
        Assert.NotNull(result);
        Assert.True(callCount >= 2);
        Assert.Equal("Sorteo Trampitas", result.Title);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WhenCallingGeminiApi_InjectsCurrentDateAndYearReferenceIntoPrompt()
    {
        // Arrange
        string? capturedBody = null;
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Otoño",
          "organizerOrAuthor": "Devir",
          "eventOrReleaseDateIso": "2026-10-02T23:59:59Z"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var client = new HttpClient(new FakeHttpHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
            };
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeTextAsync("¡Sorteo! Tienes hasta el 2 de octubre.", "Devir");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedBody);
        var todayIso = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var currentYear = DateTime.UtcNow.Year.ToString();
        Assert.Contains($"Fecha actual de referencia para el an\\u00E1lisis: {todayIso}", capturedBody);
        Assert.Contains($"(a\\u00F1o en curso: {currentYear})", capturedBody);
        Assert.Contains("NUNCA inventes o asumas a\\u00F1os pasados", capturedBody);
    }

    [Fact]
    public async Task AnalyzeMultimodalAsync_WhenCallingGeminiApi_InjectsCurrentDateAndYearReferenceIntoPrompt()
    {
        // Arrange
        string? capturedBody = null;
        var innerJson = """
        {
          "detectedType": "Giveaway",
          "title": "Sorteo Cartel",
          "organizerOrAuthor": "Maldito Games",
          "eventOrReleaseDateIso": "2026-10-02T23:59:59Z"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var client = new HttpClient(new FakeHttpHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
            };
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance);

        // Act
        var result = await service.AnalyzeMultimodalAsync(
            text: "Cartel con bases del sorteo hasta el 2 de octubre",
            basesImageBytes: [10, 20],
            basesImageMimeType: "image/jpeg",
            coverImageBytes: null,
            coverImageMimeType: null,
            authorOrChannel: "Maldito Games");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedBody);
        var todayIso = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var currentYear = DateTime.UtcNow.Year.ToString();
        Assert.Contains($"Fecha actual de referencia para el an\\u00E1lisis: {todayIso}", capturedBody);
        Assert.Contains($"(a\\u00F1o en curso: {currentYear})", capturedBody);
        Assert.Contains("NUNCA infieras o asumas a\\u00F1os pasados", capturedBody);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WithModelResolver_UsesDynamicChainAndReportsFailureAndSuccess()
    {
        var options = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "test-key",
            Model = "auto"
        });

        var resolver = new StubResolver(["gemini-3.5-flash-lite", "gemini-3.8-flash"]);
        var innerJson = """
        {
          "detectedType": "News",
          "title": "Anuncio de preventa de Brass",
          "organizerOrAuthor": "Maldito Games"
        }
        """;

        var geminiEnvelopeJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}}
                  }
                ]
              }
            }
          ]
        }
        """;

        var client = new HttpClient(new FakeHttpHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("gemini-3.5-flash-lite"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Model retired")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiEnvelopeJson, Encoding.UTF8, "application/json")
            };
        }));

        var service = new GeminiSocialAnalysisService(client, options, NullLogger<GeminiSocialAnalysisService>.Instance, resolver);

        var result = await service.AnalyzeTextAsync("Preventa de Brass disponible esta semana", "Maldito Games");

        Assert.NotNull(result);
        Assert.Equal("Anuncio de preventa de Brass", result.Title);
        Assert.Contains(("gemini-3.5-flash-lite", (HttpStatusCode?)HttpStatusCode.NotFound), resolver.Failures);
        Assert.Contains("gemini-3.8-flash", resolver.Successes);
    }

    private sealed class StubResolver(IReadOnlyList<string> models) : IGeminiModelResolver
    {
        public List<(string Model, HttpStatusCode? StatusCode)> Failures { get; } = [];
        public List<string> Successes { get; } = [];

        public Task<IReadOnlyList<string>> GetCandidateModelsAsync(CancellationToken ct = default)
            => Task.FromResult(models);

        public void ReportModelFailure(string model, HttpStatusCode? statusCode)
            => Failures.Add((model, statusCode));

        public void ReportModelSuccess(string model)
            => Successes.Add(model);
    }
}
