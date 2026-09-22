using System.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

/// <summary>
/// Prueba de composición de <c>Ludeka.Jobs</c> (INC-47, R6): reproduce el registro exacto de
/// <c>Program.cs</c> — <c>AddLudekaApplicationCore</c> + <see cref="DenyAllSessionPermissionGuard"/>
/// + <see cref="SystemCurrentUserService"/> + <c>AddLudekaJobRunners</c> — con
/// <c>validateScopes</c>/<c>validateOnBuild: true</c>, la misma red de seguridad que la prueba de
/// humo de R2b (diseño §4.6, <c>LudekaServiceCollectionExtensionsTests</c>). Demuestra que
/// <see cref="SystemCurrentUserService"/> cierra realmente el hueco de <see cref="ICurrentUserService"/>
/// (Engram <c>sdd/change-47-workers-cloud-run/hueco-currentuserservice</c>) y no solo que compila:
/// sin este registro, <c>BuildServiceProvider</c> lanzaría al construir por los 11 consumidores de
/// <c>Ludeka.Application</c> que lo exigen como dependencia dura de constructor.
/// </summary>
public class LudekaJobsCompositionTests
{
    [Fact]
    public void ComposicionDeLudekaJobs_ResuelveSinExcepcionYExponeLosCuatroRunners()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder().Build();
        services.AddLudekaApplicationCore(configuration);
        services.AddScoped<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        services.AddLudekaJobRunners();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var currentUser = sp.GetRequiredService<ICurrentUserService>();
        Assert.IsType<SystemCurrentUserService>(currentUser);
        Assert.Equal(string.Empty, currentUser.UserId);
        Assert.False(currentUser.IsFoundingTeam);
        Assert.False(currentUser.HasPermission(ModeratorPermission.CanEditGames));

        var runners = sp.GetServices<IJobRunner>().ToList();
        Assert.Equal(7, runners.Count);
        Assert.Contains(runners, r => r.Name == JobNames.NightlyCataloging && r is NightlyCatalogingJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.PriceRadar && r is PriceRadarJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.SocialCollector && r is SocialCollectorJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.NotificationOutbox && r is NotificationOutboxJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.SeedStaging && r is SeedStagingJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.DrainStaging && r is DrainStagingJobRunner);
        Assert.Contains(runners, r => r.Name == JobNames.SeedDirectory && r is SeedDirectoryJobRunner);
    }
}
