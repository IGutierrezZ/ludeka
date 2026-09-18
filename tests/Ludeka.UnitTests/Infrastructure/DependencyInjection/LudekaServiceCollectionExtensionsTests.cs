using System.Collections.Generic;
using System.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Infrastructure.Services;
using Ludeka.Infrastructure.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure.DependencyInjection;

/// <summary>
/// Prueba de humo de la rebanada R2b (INC-47, diseño §4.6): aplica únicamente
/// <see cref="LudekaServiceCollectionExtensions.AddLudekaApplicationCore"/> — las cuatro extensiones
/// completas (persistencia, dominio e integraciones externas) — más los registros mínimos que un
/// host sin web (el futuro <c>Ludeka.Jobs</c>, R6) tendría que aportar por su cuenta: <c>ILogger</c>
/// y <see cref="DenyAllSessionPermissionGuard"/> (diseño §4.6). Confirma que el grafo completo se
/// resuelve con <c>validateOnBuild: true</c>, incluido el orden completo de las dos
/// <c>IEnumerable&lt;T&gt;</c> en riesgo (tasks.md 2.5/3.1, diseño §4.3) — deferido íntegramente
/// desde R2a porque las dos colecciones viven enteras en <c>AddLudekaExternalIntegrations</c> — y
/// que el conjunto de <see cref="IHostedService"/> resuelto está vacío (diseño §4.6: los 4
/// <c>AddHostedService</c> de <c>Program.cs</c> no forman parte de esta composición y se retiran
/// en R7, tasks.md 3.5).
///
/// <see cref="INotificationOutboxRepository"/> e <c>IJobExecutionCoordinator</c> (diseño §4.6)
/// todavía no existen (llegan en R4a/R9 respectivamente); esta prueba solo afirma sobre los cinco
/// servicios de dominio ya disponibles hoy, tal como prevé tasks.md 3.1.
///
/// Hueco de diseño detectado y declarado, no silenciado: §4.6 solo prevé <c>ILogger</c> y
/// <see cref="DenyAllSessionPermissionGuard"/> como registros mínimos de host sin web, pero varios
/// servicios de dominio (p. ej. <c>AuditService</c>, <c>UserManagementService</c>,
/// <c>GameEditorService</c>, <c>FoundingVerdictService</c>) exigen <see cref="ICurrentUserService"/>
/// como dependencia dura de constructor, y ninguna de las cuatro extensiones lo registra (sus
/// implementaciones viven solo en <c>Ludeka.Web</c>, diseño §4.2). Esta prueba añade un doble mínimo
/// — nunca se invoca, solo satisface la forma del grafo —, igual que hacía R2a antes de que
/// existiera esta composición completa.
/// </summary>
public class LudekaServiceCollectionExtensionsTests
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => string.Empty;
        public string UserName => string.Empty;
        public IReadOnlyList<string> Roles => [];
        public bool IsFoundingTeam => false;
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    [Fact]
    public void AddLudekaApplicationCore_ResolvesFullCompositionWithoutWebRegistrations()
    {
        var services = new ServiceCollection();
        // Host.CreateApplicationBuilder / WebApplication.CreateBuilder registran ILogger<T> e
        // ICurrentUserService/ISessionPermissionGuard por defecto (Web) o los aportará el futuro
        // Ludeka.Jobs (R6); un ServiceCollection aislado necesita pedirlos explícitamente.
        services.AddLogging();
        services.AddSingleton<ICurrentUserService, FakeCurrentUserService>();
        services.AddSingleton<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();

        var configuration = new ConfigurationBuilder().Build();
        services.AddLudekaApplicationCore(configuration);

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
        Assert.IsAssignableFrom<ISocialCollectorService>(sp.GetRequiredService<ISocialCollectorService>());
        Assert.IsAssignableFrom<ICommunityNotificationService>(sp.GetRequiredService<ICommunityNotificationService>());
        Assert.IsAssignableFrom<ICommunityNotificationQueue>(sp.GetRequiredService<ICommunityNotificationQueue>());

        // Diseño §4.6: los 4 AddHostedService literales de Program.cs no participan de esta
        // composición (siguen registrados hasta R7, pero fuera de las cuatro extensiones).
        Assert.Empty(sp.GetServices<IHostedService>());

        // Diseño §4.3 / tasks.md 2.5: orden de registro preservado, ahora afirmable por completo
        // porque las cuatro extensiones (incluida AddLudekaExternalIntegrations) ya están aplicadas.
        var socialCollectors = sp.GetServices<ISocialChannelCollector>().ToList();
        Assert.Collection(socialCollectors,
            c => Assert.IsType<YouTubeFeedCollector>(c),
            c => Assert.IsType<TelegramChannelCollector>(c),
            c => Assert.IsType<RssBlogFeedCollector>(c),
            c => Assert.IsType<InstagramFeedCollector>(c));

        var storeStockClients = sp.GetServices<IStoreStockClient>().ToList();
        Assert.Collection(storeStockClients,
            c => Assert.IsType<SimulationStoreStockClient>(c),
            c => Assert.IsType<HtmlSchemaStoreStockClient>(c));
    }
}
