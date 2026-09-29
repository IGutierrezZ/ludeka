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
}
