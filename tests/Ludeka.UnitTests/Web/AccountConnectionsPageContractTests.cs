using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de fuente de la pantalla de conexiones (INC-49, PR #4, diseño §D3/§D4). No hay `bUnit`
/// en este repositorio (verificado): igual que <c>AuthorizationPipelineContractTests</c>, se lee
/// `AccountConnections.razor` como texto en vez de simular su renderizado.
/// </summary>
public class AccountConnectionsPageContractTests
{
    private const string PagePath = "src/Ludeka.Web/Components/Pages/AccountConnections.razor";

    [Fact]
    public void Page_ShouldDeclareItsRouteAndPlainAuthorizeWithoutAPolicy()
    {
        // INC-49, diseño §D3: primera página del proyecto con [Authorize] simple — basta con sesión
        // iniciada, sin ningún ModeratorPermission granular. No es un caso más de la TheoryData
        // ProtectedPages de AuthorizationPipelineContractTests: esa tabla exige "Policy =" en la
        // aserción, y aquí no hay política que exigir.
        var source = ReadSource(PagePath);

        Assert.Contains("@page \"/cuenta/conexiones\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[Authorize(Policy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkForm_ShouldBeAClassicHttpPostWithAntiforgeryInsteadOfAnOnClick()
    {
        // INC-49, diseño §D4: Results.Challenge escribe cabeceras HTTP de redirección, algo que un
        // circuito SignalR no puede hacer cuando llega el evento. Mismo patrón que Login.razor:35.
        var source = ReadSource(PagePath);

        Assert.Contains("method=\"post\"", source, StringComparison.Ordinal);
        Assert.Contains("action=\"/cuenta/conexiones/vincular\"", source, StringComparison.Ordinal);
        Assert.Contains("data-enhance=\"false\"", source, StringComparison.Ordinal);
        Assert.Contains("<AntiforgeryToken />", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
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
