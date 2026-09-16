using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Expansions;
using Ludeka.Application.Features.Founding;
using Ludeka.Application.Features.Library;
using Ludeka.Application.Features.Media;
using Ludeka.Application.Features.Reports;
using Ludeka.Application.Features.Directory;
using Ludeka.Application.Features.Admin;
using Ludeka.Application.Features.Home;
using Ludeka.Application.Features.Events;
using Ludeka.Application.Features.Sleeves;
using Ludeka.Application.Features.Instagram;
using Ludeka.Application.Features.Plays;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Application.Features.Identity;
using Ludeka.Infrastructure.Bgg;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.Seeding;
using Ludeka.Infrastructure.Services;
using Ludeka.Infrastructure.Notifications;
using Ludeka.Infrastructure.YouTube;
using Ludeka.Application.Features.Discovery;
using Ludeka.Infrastructure.Background;
using Ludeka.Infrastructure.Stores;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Web.Components;
using Ludeka.Web.Authentication;
using Ludeka.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ludeka.Web.Health;
using Ludeka.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Mvc;
using AuthenticationOptions = Ludeka.Application.Features.Identity.AuthenticationOptions;

var builder = WebApplication.CreateBuilder(args);

// Soporte de puerto dinámico para Google Cloud Run y entornos de contenedores
var cloudRunPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(cloudRunPort) && int.TryParse(cloudRunPort, out var parsedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configuración de Opciones de Base de Datos y Administrador
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<AdminUserOptions>(builder.Configuration.GetSection(AdminUserOptions.SectionName));

// Incremento 46: cookie de sesión propia y esquemas sociales dirigidos por configuración.
// Un proveedor habilitado sin credenciales no tumba el arranque: se avisa y no se registra.
var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>() ?? new AuthenticationOptions();
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));
builder.Services.AddLudekaAuthentication(authenticationOptions);
builder.Services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
builder.Services.AddScoped<IExternalLoginService, ExternalLoginService>();

// Configuración de persistencia dual (SQLite local / PostgreSQL en Supabase) y Clean Architecture
var dbOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("PostgreSqlConnection")
    ?? "Data Source=ludeka.db";

bool isPostgreSql = dbOptions.IsPostgreSql(connectionString);

builder.Services.AddDbContext<LudekaDbContext>(options =>
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

builder.Services.AddScoped<IGameRepository, SqliteGameRepository>();

// Caché Nivel 1 (Aplicación en Memoria) y Decorador del Catálogo
builder.Services.AddMemoryCache();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ICatalogService>(sp =>
    new CachedCatalogService(
        sp.GetRequiredService<CatalogService>(),
        sp.GetRequiredService<IMemoryCache>()));

// Caché Nivel 2 (HTTP / Output Caching con Tags de Invalidación)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("CatalogCache", policy =>
        policy.Expire(TimeSpan.FromMinutes(10))
              .Tag("tag-catalog"));

    options.AddPolicy("RadarCache", policy =>
        policy.Expire(TimeSpan.FromMinutes(5))
              .Tag("tag-radar"));

    options.AddPolicy("StaticPages", policy =>
        policy.Expire(TimeSpan.FromMinutes(60))
              .Tag("tag-static"));
});

builder.Services.Configure<BggOptions>(builder.Configuration.GetSection(BggOptions.SectionName));
builder.Services.AddTransient<BggResilienceAndAuthHandler>();
builder.Services.AddHttpClient<BggXmlApiClient>()
    .AddHttpMessageHandler<BggResilienceAndAuthHandler>();
builder.Services.AddSingleton<SimulatedBggClient>();

builder.Services.AddScoped<IBggClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BggOptions>>().Value;
    return options.ShouldSimulate
        ? sp.GetRequiredService<SimulatedBggClient>()
        : sp.GetRequiredService<BggXmlApiClient>();
});

builder.Services.AddScoped<IUserCollectionRepository, SqliteUserCollectionRepository>();
builder.Services.AddScoped<IGameLoanRepository, SqliteGameLoanRepository>();
builder.Services.AddScoped<IUserReviewRepository, SqliteUserReviewRepository>();
builder.Services.AddScoped<IGamePlayLogRepository, SqliteGamePlayLogRepository>();
builder.Services.AddScoped<IGamePlayLogService, GamePlayLogService>();
builder.Services.AddScoped<IFoundingVerdictRepository, SqliteFoundingVerdictRepository>();
builder.Services.AddScoped<IFoundingVerdictService, FoundingVerdictService>();

builder.Services.AddScoped<IMediaRepository, SqliteMediaRepository>();
builder.Services.AddHttpClient<IBrokenLinkCheckerService, BrokenLinkCheckerService>();
builder.Services.AddScoped<IMediaService, MediaService>();

builder.Services.AddScoped<IPendingBggImportRepository, SqlitePendingBggImportRepository>();
builder.Services.AddScoped<IBggImportService, BggImportService>();
builder.Services.AddScoped<IBggCatalogQueueService, BggCatalogQueueService>();
builder.Services.AddScoped<IBggSearchAssistedService, BggSearchAssistedService>();

// Incremento 24: Detección Automática de Juegos en Novedades y Cola Nocturna Inteligente BGG/Gemini
builder.Services.Configure<NightlyCatalogingOptions>(builder.Configuration.GetSection("NightlyCataloging"));
builder.Services.AddScoped<INewsGameExtractor, NewsGameExtractor>();
builder.Services.AddScoped<INightlyCatalogingLogRepository, SqliteNightlyCatalogingLogRepository>();
builder.Services.AddScoped<IBggDiscoveryService, BggDiscoveryService>();
builder.Services.AddScoped<INightlyCatalogingService, NightlyCatalogingService>();
builder.Services.AddHostedService<NightlyCatalogingHostedService>();

// Incremento 17: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas
builder.Services.AddScoped<IGameIssueReportRepository, SqliteGameIssueReportRepository>();
builder.Services.AddScoped<IGameIssueReportService, GameIssueReportService>();

// Incremento 40: Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP)
builder.Services.Configure<CloudflareR2Options>(builder.Configuration.GetSection(CloudflareR2Options.SectionName));
builder.Services.AddSingleton<IImageOptimizationService, SkiaSharpImageOptimizationService>();
builder.Services.AddScoped<CloudflareR2StorageService>();
builder.Services.AddScoped<SimulatedImageStorageService>();
builder.Services.AddScoped<PhysicalFileImageStorageService>();
builder.Services.AddScoped<IImageStorageService>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CloudflareR2Options>>().Value;
    if (options.HasValidCredentials)
    {
        return sp.GetRequiredService<CloudflareR2StorageService>();
    }
    return sp.GetRequiredService<SimulatedImageStorageService>();
});
// Incremento 41: Ingesta Masiva de Catálogo BGG, Fotos GeekDo y Síntesis IA en Lotes
builder.Services.Configure<BggMassIngestionOptions>(builder.Configuration.GetSection(BggMassIngestionOptions.SectionName));
builder.Services.AddScoped<IBggCatalogStagingRepository, SqliteBggCatalogStagingRepository>();
builder.Services.AddHttpClient<IGeekDoImagesClient, GeekDoImagesClient>();
builder.Services.AddHttpClient<IBggMassIngestionService, BggMassIngestionService>();

builder.Services.AddScoped<IGameEditLogRepository, SqliteGameEditLogRepository>();
builder.Services.AddScoped<IGameEditorService, GameEditorService>();

// Incremento 37: Motor Privado y Centralizado de Enlaces de Afiliado para Tiendas Colaboradoras
builder.Services.Configure<AffiliateOptions>(builder.Configuration.GetSection(AffiliateOptions.SectionName));
builder.Services.AddSingleton<IAffiliateUrlResolver, AffiliateUrlResolver>();

// Incremento 26: Especificación de Fundas (Sleeves) por Juego y Enlaces de Compra Contextuales
builder.Services.AddSingleton<ISleeveStoreUrlResolver, SleeveStoreUrlResolver>();

// Incremento 27: Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra
builder.Services.Configure<StoreStockOptions>(builder.Configuration.GetSection(StoreStockOptions.SectionName));
builder.Services.AddSingleton<SimulationStoreStockClient>();
builder.Services.AddHttpClient<HtmlSchemaStoreStockClient>();
builder.Services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<SimulationStoreStockClient>());
builder.Services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<HtmlSchemaStoreStockClient>());
builder.Services.AddScoped<IStoreStockService, StoreStockService>();

// Incremento 45: Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'
builder.Services.Configure<PriceRadarOptions>(builder.Configuration.GetSection(PriceRadarOptions.SectionName));
builder.Services.AddScoped<IGamePriceRepository, SqliteGamePriceRepository>();
builder.Services.AddScoped<IPriceRadarService, PriceRadarService>();
builder.Services.AddHostedService<PriceRadarHostedService>();

// Incremento 6: Sorteos, Novedades del Viernes, Q&A de Reglas y Tarjetas Sociales
builder.Services.AddScoped<IGiveawayRepository, SqliteGiveawayRepository>();
builder.Services.AddScoped<IGiveawayService, GiveawayService>();
builder.Services.AddScoped<IWeeklyReleaseRepository, SqliteWeeklyReleaseRepository>();
builder.Services.AddScoped<IWeeklyReleaseService, WeeklyReleaseService>();
builder.Services.AddScoped<IRuleQARepository, SqliteRuleQARepository>();
builder.Services.AddScoped<IRuleQAService, RuleQAService>();
builder.Services.AddScoped<ISocialCardService, SocialCardService>();

// Incremento 28: Generador y Publicador Directo de Posts para Instagram en Moderación
builder.Services.Configure<InstagramOptions>(builder.Configuration.GetSection(InstagramOptions.SectionName));
builder.Services.AddHttpClient<IInstagramApiClient, InstagramApiClient>();
builder.Services.AddScoped<IInstagramPostDraftRepository, SqliteInstagramPostDraftRepository>();
builder.Services.AddScoped<IInstagramComposerService, InstagramComposerService>();
builder.Services.AddScoped<IInstagramPublisherService, InstagramPublisherService>();

// Incremento 8: Expansiones, Sinergias y Mezclador de Mesa
builder.Services.AddScoped<IExpansionRepository, SqliteExpansionRepository>();
builder.Services.AddScoped<IExpansionService, ExpansionService>();

// Incremento 9: Notificaciones y Webhooks de Comunidad (Discord y Telegram)
builder.Services.Configure<CommunityNotificationOptions>(builder.Configuration.GetSection(CommunityNotificationOptions.SectionName));
builder.Services.AddHttpClient<IDiscordWebhookClient, DiscordWebhookClient>();
builder.Services.AddHttpClient<ITelegramBotClient, TelegramBotClient>();
builder.Services.AddSingleton<ICommunityNotificationQueue, InMemoryCommunityNotificationQueue>();
builder.Services.AddScoped<ICommunityNotificationRepository, SqliteCommunityNotificationRepository>();
builder.Services.AddScoped<ICommunityNotificationService, CommunityNotificationService>();
builder.Services.AddHostedService<CommunityNotificationDispatcherHostedService>();

// Incremento 46 (Paso 2): la identidad se resuelve desde la sesión autenticada y la simulación
// se retira. Scoped por ámbito (petición SSR o circuito) y poblada por UserCircuitHandler; sin
// sesión, UserId vacío y todo denegado, sin usuario centinela.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserIdentitySnapshot>();
builder.Services.AddScoped<ICurrentUserService, AuthenticatedCurrentUserService>();
builder.Services.AddScoped<CircuitHandler, UserCircuitHandler>();
builder.Services.AddScoped<IUserLibraryService, UserLibraryService>();
builder.Services.AddScoped<IUserLibraryStatsService, UserLibraryStatsService>();
builder.Services.AddScoped<IUserPreferenceService, SqliteUserPreferenceService>();
builder.Services.AddScoped<IUserLocationService, UserLocationService>();

// Incremento 13: Módulo de Síntesis con IA (Google Gemini / Heurística)
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddHttpClient<IAiGameSummaryService, GeminiGameSummaryService>();

// Incremento 14: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real
builder.Services.Configure<YouTubeOptions>(builder.Configuration.GetSection(YouTubeOptions.SectionName));
builder.Services.AddSingleton<IChannelFocusProvider, ChannelFocusProvider>();
builder.Services.AddHttpClient<IYouTubeSearchService, YouTubeSearchService>();

// Incremento 19: Directorio de Editoriales, Creadores y Tiendas con Foco Audiovisual
builder.Services.AddScoped<IPublisherRepository, SqlitePublisherRepository>();
builder.Services.AddScoped<ICreatorRepository, SqliteCreatorRepository>();
builder.Services.AddScoped<IStoreRepository, SqliteStoreRepository>();
builder.Services.AddScoped<IPublisherService, PublisherService>();
builder.Services.AddScoped<ICreatorService, CreatorService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IChannelDirectoryProvider, ChannelDirectoryProvider>();

// Incremento 20: Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora
builder.Services.AddScoped<IUserRepository, SqliteUserRepository>();
builder.Services.AddScoped<IAuditLogRepository, SqliteAuditLogRepository>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Incremento 21: Dashboard de Inicio Editorial y Eventos Lúdicos
builder.Services.AddScoped<IBoardGameEventRepository, SqliteBoardGameEventRepository>();
builder.Services.AddScoped<HomeDashboardService>();
builder.Services.AddScoped<IHomeDashboardService, CachedHomeDashboardService>(sp =>
    new CachedHomeDashboardService(
        sp.GetRequiredService<HomeDashboardService>(),
        sp.GetRequiredService<IMemoryCache>()));

// Incremento 22: Módulo Completo de Grandes Eventos Lúdicos
builder.Services.AddScoped<IBoardGameEventService, BoardGameEventService>();

// Incremento 42: Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés + Directorio de Cuentas)
builder.Services.AddScoped<ISocialInboxRepository, SqliteSocialInboxRepository>();
builder.Services.AddScoped<IMonitoredAccountRepository, SqliteMonitoredAccountRepository>();
builder.Services.AddHttpClient<ISocialMetadataExtractor, OpenGraphSocialMetadataExtractor>();
builder.Services.AddHttpClient<ISocialAiAnalysisService, GeminiSocialAnalysisService>();
builder.Services.AddScoped<ISocialIngestionService, SocialIngestionService>();
builder.Services.AddScoped<IMonitoredAccountService, MonitoredAccountService>();

// Incremento 44: Worker de Recolección Automática de Canales Sociales Monitorizados (YouTube RSS / Telegram / Blogs / Instagram)
builder.Services.Configure<SocialCollectorOptions>(builder.Configuration.GetSection(SocialCollectorOptions.SectionName));
builder.Services.AddHttpClient<YouTubeFeedCollector>();
builder.Services.AddHttpClient<TelegramChannelCollector>();
builder.Services.AddHttpClient<RssBlogFeedCollector>();
builder.Services.AddHttpClient<InstagramFeedCollector>();

builder.Services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<YouTubeFeedCollector>());
builder.Services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<TelegramChannelCollector>());
builder.Services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<RssBlogFeedCollector>());
builder.Services.AddScoped<ISocialChannelCollector>(sp => sp.GetRequiredService<InstagramFeedCollector>());

builder.Services.AddScoped<ISocialCollectorService, SocialCollectorService>();
builder.Services.AddHostedService<SocialCollectorHostedService>();

// Incremento 10: Observabilidad con Health Checks Oficiales de ASP.NET Core
builder.Services.AddHealthChecks()
    .AddCheck<SqliteDatabaseHealthCheck>("sqlite_db", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"])
    .AddCheck<NotificationQueueHealthCheck>("notification_queue", tags: ["ready"]);

var app = builder.Build();

// Aviso explícito de proveedores habilitados sin credenciales: la aplicación arranca sin ellos.
foreach (var authenticationWarning in ExternalAuthenticationSchemes.GetConfigurationWarnings(authenticationOptions))
{
    app.Logger.LogWarning("{AuthenticationWarning}", authenticationWarning);
}

// Inicialización automática y siembra del catálogo Offline-First con resiliencia de directorios en Docker
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dbOptionsValue = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var adminOptionsValue = scope.ServiceProvider.GetRequiredService<IOptions<AdminUserOptions>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var effectiveConn = config.GetConnectionString("DefaultConnection")
        ?? config.GetConnectionString("PostgreSqlConnection")
        ?? "Data Source=ludeka.db";

    if (!dbOptionsValue.IsPostgreSql(effectiveConn))
    {
        var match = Regex.Match(effectiveConn, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var rawPath = match.Groups[1].Value.Trim();
            var dir = Path.GetDirectoryName(rawPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    var db = scope.ServiceProvider.GetRequiredService<LudekaDbContext>();

    // En PostgreSQL (Supabase) se ejecutan las migraciones oficiales de Entity Framework Core.
    // En SQLite local se utiliza EnsureCreated con el reconciliador defensivo.
    if (db.Database.IsNpgsql())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
        if (db.Database.IsSqlite())
        {
            await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);
        }
    }

    // Garantizar siempre la existencia del usuario Administrador Fundador inicial
    await AdminUserSeeder.EnsureAdminUserAsync(db, adminOptionsValue, logger);

    // Semillado demostrativo: únicamente en desarrollo cuando SeedDemoData está activo
    if (dbOptionsValue.SeedDemoData && app.Environment.IsDevelopment())
    {
        await CatalogSeeder.SeedAsync(db);
        await DirectorySeeder.SeedDirectoryAsync(db);
        await UserManagementSeeder.SeedUsersAndAuditAsync(db);
        await BoardGameEventSeeder.SeedEventsAsync(db);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Incremento 46: la autenticación y la autorización preceden al antiforgery, según el diseño.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseOutputCache();

// Incremento 10: Endpoints de Diagnóstico y Salud
// Liveness probe (/healthz): confirma que el host está activo sin penalizar dependencias.
// Ambas sondas quedan explícitamente anónimas y fuera de cualquier política de fallback
// para no romper los probes de Cloud Run.
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow,
            mode = "liveness"
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}).AllowAnonymous();

// Readiness probe (/ready): evalúa dependencias críticas (base de datos, almacenamiento y cola)
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTimeOffset.UtcNow,
            mode = "readiness",
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}).AllowAnonymous();

// Incremento 46: acceso social y cierre de sesión. El POST solo desafía al proveedor habilitado;
// el esquema externo resuelve la identidad y firma la cookie de sesión propia de Ludeka.
// El token antiforgery se valida antes de leer el formulario y un token ausente responde 400.
app.MapPost("/login/external", async (
    HttpContext httpContext,
    [FromServices] IAntiforgery antiforgery,
    [FromServices] IOptions<AuthenticationOptions> options) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest(new { error = "Token antiforgery ausente o inválido." });
    }

    var form = await httpContext.Request.ReadFormAsync();
    var provider = form["provider"].ToString();

    var registration = ExternalAuthenticationSchemes
        .GetEnabledProviders(options.Value)
        .FirstOrDefault(candidate => string.Equals(candidate.Name, provider, StringComparison.OrdinalIgnoreCase));

    if (registration is null)
    {
        return Results.BadRequest(new { error = "El proveedor de acceso indicado no está habilitado." });
    }

    return Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [registration.Scheme]);
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(ExternalAuthenticationSchemes.SessionCookieScheme);
    return Results.Redirect("/");
}).AllowAnonymous();

// Endpoint de entrega de tarjeta vectorial para Instagram (Incremento 28)
app.MapGet("/api/instagram/card/{draftId:guid}.svg", async (Guid draftId, IInstagramPublisherService publisherService) =>
{
    var draft = await publisherService.GetDraftByIdAsync(draftId);
    if (draft == null || string.IsNullOrWhiteSpace(draft.SvgContent))
        return Results.NotFound();

    return Results.Content(draft.SvgContent, "image/svg+xml; charset=utf-8");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
