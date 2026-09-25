using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Admin;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Directory;
using Ludeka.Application.Features.Discovery;
using Ludeka.Application.Features.Events;
using Ludeka.Application.Features.Expansions;
using Ludeka.Application.Features.Founding;
using Ludeka.Application.Features.Home;
using Ludeka.Application.Features.Identity;
using Ludeka.Application.Features.Instagram;
using Ludeka.Application.Features.Jobs;
using Ludeka.Application.Features.Library;
using Ludeka.Application.Features.Media;
using Ludeka.Application.Features.Plays;
using Ludeka.Application.Features.Reports;
using Ludeka.Application.Features.Sleeves;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Bgg;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Notifications;
using Ludeka.Infrastructure.Options;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.Seeding;
using Ludeka.Infrastructure.Services;
using Ludeka.Infrastructure.Stores;
using Ludeka.Infrastructure.YouTube;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ludeka.Infrastructure.DependencyInjection;

/// <summary>
/// Raíz de composición de dominio e infraestructura de Ludeka (INC-47, diseño §4, decisión D1).
/// Vive en <c>Ludeka.Infrastructure</c> porque ya referencia <c>Ludeka.Application</c> →
/// <c>Ludeka.Core</c> y ya contiene todo lo que hay que registrar; es el único punto donde la
/// extracción no crea una dependencia nueva entre proyectos. <c>Ludeka.Web</c> y el futuro
/// <c>Ludeka.Jobs</c> son los dos únicos consumidores: ninguno de los métodos de aquí registra
/// nada específicamente web (Razor, cookies, antiforgery, output caching, health checks…), que
/// permanece en <c>Program.cs</c> junto con el resto de la frontera descrita en el diseño §4.1/§4.2.
/// </summary>
public static class LudekaServiceCollectionExtensions
{
    /// <summary>Opciones de base de datos, resolución de proveedor y <c>LudekaDbContext</c>.</summary>
    public static IServiceCollection AddLudekaPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AdminUserOptions>(configuration.GetSection(AdminUserOptions.SectionName));

        // Configuración de persistencia dual (SQLite local / PostgreSQL en Supabase) y Clean Architecture
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("PostgreSqlConnection")
            ?? "Data Source=ludeka.db";

        bool isPostgreSql = dbOptions.IsPostgreSql(connectionString);

        Action<DbContextOptionsBuilder> configureOptions = options =>
        {
            if (isPostgreSql)
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        };

        services.AddDbContext<LudekaDbContext>(configureOptions);
        services.AddDbContextFactory<LudekaDbContext>(configureOptions, ServiceLifetime.Scoped);

        return services;
    }

    /// <summary>Repositorios y servicios de dominio, incluidas las opciones que enlazan.</summary>
    public static IServiceCollection AddLudekaDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Incremento 46: cookie de sesión propia y esquemas sociales dirigidos por configuración.
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<IAccountConnectionsService, AccountConnectionsService>();

        // Incremento 64: acceso por correo mediante Magic Link.
        services.Configure<MagicLinkOptions>(configuration.GetSection(MagicLinkOptions.SectionName));
        services.AddScoped<IMagicLinkTokenRepository, MagicLinkTokenRepository>();
        services.AddScoped<IEmailSender, DevelopmentEmailSender>();
        services.AddScoped<IMagicLinkService, MagicLinkService>();

        // Incremento 65: «Me gusta» en Editoriales, Tiendas, Creadores y Vídeos
        services.AddScoped<IUserLikeRepository, SqliteUserLikeRepository>();

        services.AddScoped<IGameRepository, SqliteGameRepository>();

        // Caché Nivel 1 (Aplicación en Memoria) y Decorador del Catálogo
        services.AddMemoryCache();
        services.AddScoped<CatalogService>();
        services.AddScoped<ICatalogService>(sp =>
            new CachedCatalogService(
                sp.GetRequiredService<CatalogService>(),
                sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<IUserCollectionRepository, SqliteUserCollectionRepository>();
        services.AddScoped<IGameLoanRepository, SqliteGameLoanRepository>();
        services.AddScoped<IUserReviewRepository, SqliteUserReviewRepository>();
        services.AddScoped<IGamePlayLogRepository, SqliteGamePlayLogRepository>();
        services.AddScoped<IGamePlayLogService, GamePlayLogService>();
        services.AddScoped<IFoundingVerdictRepository, SqliteFoundingVerdictRepository>();
        services.AddScoped<IFoundingVerdictService, FoundingVerdictService>();

        services.AddScoped<IMediaRepository, SqliteMediaRepository>();
        // IBrokenLinkCheckerService (antigua Program.cs:152) se mueve aquí en R2b: ya referencia
        // Microsoft.Extensions.Http desde Ludeka.Infrastructure (deuda declarada por R2a, cerrada
        // por R2b, tasks.md 3.2).
        services.AddHttpClient<IBrokenLinkCheckerService, BrokenLinkCheckerService>();
        services.AddScoped<IMediaService, MediaService>();

        services.AddScoped<IPendingBggImportRepository, SqlitePendingBggImportRepository>();
        services.AddScoped<IBggImportService, BggImportService>();
        services.AddScoped<IBggCatalogQueueService, BggCatalogQueueService>();
        services.AddScoped<IBggSearchAssistedService, BggSearchAssistedService>();

        // Incremento 24: Detección Automática de Juegos en Novedades y Cola Nocturna Inteligente BGG/Gemini
        services.Configure<NightlyCatalogingOptions>(configuration.GetSection("NightlyCataloging"));
        services.AddScoped<INewsGameExtractor, NewsGameExtractor>();
        services.AddScoped<INightlyCatalogingLogRepository, SqliteNightlyCatalogingLogRepository>();
        services.AddScoped<IBggDiscoveryService, BggDiscoveryService>();
        services.AddScoped<INightlyCatalogingService, NightlyCatalogingService>();

        // Incremento 17: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas
        services.AddScoped<IGameIssueReportRepository, SqliteGameIssueReportRepository>();
        services.AddScoped<IGameIssueReportService, GameIssueReportService>();

        services.AddScoped<IGameEditLogRepository, SqliteGameEditLogRepository>();
        services.AddScoped<IGameEditorService, GameEditorService>();

        // Incremento 37: Motor Privado y Centralizado de Enlaces de Afiliado para Tiendas Colaboradoras
        services.Configure<AffiliateOptions>(configuration.GetSection(AffiliateOptions.SectionName));
        services.AddSingleton<IAffiliateUrlResolver, AffiliateUrlResolver>();

        // Incremento 26: Especificación de Fundas (Sleeves) por Juego y Enlaces de Compra Contextuales
        services.AddSingleton<ISleeveStoreUrlResolver, SleeveStoreUrlResolver>();

        // Incremento 45: Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'
        services.Configure<PriceRadarOptions>(configuration.GetSection(PriceRadarOptions.SectionName));
        services.AddScoped<IGamePriceRepository, SqliteGamePriceRepository>();
        services.AddScoped<IPriceRadarService, PriceRadarService>();

        // Incremento 6: Sorteos, Novedades del Viernes, Q&A de Reglas y Tarjetas Sociales
        services.AddScoped<IGiveawayRepository, SqliteGiveawayRepository>();
        services.AddScoped<IGiveawayService, GiveawayService>();
        services.AddScoped<IWeeklyReleaseRepository, SqliteWeeklyReleaseRepository>();
        services.AddScoped<IWeeklyReleaseService, WeeklyReleaseService>();
        services.AddScoped<IRuleQARepository, SqliteRuleQARepository>();
        services.AddScoped<IRuleQAService, RuleQAService>();
        services.AddScoped<ISocialCardService, SocialCardService>();

        // Incremento 28: Generador y Publicador Directo de Posts para Instagram en Moderación
        // (solo la porción de dominio; IInstagramApiClient es de AddLudekaExternalIntegrations, R2b)
        services.AddScoped<IInstagramPostDraftRepository, SqliteInstagramPostDraftRepository>();
        services.AddScoped<IInstagramComposerService, InstagramComposerService>();
        services.AddScoped<IInstagramPublisherService, InstagramPublisherService>();

        // Incremento 8: Expansiones, Sinergias y Mezclador de Mesa
        services.AddScoped<IExpansionRepository, SqliteExpansionRepository>();
        services.AddScoped<IExpansionService, ExpansionService>();

        // Incremento 9: Notificaciones y Webhooks de Comunidad (Discord y Telegram)
        services.AddScoped<ICommunityNotificationRepository, SqliteCommunityNotificationRepository>();
        services.AddScoped<ICommunityNotificationService, CommunityNotificationService>();

        // INC-47 (R4a, diseño §6.1/§5.4): repositorio y opciones del outbox de notificaciones.
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();

        // INC-47 (R4b, diseño §6.1/§6.2, tasks.md 6.11/7.10): cableado real de la cola sobre el
        // outbox persistente — la línea heredada de la tarea 2.2 (AddSingleton InMemory) pasa a
        // AddScoped sobre la implementación de outbox. InMemoryCommunityNotificationQueue solo
        // sobrevive bajo configuración explícita de desarrollo local (decisión de sdd-apply: el
        // diseño no fija el mecanismo exacto de selección; no es una comprobación de entorno).
        var outboxOptions = configuration.GetSection(OutboxOptions.SectionName).Get<OutboxOptions>() ?? new OutboxOptions();
        if (outboxOptions.UseInMemoryQueueForLocalDev)
        {
            services.AddSingleton<ICommunityNotificationQueue, InMemoryCommunityNotificationQueue>();
        }
        else
        {
            services.AddScoped<ICommunityNotificationQueue, OutboxCommunityNotificationQueue>();
        }

        services.AddScoped<INotificationOutboxDispatcher, NotificationOutboxDispatcher>();

        // INC-47 (R5, diseño §7.4, tasks.md 9.10-9.12/9.17): coordinador de idempotencia por
        // ventana e implementación del repositorio de concesiones, respaldados por
        // JobExecutionLeases (esquema de R3b). Los runners de Ludeka.Jobs lo consumen en vez de
        // estado en memoria.
        // INC-47 (R7, diseño §7.5, tasks.md 11.4): sección Workers — StaleLeaseMinutes deja de
        // vivir como constante en JobExecutionLeaseRepository (deuda declarada en tasks.md 9b.3).
        services.Configure<WorkersOptions>(configuration.GetSection(WorkersOptions.SectionName));
        services.AddScoped<IJobExecutionLeaseRepository, JobExecutionLeaseRepository>();
        services.AddScoped<IJobExecutionCoordinator, JobExecutionCoordinator>();

        // Incremento 46 (Paso 2) / hallazgo W1: biblioteca, estadísticas, preferencias y ubicación de
        // usuario. ISessionPermissionGuard e ICurrentUserService permanecen en Program.cs (diseño §4.2):
        // sus únicas implementaciones viven en Ludeka.Web.
        services.AddScoped<IUserLibraryService, UserLibraryService>();
        services.AddScoped<IUserLibraryStatsService, UserLibraryStatsService>();
        services.AddScoped<IUserPreferenceService, SqliteUserPreferenceService>();
        services.AddScoped<IUserLocationService, UserLocationService>();

        // Incremento 19: Directorio de Editoriales, Creadores y Tiendas con Foco Audiovisual
        services.AddScoped<IPublisherRepository, SqlitePublisherRepository>();
        services.AddScoped<ICreatorRepository, SqliteCreatorRepository>();
        services.AddScoped<IStoreRepository, SqliteStoreRepository>();
        services.AddScoped<IPublisherService, PublisherService>();
        services.AddScoped<ICreatorService, CreatorService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IChannelDirectoryProvider, ChannelDirectoryProvider>();
        services.AddScoped<IDirectorySeederService, DirectorySeederService>();

        // Incremento 20: Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora
        services.AddScoped<IUserRepository, SqliteUserRepository>();
        services.AddScoped<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAuditService, AuditService>();

        // Incremento 21/22: Dashboard de Inicio Editorial y Módulo Completo de Grandes Eventos Lúdicos
        services.AddScoped<IBoardGameEventRepository, SqliteBoardGameEventRepository>();
        services.AddScoped<HomeDashboardService>();
        services.AddScoped<IHomeDashboardService, CachedHomeDashboardService>(sp =>
            new CachedHomeDashboardService(
                sp.GetRequiredService<HomeDashboardService>(),
                sp.GetRequiredService<IMemoryCache>()));
        services.AddScoped<IBoardGameEventService, BoardGameEventService>();

        // Incremento 42: Hub de Ingesta Social y Multimedia — solo la porción de dominio (extractor de
        // metadatos y análisis IA son de AddLudekaExternalIntegrations, R2b, diseño §4.1 fila 299-304).
        services.AddScoped<ISocialInboxRepository, SqliteSocialInboxRepository>();
        services.AddScoped<IMonitoredAccountRepository, SqliteMonitoredAccountRepository>();
        services.AddScoped<ISocialIngestionService, SocialIngestionService>();
        services.AddScoped<IMonitoredAccountService, MonitoredAccountService>();

        // Incremento 44: Worker de Recolección Automática de Canales Sociales — solo ISocialCollectorService
        // es de dominio (diseño §4.1 fila 307-318); sus cuatro ISocialChannelCollector viven en
        // AddLudekaExternalIntegrations (R2b), que es donde tasks.md 3.1 afirma el orden completo de
        // esa IEnumerable<T>.
        services.AddScoped<ISocialCollectorService, SocialCollectorService>();

        return services;
    }

    /// <summary>Clientes HTTP tipados y adaptadores de servicios externos (BGG, Cloudflare R2,
    /// ingesta masiva BGG, tiendas, Instagram, Discord/Telegram, Gemini, YouTube, extracción/análisis
    /// social y feeds de canales sociales monitorizados). Diseño §4/D1, tabla §4.1.</summary>
    public static IServiceCollection AddLudekaExternalIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        // Incremento 23: cliente real BGG XML API2 con resiliencia y autenticación, con selector
        // hacia el cliente simulado según configuración.
        services.Configure<BggOptions>(configuration.GetSection(BggOptions.SectionName));
        services.AddTransient<BggResilienceAndAuthHandler>();
        services.AddHttpClient<BggXmlApiClient>()
            .AddHttpMessageHandler<BggResilienceAndAuthHandler>();
        services.AddSingleton<SimulatedBggClient>();

        services.AddScoped<IBggClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BggOptions>>().Value;
            return options.ShouldSimulate
                ? sp.GetRequiredService<SimulatedBggClient>()
                : sp.GetRequiredService<BggXmlApiClient>();
        });

        // Incremento 40: Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP)
        // Incremento 48 (PR1a, diseño D1): factoría de precedencia de tres vías — R2 válido, disco
        // local configurado (Media__LocalStoragePath), memoria — sustituye al selector binario original.
        services.Configure<CloudflareR2Options>(configuration.GetSection(CloudflareR2Options.SectionName));
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));
        services.AddSingleton<IImageOptimizationService, SkiaSharpImageOptimizationService>();
        services.AddScoped<CloudflareR2StorageService>();
        services.AddScoped<SimulatedImageStorageService>();
        services.AddScoped(sp =>
        {
            var env = sp.GetService<IHostEnvironment>();
            var mediaOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaOptions>>().Value;
            return new PhysicalFileImageStorageService(env, mediaOptions.ResolveLocalStoragePath(env?.ContentRootPath));
        });
        services.AddScoped<IImageStorageService>(sp =>
        {
            var r2Options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CloudflareR2Options>>().Value;
            if (r2Options.HasValidCredentials)
            {
                return sp.GetRequiredService<CloudflareR2StorageService>();
            }

            var mediaOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaOptions>>().Value;
            if (mediaOptions.HasLocalStoragePath)
            {
                return sp.GetRequiredService<PhysicalFileImageStorageService>();
            }

            return sp.GetRequiredService<SimulatedImageStorageService>();
        });

        // Incremento 41: Ingesta Masiva de Catálogo BGG, Fotos GeekDo y Síntesis IA en Lotes
        services.Configure<BggMassIngestionOptions>(configuration.GetSection(BggMassIngestionOptions.SectionName));
        services.AddScoped<IBggCatalogStagingRepository, SqliteBggCatalogStagingRepository>();
        services.AddHttpClient<IGeekDoImagesClient, GeekDoImagesClient>();
        services.AddHttpClient<IBggMassIngestionService, BggMassIngestionService>();

        // Incremento 27: Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra.
        // Orden significativo (diseño §4.3, preservado sin cambios): IEnumerable<IStoreStockClient>
        // resuelve Simulation antes de HtmlSchema, y la resolución singular de IStoreStockClient (si
        // alguien la pidiera) devolvería HtmlSchema por ser el último registro.
        services.Configure<StoreStockOptions>(configuration.GetSection(StoreStockOptions.SectionName));
        services.AddSingleton<SimulationStoreStockClient>();
        services.AddHttpClient<HtmlSchemaStoreStockClient>();
        services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<SimulationStoreStockClient>());
        services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<HtmlSchemaStoreStockClient>());
        services.AddScoped<IStoreStockService, StoreStockService>();

        // Incremento 28: Generador y Publicador Directo de Posts para Instagram en Moderación
        // (solo el cliente; IInstagramPostDraftRepository/IInstagramComposerService/IInstagramPublisherService
        // ya viven en AddLudekaDomainServices, INC-47 R2a)
        services.Configure<InstagramOptions>(configuration.GetSection(InstagramOptions.SectionName));
        services.AddHttpClient<IInstagramApiClient, InstagramApiClient>();

        // Incremento 9: Notificaciones y Webhooks de Comunidad (Discord y Telegram)
        // (solo los clientes; la cola/repositorio/servicio ya viven en AddLudekaDomainServices, INC-47 R2a)
        services.Configure<CommunityNotificationOptions>(configuration.GetSection(CommunityNotificationOptions.SectionName));
        services.AddHttpClient<IDiscordWebhookClient, DiscordWebhookClient>();
        services.AddHttpClient<ITelegramBotClient, TelegramBotClient>();

        // Incremento 13: Módulo de Síntesis con IA (Google Gemini / Heurística)
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<IAiGameSummaryService, GeminiGameSummaryService>();

        // Incremento 14: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real
        services.Configure<YouTubeOptions>(configuration.GetSection(YouTubeOptions.SectionName));
        services.AddSingleton<IChannelFocusProvider, ChannelFocusProvider>();
        services.AddHttpClient<IYouTubeSearchService, YouTubeSearchService>();

        // Incremento 42: Hub de Ingesta Social y Multimedia — solo los clientes de extracción/análisis;
        // el resto (repositorios, ISocialIngestionService, IMonitoredAccountService) ya vive en
        // AddLudekaDomainServices (INC-47 R2a, diseño §4.1 fila 299-304).
        services.AddHttpClient<ISocialMetadataExtractor, OpenGraphSocialMetadataExtractor>();
        services.AddHttpClient<ISocialAiAnalysisService, GeminiSocialAnalysisService>();

        // Incremento 44: Worker de Recolección Automática de Canales Sociales Monitorizados (YouTube
        // RSS / Telegram / Blogs / Instagram). Orden significativo (diseño §4.3, preservado sin
        // cambios): IEnumerable<ISocialChannelCollector> resuelve YouTube, Telegram, RSS, Instagram.
        services.Configure<SocialCollectorOptions>(configuration.GetSection(SocialCollectorOptions.SectionName));
        services.AddHttpClient<YouTubeFeedCollector>();
        services.AddHttpClient<TelegramChannelCollector>();
        services.AddHttpClient<RssBlogFeedCollector>();
        services.AddHttpClient<InstagramFeedCollector>();

        services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<YouTubeFeedCollector>());
        services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<TelegramChannelCollector>());
        services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<RssBlogFeedCollector>());
        services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<InstagramFeedCollector>());
        // ISocialCollectorService ya vive en AddLudekaDomainServices (INC-47 R2a, diseño §4.1 fila 307-318).

        return services;
    }

    /// <summary>Punto de entrada único de los dos hosts. Registra la composición completa de dominio
    /// e infraestructura, sin ningún registro específicamente web.</summary>
    public static IServiceCollection AddLudekaApplicationCore(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddLudekaPersistence(configuration)
            .AddLudekaDomainServices(configuration)
            .AddLudekaExternalIntegrations(configuration);
}
