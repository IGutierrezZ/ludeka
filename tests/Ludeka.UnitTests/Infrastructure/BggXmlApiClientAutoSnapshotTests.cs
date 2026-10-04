using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Bgg;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggXmlApiClientAutoSnapshotTests
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

    private class FakeBggRawSnapshotRepository : IBggRawSnapshotRepository
    {
        public ConcurrentDictionary<int, BggRawSnapshot> Snapshots { get; } = new();
        public bool ThrowOnUpsert { get; set; }

        public Task<BggRawSnapshot?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Snapshots.GetValueOrDefault(bggId));

        public Task UpsertAsync(BggRawSnapshot snapshot, CancellationToken ct = default)
        {
            if (ThrowOnUpsert) throw new InvalidOperationException("Simulated database failure");
            Snapshots[snapshot.BggId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<int>> GetMissingBggIdsAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<int>>([]);

        public Task<int> GetCountAsync(CancellationToken ct = default)
            => Task.FromResult(Snapshots.Count);

        public Task<int> GetTotalGamesWithBggIdCountAsync(CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<BggRawSnapshot>> GetAllSnapshotsAsync(int limit = 500, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggRawSnapshot>>(Snapshots.Values.Take(limit).ToList());

        public Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsAfterBggIdAsync(int lastBggId, int limit = 200, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggRawSnapshot>>(
                Snapshots.Values
                    .Where(s => s.BggId > lastBggId)
                    .OrderBy(s => s.BggId)
                    .Take(limit)
                    .ToList());

        public Task<IReadOnlyList<int>> GetBggIdsMissingVersionsAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<int>>(
                Snapshots.Values
                    .Where(s => !s.RawJson.Contains("\"versions\""))
                    .OrderBy(s => s.BggId)
                    .Select(s => s.BggId)
                    .Take(limit)
                    .ToList());

        public Task<int> GetCountWithVersionsAsync(CancellationToken ct = default)
            => Task.FromResult(Snapshots.Values.Count(s => s.RawJson.Contains("\"versions\"")));
    }

    private const string SampleThingXml = """
        <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
          <item type="boardgame" id="13">
            <name type="primary" sortindex="1" value="Catan" />
            <yearpublished value="1995" />
            <minplayers value="3" />
            <maxplayers value="4" />
          </item>
          <item type="boardgame" id="42">
            <name type="primary" sortindex="1" value="Tigris &amp; Euphrates" />
            <yearpublished value="1997" />
          </item>
        </items>
        """;

    [Fact]
    public async Task FetchRawThingsXmlAsync_WhenSnapshotRepositoryConfigured_AutoPersistsSnapshots()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleThingXml)
        });

        var fakeRepo = new FakeBggRawSnapshotRepository();
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, fakeRepo);

        // Act
        var result = await bggClient.FetchRawThingsXmlAsync([13, 42]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, fakeRepo.Snapshots.Count);
        Assert.True(fakeRepo.Snapshots.ContainsKey(13));
        Assert.True(fakeRepo.Snapshots.ContainsKey(42));
        Assert.Contains("\"@id\":\"13\"", fakeRepo.Snapshots[13].RawJson);
        Assert.Contains("\"@id\":\"42\"", fakeRepo.Snapshots[42].RawJson);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WhenRequestedIdMissingInBggXml_PersistsNotFoundPlaceholder()
    {
        const string singleItemXml = """
            <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
              <item type="boardgame" id="13">
                <name type="primary" sortindex="1" value="Catan" />
              </item>
            </items>
            """;

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(singleItemXml)
        });

        var fakeRepo = new FakeBggRawSnapshotRepository();
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, fakeRepo);

        // Act
        var result = await bggClient.FetchRawThingsXmlAsync([13, 999999]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, fakeRepo.Snapshots.Count);
        Assert.True(fakeRepo.Snapshots.ContainsKey(13));
        Assert.True(fakeRepo.Snapshots.ContainsKey(999999));
        Assert.Equal("{\"notFound\":true}", fakeRepo.Snapshots[999999].RawJson);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WhenSnapshotRepositoryThrows_DoesNotBreakXmlFetch()
    {
        // Arrange: Repositorio falla pero la petición HTTP debe tener éxito (resiliencia no bloqueante)
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleThingXml)
        });

        var fakeRepo = new FakeBggRawSnapshotRepository { ThrowOnUpsert = true };
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, fakeRepo);

        // Act
        var result = await bggClient.FetchRawThingsXmlAsync([13]);

        // Assert: Devuelve el XML sin propagar la excepción
        Assert.NotNull(result);
        Assert.Contains("Catan", result);
    }

    [Fact]
    public async Task FetchGameByBggIdAsync_WhenSnapshotRepositoryConfigured_AutoPersistsSnapshot()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleThingXml)
        });

        var fakeRepo = new FakeBggRawSnapshotRepository();
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, fakeRepo);

        // Act
        var game = await bggClient.FetchGameByBggIdAsync(13);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(13, game.BggId);
        Assert.True(fakeRepo.Snapshots.ContainsKey(13));
        Assert.Contains("\"@id\":\"13\"", fakeRepo.Snapshots[13].RawJson);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WithIncludeVersionsTrue_BuildsUrlWithVersionsParam()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleThingXml)
        });

        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, null);

        // Act
        await bggClient.FetchRawThingsXmlAsync([2651], includeVersions: true);

        // Assert
        Assert.Single(mockHandler.SentRequests);
        string requestUrl = mockHandler.SentRequests[0].RequestUri?.ToString() ?? string.Empty;
        Assert.Contains("id=2651", requestUrl);
        Assert.Contains("versions=1", requestUrl);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WithIncludeVersionsFalse_BuildsUrlWithoutVersionsParam()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleThingXml)
        });

        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, null);

        // Act
        await bggClient.FetchRawThingsXmlAsync([2651], includeVersions: false);

        // Assert
        Assert.Single(mockHandler.SentRequests);
        string requestUrl = mockHandler.SentRequests[0].RequestUri?.ToString() ?? string.Empty;
        Assert.Contains("id=2651", requestUrl);
        Assert.DoesNotContain("versions=1", requestUrl);
    }

    [Fact]
    public async Task FetchRawThingsXmlAsync_WithVersionsXml_AutoPersistsVersionsInRawJson()
    {
        // Arrange: XML que incluye el subárbol <versions>
        const string xmlWithVersions = """
            <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
              <item type="boardgame" id="2651">
                <name type="primary" sortindex="1" value="Power Grid" />
                <versions>
                  <item type="boardgameversion" id="21950">
                    <name type="primary" sortindex="1" value="Alta Tensión" />
                    <link type="language" id="2195" value="Spanish" />
                    <barcode value="8435407626515" />
                  </item>
                </versions>
              </item>
            </items>
            """;

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xmlWithVersions)
        });

        var fakeRepo = new FakeBggRawSnapshotRepository();
        var client = new HttpClient(mockHandler);
        using var bggClient = new BggXmlApiClient(client, null, fakeRepo);

        // Act
        await bggClient.FetchRawThingsXmlAsync([2651], includeVersions: true);

        // Assert: Auto-persiste el snapshot y contiene el subárbol de versiones
        Assert.True(fakeRepo.Snapshots.ContainsKey(2651));
        var snapshot = fakeRepo.Snapshots[2651];
        Assert.True(BggRawSnapshotParser.HasVersionsFromJson(snapshot.RawJson));

        var spanishInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(snapshot.RawJson);
        Assert.NotNull(spanishInfo);
        Assert.Equal("Alta Tensión", spanishInfo.Title);
        Assert.Equal("8435407626515", spanishInfo.Ean);
    }

    [Fact]
    public async Task GetBggIdsMissingVersionsAsync_And_GetCountWithVersionsAsync_ReturnAccurateMetrics()
    {
        // Arrange
        var fakeRepo = new FakeBggRawSnapshotRepository();
        await fakeRepo.UpsertAsync(new BggRawSnapshot(1, "{\"item\":{\"@id\":\"1\",\"name\":\"Game 1\"}}", 2));
        await fakeRepo.UpsertAsync(new BggRawSnapshot(2, "{\"item\":{\"@id\":\"2\",\"versions\":{\"item\":{}}}}", 2));

        // Act
        var missingIds = await fakeRepo.GetBggIdsMissingVersionsAsync(10);
        int countWithVersions = await fakeRepo.GetCountWithVersionsAsync();

        // Assert
        Assert.Equal([1], missingIds);
        Assert.Equal(1, countWithVersions);
    }
}
