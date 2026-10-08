using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato de fuente para el rediseño editorial de la página de novedades (/novedades).
/// Verifica paginación, filtros reactivos, soporte de IsMonthOnly, badges editoriales y prevención de regresiones.
/// </summary>
public class NewsPageContractTests
{
    private const string PagePath = "src/Ludeka.Web/Components/Pages/News.razor";

    [Fact]
    public void Page_ShouldDeclareCorrectRouteAndRenderMode()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("@page \"/novedades\"", source, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldImplementPaginationStructureAndControls()
    {
        var source = ReadSource(PagePath);

        // Constante o propiedad de tamaño de página
        Assert.Contains("PageSize", source, StringComparison.Ordinal);
        Assert.Contains("_currentPage", source, StringComparison.Ordinal);

        // Controles de paginación
        Assert.Contains("Anterior", source, StringComparison.Ordinal);
        Assert.Contains("Siguiente", source, StringComparison.Ordinal);
        Assert.Contains("TotalPages", source, StringComparison.Ordinal);
        Assert.Contains("GoToPage(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldSupportIsMonthOnlyCheckboxAndDisplay()
    {
        var source = ReadSource(PagePath);

        // Soporte en modal de creación
        Assert.Contains("_formIsMonthOnly", source, StringComparison.Ordinal);
        Assert.Contains("Solo mes", source, StringComparison.Ordinal);

        // Formato visual en badge o día cuando IsMonthOnly es verdadero
        Assert.Contains("IsMonthOnly", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldContainPillFiltersAndSearch()
    {
        var source = ReadSource(PagePath);

        Assert.Contains("_typeFilter", source, StringComparison.Ordinal);
        Assert.Contains("_selectedPublisher", source, StringComparison.Ordinal);
        Assert.Contains("_searchTerm", source, StringComparison.Ordinal);
        Assert.Contains("Todas", source, StringComparison.Ordinal);
        Assert.Contains("Novedades", source, StringComparison.Ordinal);
        Assert.Contains("Reimpresiones", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_ShouldNotContainProhibitedEmojisOrLegacyColors()
    {
        var source = ReadSource(PagePath);

        string[] prohibitedEmojis = ["⭐", "🎁", "🎲", "📸", "🔗", "📰", "📅"];
        foreach (var emoji in prohibitedEmojis)
        {
            Assert.DoesNotContain(emoji, source, StringComparison.Ordinal);
        }

        string[] prohibitedClasses = ["dark:", "text-pink-500", "text-sky-500", "text-rose-600"];
        foreach (var cls in prohibitedClasses)
        {
            Assert.DoesNotContain(cls, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Page_ShouldContainComparativeModerationInboxForModerators()
    {
        var source = ReadSource(PagePath);

        // Pestaña y contenedor de moderación
        Assert.Contains("Pendientes", source, StringComparison.Ordinal);
        Assert.Contains("Bandeja de Moderación Editorial", source, StringComparison.Ordinal);
        Assert.Contains("_pendingReleases", source, StringComparison.Ordinal);

        // Comparativa y sugerencia IA
        Assert.Contains("Propuesta IA (Gemini &amp; BGG)", source, StringComparison.Ordinal);
        Assert.Contains("AiSuggestedBggId", source, StringComparison.Ordinal);
        Assert.Contains("AiMatchReasoning", source, StringComparison.Ordinal);

        // Acciones de moderación
        Assert.Contains("HandleApprove", source, StringComparison.Ordinal);
        Assert.Contains("HandleReject", source, StringComparison.Ordinal);
        Assert.Contains("Aprobar y vincular a BGG", source, StringComparison.Ordinal);
        Assert.Contains("Aprobar sin enlace BGG", source, StringComparison.Ordinal);
        Assert.Contains("Descartar", source, StringComparison.Ordinal);
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
