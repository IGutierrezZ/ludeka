using System;
using System.IO;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Web.Components.Shared;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y verificación para la paginación del catálogo y modos de vista (INC-58).
/// Valida el cálculo de páginas, iconografía whitelist de Lucide, atributos de accesibilidad WCAG 2.2 AA,
/// dimensiones anti-CLS y sincronización de URL.
/// </summary>
public class CatalogPaginationContractTests
{
    private const string GameListItemPath = "src/Ludeka.Web/Components/Shared/GameListItem.razor";
    private const string HomeCatalogPath = "src/Ludeka.Web/Components/Pages/Home.razor";

    [Theory]
    [InlineData(0, 24, 0)]
    [InlineData(1, 24, 1)]
    [InlineData(10, 24, 1)]
    [InlineData(24, 24, 1)]
    [InlineData(25, 24, 2)]
    [InlineData(48, 24, 2)]
    [InlineData(49, 24, 3)]
    [InlineData(8000, 24, 334)]
    [InlineData(100, 0, 0)] // Salvaguarda contra división por cero
    public void CatalogResult_TotalPages_ShouldCalculateCorrectly(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new CatalogResult(Array.Empty<GameSummaryDto>(), totalCount, 1, pageSize);

        Assert.Equal(expectedTotalPages, result.TotalPages);
    }

    [Theory]
    [InlineData("layout-grid")]
    [InlineData("list")]
    [InlineData("chevron-left")]
    [InlineData("chevron-right")]
    public void IconCatalog_ShouldContainRequiredLucideIcons(string iconName)
    {
        Assert.True(IconCatalog.Icons.ContainsKey(iconName), $"IconCatalog debe contener el icono '{iconName}'.");
        Assert.False(string.IsNullOrWhiteSpace(IconCatalog.Icons[iconName]), $"El path de '{iconName}' no puede estar vacío.");
    }

    [Fact]
    public void GameListItem_MarkupContract_ShouldFollowAccessibilityAndPerfRules()
    {
        var source = ReadSource(GameListItemPath);

        // Rendimiento y dimensiones anti-CLS (regla INC-57)
        Assert.Contains("width=\"64\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"64\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
        Assert.Contains("Game.ThumbnailUrl", source, StringComparison.Ordinal);

        // Accesibilidad y semántica (WCAG 2.2 AA)
        Assert.Contains("aria-label=\"Ver ficha de @Game.SpanishTitle\"", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);
        Assert.Contains("Game.SpanishTitle", source, StringComparison.Ordinal);
        Assert.Contains("Game.YearPublished", source, StringComparison.Ordinal);
        Assert.Contains("Game.BggRating", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeCatalog_MarkupContract_ShouldIncludePaginationControlsAndUrlSynchronization()
    {
        var source = ReadSource(HomeCatalogPath);

        // Ciclo de vida y limpieza de recursos
        Assert.Contains("@implements IDisposable", source, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged", source, StringComparison.Ordinal);

        // Tamaño de página estándar (24 elementos para múltiplos de 2, 3, 4, 6 columnas)
        Assert.Contains("_pageSize = 24", source, StringComparison.Ordinal);

        // Modos de vista y alternancia de componentes
        Assert.Contains("<GameCard", source, StringComparison.Ordinal);
        Assert.Contains("<GameListItem", source, StringComparison.Ordinal);
        Assert.Contains("role=\"group\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Modo de visualización\"", source, StringComparison.Ordinal);
        Assert.Contains("layout-grid", source, StringComparison.Ordinal);
        Assert.Contains("list", source, StringComparison.Ordinal);

        // Controles de paginación accesibles
        Assert.Contains("aria-label=\"Página anterior\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Página siguiente\"", source, StringComparison.Ordinal);
        Assert.Contains("PreviousPage", source, StringComparison.Ordinal);
        Assert.Contains("NextPage", source, StringComparison.Ordinal);

        // Sincronización y persistencia de URL
        Assert.Contains("SyncUrl", source, StringComparison.Ordinal);
        Assert.Contains("ReadQueryParameters", source, StringComparison.Ordinal);
        Assert.Contains("QueryHelpers.AddQueryString", source, StringComparison.Ordinal);
        Assert.Contains("replace: true", source, StringComparison.Ordinal);
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
