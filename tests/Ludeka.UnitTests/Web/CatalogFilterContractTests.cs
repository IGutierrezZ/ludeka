using System;
using System.IO;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y verificación de interfaz para el motor de filtros y tamaño en mesa (INC-59).
/// Valida la accesibilidad WCAG 2.2 AA de las facetas, sincronización en URL y marcado de TableFootprint.
/// </summary>
public class CatalogFilterContractTests
{
    private const string GameCardPath = "src/Ludeka.Web/Components/Shared/GameCard.razor";
    private const string HomeCatalogPath = "src/Ludeka.Web/Components/Pages/Home.razor";

    [Fact]
    public void GameFilterCriteria_Contract_ShouldSupportAllFacetsIncludingFootprint()
    {
        var criteria = new GameFilterCriteria(
            SearchTerm: "Catan",
            PlayerCount: 4,
            Style: GameStyle.Eurogame,
            Confrontation: ConfrontationType.Competitive,
            MaxDurationMinutes: 90,
            EspecialParejas: false,
            MesaFamiliar: true,
            SoloTop: false,
            TypeFilter: GameType.BaseGame,
            Footprint: TableFootprint.SmallTable
        );

        Assert.Equal("Catan", criteria.SearchTerm);
        Assert.Equal(4, criteria.PlayerCount);
        Assert.Equal(GameStyle.Eurogame, criteria.Style);
        Assert.Equal(ConfrontationType.Competitive, criteria.Confrontation);
        Assert.Equal(90, criteria.MaxDurationMinutes);
        Assert.False(criteria.EspecialParejas);
        Assert.True(criteria.MesaFamiliar);
        Assert.False(criteria.SoloTop);
        Assert.Equal(GameType.BaseGame, criteria.TypeFilter);
        Assert.Equal(TableFootprint.SmallTable, criteria.Footprint);
    }

    [Fact]
    public void GameCard_MarkupContract_ShouldIncludeFootprintChipInGridMode()
    {
        var source = ReadSource(GameCardPath);

        // Huella en mesa visible en cuadrícula con iconos oficiales
        Assert.Contains("Game.Footprint", source, StringComparison.Ordinal);
        Assert.Contains("TableFootprint.SmallTable", source, StringComparison.Ordinal);
        Assert.Contains("TableFootprint.TableMonster", source, StringComparison.Ordinal);
        Assert.Contains("Mesa pequeña", source, StringComparison.Ordinal);
        Assert.Contains("Mesa grande", source, StringComparison.Ordinal);
        Assert.Contains("Mesa estándar", source, StringComparison.Ordinal);
        Assert.Contains("armchair", source, StringComparison.Ordinal);
        Assert.Contains("castle", source, StringComparison.Ordinal);
        Assert.Contains("utensils", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeCatalog_FilterContract_ShouldIncludeFacetControlsAndUrlParameters()
    {
        var source = ReadSource(HomeCatalogPath);

        // Grupos semánticos y accesibilidad (WCAG 2.2 AA)
        Assert.Contains("aria-label=\"Filtrar por tamaño en mesa\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filtrar por número de jugadores\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filtrar por duración máxima\"", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);

        // Variables de estado de facetas
        Assert.Contains("_selectedFootprint", source, StringComparison.Ordinal);
        Assert.Contains("_selectedPlayerCount", source, StringComparison.Ordinal);
        Assert.Contains("_selectedMaxDuration", source, StringComparison.Ordinal);
        Assert.Contains("HasActiveRefinements", source, StringComparison.Ordinal);

        // Métodos de mutación y restablecimiento
        Assert.Contains("SetFootprint", source, StringComparison.Ordinal);
        Assert.Contains("SetPlayerCount", source, StringComparison.Ordinal);
        Assert.Contains("SetMaxDuration", source, StringComparison.Ordinal);
        Assert.Contains("ResetFilters", source, StringComparison.Ordinal);

        // Sincronización en URL (lectura y persistencia)
        Assert.Contains("queryParams[\"mesa\"]", source, StringComparison.Ordinal);
        Assert.Contains("queryParams[\"jugadores\"]", source, StringComparison.Ordinal);
        Assert.Contains("queryParams[\"duracion\"]", source, StringComparison.Ordinal);
        Assert.Contains("query.TryGetValue(\"mesa\"", source, StringComparison.Ordinal);
        Assert.Contains("query.TryGetValue(\"jugadores\"", source, StringComparison.Ordinal);
        Assert.Contains("query.TryGetValue(\"duracion\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeCatalog_FilterContract_ShouldIncludeCollapsibleAdvancedFiltersAndMultiSelectFacets()
    {
        var source = ReadSource(HomeCatalogPath);

        // Control de panel desplegable y accesibilidad
        Assert.Contains("_showAdvancedFilters", source, StringComparison.Ordinal);
        Assert.Contains("ToggleAdvancedFilters", source, StringComparison.Ordinal);
        Assert.Contains("id=\"panel-filtros-avanzados\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"panel-filtros-avanzados\"", source, StringComparison.Ordinal);

        // Colecciones de multiselección
        Assert.Contains("_selectedComplexities", source, StringComparison.Ordinal);
        Assert.Contains("_selectedTypes", source, StringComparison.Ordinal);
        Assert.Contains("_selectedStyles", source, StringComparison.Ordinal);
        Assert.Contains("_selectedConfrontations", source, StringComparison.Ordinal);
        Assert.Contains("_selectedFootprints", source, StringComparison.Ordinal);
        Assert.Contains("_selectedPlayerCounts", source, StringComparison.Ordinal);

        // Sincronización en URL de nuevas facetas
        Assert.Contains("queryParams[\"dureza\"]", source, StringComparison.Ordinal);
        Assert.Contains("queryParams[\"tipos\"]", source, StringComparison.Ordinal);
        Assert.Contains("queryParams[\"estilos\"]", source, StringComparison.Ordinal);
        Assert.Contains("queryParams[\"confrontacion\"]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_MobileMenu_ShouldAutoCloseOnNavigationLinks()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Layout/MainLayout.razor");

        // Cierre automático del menú móvil al pulsar enlaces
        Assert.Contains("onclick=\"this.closest('details')?.removeAttribute('open')\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/catalogo\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_MarkupContract_ShouldSupportThreeViewImagesAndExcludeEnglishSection()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/GameDetail.razor");

        // Selector de 3 vistas de imagen
        Assert.Contains("_selectedHeroImageKey", source, StringComparison.Ordinal);
        Assert.Contains("CurrentHeroImageUrl", source, StringComparison.Ordinal);
        Assert.Contains("BackCoverImageUrl", source, StringComparison.Ordinal);
        Assert.Contains("TableImageUrl", source, StringComparison.Ordinal);

        // Sección 'Sobre el juego' retirada
        Assert.DoesNotContain("Sobre el juego", source, StringComparison.Ordinal);
        Assert.DoesNotContain("@((MarkupString)Game.Description)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeCatalog_FilterContract_ShouldDecoupleSortFromFiltersAndRedirectTrending()
    {
        var source = ReadSource(HomeCatalogPath);

        // Ya no contiene el botón de ordenación 'En tendencia'
        Assert.DoesNotContain("<span>En tendencia</span>", source, StringComparison.Ordinal);

        // Redirige URLs con orden=tendencia hacia /tendencias
        Assert.Contains("Navigation.NavigateTo(\"/tendencias\", replace: true)", source, StringComparison.Ordinal);

        // ActiveAdvancedFiltersCount no incluye _sortBy
        Assert.DoesNotContain("(_sortBy != GameSortOrder.Rank ? 1 : 0)", source, StringComparison.Ordinal);

        // Enlace en portada (WeeklyTopSection) apunta a /tendencias
        var weeklyTopSource = ReadSource("src/Ludeka.Web/Components/Home/WeeklyTopSection.razor");
        Assert.Contains("href=\"/tendencias\"", weeklyTopSource, StringComparison.Ordinal);
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
