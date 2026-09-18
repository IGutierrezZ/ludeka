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
using Ludeka.Application.Features.Library;
using Ludeka.Application.Features.Media;
using Ludeka.Application.Features.Plays;
using Ludeka.Application.Features.Reports;
using Ludeka.Application.Features.Sleeves;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Notifications;
using Ludeka.Infrastructure.Options;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddDbContext<LudekaDbContext>(options =>
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
        });

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
        // IBrokenLinkCheckerService (Program.cs, AddHttpClient) NO se mueve en R2a: registrarlo aquí
        // exigiría referenciar Microsoft.Extensions.Http desde Ludeka.Infrastructure, y ese paquete
        // se añade explícitamente en R2b (tasks.md 3.2). Desviación declarada, ver informe de sdd-apply.
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
        // Línea heredada sin cambios (diseño §4.1, fila 241): R4a la convierte a outbox más adelante.
        services.AddSingleton<ICommunityNotificationQueue, InMemoryCommunityNotificationQueue>();
        services.AddScoped<ICommunityNotificationRepository, SqliteCommunityNotificationRepository>();
        services.AddScoped<ICommunityNotificationService, CommunityNotificationService>();

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
        // es de dominio (diseño §4.1 fila 307-318); sus cuatro ISocialChannelCollector siguen en Program.cs
        // hasta R2b, así que el orden de esa IEnumerable<T> se afirma en la prueba de humo de R2b (3.1).
        services.AddScoped<ISocialCollectorService, SocialCollectorService>();

        return services;
    }
}
