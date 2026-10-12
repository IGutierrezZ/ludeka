using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Seo;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class SitemapServiceTests
{
    private class FakeGameRepository : IGameRepository
    {
        public List<GameSitemapDto> SitemapEntries { get; set; } = [];

        public Task<IReadOnlyList<GameSitemapDto>> GetGameSitemapEntriesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameSitemapDto>>(SitemapEntries);

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult<(IReadOnlyList<Game>, int)>(([], 0));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task GetSitemapEntriesAsync_ShouldIncludeCoreStaticRoutes()
    {
        var gameRepo = new FakeGameRepository();
        var service = new SitemapService(gameRepo);

        var entries = await service.GetSitemapEntriesAsync("https://ludeka.es");

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/catalogo");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/novedades");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/sorteos");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/eventos");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/clasificaciones");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/editoriales");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/creadores");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/tiendas");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/transparencia");
    }

    [Fact]
    public async Task GetSitemapEntriesAsync_ShouldIncludeDynamicCatalogGames()
    {
        var gameRepo = new FakeGameRepository
        {
            SitemapEntries =
            {
                new("wingspan", DateTimeOffset.UtcNow),
                new("catan", DateTimeOffset.UtcNow.AddDays(-1))
            }
        };

        var service = new SitemapService(gameRepo);
        var entries = await service.GetSitemapEntriesAsync("https://ludeka.es");

        Assert.Contains(entries, e => e.Location == "https://ludeka.es/juegos/wingspan");
        Assert.Contains(entries, e => e.Location == "https://ludeka.es/juegos/catan");
    }

    [Fact]
    public async Task GenerateSitemapXmlAsync_ShouldProduceValidXmlConformingToSitemapsProtocol()
    {
        var gameRepo = new FakeGameRepository
        {
            SitemapEntries =
            {
                new("wingspan", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero))
            }
        };

        var service = new SitemapService(gameRepo);
        var xml = await service.GenerateSitemapXmlAsync("https://ludeka.es");

        Assert.False(string.IsNullOrWhiteSpace(xml));

        var doc = XDocument.Parse(xml);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var root = doc.Element(ns + "urlset");
        Assert.NotNull(root);

        var urlElements = root.Elements(ns + "url");
        Assert.NotEmpty(urlElements);

        // Comprobar estructura de una entrada individual
        var gameUrl = Assert.Single(urlElements, u => u.Element(ns + "loc")?.Value == "https://ludeka.es/juegos/wingspan");
        Assert.Equal("2026-10-10", gameUrl.Element(ns + "lastmod")?.Value);
        Assert.Equal("weekly", gameUrl.Element(ns + "changefreq")?.Value);
        Assert.Equal("0.8", gameUrl.Element(ns + "priority")?.Value);
    }
}
