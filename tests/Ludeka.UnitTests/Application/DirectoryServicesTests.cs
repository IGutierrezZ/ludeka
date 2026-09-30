using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Directory;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class DirectoryServicesTests
{
    private class FakePublisherRepository : IPublisherRepository
    {
        public List<Publisher> Items = [];

        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Publisher>>(Items.ToList());

        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Publisher publisher, CancellationToken ct = default)
        {
            Items.Add(publisher);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Publisher publisher, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(p => p.Id == publisher.Id);
            if (idx >= 0) Items[idx] = publisher;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeCreatorRepository : ICreatorRepository
    {
        public List<Creator> Items = [];

        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Creator>>(Items.ToList());

        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Creator creator, CancellationToken ct = default)
        {
            Items.Add(creator);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Creator creator, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(c => c.Id == creator.Id);
            if (idx >= 0) Items[idx] = creator;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeStoreRepository : IStoreRepository
    {
        public List<Store> Items = [];

        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Store>>(Items.ToList());

        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Store store, CancellationToken ct = default)
        {
            Items.Add(store);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Store store, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(s => s.Id == store.Id);
            if (idx >= 0) Items[idx] = store;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);

        public Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default)
        {
            var filtered = Games.Where(g => g.Publisher.Contains(publisherName, StringComparison.OrdinalIgnoreCase) ||
                                            publisherName.Contains(g.Publisher, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult<IReadOnlyList<Game>>(filtered);
        }

        public Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default)
        {
            var filtered = Games.Where(g => g.Designer.Contains(designerName, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult<IReadOnlyList<Game>>(filtered);
        }

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Game>>(Games.ToList());

        public Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in Games)
            {
                var stores = g.PurchaseLinks.Select(l => l.StoreName).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var s in stores)
                {
                    dict[s] = dict.GetValueOrDefault(s, 0) + 1;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<string, int>>(dict);
        }

        public Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in Games)
            {
                var pubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(g.Publisher)) pubs.Add(g.Publisher);
                if (!string.IsNullOrWhiteSpace(g.SpanishPublisher)) pubs.Add(g.SpanishPublisher);
                if (g.RegionalPublishers != null)
                {
                    foreach (var r in g.RegionalPublishers)
                    {
                        if (!string.IsNullOrWhiteSpace(r.PublisherName)) pubs.Add(r.PublisherName);
                    }
                }
                foreach (var p in pubs)
                {
                    dict[p] = dict.GetValueOrDefault(p, 0) + 1;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<string, int>>(dict);
        }

        public Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default)
        {
            var clean = storeName.Trim();
            var filtered = Games.Where(g => g.PurchaseLinks.Any(l =>
                l.StoreName.Contains(clean, StringComparison.OrdinalIgnoreCase) ||
                clean.Contains(l.StoreName, StringComparison.OrdinalIgnoreCase))).ToList();
            return Task.FromResult<IReadOnlyList<Game>>(filtered);
        }

        public Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default)
        {
            var res = Games.Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0);
            if (limit.HasValue && limit.Value > 0) res = res.Take(limit.Value);
            return Task.FromResult<IReadOnlyList<Game>>(res.ToList());
        }
    }

    private static Game CreateTestGame(string title, string publisher, string designer, List<GamePurchaseLink>? purchaseLinks = null)
    {
        var game = new Game(
            bggId: Random.Shared.Next(1, 99999),
            spanishTitle: title,
            originalTitle: title,
            designer: designer,
            publisher: publisher,
            yearPublished: 2020,
            coverImageUrl: "/images/games/test.jpg",
            thumbnailUrl: "/images/games/test.jpg",
            description: "Descripción de prueba",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            scalability: new[]
            {
                new ScalabilityEntry(2, "2", ScalabilityStatus.Recommended),
                new ScalabilityEntry(4, "4", ScalabilityStatus.Recommended)
            }
        );

        if (purchaseLinks != null)
        {
            game.UpdatePurchaseLinks(purchaseLinks);
        }

        return game;
    }

    [Fact]
    public async Task PublisherService_CRUD_And_CatalogMatching_Works()
    {
        var pubRepo = new FakePublisherRepository();
        var gameRepo = new FakeGameRepository();
        gameRepo.Games.Add(CreateTestGame("Catan", "Devir Iberia", "Klaus Teuber"));
        gameRepo.Games.Add(CreateTestGame("Carcassonne", "Devir", "Klaus-Jürgen Wrede"));
        gameRepo.Games.Add(CreateTestGame("Wingspan", "Maldito Games", "Elizabeth Hargrave"));

        var service = new PublisherService(pubRepo, gameRepo);

        // 1. Create
        var created = await service.CreateAsync(new CreatePublisherDto(
            "Devir Iberia",
            "devir-iberia",
            "España",
            "Barcelona",
            "Editorial decana",
            null,
            "https://devir.es",
            new List<SocialNetworkLinkDto> { new(SocialPlatform.YouTube, "https://youtube.com/@devirtv", "@devirtv") }
        ));

        Assert.Equal("Devir Iberia", created.Name);
        Assert.Equal("devir-iberia", created.Slug);

        // 2. GetAll
        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(2, all[0].GamesCount); // Catan and Carcassonne

        // 3. GetBySlug
        var detail = await service.GetBySlugAsync("devir-iberia");
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Games.Count);

        // 4. Update
        var updated = await service.UpdateAsync(created.Id, new UpdatePublisherDto(
            "Devir Global",
            "España",
            "Barcelona",
            "Desc actualizada",
            null,
            "https://devir.es",
            null
        ));
        Assert.Equal("Devir Global", updated.Name);

        // 5. Delete
        await service.DeleteAsync(created.Id);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task CreatorService_CRUD_Works()
    {
        var creatorRepo = new FakeCreatorRepository();
        var service = new CreatorService(creatorRepo);

        // 1. Create
        var created = await service.CreateAsync(new CreateCreatorDto(
            "Análisis Parálisis",
            "analisis-paralisis",
            "España",
            "Referente de la divulgación lúdica en español",
            null,
            null,
            null,
            new List<SocialNetworkLinkDto> { new(SocialPlatform.YouTube, "https://youtube.com/@AnalisisParalisis", "@AnalisisParalisis") }
        ));

        Assert.Equal("Análisis Parálisis", created.Name);

        // 2. GetAll (sin cruce por Game.Designer: sin conteos de obras)
        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Análisis Parálisis", all[0].Name);
        Assert.Contains(all[0].SocialLinks, l => l.Platform == SocialPlatform.YouTube);

        // 3. GetBySlug (la ficha expone redes sociales, no obras)
        var detail = await service.GetBySlugAsync("analisis-paralisis");
        Assert.NotNull(detail);
        Assert.Equal("Análisis Parálisis", detail.Name);
        Assert.Contains(detail.SocialLinks, l => l.Url == "https://youtube.com/@AnalisisParalisis");

        // 4. Update
        var updated = await service.UpdateAsync(created.Id, new UpdateCreatorDto(
            "Análisis Parálisis",
            "España",
            "Referente absoluto de la divulgación audiovisual lúdica",
            null,
            null,
            "https://analisisparalisis.es",
            new List<SocialNetworkLinkDto> { new(SocialPlatform.YouTube, "https://youtube.com/@APNuevo", "@APNuevo") }
        ));
        Assert.Equal("Referente absoluto de la divulgación audiovisual lúdica", updated.Bio);

        // 5. Delete
        await service.DeleteAsync(created.Id);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task CreatorService_CreateWithoutSocialLinks_DetailExposesEmptyLinks()
    {
        var creatorRepo = new FakeCreatorRepository();
        var service = new CreatorService(creatorRepo);

        // Alta de un creador SIN redes sociales (spec creators-directory: "Ficha sin redes")
        var created = await service.CreateAsync(new CreateCreatorDto(
            "Creador Sin Redes",
            "creador-sin-redes",
            "Chile",
            "Divulgador sin presencia en redes sociales",
            null,
            null,
            null,
            null
        ));

        Assert.Equal("Creador Sin Redes", created.Name);

        // La ficha resultante expone la lista de redes vacía, sin error
        var detail = await service.GetBySlugAsync("creador-sin-redes");
        Assert.NotNull(detail);
        Assert.Equal("Creador Sin Redes", detail.Name);
        Assert.Empty(detail.SocialLinks);
    }

    [Fact]
    public async Task StoreService_CRUD_Filter_And_OffersExtraction_Works()
    {
        var storeRepo = new FakeStoreRepository();
        var gameRepo = new FakeGameRepository();

        var zacatrusLink = new GamePurchaseLink("Zacatrus!", "https://zacatrus.es/wingspan", 45m, "€", true, "Top Ventas");
        gameRepo.Games.Add(CreateTestGame("Wingspan", "Maldito", "Elizabeth Hargrave", new List<GamePurchaseLink> { zacatrusLink }));
        gameRepo.Games.Add(CreateTestGame("Catan", "Devir", "Klaus Teuber"));

        var service = new StoreService(storeRepo, gameRepo);

        // 1. Create
        var store1 = await service.CreateAsync(new CreateStoreDto(
            "Zacatrus!",
            "zacatrus",
            StoreType.Hybrid,
            "Madrid",
            "Calle Fernández 57",
            "Tienda física y online",
            null,
            "https://zacatrus.es",
            "LDKZAC",
            true,
            new List<SocialNetworkLinkDto> { new(SocialPlatform.YouTube, "https://youtube.com/@zacatrustv", "@zacatrustv") }
        ));

        var store2 = await service.CreateAsync(new CreateStoreDto(
            "Online Games",
            "online-games",
            StoreType.OnlineOnly,
            null,
            null,
            "Solo online",
            null,
            "https://onlinegames.es",
            null,
            false,
            null
        ));

        // 2. GetAll with Filter
        var hybridOnly = await service.GetAllAsync(type: StoreType.Hybrid);
        Assert.Single(hybridOnly);
        Assert.Equal("Zacatrus!", hybridOnly[0].Name);
        Assert.Equal(1, hybridOnly[0].ActiveOffersCount);

        // 3. GetBySlug with Active Offers
        var detail = await service.GetBySlugAsync("zacatrus");
        Assert.NotNull(detail);
        Assert.Single(detail.Offers);
        Assert.Equal("Wingspan", detail.Offers[0].GameTitle);
        Assert.Equal(45m, detail.Offers[0].Price);
    }

    [Fact]
    public async Task ChannelDirectoryProvider_ShouldExtractAllYouTubeChannels_FromEntities()
    {
        var pubRepo = new FakePublisherRepository();
        var creatorRepo = new FakeCreatorRepository();
        var storeRepo = new FakeStoreRepository();

        pubRepo.Items.Add(new Publisher("Devir", "devir", "España", socialLinks: new[]
        {
            new SocialNetworkLink(SocialPlatform.YouTube, "https://youtube.com/@devirtv", "@devirtv")
        }));

        creatorRepo.Items.Add(new Creator("Sergio AP", "sergio-ap", socialLinks: new[]
        {
            new SocialNetworkLink(SocialPlatform.YouTube, "https://youtube.com/@AnalisisParalisis", "@AnalisisParalisis")
        }));

        storeRepo.Items.Add(new Store("Zacatrus", "zacatrus", socialLinks: new[]
        {
            new SocialNetworkLink(SocialPlatform.YouTube, "https://youtube.com/@zacatrustv", "@zacatrustv")
        }));

        var directoryProvider = new ChannelDirectoryProvider(pubRepo, creatorRepo, storeRepo);

        var channels = await directoryProvider.GetDynamicReferenceChannelsAsync();

        Assert.Equal(3, channels.Count);
        Assert.Contains(channels, c => c.ChannelName == "Devir" && c.Category == ChannelCategory.Publisher);
        Assert.Contains(channels, c => c.ChannelName == "Sergio AP" && c.Category == ChannelCategory.Creator);
        Assert.Contains(channels, c => c.ChannelName == "Zacatrus" && c.Category == ChannelCategory.Store);
    }
}
