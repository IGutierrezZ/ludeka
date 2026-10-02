using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggXmlApiClientResilienceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> SentRequests { get; } = new();

        public void EnqueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static readonly string ValidCollectionXml = """
        <items totalitems="1" termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
          <item objecttype="thing" objectid="13" subtype="boardgame" collid="12345">
            <name sortindex="1">Catan</name>
            <yearpublished>1995</yearpublished>
            <thumbnail>https://example.com/catan.jpg</thumbnail>
            <status own="1" prevowned="0" fortrade="0" want="0" wanttoplay="0" wanttobuy="0" wishlist="0" preordered="0" lastmodified="2026-01-01 12:00:00" />
            <numplays>3</numplays>
          </item>
        </items>
        """;

    [Fact]
    public async Task FetchUserCollectionAsync_WhenBggReturns202AcceptedThen200_RetriesAndReturnsCollection()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Accepted));
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Accepted));
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidCollectionXml)
        });

        var client = new HttpClient(mockHandler);
        var options = Options.Create(new BggOptions
        {
            MaxPollingRetries = 5,
            PollingTimeoutSeconds = 15,
            InitialPollingDelaySeconds = 0 // Delay inmediato para pruebas rápidas
        });

        using var bggClient = new BggXmlApiClient(client, options);

        var reportedProgress = new ConcurrentBag<BggImportProgressReport>();
        var progress = new Progress<BggImportProgressReport>(p => reportedProgress.Add(p));

        // Act
        var result = await bggClient.FetchUserCollectionAsync("ludomaster", progress);
        await Task.Delay(50);

        // Assert
        Assert.Single(result);
        Assert.Equal(13, result[0].BggId);
        Assert.Equal("Catan", result[0].Title);
        Assert.True(result[0].IsOwned);

        Assert.Equal(3, mockHandler.SentRequests.Count);

        // Comprobar que se notificaron las fases correspondientes
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.Initializing);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.RequestingBgg);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.PreparingInBgg && p.CurrentAttempt == 1);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.PreparingInBgg && p.CurrentAttempt == 2);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.ProcessingItems && p.ItemsFound == 1);
    }

    [Fact]
    public async Task FetchUserCollectionAsync_WhenBggReturns429WithRetryAfter_WaitsAndRetriesSuccessfully()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var rateLimitResponse = new HttpResponseMessage((HttpStatusCode)429);
        rateLimitResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(0));
        mockHandler.EnqueueResponse(rateLimitResponse);

        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidCollectionXml)
        });

        var client = new HttpClient(mockHandler);
        var options = Options.Create(new BggOptions
        {
            MaxRateLimitRetries = 3,
            PollingTimeoutSeconds = 10,
            InitialPollingDelaySeconds = 0
        });

        using var bggClient = new BggXmlApiClient(client, options);

        var reportedProgress = new ConcurrentBag<BggImportProgressReport>();
        var progress = new Progress<BggImportProgressReport>(p => reportedProgress.Add(p));

        // Act
        var result = await bggClient.FetchUserCollectionAsync("ludomaster", progress);
        await Task.Delay(50);

        // Assert
        Assert.Single(result);
        Assert.Equal(2, mockHandler.SentRequests.Count);

        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.RateLimitedWaiting);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.ProcessingItems);
    }

    [Fact]
    public async Task FetchUserCollectionAsync_WhenBggPersistentlyReturns202ExceedingMaxRetries_TerminatesSafely()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        for (int i = 0; i < 5; i++)
        {
            mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Accepted));
        }

        var client = new HttpClient(mockHandler);
        var options = Options.Create(new BggOptions
        {
            MaxPollingRetries = 2,
            PollingTimeoutSeconds = 10,
            InitialPollingDelaySeconds = 0
        });

        using var bggClient = new BggXmlApiClient(client, options);

        var reportedProgress = new ConcurrentBag<BggImportProgressReport>();
        var progress = new Progress<BggImportProgressReport>(p => reportedProgress.Add(p));

        // Act
        var result = await bggClient.FetchUserCollectionAsync("ludomaster", progress);
        await Task.Delay(50);

        // Assert
        Assert.Empty(result);
        Assert.Equal(2, mockHandler.SentRequests.Count);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.Failed);
    }

    [Fact]
    public async Task BggResilienceAndAuthHandler_InjectsUserAgentAndBearerTokenAndApiKey()
    {
        // Arrange
        var mockInner = new MockHttpMessageHandler();
        mockInner.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<items />")
        });

        var options = Options.Create(new BggOptions
        {
            BearerToken = "test-bearer-token-xyz",
            ApiKey = "test-api-key-123",
            UserAgent = "LudekaTestBot/1.0"
        });

        var handler = new BggResilienceAndAuthHandler(options)
        {
            InnerHandler = mockInner
        };

        using var client = new HttpClient(handler);

        // Act
        var response = await client.GetAsync("https://boardgamegeek.com/xmlapi2/collection?username=test");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Single(mockInner.SentRequests);

        var sentRequest = mockInner.SentRequests[0];
        Assert.Equal("LudekaTestBot/1.0", sentRequest.Headers.UserAgent.ToString());
        Assert.NotNull(sentRequest.Headers.Authorization);
        Assert.Equal("Bearer", sentRequest.Headers.Authorization.Scheme);
        Assert.Equal("test-bearer-token-xyz", sentRequest.Headers.Authorization.Parameter);
        Assert.True(sentRequest.Headers.Contains("X-BGG-API-KEY"));
        Assert.Equal("test-api-key-123", sentRequest.Headers.GetValues("X-BGG-API-KEY").First());
    }

    [Fact]
    public void BggResilienceAndAuthHandler_ExtractRetryAfterSeconds_ParsesCorrectly()
    {
        // Test con delta en segundos
        var respWithDelta = new HttpResponseMessage((HttpStatusCode)429);
        respWithDelta.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
        Assert.Equal(12, BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(respWithDelta));

        // Test con cabecera raw numérica
        var respWithRaw = new HttpResponseMessage((HttpStatusCode)429);
        respWithRaw.Headers.TryAddWithoutValidation("Retry-After", "25");
        Assert.Equal(25, BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(respWithRaw));

        // Test sin cabecera Retry-After
        var respWithoutHeader = new HttpResponseMessage((HttpStatusCode)429);
        Assert.Null(BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(respWithoutHeader));
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_ConstructsCommaSeparatedIdsUrl_AndReturnsXml()
    {
        var mockHandler = new MockHttpMessageHandler();
        string sampleXml = "<items><item id=\"13\" /><item id=\"42\" /></items>";
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sampleXml)
        });

        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client);

        var result = await bggClient.FetchRawThingsXmlAsync([13, 42, 13, -5, 0]);

        Assert.Equal(sampleXml, result);
        Assert.Single(mockHandler.SentRequests);
        var sentUrl = mockHandler.SentRequests[0].RequestUri?.ToString();
        Assert.Contains("id=13,42&stats=1", sentUrl);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WhenIdsNullOrEmpty_ReturnsNull()
    {
        var mockHandler = new MockHttpMessageHandler();
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client);

        var nullResult = await bggClient.FetchRawThingsXmlAsync(null!);
        var emptyResult = await bggClient.FetchRawThingsXmlAsync([]);
        var invalidResult = await bggClient.FetchRawThingsXmlAsync([-1, 0]);

        Assert.Null(nullResult);
        Assert.Null(emptyResult);
        Assert.Null(invalidResult);
        Assert.Empty(mockHandler.SentRequests);
    }
}
