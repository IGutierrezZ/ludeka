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
using Ludeka.Infrastructure.YouTube;
using Ludeka.Application.Features.Discovery;
using Ludeka.Infrastructure.Stores;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Web;
using Ludeka.Web.Components;
using Ludeka.Web.Extensions;
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

// Composición completa de dominio e infraestructura (INC-47, diseño §4 D1): extraída a
// Ludeka.Infrastructure.DependencyInjection.LudekaServiceCollectionExtensions para que el futuro
// Ludeka.Jobs pueda consumirla sin referenciar este host web.
builder.Services.AddLudekaApplicationCore(builder.Configuration);

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

// Incremento 10: Observabilidad con Health Checks Oficiales de ASP.NET Core
builder.Services.AddHealthChecks()
    .AddCheck<SqliteDatabaseHealthCheck>("sqlite_db", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"])
    .AddCheck<NotificationQueueHealthCheck>("notification_queue", tags: ["ready"]);

var app = builder.Build();

// Incremento 48 (PR2), diseño D3: guarda de arranque de coherencia de proveedor — espejo de la
// Guarda 1 de Ludeka.Jobs.StartupGuards. Va ANTES que cualquier otro efecto de arranque (avisos,
// inicialización de base de datos): en Production sin PostgreSQL resoluble el proceso debe fallar
// explícitamente, nunca caer en silencio a una SQLite efímera.
var webStartupGuardFailure = WebStartupGuards.Evaluate(
    app.Configuration,
    app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value,
    app.Environment.EnvironmentName);
if (webStartupGuardFailure is not null)
{
    throw new InvalidOperationException(webStartupGuardFailure);
}

// Aviso explícito de proveedores habilitados sin credenciales: la aplicación arranca sin ellos.
foreach (var authenticationWarning in ExternalAuthenticationSchemes.GetConfigurationWarnings(authenticationOptions))
{
    app.Logger.LogWarning("{AuthenticationWarning}", authenticationWarning);
}

// Incremento 48 (PR1a): aviso explícito de almacén de medios degradado en Production sin R2 válido.
// La aplicación arranca igual; el almacén activo (disco local o memoria) puede no sobrevivir un reinicio.
var cloudflareOptionsValue = app.Services.GetRequiredService<IOptions<CloudflareR2Options>>().Value;
foreach (var mediaStorageWarning in MediaStorageWarnings.GetConfigurationWarnings(cloudflareOptionsValue, app.Environment.EnvironmentName))
{
    app.Logger.LogWarning("{MediaStorageWarning}", mediaStorageWarning);
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

// Incremento 48 (PR1b), diseño D2: entrega HTTP real del fallback de medios en disco.
// MapStaticAssets() (más abajo) solo sirve el manifiesto de activos generado en compilación; no
// sirve ficheros escritos en tiempo de ejecución por PhysicalFileImageStorageService, que es
// exactamente lo que este middleware resuelve. Va antes de la autenticación porque los medios son
// públicos y no deben pagar autenticación ni antiforgery. UseLudekaMediaFiles no hace nada si no
// hay ruta local configurada (MediaOptions.HasLocalStoragePath): en ese caso el almacén activo es
// Cloudflare R2 o memoria, ninguno de los cuales escribe ficheros en este proceso.
var mediaOptionsValue = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
app.UseLudekaMediaFiles(mediaOptionsValue, app.Environment);

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
