using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Library;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class UserLibraryStatsServiceTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "usr-demo";
        public string UserName { get; set; } = "Demo Player";
        public IReadOnlyList<string> Roles { get; set; } = ["User"];
        public bool IsFoundingTeam => false;
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    private class FakeCollectionRepo : IUserCollectionRepository
    {
        public List<UserCollectionItem> Items { get; } = [];

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && (i.BggId == bggId || (i.Game != null && i.Game.BggId == bggId))));

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken ct = default)
        {
            var query = Items.Where(i => i.UserId == userId);
            if (status.HasValue) query = query.Where(i => i.Status == status.Value);
            return Task.FromResult(query.ToList());
        }

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken ct = default)
        {
            var dict = Items
                .Where(i => i.UserId == userId && i.Status.HasValue)
                .GroupBy(i => i.Status!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(dict);
        }

        public Task AddAsync(UserCollectionItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserCollectionItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(UserCollectionItem item, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeLoanRepo : IGameLoanRepository
    {
        public List<GameLoan> Loans { get; } = [];
        public Task<GameLoan?> GetByIdAsync(Guid loanId, CancellationToken ct = default) =>
            Task.FromResult(Loans.FirstOrDefault(l => l.Id == loanId));
        public Task<GameLoan?> GetActiveLoanByUserAndGameAsync(string userId, Guid gameId, CancellationToken ct = default) =>
            Task.FromResult(Loans.FirstOrDefault(l => l.UserId == userId && l.GameId == gameId && !l.IsReturned));
        public Task<List<GameLoan>> GetActiveLoansByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Loans.Where(l => l.UserId == userId && !l.IsReturned).ToList());
        public Task<List<GameLoan>> GetLoanHistoryByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Loans.Where(l => l.UserId == userId).ToList());
        public Task<int> GetActiveLoansCountAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Loans.Count(l => l.UserId == userId && !l.IsReturned));
        public Task AddAsync(GameLoan loan, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(GameLoan loan, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games { get; } = [];
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
    }

    private static Game CreateGame(
        int bggId,
        string title,
        int minMin = 30,
        int maxMin = 60,
        GameStyle style = GameStyle.Eurogame,
        ConfrontationType confrontation = ConfrontationType.Competitive,
        bool isSolo = false,
        string designer = "Uwe Rosenberg",
        string publisher = "Lookout Games",
        IEnumerable<ScalabilityEntry>? scalability = null,
        IEnumerable<SleeveItem>? sleeves = null)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: designer,
            publisher: publisher,
            yearPublished: 2020,
            coverImageUrl: $"https://images.ludeka.es/{bggId}.jpg",
            thumbnailUrl: null,
            description: "Descripción de prueba",
            bggRating: 8.0,
            bggRank: 50,
            ludistRating: 0.0,
            confrontation: confrontation,
            style: style,
            isOfficialSolo: isSolo,
            age: new AgeRating(12, 12),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(minMin, maxMin, 30),
            scalability: scalability ?? [
                new ScalabilityEntry(2, "2", ScalabilityStatus.Recommended),
                new ScalabilityEntry(4, "4", ScalabilityStatus.MustPlay)
            ],
            sleeves: sleeves
        );
    }

    [Fact]
    public async Task GetUserStatsAsync_EmptyCollection_ReturnsZeroMetricsAndBlankBadge()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        // Act
        var stats = await service.GetUserStatsAsync("user-empty");

        // Assert
        Assert.Equal(0, stats.TotalGamesInCollection);
        Assert.Equal(0, stats.ShelfTime.TotalMinMinutes);
        Assert.Equal(0, stats.ShelfTime.TotalMaxMinutes);
        Assert.Equal(0.0, stats.ShelfTime.TotalMinHours);
        Assert.Contains("0 horas", stats.ShelfTime.FormattedShelfHours);
        Assert.Equal("Sin definir", stats.DnaDistribution.DominantStyleName);
        Assert.Equal("Sin datos suficientes", stats.Scalability.SweetSpotSummaryText);
        Assert.Empty(stats.Scalability.SweetSpotPlayerCounts);
        Assert.Equal(0, stats.SleevesRadar.TotalCards);
        Assert.Equal(0, stats.SleevesRadar.TotalPacksEstimated);
        Assert.Empty(stats.TopDesigners);
        Assert.Empty(stats.TopPublishers);
        Assert.Equal(0, stats.Badge.RankLevel);
        Assert.Equal("Estantería en Blanco", stats.Badge.RankName);
        Assert.Equal("Paladar Ecléctico", stats.Badge.TraitName);
    }

    [Fact]
    public async Task GetUserStatsAsync_ComputesCorrectShelfTime()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "Juego Corto", minMin: 30, maxMin: 60);
        var g2 = CreateGame(2, "Juego Largo", minMin: 90, maxMin: 120);
        gameRepo.Games.AddRange([g1, g2]);

        var item1 = new UserCollectionItem("usr-1", g1.Id, CollectionStatus.InCollection);
        var item2 = new UserCollectionItem("usr-1", g2.Id, CollectionStatus.InCollection);
        collectionRepo.Items.AddRange([item1, item2]);

        // Act
        var stats = await service.GetUserStatsAsync("usr-1");

        // Assert
        Assert.Equal(2, stats.TotalGamesInCollection);
        Assert.Equal(120, stats.ShelfTime.TotalMinMinutes);
        Assert.Equal(180, stats.ShelfTime.TotalMaxMinutes);
        Assert.Equal(2.0, stats.ShelfTime.TotalMinHours);
        Assert.Equal(3.0, stats.ShelfTime.TotalMaxHours);
        Assert.Equal("2 – 3 horas de juego acumuladas", stats.ShelfTime.FormattedShelfHours);
    }

    [Fact]
    public async Task GetUserStatsAsync_ComputesCorrectDnaDistributionAndDominantStyle()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "Euro 1", style: GameStyle.Eurogame, isSolo: true);
        var g2 = CreateGame(2, "Euro 2", style: GameStyle.Eurogame, confrontation: ConfrontationType.Cooperative);
        var g3 = CreateGame(3, "Temático 1", style: GameStyle.Ameritrash, confrontation: ConfrontationType.Cooperative);
        gameRepo.Games.AddRange([g1, g2, g3]);

        collectionRepo.Items.AddRange([
            new UserCollectionItem("usr-dna", g1.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-dna", g2.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-dna", g3.Id, CollectionStatus.InCollection)
        ]);

        // Act
        var stats = await service.GetUserStatsAsync("usr-dna");

        // Assert
        Assert.Equal("Eurogames", stats.DnaDistribution.DominantStyleName);
        var euroStyle = stats.DnaDistribution.Styles.First(s => s.Style == GameStyle.Eurogame);
        Assert.Equal(2, euroStyle.GameCount);
        Assert.Equal(66.7, euroStyle.Percentage);

        var ameriStyle = stats.DnaDistribution.Styles.First(s => s.Style == GameStyle.Ameritrash);
        Assert.Equal(1, ameriStyle.GameCount);
        Assert.Equal(33.3, ameriStyle.Percentage);

        // Cooperativos: g2 y g3 = 2 de 3 => 66.7%
        Assert.Equal(2, stats.DnaDistribution.CooperativeGamesCount);
        Assert.Equal(66.7, stats.DnaDistribution.CooperativePercentage);

        // Solitario: g1 = 1 de 3 => 33.3%
        Assert.Equal(1, stats.DnaDistribution.SoloReadyGamesCount);
        Assert.Equal(33.3, stats.DnaDistribution.SoloReadyPercentage);
    }

    [Fact]
    public async Task GetUserStatsAsync_IdentifiesSweetSpotPlayerCounts()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "G1", scalability: [
            new ScalabilityEntry(2, "2", ScalabilityStatus.Recommended),
            new ScalabilityEntry(4, "4", ScalabilityStatus.MustPlay)
        ]);
        var g2 = CreateGame(2, "G2", scalability: [
            new ScalabilityEntry(4, "4", ScalabilityStatus.Recommended)
        ]);
        var g3 = CreateGame(3, "G3", scalability: [
            new ScalabilityEntry(7, "7+", ScalabilityStatus.MustPlay)
        ]);
        gameRepo.Games.AddRange([g1, g2, g3]);

        collectionRepo.Items.AddRange([
            new UserCollectionItem("usr-scale", g1.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-scale", g2.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-scale", g3.Id, CollectionStatus.InCollection)
        ]);

        // Act
        var stats = await service.GetUserStatsAsync("usr-scale");

        // Assert
        // 4 jugadores tiene 2 juegos optimizados (g1 y g2) -> Sweet Spot
        Assert.Single(stats.Scalability.SweetSpotPlayerCounts);
        Assert.Equal(4, stats.Scalability.SweetSpotPlayerCounts[0]);
        Assert.Equal("Especializada en mesas de 4 jugadores", stats.Scalability.SweetSpotSummaryText);

        var entry4 = stats.Scalability.Curve.First(c => c.PlayerCount == 4);
        Assert.True(entry4.IsSweetSpot);
        Assert.Equal(2, entry4.OptimizedGamesCount);

        var entry7 = stats.Scalability.Curve.First(c => c.PlayerCount == 7);
        Assert.Equal(1, entry7.OptimizedGamesCount);
    }

    [Fact]
    public async Task GetUserStatsAsync_ComputesSleevesRadarCorrectly()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "G1", sleeves: [
            new SleeveItem("Estándar 63.5 x 88 mm", 63.5, 88.0, 110, null)
        ]);
        var g2 = CreateGame(2, "G2", sleeves: [
            new SleeveItem("Mini Euro 45 x 68 mm", 45.0, 68.0, 40, null),
            new SleeveItem("Estándar 63.5 x 88 mm", 63.5, 88.0, 30, null)
        ]);
        gameRepo.Games.AddRange([g1, g2]);

        collectionRepo.Items.AddRange([
            new UserCollectionItem("usr-sleeves", g1.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-sleeves", g2.Id, CollectionStatus.InCollection)
        ]);

        // Act
        var stats = await service.GetUserStatsAsync("usr-sleeves");

        // Assert
        // G1: 110 estándar -> ceil(110/50) = 3 packs
        // G2: 40 mini -> ceil(40/50) = 1 pack; 30 estándar -> ceil(30/50) = 1 pack
        // Total cards: 110 + 40 + 30 = 180
        // Total packs: 3 + 1 + 1 = 5
        Assert.Equal(180, stats.SleevesRadar.TotalCards);
        Assert.Equal(5, stats.SleevesRadar.TotalPacksEstimated);
        Assert.Equal(2, stats.SleevesRadar.GamesRequiringSleevesCount);

        var topFormat = stats.SleevesRadar.TopFormats.First();
        Assert.Equal("Estándar 63.5 x 88 mm", topFormat.FormatName);
        Assert.Equal(140, topFormat.CardCount);
        Assert.Equal(4, topFormat.PacksNeeded);
    }

    [Fact]
    public async Task GetUserStatsAsync_ParsesMultipleDesignersCorrectly()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "G1", designer: "Alexander Pfister, Virginio Gigli");
        var g2 = CreateGame(2, "G2", designer: "Alexander Pfister / Simone Luciani");
        var g3 = CreateGame(3, "G3", designer: "Reiner Knizia y Bruno Cathala & Martin Wallace");
        gameRepo.Games.AddRange([g1, g2, g3]);

        collectionRepo.Items.AddRange([
            new UserCollectionItem("usr-auth", g1.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-auth", g2.Id, CollectionStatus.InCollection),
            new UserCollectionItem("usr-auth", g3.Id, CollectionStatus.InCollection)
        ]);

        // Act
        var stats = await service.GetUserStatsAsync("usr-auth");

        // Assert
        // Alexander Pfister aparece en 2 juegos (g1 y g2) -> Top 1
        Assert.NotEmpty(stats.TopDesigners);
        var topDesigner = stats.TopDesigners[0];
        Assert.Equal("Alexander Pfister", topDesigner.Name);
        Assert.Equal(2, topDesigner.Count);
        Assert.Equal(66.7, topDesigner.Percentage);

        // Verificar que Bruno Cathala, Reiner Knizia, etc. fueron extraídos
        var designerNames = stats.TopDesigners.Select(d => d.Name).ToList();
        Assert.Contains("Alexander Pfister", designerNames);
    }

    [Fact]
    public async Task GetUserStatsAsync_AssignsCorrectBadgesAndTraitsBasedOnThresholds()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        // 6 juegos en estantería: Nivel 2 "Explorador Lúdico" (rango 5–14)
        // 4 Eurogames de 6 => 66.7% Eurogame (>= 50%) => Rasgo "Cerebro Eurogamer 🧠"
        var games = new List<Game>();
        for (int i = 1; i <= 4; i++)
        {
            games.Add(CreateGame(i, $"Euro {i}", style: GameStyle.Eurogame));
        }
        games.Add(CreateGame(5, "Ameri 1", style: GameStyle.Ameritrash));
        games.Add(CreateGame(6, "Party 1", style: GameStyle.PartyGame));
        gameRepo.Games.AddRange(games);

        foreach (var g in games)
        {
            collectionRepo.Items.Add(new UserCollectionItem("usr-badge", g.Id, CollectionStatus.InCollection));
        }

        // Act
        var stats = await service.GetUserStatsAsync("usr-badge");

        // Assert
        Assert.Equal(2, stats.Badge.RankLevel);
        Assert.Equal("Explorador Lúdico", stats.Badge.RankName);
        Assert.Equal("Cerebro Eurogamer", stats.Badge.TraitName);
        Assert.Equal("🧠", stats.Badge.IconEmoji);
    }

    [Fact]
    public async Task GetPublicProfileAsync_ReturnsFullProfileForExistingUser()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        var g1 = CreateGame(1, "Terraforming Mars");
        gameRepo.Games.Add(g1);
        collectionRepo.Items.Add(new UserCollectionItem("usr-public", g1.Id, CollectionStatus.InCollection));

        // Act
        var profile = await service.GetPublicProfileAsync("usr-public");

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("usr-public", profile.UserId);
        Assert.Equal(1, profile.Stats.TotalGamesInCollection);
        Assert.Single(profile.ShelfGames);
        Assert.Equal("Terraforming Mars", profile.ShelfGames[0].GameTitle);
    }

    [Fact]
    public async Task GetPublicProfileAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var collectionRepo = new FakeCollectionRepo();
        var loanRepo = new FakeLoanRepo();
        var gameRepo = new FakeGameRepo();
        var currentUserService = new FakeCurrentUserService();
        var service = new UserLibraryStatsService(collectionRepo, loanRepo, gameRepo, currentUserService);

        // Act
        var profile = await service.GetPublicProfileAsync("usr-inexistente-xyz");

        // Assert
        Assert.Null(profile);
    }
}
