using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Library;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure.DependencyInjection;

/// <summary>
/// Prueba de humo de la rebanada R2a (INC-47, diseño §4/§4.6): aplica únicamente
/// <see cref="LudekaServiceCollectionExtensions.AddLudekaPersistence"/> y
/// <see cref="LudekaServiceCollectionExtensions.AddLudekaDomainServices"/> — sin
/// <c>AddLudekaExternalIntegrations</c> (que R2b ya extrajo, pero esta prueba sigue afirmando la
/// composición AISLADA de persistencia+dominio a propósito, como red de seguridad independiente de
/// esa costura) — y confirma que el grafo de dependencias de persistencia y dominio se resuelve
/// completo con <c>validateOnBuild: true</c>, que convierte cualquier dependencia ausente en un
/// fallo de construcción con el nombre del servicio (diseño §4.6).
///
/// Los tipos que el diseño clasifica como <c>AddLudekaExternalIntegrations</c> (fila §4.1) viven
/// ahora en esa extensión real (INC-47 R2b); varios servicios de dominio los exigen como
/// dependencia dura de constructor (p. ej. <c>NightlyCatalogingService</c> necesita
/// <c>IBggClient</c>, <c>PriceRadarService</c> necesita <c>IStoreStockService</c>,
/// <c>CommunityNotificationService</c> necesita los dos clientes de webhook). Para que esta
/// composición AISLADA siga siendo construible sin arrastrar <c>AddLudekaExternalIntegrations</c>
/// completa, esta prueba registra dobles mínimos de esos límites externos — nunca se invocan, solo
/// satisfacen la forma del grafo — y la composición real de <c>AddLudekaApplicationCore</c> sigue
/// proveyendo las implementaciones reales sin cambios.
/// </summary>
public class LudekaPersistenceAndDomainServicesTests
{
    private sealed class FakeBggClient : IBggClient
    {
        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeStoreStockService : IStoreStockService
    {
        public ValueTask<StoreStockInfo> GetStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public ValueTask<IReadOnlyDictionary<string, StoreStockInfo>> GetStockBatchAsync(IEnumerable<GamePurchaseLink> offers, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public void InvalidateStockCache(string affiliateUrl) => throw new NotImplementedException();
    }

    private sealed class FakeDiscordWebhookClient : IDiscordWebhookClient
    {
        public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => string.Empty;
        public string UserName => string.Empty;
        public IReadOnlyList<string> Roles => [];
        public bool IsFoundingTeam => false;
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    private sealed class FakeInstagramApiClient : IInstagramApiClient
    {
        public Task<string> CreateMediaContainerAsync(string imageUrl, string caption, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string> PublishMediaAsync(string creationId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSocialMetadataExtractor : ISocialMetadataExtractor
    {
        public Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSocialAiAnalysisService : ISocialAiAnalysisService
    {
        public Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(
            string? rulesText = null,
            byte[]? rulesImageBytes = null,
            string? rulesImageMimeType = null,
            byte[]? coverImageBytes = null,
            string? coverImageMimeType = null,
            string? authorOrChannel = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public string GetPublicUrl(string objectKey) => throw new NotImplementedException();
        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<GameImageUploadResult> SaveCommunityImageAsync(string subfolder, string identifier, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeYouTubeSearchService : IYouTubeSearchService
    {
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchTutorialsAsync(string gameTitle, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchPlaythroughsAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchConsolidatedCandidatesAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MediaItemDto> IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestConsolidatedForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeDevirReleasesExtractor : IDevirReleasesExtractor
    {
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html) => throw new NotImplementedException();
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html, DateOnly? referenceDate = null) => throw new NotImplementedException();
        public Task<DevirProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default) => throw new NotImplementedException();
        public DevirProductGalleryDto? ParseProductGalleryHtml(string html) => throw new NotImplementedException();
        public Task<DevirCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default) => throw new NotImplementedException();
        public DevirCatalogPageResultDto ParseCatalogPageHtml(string html) => throw new NotImplementedException();
    }

    private sealed class FakeMalditoReleasesExtractor : IMalditoReleasesExtractor
    {
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => throw new NotImplementedException();
    }

    /// <summary>
    /// Registra dobles mínimos de los límites de <c>AddLudekaExternalIntegrations</c> (R2b) que los
    /// servicios de dominio de R2a exigen como dependencia dura de constructor. Ninguno se invoca:
    /// <c>validateOnBuild</c> solo necesita que el tipo exista en el contenedor.
    ///
    /// INC-47 R2b retiró de aquí los dobles de <c>IBrokenLinkCheckerService</c> y de <see
    /// cref="HttpClient"/> desnudo: ambos quedan cubiertos por el registro REAL que
    /// <c>AddLudekaDomainServices</c> añade ahora para <c>IBrokenLinkCheckerService</c> vía
    /// <c>AddHttpClient&lt;T,U&gt;</c>, cuyo efecto colateral (<c>TryAddTransient&lt;HttpClient&gt;</c>)
    /// también satisface a <c>SocialIngestionService</c>. Confirmado en verde tras la extracción, no
    /// asumido: eran dobles que la deuda de paquete de R2a obligaba, no dobles permanentes de frontera.
    /// </summary>
    private static IServiceCollection AddExternalIntegrationBoundaryDoubles(IServiceCollection services) =>
        services
            .AddSingleton<IBggClient, FakeBggClient>()
            .AddSingleton<IStoreStockService, FakeStoreStockService>()
            .AddSingleton<IDiscordWebhookClient, FakeDiscordWebhookClient>()
            .AddSingleton<ITelegramBotClient, FakeTelegramBotClient>()
            .AddSingleton<ICurrentUserService, FakeCurrentUserService>()
            .AddSingleton<IInstagramApiClient, FakeInstagramApiClient>()
            .AddSingleton<ISocialMetadataExtractor, FakeSocialMetadataExtractor>()
            .AddSingleton<ISocialAiAnalysisService, FakeSocialAiAnalysisService>()
            .AddSingleton<IImageStorageService, FakeImageStorageService>()
            .AddSingleton<IYouTubeSearchService, FakeYouTubeSearchService>()
            .AddSingleton<IDevirReleasesExtractor, FakeDevirReleasesExtractor>()
            .AddSingleton<IMalditoReleasesExtractor, FakeMalditoReleasesExtractor>();

    [Fact]
    public void AddLudekaPersistenceAndAddLudekaDomainServices_ResolveExpectedDomainServices()
    {
        var services = new ServiceCollection();
        // Host.CreateApplicationBuilder / WebApplication.CreateBuilder registran ILogger<T> por
        // defecto; un ServiceCollection aislado necesita pedirlo explícitamente. No es un doble de
        // frontera externa: es infraestructura de logging que ambos hosts reales ya instalan.
        services.AddLogging();
        AddExternalIntegrationBoundaryDoubles(services);

        var configuration = new ConfigurationBuilder().Build();
        services.AddLudekaPersistence(configuration);
        services.AddLudekaDomainServices(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.IsType<LudekaDbContext>(sp.GetRequiredService<LudekaDbContext>());
        Assert.IsAssignableFrom<INightlyCatalogingService>(sp.GetRequiredService<INightlyCatalogingService>());
        Assert.IsAssignableFrom<IPriceRadarService>(sp.GetRequiredService<IPriceRadarService>());
        Assert.IsAssignableFrom<ICommunityNotificationService>(sp.GetRequiredService<ICommunityNotificationService>());
        Assert.IsAssignableFrom<IGiveawayService>(sp.GetRequiredService<IGiveawayService>());
        Assert.IsAssignableFrom<IUserLibraryService>(sp.GetRequiredService<IUserLibraryService>());
        Assert.IsAssignableFrom<IYouTubeCatalogAutoIngestService>(sp.GetRequiredService<IYouTubeCatalogAutoIngestService>());
        Assert.IsAssignableFrom<IEditorialReleasesSyncService>(sp.GetRequiredService<IEditorialReleasesSyncService>());
    }

    // Nota de alcance (tasks.md 2.5, cerrada por R2b): el riesgo 2 de la propuesta (orden de registro
    // de IEnumerable<ISocialChannelCollector> e IEnumerable<IStoreStockClient>) NO se afirma en esta
    // rebanada. Las dos colecciones completas viven enteramente en AddLudekaExternalIntegrations
    // (diseño §4.1, filas 204-209 y 307-318: ni siquiera IStoreStockService cae en dominio — solo
    // ISocialCollectorService lo hace, y no participa de ninguna de las dos IEnumerable<T> en riesgo).
    // No hay "parte de dominio" de ese orden que afirmar en esta composición aislada: la aserción
    // completa de ambas secuencias vive en
    // LudekaServiceCollectionExtensionsTests.AddLudekaApplicationCore_ResolvesFullCompositionWithoutWebRegistrations
    // (INC-47 R2b, tasks.md 3.1), que sí aplica las cuatro extensiones.
}
