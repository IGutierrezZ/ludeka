using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para los carruseles horizontales y rediseño compacto
/// de las secciones de Expansiones y Hub Multimedia en la ficha de juego (INC-99).
/// </summary>
public class ExpansionAndMediaCarouselUiContractTests
{
    private const string ExpansionSectionPath = "src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor";
    private const string MultimediaHubPath = "src/Ludeka.Web/Components/Shared/MultimediaHub.razor";
    private const string RailScrollJsPath = "src/Ludeka.Web/wwwroot/js/rail-scroll.js";

    [Fact]
    public void ExpansionEcosystemSection_ShouldDeclareEditorialTwoColumnGrid()
    {
        var source = ReadSource(ExpansionSectionPath);

        // Cuadrícula editorial de 2 columnas según prototipo Claude
        Assert.Contains("grid grid-cols-1 md:grid-cols-2 gap-4", source, StringComparison.Ordinal);
        Assert.Contains("rounded-2xl bg-[var(--paper-2)]", source, StringComparison.Ordinal);

        // Optimización anti-CLS y carga asíncrona
        Assert.Contains("width=\"64\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"64\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionEcosystemSection_ShouldDeclareCleanCardWithLibraryButton()
    {
        var source = ReadSource(ExpansionSectionPath);

        // Botón interactivo de ludoteca con estados
        Assert.Contains("+ A mi ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("En mi ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("HandleToggleLibrary", source, StringComparison.Ordinal);
        Assert.Contains("LibraryService.SetCollectionStateAsync", source, StringComparison.Ordinal);

        // Etiqueta de necesidad simplificada (Opcional, Imprescindible, etc.)
        Assert.Contains("GetNecessitySimpleLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionEcosystemSection_ShouldNotContainMixerOrRecipes()
    {
        var source = ReadSource(ExpansionSectionPath);

        // Se retiran el mezclador en mesa, recetas y botones de scroll por JavaScript
        Assert.DoesNotContain("expansions-carousel-rail", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollExpansionsRailAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EvaluateMixerCombinationAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_activeTab", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lg:grid-cols-12", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareHorizontalCarouselRail()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("id=\"media-carousel-rail\"", source, StringComparison.Ordinal);
        Assert.Contains("overflow-x-auto", source, StringComparison.Ordinal);
        Assert.Contains("snap-x", source, StringComparison.Ordinal);
        Assert.Contains("snap-mandatory", source, StringComparison.Ordinal);
        Assert.Contains("scrollbar-none", source, StringComparison.Ordinal);
        Assert.Contains("ScrollMediaRailAsync", source, StringComparison.Ordinal);
        Assert.Contains("ludekaScrollRail", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldNotUseVerticalGridForVideos()
    {
        var source = ReadSource(MultimediaHubPath);

        // Se elimina la cuadrícula vertical de 2 columnas
        Assert.DoesNotContain("<div class=\"grid grid-cols-1 sm:grid-cols-2 gap-4\">", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareCompactVideoCardsWith16x9Aspect()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("snap-start", source, StringComparison.Ordinal);
        Assert.Contains("w-[260px]", source, StringComparison.Ordinal);
        Assert.Contains("aspect-video", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareSegmentedFilterBarInSingleRow()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("role=\"tablist\"", source, StringComparison.Ordinal);
        Assert.Contains("_activeFilter = \"all\"", source, StringComparison.Ordinal);
        Assert.Contains("_activeFilter = \"tutorials\"", source, StringComparison.Ordinal);
        Assert.Contains("_activeFilter = \"quick\"", source, StringComparison.Ordinal);
        Assert.Contains("_activeFilter = \"playthroughs\"", source, StringComparison.Ordinal);
        Assert.Contains("_activeFilter = \"social\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RailScrollJs_ShouldDeclareLudekaScrollRailHelper()
    {
        var source = ReadSource(RailScrollJsPath);

        Assert.Contains("window.ludekaScrollRail = function (elementId, distance)", source, StringComparison.Ordinal);
        Assert.Contains("el.scrollBy({ left: distance, behavior: 'smooth' });", source, StringComparison.Ordinal);
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
