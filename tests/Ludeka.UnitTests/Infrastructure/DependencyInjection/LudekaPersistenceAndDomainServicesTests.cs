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
/// <c>AddLudekaExternalIntegrations</c>, que todavía no existe (llega en R2b) — y confirma que el
/// grafo de dependencias de persistencia y dominio se resuelve completo con
/// <c>validateOnBuild: true</c>, que convierte cualquier dependencia ausente en un fallo de
/// construcción con el nombre del servicio (diseño §4.6).
///
/// Los tipos que el diseño clasifica como <c>AddLudekaExternalIntegrations</c> (fila §4.1) siguen
/// registrados de forma inline en <c>Program.cs</c> hasta que R2b los extraiga; varios servicios de
/// dominio los exigen como dependencia dura de constructor (p. ej. <c>NightlyCatalogingService</c>
/// necesita <c>IBggClient</c>, <c>PriceRadarService</c> necesita <c>IStoreStockService</c>,
/// <c>CommunityNotificationService</c> necesita los dos clientes de webhook). Para que esta
/// composición AISLADA sea construible sin adelantar el trabajo de R2b, esta prueba registra dobles
/// mínimos de esos límites externos — nunca se invocan, solo satisfacen la forma del grafo — y la
/// composición real de <c>Program.cs</c> sigue proveyendo las implementaciones reales sin cambios,
/// porque ese fichero conserva inline sus propios registros hasta que R2b los mueva.
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

    private sealed class FakeBrokenLinkCheckerService : IBrokenLinkCheckerService
    {
        public Task<BrokenLinkReportDto> CheckLinksAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Registra dobles mínimos de los límites de <c>AddLudekaExternalIntegrations</c> (R2b, todavía
    /// no extraída) que los servicios de dominio de R2a exigen como dependencia dura de constructor.
    /// Ninguno se invoca: <c>validateOnBuild</c> solo necesita que el tipo exista en el contenedor.
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
            .AddSingleton<IBrokenLinkCheckerService, FakeBrokenLinkCheckerService>()
            .AddSingleton(new HttpClient());

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
    }

    // Nota de alcance (tasks.md 2.5): el riesgo 2 de la propuesta (orden de registro de
    // IEnumerable<ISocialChannelCollector> e IEnumerable<IStoreStockClient>) NO se afirma en esta
    // rebanada. Las dos colecciones completas viven enteramente en AddLudekaExternalIntegrations
    // (diseño §4.1, filas 204-209 y 307-318: ni siquiera ISocialCollectorService, IStoreStockService
    // — solo ISocialCollectorService cae en dominio, y no participa de ninguna de las dos
    // IEnumerable<T> en riesgo). No hay "parte de dominio" de ese orden que afirmar en la composición
    // aislada de R2a: la aserción completa de ambas secuencias queda en R2b, tarea 3.1, que sí aplica
    // AddLudekaApplicationCore con las cuatro extensiones ya disponibles. Desviación declarada.
}
