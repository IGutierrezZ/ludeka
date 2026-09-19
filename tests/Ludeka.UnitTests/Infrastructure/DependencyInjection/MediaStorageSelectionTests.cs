using System.Collections.Generic;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure.DependencyInjection;

/// <summary>
/// INC-48 (PR1a), spec `media-storage-precedence`, requisito 1: <c>IImageStorageService</c> se
/// selecciona por precedencia estricta de tres vías — R2 válido, disco local configurado
/// (<c>Media__LocalStoragePath</c>), memoria — dentro de la factoría real de
/// <see cref="LudekaServiceCollectionExtensions.AddLudekaApplicationCore"/>. Misma técnica que
/// <c>LudekaServiceCollectionExtensionsTests</c> y <c>WebHostHostedServiceCompositionTests</c>
/// escenario 1: <c>ServiceCollection</c> real + <c>ValidateOnBuild: true</c>, nunca
/// <c>WebApplicationFactory</c>.
/// </summary>
public class MediaStorageSelectionTests
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

    private static IImageStorageService ResolveImageStorageService(Dictionary<string, string?> configValues)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService, FakeCurrentUserService>();
        services.AddSingleton<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        services.AddLudekaApplicationCore(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IImageStorageService>();
    }

    [Fact]
    public void R2ConCredencialesValidas_GanaSobreDiscoYMemoria()
    {
        var service = ResolveImageStorageService(new Dictionary<string, string?>
        {
            ["Cloudflare:Simulate"] = "false",
            ["Cloudflare:AccountId"] = "cuenta-test",
            ["Cloudflare:AccessKeyId"] = "clave-test",
            ["Cloudflare:SecretAccessKey"] = "secreto-test",
            ["Cloudflare:BucketName"] = "ludeka-media",
            ["Media:LocalStoragePath"] = "/data/media"
        });

        Assert.IsType<CloudflareR2StorageService>(service);
    }

    [Fact]
    public void SinR2PeroConRutaLocalConfigurada_GanaSobreMemoria()
    {
        var service = ResolveImageStorageService(new Dictionary<string, string?>
        {
            ["Media:LocalStoragePath"] = "/data/media"
        });

        Assert.IsType<PhysicalFileImageStorageService>(service);
    }

    [Fact]
    public void SinR2YSinRutaLocal_SeleccionaMemoria()
    {
        var service = ResolveImageStorageService(new Dictionary<string, string?>());

        Assert.IsType<SimulatedImageStorageService>(service);
    }
}
