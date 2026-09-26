using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para la página pública de clasificaciones
/// mensuales (/clasificaciones) y su integración en el diseño global (INC-68).
/// </summary>
public class LeaderboardsPageContractTests
{
    private const string LeaderboardsPath = "src/Ludeka.Web/Components/Pages/Leaderboards.razor";
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";

    [Fact]
    public void Leaderboards_ShouldDeclareRouteAndInjectRequiredContracts()
    {
        var source = ReadSource(LeaderboardsPath);

        Assert.Contains("@page \"/clasificaciones\"", source, StringComparison.Ordinal);
        Assert.Contains("ILeaderboardService LeaderboardService", source, StringComparison.Ordinal);
        Assert.Contains("ICurrentUserService CurrentUserService", source, StringComparison.Ordinal);
        Assert.Contains("NavigationManager Navigation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaderboards_ShouldDeclareMonthNavigationControls()
    {
        var source = ReadSource(LeaderboardsPath);

        Assert.Contains("id=\"prev-month-btn\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"next-month-btn\"", source, StringComparison.Ordinal);
        Assert.Contains("PreviousMonthAsync", source, StringComparison.Ordinal);
        Assert.Contains("NextMonthAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsCurrentOrFutureMonth", source, StringComparison.Ordinal);
        Assert.Contains("GoToCurrentMonthAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaderboards_ShouldMeetAccessibilityAndWcagContracts()
    {
        var source = ReadSource(LeaderboardsPath);

        Assert.Contains("aria-label=\"Mes anterior\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Mes siguiente\"", source, StringComparison.Ordinal);
        Assert.Contains("scope=\"col\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", source, StringComparison.Ordinal);
        Assert.Contains("<table", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaderboards_ShouldHandleOptInAndAnonymousBadges()
    {
        var source = ReadSource(LeaderboardsPath);

        // Estado del usuario conectado
        Assert.Contains("CurrentUserHasOptedIn", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/cuenta/privacidad\"", source, StringComparison.Ordinal);

        // Anonimato y badges anti-doxxing
        Assert.Contains("entry.IsAnonymous", source, StringComparison.Ordinal);
        Assert.Contains("Anónimo", source, StringComparison.Ordinal);
        Assert.Contains("Reservado", source, StringComparison.Ordinal);

        // Métricas de podio y tabla
        Assert.Contains("TotalPlaysThisMonth", source, StringComparison.Ordinal);
        Assert.Contains("UnlockedMilestonesCount", source, StringComparison.Ordinal);
        Assert.Contains("BadgeName", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_ShouldLinkToLeaderboardsInHeaderAndFooter()
    {
        var source = ReadSource(MainLayoutPath);

        // Verificamos presencia en la navegación de escritorio, menú móvil y pie de página
        Assert.Contains("href=\"/clasificaciones\"", source, StringComparison.Ordinal);
        Assert.Contains("Clasificación", source, StringComparison.Ordinal);
        Assert.Contains("Clasificaciones", source, StringComparison.Ordinal);
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
