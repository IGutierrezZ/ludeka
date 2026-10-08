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

        // Botonera rápida de colección al alcance del pulgar (Prestar trasladado a Mi Ludoteca)
        Assert.Contains("mi ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("Jugado", source, StringComparison.Ordinal);
        Assert.Contains("Lo quiero", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Prestar", source, StringComparison.Ordinal);
        Assert.Contains("Registrar partida", source, StringComparison.Ordinal);

        // Franja de cifras clave editoriales: BGG, Consenso Ludeka, Jugadores ideales y Minutos
        Assert.Contains("Rating BGG", source, StringComparison.Ordinal);
        Assert.Contains("Consenso Ludeka", source, StringComparison.Ordinal);
        Assert.Contains("Jugadores ideales", source, StringComparison.Ordinal);
        Assert.Contains("Minutos", source, StringComparison.Ordinal);
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
    public void GameDetail_ShouldRenderThumbFriendlyMobileActions()
    {
        var source = ReadSource(GameDetailPath);

        // Acciones móviles limpias integradas en cabecera sin solapamiento con MobileBottomNav
        Assert.Contains("Registrar partida", source, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Compartir\"", source, StringComparison.Ordinal);
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

    [Fact]
    public void GameDetail_ShouldFilterStockInBestPriceAndExcludePolaroidSticker()
    {
        var source = ReadSource(GameDetailPath);

        // La pegatina flotante girada -6° sobre la carátula ha sido retirada
        Assert.DoesNotContain("-rotate-6 flex flex-col shadow-2xl pointer-events-none", source, StringComparison.Ordinal);

        // Filtrado riguroso de stock en la obtención del mejor precio del día
        Assert.Contains("p.InStock", source, StringComparison.Ordinal);
        Assert.Contains("Agotado en tiendas", source, StringComparison.Ordinal);

        // Veredicto estructurado como sección limpia en línea bajo pestaña dedicada
        Assert.Contains("id=\"bloque-02-veredicto\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldAlignWithClaudeEditorialDesign_CleanHeaderSummaryAndVideosTab()
    {
        var source = ReadSource(GameDetailPath);

        // Cabecera limpia: solo nombres de diseñador y editorial, sin prefijos redundantes
        Assert.DoesNotContain("Diseñado por <span", source, StringComparison.Ordinal);
        Assert.DoesNotContain("editado en España por", source, StringComparison.Ordinal);
        Assert.Contains("@GetEyebrowText()", source, StringComparison.Ordinal);

        // Minutos en una sola línea protegidos contra salto
        Assert.Contains("whitespace-nowrap\">@GetDurationNumber()′</b>", source, StringComparison.Ordinal);

        // Veredicto en resumen truncado a 3 líneas con line-clamp-3
        Assert.Contains("line-clamp-3", source, StringComparison.Ordinal);
        Assert.Contains("Leer el veredicto completo", source, StringComparison.Ordinal);

        // Zona roja limpia: sin botón redundante de veredicto ni botón de compartir
        Assert.DoesNotContain("<span>Leer el veredicto &rarr;</span>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Compartir ficha\"", source, StringComparison.Ordinal);

        // Pestañas independientes: Videos muestra el reproductor hero y cuadrícula sin subpestañas
        Assert.Contains("AllGameVideos", source, StringComparison.Ordinal);
        Assert.Contains("OpenVideoEmbed(featured)", source, StringComparison.Ordinal);
        Assert.Contains("¿Conoces un vídeo mejor?", source, StringComparison.Ordinal);
        Assert.Contains("Propón uno", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldSupportHeroImageZoomAndPaddedUserReviewCard()
    {
        var gameDetailSource = ReadSource(GameDetailPath);
        var reviewCardSource = ReadSource("src/Ludeka.Web/Components/Shared/UserReviewCard.razor");

        // Ampliación de imagen / zoom lightbox al pulsar sobre la imagen seleccionada
        Assert.Contains("_isImageZoomModalOpen = true", gameDetailSource, StringComparison.Ordinal);
        Assert.Contains("cursor-zoom-in", gameDetailSource, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Vista ampliada de la imagen\"", gameDetailSource, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"maximize-2\"", gameDetailSource, StringComparison.Ordinal);

        // Recuadro de valoración del usuario con padding completo y márgenes adecuados
        Assert.Contains("p-5 sm:p-6 rounded-2xl", reviewCardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("p-4.5", reviewCardSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldToggleAdnLudicoExtendedDetails()
    {
        var source = ReadSource(GameDetailPath);

        // Título dinámico según estado de despliegue
        Assert.Contains("@(_isTechOpen ? \"Ficha Técnica Completa\" : \"ADN Lúdico\")", source, StringComparison.Ordinal);

        // Botón conmutador con aria-expanded y etiquetas adecuadas
        Assert.Contains("@onclick=\"() => _isTechOpen = !_isTechOpen\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"@_isTechOpen\"", source, StringComparison.Ordinal);
        Assert.Contains("@(_isTechOpen ? \"Ver menos\" : \"Ver todos los detalles\")", source, StringComparison.Ordinal);

        // Los atributos extendidos del ADN lúdico deben condicionarse a @_isTechOpen
        Assert.Contains("@if (_isTechOpen)", source, StringComparison.Ordinal);
        Assert.Contains("Dureza / Peso", source, StringComparison.Ordinal);
        Assert.Contains("Confrontación", source, StringComparison.Ordinal);
        Assert.Contains("Huella en mesa", source, StringComparison.Ordinal);
        Assert.Contains("Modo solitario", source, StringComparison.Ordinal);
        Assert.Contains("Dependencia idioma", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldCorrectlyBindExpansionComponentsWithoutExtraneousParameters()
    {
        var source = ReadSource(GameDetailPath);

        // Bloque 04: Expansiones debe vincular ExpansionEcosystemSection con BaseGameId y Expansions
        Assert.Contains("<ExpansionEcosystemSection BaseGameId=\"@Game.Id\" Expansions=\"@_expansions\" />", source, StringComparison.Ordinal);

        // No debe pasar parámetros inexistentes a ExpansionEcosystemSection (causante de circuit crash en Blazor)
        var withoutValidEco = source.Replace("<ExpansionEcosystemSection BaseGameId=\"@Game.Id\" Expansions=\"@_expansions\" />", string.Empty);
        Assert.DoesNotContain("<ExpansionEcosystemSection", withoutValidEco, StringComparison.Ordinal);

        // Debe soportar expansiones hijas mediante ExpansionAporteCard y ExpansionSisterList
        Assert.Contains("<ExpansionAporteCard", source, StringComparison.Ordinal);
        Assert.Contains("<ExpansionSisterList", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldDisplayBggWeightAndComplexityCanonicalHelpers()
    {
        var source = ReadSource(GameDetailPath);

        // Bloque ADN Lúdico debe mostrar Dureza / Peso y llamar a GetComplexityAdnText()
        Assert.Contains("Dureza / Peso", source, StringComparison.Ordinal);
        Assert.Contains("@GetComplexityAdnText()", source, StringComparison.Ordinal);

        // Lógica de cálculo debe delegar en ComplexityCalculator.Calculate
        Assert.Contains("ComplexityCalculator.Calculate", source, StringComparison.Ordinal);

        // Eyebrow debe formatear BggWeight si está presente
        Assert.Contains("Game.BggWeight.HasValue", source, StringComparison.Ordinal);
    }
}

