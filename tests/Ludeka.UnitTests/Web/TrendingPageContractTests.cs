using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class TrendingPageContractTests
{
    private const string TrendingPagePath = "src/Ludeka.Web/Components/Pages/TrendingGames.razor";

    [Fact]
    public void TrendingPage_ShouldDeclareRouteAndInjectService()
    {
        var source = ReadSource(TrendingPagePath);

        Assert.Contains("@page \"/tendencias\"", source, StringComparison.Ordinal);
        Assert.Contains("@inject ITrendingService TrendingService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrendingPage_ShouldContainCatalogNavigationButton()
    {
        var source = ReadSource(TrendingPagePath);

        Assert.Contains("href=\"/catalogo\"", source, StringComparison.Ordinal);
        Assert.Contains("Ir al catálogo", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrendingPage_ShouldRenderMovementIndicatorsAndSemanticList()
    {
        var source = ReadSource(TrendingPagePath);

        // Semántica de lista
        Assert.Contains("role=\"list\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"listitem\"", source, StringComparison.Ordinal);

        // Indicadores de movimiento (Up, Down, Same, New)
        Assert.Contains("case RankMovement.Up:", source, StringComparison.Ordinal);
        Assert.Contains("case RankMovement.Down:", source, StringComparison.Ordinal);
        Assert.Contains("case RankMovement.Same:", source, StringComparison.Ordinal);
        Assert.Contains("case RankMovement.New:", source, StringComparison.Ordinal);

        // Iconografía y etiquetas
        Assert.Contains("arrow-up", source, StringComparison.Ordinal);
        Assert.Contains("arrow-down", source, StringComparison.Ordinal);
        Assert.Contains("sparkles", source, StringComparison.Ordinal);
        Assert.Contains("Entra", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrendingPage_ShouldSupportCatalogLinksAndBggRating()
    {
        var source = ReadSource(TrendingPagePath);

        Assert.Contains("href=\"/juegos/@item.Slug\"", source, StringComparison.Ordinal);
        Assert.Contains("item.BggRating", source, StringComparison.Ordinal);
        Assert.Contains("item.YearPublished", source, StringComparison.Ordinal);
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

        return dir?.FullName ?? throw new InvalidOperationException("No se pudo localizar la raíz de la solución.");
    }
}
