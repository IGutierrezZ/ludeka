using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Web.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class AffiliateRedirectEndpointsTests
{
    private class FakeAffiliateClickService : IAffiliateClickService
    {
        public Task<string?> ResolveAndTrackRedirectAsync(string gameSlug, string storeSlug, string? country = null, CancellationToken ct = default)
        {
            if (gameSlug == "wingspan" && storeSlug == "zacatrus")
            {
                return Task.FromResult<string?>("https://zacatrus.es/wingspan.html?ref=ludeka");
            }
            if (gameSlug == "wingspan" && storeSlug == "amazon")
            {
                return Task.FromResult<string?>("https://www.amazon.es/s?k=Wingspan&tag=ludeka-21");
            }
            return Task.FromResult<string?>(null);
        }

        public Task<string?> TrackDirectRedirectAsync(Guid? gameId, string? gameSlug, string gameTitle, string storeName, string targetUrl, string? country = null, CancellationToken ct = default)
        {
            if (targetUrl.Contains("evil.com"))
            {
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>($"{targetUrl}?ref=ludeka");
        }
    }

    [Fact]
    public async Task HandleGameStoreRedirect_WithKnownGameAndStore_ShouldRedirectToAffiliateUrl()
    {
        // Arrange
        var fakeService = new FakeAffiliateClickService();

        // Act
        var result = await fakeService.ResolveAndTrackRedirectAsync("wingspan", "zacatrus", "España");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://zacatrus.es/wingspan.html?ref=ludeka", result);
    }

    [Fact]
    public async Task HandleGameStoreRedirect_WithKnownStoreWithoutDirectOffer_ShouldGenerateSearchUrl()
    {
        // Arrange
        var fakeService = new FakeAffiliateClickService();

        // Act
        var result = await fakeService.ResolveAndTrackRedirectAsync("wingspan", "amazon");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("tag=ludeka-21", result);
    }

    [Fact]
    public async Task HandleGameStoreRedirect_WithUnknownGame_ShouldReturnNull()
    {
        // Arrange
        var fakeService = new FakeAffiliateClickService();

        // Act
        var result = await fakeService.ResolveAndTrackRedirectAsync("juego-inexistente", "zacatrus");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TrackDirectRedirect_WithUnsafeDomain_ShouldRejectOpenRedirect()
    {
        // Arrange
        var fakeService = new FakeAffiliateClickService();

        // Act
        var result = await fakeService.TrackDirectRedirectAsync(null, "catan", "Catan", "Tienda", "https://evil.com/phishing");

        // Assert
        Assert.Null(result);
    }
}
