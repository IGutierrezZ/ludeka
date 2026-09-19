using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Background;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-47, R7 (tasks.md 11.1/11.2), requisito <c>background-jobs-scheduling</c> "El host web no
/// ejecuta ningún trabajo de negocio en proceso". Dos escenarios, dos técnicas (la técnica exacta
/// de 11.2 queda explícitamente abierta a <c>sdd-apply</c>; se extiende aquí el mismo criterio a
/// 11.1, cuyo enunciado no la fija):
///
/// <list type="bullet">
/// <item><b>Escenario 1 (11.1, unitaria).</b> Se construye un <see cref="IServiceProvider"/> real
/// con <c>ValidateOnBuild: true</c> y se resuelve <see cref="IHostedService"/>. Mismo criterio que
/// <c>LudekaServiceCollectionExtensionsTests</c> (R2b): un espejo deliberado de los registros
/// reales de <c>Program.cs</c>, mantenido en sincronía con el código de producción — NO una
/// ejecución literal de <c>Program.cs</c> vía <c>WebApplicationFactory</c>, desproporcionada
/// (manifiesto de activos estáticos, resolución de <i>content root</i> cruzada entre proyectos,
/// claves de <c>DataProtection</c> en disco) para una verificación que la propia especificación
/// clasifica como "unitaria (SQLite o dobles de prueba)". A diferencia de la prueba de humo de
/// R2b —que nunca incluyó los cuatro <c>AddHostedService</c> porque nunca vivieron dentro de
/// <c>AddLudekaApplicationCore</c>, sino directamente en <c>Program.cs</c>—, el RED de esta prueba
/// los añadió explícitamente al montaje para reproducir el estado de <c>Program.cs</c> ANTES de
/// la tarea 11.3 (confirmado: fallaba con los cuatro presentes). El GREEN los retira del montaje
/// en el mismo cambio que los retira de <c>Program.cs</c>.</item>
/// <item><b>Escenario 2 (11.2, estructural).</b> Búsqueda de texto sobre el árbol real de
/// ficheros de <c>src/</c> — complementaria, no redundante: cubre cualquier registro futuro de los
/// cuatro tipos en CUALQUIER fichero de <c>src/</c>, no solo en el montaje que la primera prueba
/// construye a mano.</item>
/// </list>
/// </summary>
public class WebHostHostedServiceCompositionTests
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
    public void WebHostComposition_NoResuelveNingunTrabajoDeNegocioComoHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService, FakeCurrentUserService>();
        services.AddSingleton<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();

        var configuration = new ConfigurationBuilder().Build();
        services.AddLudekaApplicationCore(configuration);

        // GREEN (tasks.md 11.3): Program.cs ya no registra los cuatro AddHostedService — el RED
        // de esta prueba los añadía aquí explícitamente (mismas cuatro líneas que Program.cs
        // tenía entonces) y fallaba con las cuatro instancias presentes en la colección resuelta.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>().ToList();

        Assert.DoesNotContain(hostedServices, hs => hs is NightlyCatalogingHostedService);
        Assert.DoesNotContain(hostedServices, hs => hs is PriceRadarHostedService);
        Assert.DoesNotContain(hostedServices, hs => hs is SocialCollectorHostedService);
        Assert.DoesNotContain(hostedServices, hs => hs is CommunityNotificationDispatcherHostedService);
    }

    [Fact]
    public void CodigoFuente_NingunFicheroDeSrcRegistraAddHostedServiceParaLosCuatroTiposDeNegocio()
    {
        var srcDir = Path.Combine(GetRepoRoot(), "src");
        var forbidden = new[]
        {
            "AddHostedService<NightlyCatalogingHostedService>",
            "AddHostedService<PriceRadarHostedService>",
            "AddHostedService<SocialCollectorHostedService>",
            "AddHostedService<CommunityNotificationDispatcherHostedService>"
        };

        // Excluye bin/obj: son artefactos de compilación, no el árbol de fuentes real que pide
        // el escenario 2 — y crecen con cada build, ralentizando la búsqueda sin aportar nada.
        var offendingFiles = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Where(file => forbidden.Any(pattern => File.ReadAllText(file).Contains(pattern, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(srcDir, file))
            .ToList();

        Assert.Empty(offendingFiles);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
