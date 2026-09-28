using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato de fuente para la ficha de sorteo (INC-79).
/// Verifica rutas, interactividad, componentes editoriales, anti-CLS y capacidades administrativas.
/// </summary>
public class GiveawayDetailPageContractTests
{
    private const string PagePath = "src/Ludeka.Web/Components/Pages/GiveawayDetail.razor";

    [Fact]
    public void Page_ShouldDeclareCorrectRoutesAndRenderMode()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("@page \"/sorteos/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/sorteo/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldContainEditorialNavigationAndFallbackImage()
    {
        var source = ReadSource(PagePath);

        // Enlace de vuelta al radar
        Assert.Contains("href=\"/sorteos\"", source, StringComparison.Ordinal);
        Assert.Contains("Volver al radar de sorteos", source, StringComparison.Ordinal);

        // Fallback de imagen por dominio y dimensiones anti-CLS
        Assert.Contains("DefaultImage", source, StringComparison.Ordinal);
        Assert.Contains("DefaultImageDomain.Sorteo", source, StringComparison.Ordinal);
        Assert.Contains("sorteo-default.svg", source, StringComparison.Ordinal);
        Assert.Contains("onerror", source, StringComparison.Ordinal);
        Assert.Contains("this.onerror=null", source, StringComparison.Ordinal);
        Assert.Contains("width=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);

        // CTA oficial de participación
        Assert.Contains("Participar en el sorteo oficial", source, StringComparison.Ordinal);
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
        Assert.Contains("Editar Ficha del Sorteo", source, StringComparison.Ordinal);
        Assert.Contains("Eliminar Sorteo", source, StringComparison.Ordinal);

        // Subida de imagen y llamadas de servicio
        Assert.Contains("<InputFile", source, StringComparison.Ordinal);
        Assert.Contains("UploadOptimizedImageAsync", source, StringComparison.Ordinal);
        Assert.Contains("UpdateGiveawayAsync", source, StringComparison.Ordinal);
        Assert.Contains("DeleteGiveawayAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldNotContainProhibitedEmojisOrLegacyColorClasses()
    {
        var source = ReadSource(PagePath);

        // Prohibición de emojis directos en código (se usan iconos Lucide o catálogo dinámico)
        string[] prohibitedEmojis = ["⭐", "🎁", "🎲", "📸", "🔗", "🎬", "🇪🇸", "🌎"];
        foreach (var emoji in prohibitedEmojis)
        {
            Assert.DoesNotContain(emoji, source, StringComparison.Ordinal);
        }

        // Prohibición de clases de temas y colores duros
        string[] prohibitedClasses = ["dark:", "text-amber-500", "text-pink-500", "text-sky-500", "text-rose-600"];
        foreach (var cls in prohibitedClasses)
        {
            Assert.DoesNotContain(cls, source, StringComparison.Ordinal);
        }
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
