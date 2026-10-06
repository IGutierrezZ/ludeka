using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para la Ficha Editorial en 5 bloques (01-05), Veredicto Estructurado
/// y StaffTools de INC-117.
/// </summary>
public class GameDetailEditorialBlocksContractTests
{
    private const string GameDetailPath = "src/Ludeka.Web/Components/Pages/GameDetail.razor";
    private const string ScalabilityTrafficLightPath = "src/Ludeka.Web/Components/Shared/ScalabilityTrafficLight.razor";
    private const string GameStaffToolsPanelPath = "src/Ludeka.Web/Components/Shared/GameStaffToolsPanel.razor";
    private const string AssociateBggModalPath = "src/Ludeka.Web/Components/Shared/AssociateBggModal.razor";

    private static string ReadSource(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        throw new FileNotFoundException($"No se localizó el archivo fuente en la ruta {relativePath}");
    }

    [Fact]
    public void GameDetail_ShouldContainAllFiveEditorialBlocksInOrder()
    {
        var source = ReadSource(GameDetailPath);

        // Bloque 01: Cabecera editorial y acciones de colección
        Assert.Contains("id=\"bloque-01-cabecera\"", source, StringComparison.Ordinal);
        Assert.Contains("En la Mesa &amp; Colección", source, StringComparison.Ordinal);

        // Bloque 02: Veredicto de la Mesa y Síntesis IA
        Assert.Contains("id=\"bloque-02-veredicto\"", source, StringComparison.Ordinal);
        Assert.Contains("Veredicto de la Mesa", source, StringComparison.Ordinal);

        // Bloque 03: Ficha Técnica y Escalabilidad Comunitaria
        Assert.Contains("id=\"bloque-03-ficha-tecnica\"", source, StringComparison.Ordinal);
        Assert.Contains("Ficha Técnica &amp; Escalabilidad", source, StringComparison.Ordinal);

        // Bloque 04: Expansiones y Dónde Comprar
        Assert.Contains("id=\"bloque-04-expansiones-tiendas\"", source, StringComparison.Ordinal);
        Assert.Contains("Expansiones &amp; Dónde Comprar", source, StringComparison.Ordinal);

        // Bloque 05: Hub Multimedia y Opiniones
        Assert.Contains("id=\"bloque-05-multimedia-comunidad\"", source, StringComparison.Ordinal);
        Assert.Contains("Multimedia &amp; Comunidad", source, StringComparison.Ordinal);

        // Verificar el orden secuencial de los bloques en el marcado
        int idx1 = source.IndexOf("id=\"bloque-01-cabecera\"", StringComparison.Ordinal);
        int idx2 = source.IndexOf("id=\"bloque-02-veredicto\"", StringComparison.Ordinal);
        int idx3 = source.IndexOf("id=\"bloque-03-ficha-tecnica\"", StringComparison.Ordinal);
        int idx4 = source.IndexOf("id=\"bloque-04-expansiones-tiendas\"", StringComparison.Ordinal);
        int idx5 = source.IndexOf("id=\"bloque-05-multimedia-comunidad\"", StringComparison.Ordinal);

        Assert.True(idx1 < idx2, "El Bloque 01 debe preceder al Bloque 02.");
        Assert.True(idx2 < idx3, "El Bloque 02 debe preceder al Bloque 03.");
        Assert.True(idx3 < idx4, "El Bloque 03 debe preceder al Bloque 04.");
        Assert.True(idx4 < idx5, "El Bloque 04 debe preceder al Bloque 05.");
    }

    [Fact]
    public void GameDetail_ShouldRenderThumbFriendlyCollectionButtonsAndTicker()
    {
        var source = ReadSource(GameDetailPath);

        // Botonera rápida de colección al alcance del pulgar
        Assert.Contains("mi ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("Jugado", source, StringComparison.Ordinal);
        Assert.Contains("Lo quiero", source, StringComparison.Ordinal);
        Assert.Contains("Prestar", source, StringComparison.Ordinal);

        // Franja de 3 cifras clave: BGG, Consenso Ludeka y Jugadores ideales
        Assert.Contains("Rating BGG", source, StringComparison.Ordinal);
        Assert.Contains("Consenso Ludeka", source, StringComparison.Ordinal);
        Assert.Contains("Jugadores ideales", source, StringComparison.Ordinal);

        // Cinta inversa de mercado
        Assert.Contains("var(--ticker)", source, StringComparison.Ordinal);
        Assert.Contains("var(--inverse)", source, StringComparison.Ordinal);
        Assert.Contains("Mejor precio hoy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldIntegrateStaffToolsAndBggModal()
    {
        var source = ReadSource(GameDetailPath);

        // Integración de GameStaffToolsPanel
        Assert.Contains("<GameStaffToolsPanel", source, StringComparison.Ordinal);
        Assert.Contains("OnEditMetadata=\"() => _isEditorModalOpen = true\"", source, StringComparison.Ordinal);
        Assert.Contains("OnRequestAiSummary=\"HandleRequestAiSummary\"", source, StringComparison.Ordinal);
        Assert.Contains("OnAssociateBgg=\"() => _isAssociateBggModalOpen = true\"", source, StringComparison.Ordinal);

        // Integración del modal de asociación de BGG
        Assert.Contains("<AssociateBggModal", source, StringComparison.Ordinal);
        Assert.Contains("@bind-IsOpen=\"_isAssociateBggModalOpen\"", source, StringComparison.Ordinal);
        Assert.Contains("OnBggAssociated=\"HandleBggAssociated\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldRenderMobileFixedBottomBar()
    {
        var source = ReadSource(GameDetailPath);

        // Barra inferior fija en móvil (lg:hidden fixed bottom-0 left-0 right-0)
        Assert.Contains("lg:hidden fixed bottom-0 left-0 right-0", source, StringComparison.Ordinal);
        Assert.Contains("GetBestPriceLabel()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalabilityTrafficLight_ShouldRenderSevenColumnsEditorialLayout()
    {
        var source = ReadSource(ScalabilityTrafficLightPath);

        // 7 columnas y barra de 10px con tokens de diseño
        Assert.Contains("grid-cols-7", source, StringComparison.Ordinal);
        Assert.Contains("h-[10px]", source, StringComparison.Ordinal);
        Assert.Contains("var(--accent-green)", source, StringComparison.Ordinal);
        Assert.Contains("var(--mustard)", source, StringComparison.Ordinal);
        Assert.Contains("var(--line)", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"sparkles\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameStaffToolsPanel_ShouldProvideAllModeratorActionsWithLucideIcons()
    {
        var source = ReadSource(GameStaffToolsPanelPath);

        // Acciones disponibles para moderación
        Assert.Contains("HandleEditMetadata", source, StringComparison.Ordinal);
        Assert.Contains("HandleRequestAiSummary", source, StringComparison.Ordinal);
        Assert.Contains("HandleAssociateBgg", source, StringComparison.Ordinal);
        Assert.Contains("HandleSocialCard", source, StringComparison.Ordinal);
        Assert.Contains("HandleOpenReports", source, StringComparison.Ordinal);

        // Iconos Lucide (cero emojis)
        Assert.Contains("<Icon Name=\"zap\"", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"pen-line\"", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"bot\"", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"globe\"", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"palette\"", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"flag\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("⚡", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AssociateBggModal_ShouldValidateAndShowPreviewLink()
    {
        var source = ReadSource(AssociateBggModalPath);

        // Interfaz y enlace de previsualización
        Assert.Contains("boardgamegeek.com/boardgame/", source, StringComparison.Ordinal);
        Assert.Contains("AssociateBggIdAsync", source, StringComparison.Ordinal);
        Assert.Contains("Asociar BGG ID", source, StringComparison.Ordinal);
    }
}
