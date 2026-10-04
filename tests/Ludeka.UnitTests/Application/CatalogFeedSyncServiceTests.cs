using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class CatalogFeedSyncServiceTests
{
    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeFeedSourceRepository _sourceRepo = new();
    private readonly FakeDiscrepancyRepository _discrepancyRepo = new();
    private readonly GoogleShoppingFeedParser _parser = new();
    private readonly AffiliateUrlResolver _urlResolver = new();
    private readonly CatalogFeedSyncService _service;

    public CatalogFeedSyncServiceTests()
    {
        _service = new CatalogFeedSyncService(
            _sourceRepo,
            _discrepancyRepo,
            _gameRepo,
            _parser,
            _urlResolver,
            NullLogger<CatalogFeedSyncService>.Instance);
    }

    private static readonly string ValidEan1 = "843540760123" + BarcodeValidator.CalculateEan13CheckDigit("843540760123");
    private static readonly string ValidEan2 = "843540762955" + BarcodeValidator.CalculateEan13CheckDigit("843540762955");

    private static Game CreateGame(int bggId, string title, string? spanishTitle = null, string? ean = null, IEnumerable<string>? additionalBarcodes = null)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: spanishTitle ?? title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2020,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción test",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 75),
            ean: ean,
            additionalBarcodes: additionalBarcodes
        );
    }

    [Fact]
    public async Task SyncFeedStreamAsync_DeterministicEanMatch_UpdatesOfferAndDoesNotAutoAssignOrFlagDiscrepancy()
    {
        // Arrange: Juego con EAN conocido
        var game = CreateGame(bggId: 13, title: "Catan", spanishTitle: "Catán", ean: ValidEan1);
        _gameRepo.Add(game);

        var source = new AffiliateFeedSource(
            storeName: "Zacatrus",
            feedUrl: "https://zacatrus.es/feed.xml",
            format: FeedFormat.GoogleShoppingXml,
            affiliateTag: "ludeka-21",
            country: "España"
        );
        _sourceRepo.Add(source);

        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>ZAC-100</g:id>
                  <title>Catan el Juego</title>
                  <link>https://zacatrus.es/juegos/catan.html</link>
                  <g:price>42.99 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>{ValidEan1}</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await _service.SyncFeedStreamAsync(source, stream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsRead);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(0, result.AutoAssignedEanCount);
        Assert.Equal(0, result.DiscrepanciesCount);

        var updated = await _gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updated);
        Assert.Single(updated.PurchaseLinks);
        var offer = updated.PurchaseLinks[0];
        Assert.Equal("Zacatrus", offer.StoreName);
        Assert.Equal(42.99m, offer.Price);
        Assert.True(offer.InStock);
        Assert.Equal("España", offer.Country);
    }

    [Fact]
    public async Task SyncFeedStreamAsync_GameWithoutEan_AutoAssignsEanWhenTitleMatchesUnambiguously()
    {
        // Arrange: Juego sin EAN
        var game = CreateGame(bggId: 266192, title: "Wingspan", spanishTitle: "Wingspan", ean: null);
        _gameRepo.Add(game);

        var source = new AffiliateFeedSource("Jugamos Otra", "https://jugamosotra.com/feed.xml");
        _sourceRepo.Add(source);

        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>JO-WING</g:id>
                  <title>Wingspan (Juego de mesa)</title>
                  <link>https://jugamosotra.com/wingspan</link>
                  <g:price>51.00 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>{ValidEan1}</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await _service.SyncFeedStreamAsync(source, stream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(1, result.AutoAssignedEanCount);
        Assert.Equal(0, result.DiscrepanciesCount);

        var updated = await _gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updated);
        Assert.Equal(ValidEan1, updated.Ean);
        Assert.Single(updated.PurchaseLinks);
    }

    [Fact]
    public async Task SyncFeedStreamAsync_EanDiffersFromCurrent_LogsDiscrepancyAndAddsToAdditionalBarcodes()
    {
        // Arrange: Juego con EAN de BGG que difiere del de la tienda
        var game = CreateGame(bggId: 342942, title: "Ark Nova", spanishTitle: "Ark Nova", ean: ValidEan1);
        _gameRepo.Add(game);

        var source = new AffiliateFeedSource("Zacatrus", "https://zacatrus.es/feed.xml");
        _sourceRepo.Add(source);

        // El feed de la tienda trae otro EAN válido: ValidEan2
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>ZAC-ARK</g:id>
                  <title>Ark Nova</title>
                  <link>https://zacatrus.es/ark-nova</link>
                  <g:price>64.95 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>{ValidEan2}</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await _service.SyncFeedStreamAsync(source, stream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(0, result.AutoAssignedEanCount);
        Assert.Equal(1, result.DiscrepanciesCount);

        var updated = await _gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updated);
        // Ean principal se mantiene intacto hasta revisión humana
        Assert.Equal(ValidEan1, updated.Ean);
        // El EAN del feed se agrega a AdditionalBarcodes para no perder el cruce
        Assert.Contains(ValidEan2, updated.AdditionalBarcodes);

        // Se registra la discrepancia en el repositorio
        var pending = await _discrepancyRepo.GetPendingDiscrepanciesAsync();
        Assert.Single(pending);
        var disc = pending[0];
        Assert.Equal(game.Id, disc.GameId);
        Assert.Equal(ValidEan1, disc.CurrentEan);
        Assert.Equal(ValidEan2, disc.FeedEan);
        Assert.Equal("Zacatrus", disc.StoreName);
        Assert.False(disc.IsResolved);
    }

    [Fact]
    public async Task SyncFeedStreamAsync_SecondRun_IsIdempotentAndDoesNotDuplicateOffersOrDiscrepancies()
    {
        // Arrange
        var game = CreateGame(bggId: 342942, title: "Ark Nova", spanishTitle: "Ark Nova", ean: ValidEan1);
        _gameRepo.Add(game);

        var source = new AffiliateFeedSource("Zacatrus", "https://zacatrus.es/feed.xml");
        _sourceRepo.Add(source);

        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>ZAC-ARK</g:id>
                  <title>Ark Nova</title>
                  <link>https://zacatrus.es/ark-nova</link>
                  <g:price>64.95 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>{ValidEan2}</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        // Act: Primera ejecución
        using (var s1 = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
        {
            await _service.SyncFeedStreamAsync(source, s1);
        }

        // Act: Segunda ejecución con el mismo feed pero precio actualizado
        string xml2 = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>ZAC-ARK</g:id>
                  <title>Ark Nova</title>
                  <link>https://zacatrus.es/ark-nova</link>
                  <g:price>59.95 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>{ValidEan2}</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        FeedSyncResult result2;
        using (var s2 = new MemoryStream(Encoding.UTF8.GetBytes(xml2)))
        {
            result2 = await _service.SyncFeedStreamAsync(source, s2);
        }

        // Assert
        Assert.True(result2.Success);
        Assert.Equal(1, result2.MatchedCount);
        Assert.Equal(0, result2.DiscrepanciesCount); // No crea discrepancia duplicada

        var updated = await _gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updated);
        Assert.Single(updated.PurchaseLinks); // Sigue teniendo una sola oferta
        Assert.Equal(59.95m, updated.PurchaseLinks[0].Price); // Precio actualizado

        var pending = await _discrepancyRepo.GetPendingDiscrepanciesAsync();
        Assert.Single(pending); // Sigue existiendo solo 1 discrepancia
    }

    [Fact]
    public async Task SyncFeedSourceByIdAsync_NonExistingSource_ReturnsFailureResult()
    {
        var result = await _service.SyncFeedSourceByIdAsync(Guid.NewGuid());
        Assert.False(result.Success);
        Assert.Contains("No existe", result.ErrorMessage);
    }

    // -------------------------------------------------------------
    // Fakes para aislamiento unitario
    // -------------------------------------------------------------

    private class FakeGameRepository : IGameRepository
    {
        private readonly List<Game> _games = [];

        public void Add(Game game) => _games.Add(game);

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Game>>(_games.ToList());

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.BggId == bggId));

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            int idx = _games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) _games[idx] = game;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            _games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(_games.Count > 0);

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((_games.Take(pageSize).ToList(), _games.Count));
    }

    private class FakeFeedSourceRepository : IAffiliateFeedSourceRepository
    {
        private readonly List<AffiliateFeedSource> _sources = [];

        public void Add(AffiliateFeedSource source) => _sources.Add(source);

        public Task<AffiliateFeedSource> AddAsync(AffiliateFeedSource source, CancellationToken ct = default)
        {
            _sources.Add(source);
            return Task.FromResult(source);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _sources.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }

        public Task<List<AffiliateFeedSource>> GetActiveSourcesAsync(CancellationToken ct = default)
            => Task.FromResult(_sources.Where(s => s.IsEnabled).ToList());

        public Task<List<AffiliateFeedSource>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_sources.ToList());

        public Task<AffiliateFeedSource?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_sources.FirstOrDefault(s => s.Id == id));

        public Task<AffiliateFeedSource?> GetByStoreNameAsync(string storeName, CancellationToken ct = default)
            => Task.FromResult(_sources.FirstOrDefault(s => string.Equals(s.StoreName, storeName, StringComparison.OrdinalIgnoreCase)));

        public Task UpdateAsync(AffiliateFeedSource source, CancellationToken ct = default)
        {
            int idx = _sources.FindIndex(s => s.Id == source.Id);
            if (idx >= 0) _sources[idx] = source;
            return Task.CompletedTask;
        }
    }

    private class FakeDiscrepancyRepository : IAffiliateEanDiscrepancyRepository
    {
        private readonly List<AffiliateEanDiscrepancyLog> _discrepancies = [];

        public Task<AffiliateEanDiscrepancyLog> AddAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default)
        {
            _discrepancies.Add(log);
            return Task.FromResult(log);
        }

        public Task<AffiliateEanDiscrepancyLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_discrepancies.FirstOrDefault(d => d.Id == id));

        public Task<List<AffiliateEanDiscrepancyLog>> GetPendingDiscrepanciesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult(_discrepancies.Where(d => !d.IsResolved).Take(limit).ToList());

        public Task<AffiliateEanDiscrepancyLog?> FindExistingPendingAsync(Guid gameId, string feedEan, string storeName, CancellationToken ct = default)
            => Task.FromResult(_discrepancies.FirstOrDefault(d => !d.IsResolved && d.GameId == gameId && d.FeedEan == feedEan && string.Equals(d.StoreName, storeName, StringComparison.OrdinalIgnoreCase)));

        public Task UpdateAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default)
        {
            int idx = _discrepancies.FindIndex(d => d.Id == log.Id);
            if (idx >= 0) _discrepancies[idx] = log;
            return Task.CompletedTask;
        }
    }
}
