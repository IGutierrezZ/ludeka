using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
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
}
