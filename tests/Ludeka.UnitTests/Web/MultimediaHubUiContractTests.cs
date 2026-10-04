using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para la experiencia de usuario y administración del Hub Multimedia (INC-91).
/// Garantiza la eliminación de textos redundantes, integración de Ingesta Flash y controles de moderación directa.
/// </summary>
public class MultimediaHubUiContractTests
{
    private const string MultimediaHubPath = "src/Ludeka.Web/Components/Shared/MultimediaHub.razor";

    [Fact]
    public void MultimediaHub_ShouldNotContainRedundantHeadingOrSubtitle()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.DoesNotContain("Hub Multimedia en Español", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tutoriales, partidas completas y reseñas en redes segregados sin mezclar formatos", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultimediaHub_ShouldNotContainRedundantCardFooterButtons()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.DoesNotContain("YouTube Tutorial", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reproducir &rarr;", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ver análisis &rarr;", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareFlashIngestControlsAndModal()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("Ingesta Flash", source, StringComparison.Ordinal);
        Assert.Contains("OpenFlashIngestModal", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteFlashIngestAsync", source, StringComparison.Ordinal);
        Assert.Contains("flash-video-url", source, StringComparison.Ordinal);
        Assert.Contains("flash-video-category", source, StringComparison.Ordinal);
        Assert.Contains("MediaService.FlashIngestAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareDirectModeratorActions()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("PromptDeleteDirect", source, StringComparison.Ordinal);
        Assert.Contains("OpenModerateModal", source, StringComparison.Ordinal);
        Assert.Contains("Cambiar Categoría", source, StringComparison.Ordinal);
        Assert.Contains("Eliminar", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareGameIdParameter()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("[Parameter]", source, StringComparison.Ordinal);
        Assert.Contains("public Guid GameId { get; set; }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareYouTubeSearchControlsAndModal()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("Buscar en YouTube", source, StringComparison.Ordinal);
        Assert.Contains("OpenYouTubeSearchModal", source, StringComparison.Ordinal);
        Assert.Contains("YouTubeSearchModal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldDeclareResponsiveFilterPillsWithoutScroll()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("Todos", source, StringComparison.Ordinal);
        Assert.Contains("GetDisplayedItems", source, StringComparison.Ordinal);
        Assert.Contains("GetTypeBadge", source, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(x => x.UserLikesCount)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void YouTubeSearchModal_ShouldSupportInitialGameParameters()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Shared/YouTubeSearchModal.razor");

        Assert.Contains("public Guid? InitialGameId { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("public string? InitialGameTitle { get; set; }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultimediaHub_ShouldKeepYouTubeSearchModalOpen_AfterIngestion()
    {
        var source = ReadSource(MultimediaHubPath);

        Assert.Contains("HandleYouTubeVideoIngestedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_isYouTubeSearchOpen = false;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void YouTubeSearchModal_ShouldDeclareProcessedVideosStateAndBadges()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Shared/YouTubeSearchModal.razor");

        Assert.Contains("_processedVideos", source, StringComparison.Ordinal);
        Assert.Contains("Aprobado", source, StringComparison.Ordinal);
        Assert.Contains("En Pendientes", source, StringComparison.Ordinal);
        Assert.Contains("_loadedForGameId", source, StringComparison.Ordinal);
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
