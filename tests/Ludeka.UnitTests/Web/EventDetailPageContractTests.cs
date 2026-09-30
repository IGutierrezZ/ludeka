using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato de fuente para la ficha de evento o feria lúdica (INC-87).
/// Verifica rutas, interactividad, componentes editoriales, anti-CLS y capacidades administrativas.
/// </summary>
public class EventDetailPageContractTests
{
    private const string PagePath = "src/Ludeka.Web/Components/Pages/EventDetail.razor";

    [Fact]
    public void Page_ShouldDeclareCorrectRoutesAndRenderMode()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("@page \"/eventos/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/evento/{Id:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldContainEditorialNavigationAndFallbackImage()
    {
        var source = ReadSource(PagePath);

        // Enlace de vuelta al calendario de eventos
        Assert.Contains("href=\"/eventos\"", source, StringComparison.Ordinal);
        Assert.Contains("Volver al calendario de eventos", source, StringComparison.Ordinal);

        // Fallback de imagen por dominio y dimensiones anti-CLS
        Assert.Contains("DefaultImage", source, StringComparison.Ordinal);
        Assert.Contains("DefaultImageDomain.Evento", source, StringComparison.Ordinal);
        Assert.Contains("evento-default.svg", source, StringComparison.Ordinal);
        Assert.Contains("onerror", source, StringComparison.Ordinal);
        Assert.Contains("this.onerror=null", source, StringComparison.Ordinal);
        Assert.Contains("width=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"600\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);

        // CTA oficial hacia la web del evento
        Assert.Contains("Visitar web oficial del evento", source, StringComparison.Ordinal);
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
        Assert.Contains("Editar Evento", source, StringComparison.Ordinal);
        Assert.Contains("Eliminar Evento", source, StringComparison.Ordinal);

        // Subida de cartel WebP a R2 y llamadas de servicio
        Assert.Contains("<InputFile", source, StringComparison.Ordinal);
        Assert.Contains("UploadOptimizedImageAsync", source, StringComparison.Ordinal);
        Assert.Contains("UpdateEventAsync", source, StringComparison.Ordinal);
        Assert.Contains("DeleteEventAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldNotContainProhibitedEmojisOrLegacyColorClasses()
    {
        var source = ReadSource(PagePath);

        // Prohibición de emojis directos en código (se usan iconos Lucide o catálogo dinámico)
        string[] prohibitedEmojis = ["⭐", "🎁", "🎲", "📸", "🔗", "🎪", "📅"];
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
