using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato de fuente para la ficha de novedad editorial (INC-87).
/// Verifica rutas, interactividad, componentes editoriales, anti-CLS y capacidades administrativas.
/// </summary>
public class ReleaseDetailPageContractTests
{
    private const string PagePath = "src/Ludeka.Web/Components/Pages/ReleaseDetail.razor";

    [Fact]
    public void Page_ShouldDeclareCorrectRoutesAndRenderMode()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("@page \"/novedades/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/novedad/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldContainEditorialNavigationAndFallbackImage()
    {
        var source = ReadSource(PagePath);

        // Enlace de vuelta al calendario de novedades
        Assert.Contains("href=\"/novedades\"", source, StringComparison.Ordinal);
        Assert.Contains("Volver al calendario de novedades", source, StringComparison.Ordinal);

        // Fallback de imagen por dominio y dimensiones anti-CLS
        Assert.Contains("DefaultImage", source, StringComparison.Ordinal);
        Assert.Contains("DefaultImageDomain.Novedad", source, StringComparison.Ordinal);
        Assert.Contains("novedad-default.svg", source, StringComparison.Ordinal);
        Assert.Contains("onerror", source, StringComparison.Ordinal);
        Assert.Contains("this.onerror=null", source, StringComparison.Ordinal);
        Assert.Contains("width=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);

        // CTA oficial hacia la noticia o fuente original
        Assert.Contains("Ir a la noticia oficial", source, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", source, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldContainAdminEditAndUploadCapabilities()
    {
        var source = ReadSource(PagePath);

        // Permisos y modales administrativos
        Assert.Contains("CanModerate", source, StringComparison.Ordinal);
        Assert.Contains("EditorialModal", source, StringComparison.Ordinal);
        Assert.Contains("Editar Novedad", source, StringComparison.Ordinal);
        Assert.Contains("Eliminar Lanzamiento", source, StringComparison.Ordinal);

        // Subida de carátula WebP en R2 y llamadas de servicio
        Assert.Contains("<InputFile", source, StringComparison.Ordinal);
        Assert.Contains("UploadOptimizedImageAsync", source, StringComparison.Ordinal);
        Assert.Contains("UpdateReleaseAsync", source, StringComparison.Ordinal);
        Assert.Contains("DeleteReleaseAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldNotContainProhibitedEmojisOrLegacyColorClasses()
    {
        var source = ReadSource(PagePath);

        // Prohibición de emojis directos en código (se usan iconos Lucide)
        string[] prohibitedEmojis = ["⭐", "🎁", "🎲", "📸", "🔗", "📰", "📅"];
        foreach (var emoji in prohibitedEmojis)
        {
            Assert.DoesNotContain(emoji, source, StringComparison.Ordinal);
        }

        // Prohibición de clases de temas y colores duros
        string[] prohibitedClasses = ["dark:", "text-pink-500", "text-sky-500", "text-rose-600"];
        foreach (var cls in prohibitedClasses)
        {
            Assert.DoesNotContain(cls, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Page_ShouldDeclareModeratorAddLinkActionWhenSourceUrlIsEmpty()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("Añadir enlace oficial a la noticia", source, StringComparison.Ordinal);
        Assert.Contains("Información catalogada directamente por el equipo editorial", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldValidateSourceUrlMandatoryInEditModal()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("Enlace Oficial a la Noticia / Fuente (URL) *", source, StringComparison.Ordinal);
        Assert.Contains("El enlace oficial o noticia de la novedad es obligatorio", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NewsPage_ShouldValidateSourceUrlMandatoryInCreateModal()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/News.razor");

        Assert.Contains("Enlace Oficial a la Noticia / Fuente *", source, StringComparison.Ordinal);
        Assert.Contains("El enlace oficial o noticia de la novedad es obligatorio (igual que en sorteos).", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialInboxEditModal_ShouldDeclareSourceUrlFieldAndValidation()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Shared/SocialInboxEditModal.razor");

        Assert.Contains("_sourceUrl", source, StringComparison.Ordinal);
        Assert.Contains("Enlace Oficial a la Noticia / Fuente *", source, StringComparison.Ordinal);
        Assert.Contains("El enlace oficial o noticia de la novedad es obligatorio (igual que en sorteos).", source, StringComparison.Ordinal);
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
