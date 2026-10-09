using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.ValueObjects;
using Ludeka.Web.Components.Shared;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class StoreOffersCardTests
{
    private class FakeLocationService : IUserLocationService
    {
        public string? CurrentCountry { get; set; } = "España";
        public string? DetectedCountry { get; set; } = "España";
        public string? EffectiveCountry => CurrentCountry ?? DetectedCountry;

        public void SetUserCountry(string? country) => CurrentCountry = country;
        public void SetDetectedCountry(string? country) => DetectedCountry = country;

        public Task<string?> GetEffectiveCountryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(EffectiveCountry);

        public Task<string?> GetDetectedCountryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DetectedCountry);

        public Task<UserLocationState> GetUserLocationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UserLocationState(CurrentCountry, DetectedCountry, EffectiveCountry));

        public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, Func<T, string?> countrySelector, string? preferredCountry = null)
            => items;

        public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, string? preferredCountry = null, Func<T, string?>? countrySelector = null)
            => items;
    }

    private class FakeStockService : IStoreStockService
    {
        public ValueTask<StoreStockInfo> GetStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(StoreStockInfo.InStock(price: 49.95m));

        public ValueTask<IReadOnlyDictionary<string, StoreStockInfo>> GetStockBatchAsync(IEnumerable<GamePurchaseLink> offers, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyDictionary<string, StoreStockInfo>>(new Dictionary<string, StoreStockInfo>());

        public void InvalidateStockCache(string affiliateUrl) { }
    }

    private class FakeAffiliateUrlResolver : IAffiliateUrlResolver
    {
        public string ResolveAffiliateUrl(string rawUrl, string? storeName = null) => rawUrl;
        public string BuildSearchUrl(string storeName, string searchQuery) => $"https://example.com/search?q={searchQuery}";
        public bool IsAllowedStoreUrl(string url) => true;
    }

    private class FakePriceRadarService : IPriceRadarService
    {
        public Task<GamePriceMetrics> GetGamePriceMetricsAsync(Guid gameId, CancellationToken ct = default)
            => Task.FromResult(GamePriceMetrics.Empty(gameId));

        public Task RecordPriceObservationAsync(Guid gameId, string storeName, string affiliateUrl, decimal price, bool inStock, string currency = "€", CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PriceDropAlertDto>> GetTopDiscountsAsync(int limit = 20, string? country = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PriceDropAlertDto>>([]);

        public Task<IReadOnlyList<PriceDropAlertDto>> GetUserWantToBuyAlertsAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PriceDropAlertDto>>([]);

        public Task<IReadOnlyList<PriceHistoryEntryDto>> GetGamePriceHistoryAsync(Guid gameId, int limit = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PriceHistoryEntryDto>>([]);

        public Task<int> ScanWantToBuyPricesAsync(int maxGames = 20, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private static StoreOffersCard CreateComponent(
        IReadOnlyList<GamePurchaseLink>? offers,
        string? gameTitle = "Wingspan",
        string? gameSlug = "wingspan",
        Guid? gameId = null)
    {
        var component = new StoreOffersCard
        {
            Offers = offers,
            GameTitle = gameTitle,
            GameSlug = gameSlug,
            GameId = gameId ?? Guid.NewGuid()
        };

        InjectService(component, "LocationService", new FakeLocationService());
        InjectService(component, "StockService", new FakeStockService());
        InjectService(component, "AffiliateResolver", new FakeAffiliateUrlResolver());
        InjectService(component, "PriceRadarService", new FakePriceRadarService());

        return component;
    }

    private static void InjectService<T>(StoreOffersCard component, string propertyName, T service)
    {
        var prop = typeof(StoreOffersCard).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(component, service);
    }

    private static async Task InvokeOnParametersSetAsync(StoreOffersCard component)
    {
        var method = typeof(StoreOffersCard).GetMethod(
            "OnParametersSetAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró OnParametersSetAsync.");

        var task = (Task)method.Invoke(component, null)!;
        await task;
    }

    private static IReadOnlyList<GamePurchaseLink> GetDisplayedOffers(StoreOffersCard component)
    {
        var field = typeof(StoreOffersCard).GetField(
            "_displayedOffers",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró _displayedOffers.");

        return (IReadOnlyList<GamePurchaseLink>)field.GetValue(component)!;
    }

    private static Dictionary<string, StoreStockInfo> GetStockByUrl(StoreOffersCard component)
    {
        var field = typeof(StoreOffersCard).GetField(
            "_stockByUrl",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró _stockByUrl.");

        return (Dictionary<string, StoreStockInfo>)field.GetValue(component)!;
    }

    [Fact]
    public async Task WhenGameHasPriceFromOtherStores_AmazonOptionDoesNotDisappear()
    {
        // Arrange: Un juego que tiene precio en Zacatrus (por feed o catálogo)
        var zacatrusOffer = new GamePurchaseLink(
            storeName: "Zacatrus",
            affiliateUrl: "https://zacatrus.es/wingspan.html",
            price: 49.95m,
            currency: "€",
            inStock: true,
            badge: "Envío 24h",
            country: "España");

        var component = CreateComponent(new[] { zacatrusOffer }, gameTitle: "Wingspan");

        // Act
        await InvokeOnParametersSetAsync(component);

        // Assert: Ambas opciones deben estar presentes
        var displayed = GetDisplayedOffers(component);
        Assert.Contains(displayed, o => string.Equals(o.StoreName, "Zacatrus", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(displayed, o => string.Equals(o.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase));

        // Comprobar que Amazon no aparece como falso agotado
        var amazonOffer = displayed.First(o => string.Equals(o.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase));
        var stockMap = GetStockByUrl(component);
        Assert.True(stockMap.ContainsKey(amazonOffer.AffiliateUrl));
        Assert.False(stockMap[amazonOffer.AffiliateUrl].IsOutOfStock);
    }

    [Fact]
    public async Task WhenGameAlreadyHasAmazonPrice_AmazonOfferIsPreservedWithoutDuplication()
    {
        // Arrange: Un juego que ya tiene precio tanto en Zacatrus como en Amazon
        var zacatrusOffer = new GamePurchaseLink(
            storeName: "Zacatrus",
            affiliateUrl: "https://zacatrus.es/wingspan.html",
            price: 49.95m,
            currency: "€",
            inStock: true);
        var amazonOffer = new GamePurchaseLink(
            storeName: "Amazon",
            affiliateUrl: "https://www.amazon.es/dp/B07MZT757D?tag=ludeka-21",
            price: 52.00m,
            currency: "€",
            inStock: true);

        var component = CreateComponent(new[] { zacatrusOffer, amazonOffer }, gameTitle: "Wingspan");

        // Act
        await InvokeOnParametersSetAsync(component);

        // Assert: No debe haber Amazon duplicado y debe conservar su precio real
        var displayed = GetDisplayedOffers(component);
        Assert.Equal(1, displayed.Count(o => string.Equals(o.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase)));
        var resolvedAmazon = displayed.First(o => string.Equals(o.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(52.00m, resolvedAmazon.Price);
    }

    [Fact]
    public async Task WhenGameHasNoOffers_FallbackIncludesAmazon()
    {
        // Arrange: Juego sin ofertas comerciales registradas
        var component = CreateComponent([], gameTitle: "Catan");

        // Act
        await InvokeOnParametersSetAsync(component);

        // Assert: Fallback contiene Amazon
        var displayed = GetDisplayedOffers(component);
        Assert.NotEmpty(displayed);
        Assert.Contains(displayed, o => string.Equals(o.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WhenGameHasUpcomingReprint_LoadsUpcomingReprintDetails()
    {
        var gameId = Guid.NewGuid();
        var component = CreateComponent([], gameTitle: "Spirit Island", gameId: gameId);

        var fakeReleaseService = new FakeWeeklyReleaseService
        {
            ReprintToReturn = new WeeklyReleaseDto(
                Id: Guid.NewGuid(),
                Title: "Spirit Island",
                Publisher: "Arrakis Games",
                ReleaseDate: new DateOnly(2026, 11, 1),
                GameId: gameId,
                CoverImageUrl: "https://arrakisgames.com/spirit.png",
                EstimatedPvp: 84.95m,
                IsReprint: true,
                Notes: "Reimpresión oficial anunciada",
                InstagramPermalink: null,
                IsPublishedOnInstagram: false,
                SourceUrl: "https://arrakisgames.com/spirit-island/",
                CreatedAt: DateTimeOffset.UtcNow,
                IsMonthOnly: true,
                Status: Ludeka.Core.Enums.WeeklyReleaseStatus.Published,
                AiSuggestedBggId: null,
                AiSuggestedTitle: null,
                AiMatchReasoning: null)
        };

        InjectService(component, "WeeklyReleaseService", fakeReleaseService);

        // Act
        await InvokeOnParametersSetAsync(component);

        // Assert: El campo privado _upcomingReprint tiene los datos de Arrakis
        var field = typeof(StoreOffersCard).GetField("_upcomingReprint", BindingFlags.Instance | BindingFlags.NonPublic);
        var reprint = field?.GetValue(component) as WeeklyReleaseDto;

        Assert.NotNull(reprint);
        Assert.Equal("Arrakis Games", reprint.Publisher);
        Assert.True(reprint.IsReprint);
        Assert.Equal("https://arrakisgames.com/spirit-island/", reprint.SourceUrl);
    }

    private class FakeWeeklyReleaseService : IWeeklyReleaseService
    {
        public WeeklyReleaseDto? ReprintToReturn { get; set; }

        public Task<WeeklyReleaseDto?> GetUpcomingReprintByGameIdAsync(Guid gameId, CancellationToken ct = default)
            => Task.FromResult(ReprintToReturn);

        public Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WeeklyReleaseDto>>([]);
        public Task<IReadOnlyList<WeeklyReleaseDto>> GetPendingModerationReleasesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WeeklyReleaseDto>>([]);
        public Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<WeeklyReleaseDto?>(null);
        public Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WeeklyReleaseDto> ApproveReleaseAsync(Guid id, Guid? linkedGameId = null, bool useAiSuggestionIfAvailable = true, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RejectReleaseAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteReleaseAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
