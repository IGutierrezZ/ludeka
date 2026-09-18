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
using Ludeka.Infrastructure.DependencyInjection;
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
using System.Security.Claims;
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

// Composición de persistencia y de dominio (INC-47, diseño §4 D1): extraída a
// Ludeka.Infrastructure.DependencyInjection.LudekaServiceCollectionExtensions para que el futuro
// Ludeka.Jobs pueda consumirla sin referenciar este host web.
builder.Services.AddLudekaPersistence(builder.Configuration);
builder.Services.AddLudekaDomainServices(builder.Configuration);

// Incremento 46: cookie de sesión propia y esquemas sociales dirigidos por configuración.
// Un proveedor habilitado sin credenciales no tumba el arranque: se avisa y no se registra.
var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>() ?? new AuthenticationOptions();
builder.Services.AddLudekaAuthentication(authenticationOptions);

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

// IBrokenLinkCheckerService NO se mueve en R2a (INC-47): registrarlo en AddLudekaDomainServices
// exigiría referenciar Microsoft.Extensions.Http desde Ludeka.Infrastructure, paquete que R2b añade
// explícitamente (tasks.md 3.2). El resto de este bloque (repositorios y servicios de dominio de
// colección/préstamos/reseñas/partidas/veredictos/medios/importación BGG) ya vive en
// AddLudekaDomainServices. Desviación declarada en el informe de sdd-apply de R2a.
builder.Services.AddHttpClient<IBrokenLinkCheckerService, BrokenLinkCheckerService>();

builder.Services.AddHostedService<NightlyCatalogingHostedService>();

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

// Incremento 27: Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra
builder.Services.Configure<StoreStockOptions>(builder.Configuration.GetSection(StoreStockOptions.SectionName));
builder.Services.AddSingleton<SimulationStoreStockClient>();
builder.Services.AddHttpClient<HtmlSchemaStoreStockClient>();
builder.Services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<SimulationStoreStockClient>());
builder.Services.AddSingleton<IStoreStockClient>(sp => sp.GetRequiredService<HtmlSchemaStoreStockClient>());
builder.Services.AddScoped<IStoreStockService, StoreStockService>();

builder.Services.AddHostedService<PriceRadarHostedService>();

// Incremento 28: Generador y Publicador Directo de Posts para Instagram en Moderación
// (solo el cliente; IInstagramPostDraftRepository/IInstagramComposerService/IInstagramPublisherService
// ya viven en AddLudekaDomainServices, INC-47 R2a)
builder.Services.Configure<InstagramOptions>(builder.Configuration.GetSection(InstagramOptions.SectionName));
builder.Services.AddHttpClient<IInstagramApiClient, InstagramApiClient>();

// Incremento 9: Notificaciones y Webhooks de Comunidad (Discord y Telegram)
// (solo los clientes; la cola/repositorio/servicio ya viven en AddLudekaDomainServices, INC-47 R2a)
builder.Services.Configure<CommunityNotificationOptions>(builder.Configuration.GetSection(CommunityNotificationOptions.SectionName));
builder.Services.AddHttpClient<IDiscordWebhookClient, DiscordWebhookClient>();
builder.Services.AddHttpClient<ITelegramBotClient, TelegramBotClient>();
builder.Services.AddHostedService<CommunityNotificationDispatcherHostedService>();

// Incremento 46 (Paso 2): la identidad se resuelve desde la sesión autenticada y la simulación
// se retira. Scoped por ámbito (petición SSR o circuito) y poblada por UserCircuitHandler; sin
// sesión, UserId vacío y todo denegado, sin usuario centinela.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserIdentitySnapshot>();
builder.Services.AddScoped<ICurrentUserService, AuthenticatedCurrentUserService>();
builder.Services.AddScoped<CircuitHandler, UserCircuitHandler>();
builder.Services.AddSingleton<IUserSessionInvalidator, InMemoryUserSessionInvalidator>();

// Hallazgo W1: las escrituras administrativas revalidan el permiso sobre el AppUser actual (relectura
// sin rastreo), de modo que la suspensión o la revocación en caliente bloquean la siguiente operación.
builder.Services.AddScoped<ISessionPermissionGuard, SessionPermissionGuard>();

// Incremento 13: Módulo de Síntesis con IA (Google Gemini / Heurística)
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddHttpClient<IAiGameSummaryService, GeminiGameSummaryService>();

// Incremento 14: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real
builder.Services.Configure<YouTubeOptions>(builder.Configuration.GetSection(YouTubeOptions.SectionName));
builder.Services.AddSingleton<IChannelFocusProvider, ChannelFocusProvider>();
builder.Services.AddHttpClient<IYouTubeSearchService, YouTubeSearchService>();

// Incremento 42: Hub de Ingesta Social y Multimedia — solo los clientes de extracción/análisis;
// el resto (repositorios, ISocialIngestionService, IMonitoredAccountService) ya vive en
// AddLudekaDomainServices (INC-47 R2a, diseño §4.1 fila 299-304).
builder.Services.AddHttpClient<ISocialMetadataExtractor, OpenGraphSocialMetadataExtractor>();
builder.Services.AddHttpClient<ISocialAiAnalysisService, GeminiSocialAnalysisService>();

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

// ISocialCollectorService ya vive en AddLudekaDomainServices (INC-47 R2a, diseño §4.1 fila 307-318).
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

// Incremento 49: desafío de VINCULACIÓN. A diferencia de /login/external, exige sesión y marca la
// intención y el UserId del servidor en AuthenticationProperties.Items, que viajan dentro del
// parámetro state protegido por Data Protection. El formulario del navegador no puede alterarlos.
app.MapPost("/cuenta/conexiones/vincular", async (
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

    // El UserId se lee EXCLUSIVAMENTE de la sesión autenticada en servidor, nunca de un campo del
    // formulario: es la frontera de seguridad del incremento (diseño §4.3 y §D1).
    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var form = await httpContext.Request.ReadFormAsync();
    var registration = ExternalAuthenticationSchemes
        .GetEnabledProviders(options.Value)
        .FirstOrDefault(candidate => string.Equals(candidate.Name, form["provider"].ToString(), StringComparison.OrdinalIgnoreCase));

    if (registration is null)
    {
        return Results.BadRequest(new { error = "El proveedor de acceso indicado no está habilitado." });
    }

    var properties = new AuthenticationProperties { RedirectUri = AccountConnectionRoutes.Page };
    ExternalLoginIntent.MarkLink(properties, userId);
    return Results.Challenge(properties, [registration.Scheme]);
}).RequireAuthorization();

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
