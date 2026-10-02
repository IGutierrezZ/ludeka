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
    public void ExpansionEcosystemSection_ShouldDeclareHorizontalCarouselRail()
    {
        var source = ReadSource(ExpansionSectionPath);

        Assert.Contains("id=\"expansions-carousel-rail\"", source, StringComparison.Ordinal);
        Assert.Contains("overflow-x-auto", source, StringComparison.Ordinal);
        Assert.Contains("snap-x", source, StringComparison.Ordinal);
        Assert.Contains("snap-mandatory", source, StringComparison.Ordinal);
        Assert.Contains("scrollbar-none", source, StringComparison.Ordinal);
        Assert.Contains("ScrollExpansionsRailAsync", source, StringComparison.Ordinal);
        Assert.Contains("ludekaScrollRail", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionEcosystemSection_ShouldNotUseVerticalGridInExpansionsListTab()
    {
        var source = ReadSource(ExpansionSectionPath);

        // La pestaña list ya no debe tener cuadrícula vertical de 2 columnas
        Assert.DoesNotContain("<div class=\"grid grid-cols-1 md:grid-cols-2 gap-4\">", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionEcosystemSection_ShouldDeclareCompactCardsWithSnapStart()
    {
        var source = ReadSource(ExpansionSectionPath);

        Assert.Contains("snap-start", source, StringComparison.Ordinal);
        Assert.Contains("w-[275px]", source, StringComparison.Ordinal);
        Assert.Contains("shrink-0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionEcosystemSection_MixerShouldUseTwoColumnLayoutWithoutVerticalSprawl()
    {
        var source = ReadSource(ExpansionSectionPath);

        Assert.Contains("lg:grid-cols-12", source, StringComparison.Ordinal);
        Assert.Contains("lg:col-span-5", source, StringComparison.Ordinal);
        Assert.Contains("lg:col-span-7", source, StringComparison.Ordinal);
        Assert.Contains("max-h-[380px] overflow-y-auto", source, StringComparison.Ordinal);
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
