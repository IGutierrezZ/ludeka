using System;
using System.IO;
using System.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Events;
using Ludeka.Infrastructure.YouTube;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Contrato de la revalidación administrativa (INC-46, hallazgo W1): cada servicio administrativo de
/// escritura declara la dependencia <see cref="ISessionPermissionGuard"/> y el contenedor la registra.
/// Fija por prueba la lista completa de servicios tratados para que un servicio nuevo no se cuele sin
/// guarda.
/// </summary>
public class AdministrativeWriteGuardContractTests
{
    /// <summary>Servicios administrativos de escritura con revalidación de permiso por sesión.</summary>
    public static TheoryData<Type> GuardedServices => new()
    {
        typeof(BoardGameEventService),
        typeof(WeeklyReleaseService),
        typeof(GiveawayService),
        typeof(SocialIngestionService),
        typeof(SocialCollectorService),
        typeof(MonitoredAccountService),
        typeof(CommunityNotificationService),
        typeof(YouTubeSearchService),
    };

    [Theory]
    [MemberData(nameof(GuardedServices))]
    public void EveryAdministrativeWriteService_DeclaresTheSessionPermissionGuard(Type serviceType)
    {
        var parameters = serviceType.GetConstructors().SelectMany(ctor => ctor.GetParameters()).ToList();

        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(ISessionPermissionGuard));
    }

    [Fact]
    public void Program_RegistersTheSessionPermissionGuard()
    {
        var program = File.ReadAllText(Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Program.cs"));

        Assert.Contains("AddScoped<ISessionPermissionGuard, SessionPermissionGuard>()", program);
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
