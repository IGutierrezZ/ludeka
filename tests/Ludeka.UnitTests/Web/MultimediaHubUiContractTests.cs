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
