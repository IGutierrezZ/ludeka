using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para la Portada Editorial «Revista Lúdica» (INC-115).
/// Valida la estructura jerárquica de HomeDashboard, los 6 bloques editoriales canónicos,
/// el Hero en arco con pegatina inclinada, la cinta reactiva TickerBar, el Top Semanal ciclálico,
/// las polaroids físicas de sorteos, las novedades agrupadas y la ausencia de emojis.
/// </summary>
public class HomeDashboardContractTests
{
    private const string HomeDashboardPath = "src/Ludeka.Web/Components/Pages/HomeDashboard.razor";
    private const string HeroPath = "src/Ludeka.Web/Components/Home/HeroRevistaLudica.razor";
    private const string TickerPath = "src/Ludeka.Web/Components/Home/TickerBar.razor";
    private const string WeeklyTopPath = "src/Ludeka.Web/Components/Home/WeeklyTopSection.razor";
    private const string PolaroidPath = "src/Ludeka.Web/Components/Home/PolaroidGiveawayCard.razor";
    private const string EventsGridPath = "src/Ludeka.Web/Components/Home/UpcomingEventsGrid.razor";
    private const string HomeGameCardPath = "src/Ludeka.Web/Components/Home/HomeGameCard.razor";

    [Fact]
    public void HomeDashboard_ShouldOrchestrateAllEditorialBlocks()
    {
        var source = ReadSource(HomeDashboardPath);

        // 1. Hero Terracota
        Assert.Contains("<HeroRevistaLudica", source, StringComparison.Ordinal);

        // 2. Cinta de avisos inversa con temporizador
        Assert.Contains("<TickerBar", source, StringComparison.Ordinal);

        // 3. El Top de la semana
        Assert.Contains("<WeeklyTopSection", source, StringComparison.Ordinal);

        // 4. Banda de Sorteos en marcha con polaroids (flujo directo tras Top)
        Assert.Contains("<PolaroidGiveawayCard", source, StringComparison.Ordinal);
        Assert.Contains("Sorteos en marcha", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/sorteos\"", source, StringComparison.Ordinal);

        // 5. Banda verde de Novedades en tiendas
        Assert.Contains("Novedades en tiendas", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/novedades/", source, StringComparison.Ordinal);

        // 6. Ferias y grandes citas pastel
        Assert.Contains("<UpcomingEventsGrid", source, StringComparison.Ordinal);

        // 7. Integración con modal de colección rápida
        Assert.Contains("<QuickCollectionModal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeroRevistaLudica_ShouldFollowDesignSpecs()
    {
        var source = ReadSource(HeroPath);

        // Fondo terracota y texto on-brand
        Assert.Contains("bg-[var(--brand)]", source, StringComparison.Ordinal);
        Assert.Contains("text-[var(--on-brand)]", source, StringComparison.Ordinal);

        // Eyebrow editorial
        Assert.Contains("Juegos, sorteos, eventos y opiniones de verdad", source, StringComparison.Ordinal);

        // Titular display con palabra final en cursiva mostaza
        Assert.Contains("¿A qué", source, StringComparison.Ordinal);
        Assert.Contains("jugamos", source, StringComparison.Ordinal);
        Assert.Contains("text-[var(--mustard)]", source, StringComparison.Ordinal);
        Assert.Contains("hoy?", source, StringComparison.Ordinal);
        Assert.Contains("focus:outline-none", source, StringComparison.Ordinal);

        // Buscador píldora de 60px
        Assert.Contains("rounded-full bg-[#FFF8EE]", source, StringComparison.Ordinal);
        Assert.Contains("Busca un juego, editorial o diseñador", source, StringComparison.Ordinal);
        Assert.Contains("/catalogo?q=", source, StringComparison.Ordinal);

        // Composición en arco con borde blanco de 6px y pegatina inclinada a -12deg
        Assert.Contains("border-[#FFF8EE]", source, StringComparison.Ordinal);
        Assert.Contains("-rotate-12", source, StringComparison.Ordinal);
        Assert.Contains("Bienvenido", source, StringComparison.Ordinal);
        Assert.Contains("a tu mesa", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TickerBar_ShouldHaveInverseBackgroundAndReactiveTimer()
    {
        var source = ReadSource(TickerPath);

        // Fondo inverso y texto mostaza
        Assert.Contains("bg-[var(--inverse)]", source, StringComparison.Ordinal);
        Assert.Contains("text-[var(--ticker)]", source, StringComparison.Ordinal);

        // Puntos terracota separadores
        Assert.Contains("bg-[var(--accent)]", source, StringComparison.Ordinal);

        // Temporizador reactivo determinista y limpieza IDisposable
        Assert.Contains("System.Threading", source, StringComparison.Ordinal);
        Assert.Contains("Timer", source, StringComparison.Ordinal);
        Assert.Contains("Dispose()", source, StringComparison.Ordinal);

        // Enlaces canónicos
        Assert.Contains("href=\"/sorteos", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/catalogo\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/novedades\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/tendencias\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyTopSection_ShouldIncludeTabsCyclicNumbersAndMobileCompactList()
    {
        var source = ReadSource(WeeklyTopPath);

        // Encabezado con regla de 3px
        Assert.Contains("El top de la semana", source, StringComparison.Ordinal);
        Assert.Contains("border-[var(--rule)]", source, StringComparison.Ordinal);

        // Pestañas editoriales
        Assert.Contains("En tendencia", source, StringComparison.Ordinal);
        Assert.Contains("Mejor valorados", source, StringComparison.Ordinal);
        Assert.Contains("Para 2", source, StringComparison.Ordinal);

        // Numerales 01, 02, 03... formateados con dos dígitos de Claude Design
        Assert.Contains("ToString(\"00\")", source, StringComparison.Ordinal);

        // Enlace canónico a tendencias y catálogo
        Assert.Contains("/tendencias", source, StringComparison.Ordinal);
        Assert.Contains("/catalogo", source, StringComparison.Ordinal);


        // Colores cíclicos
        Assert.Contains("var(--accent)", source, StringComparison.Ordinal);
        Assert.Contains("var(--mustard)", source, StringComparison.Ordinal);
        Assert.Contains("var(--accent-green)", source, StringComparison.Ordinal);
        Assert.Contains("var(--sky)", source, StringComparison.Ordinal);
        Assert.Contains("var(--pink)", source, StringComparison.Ordinal);

        // Enlace al catálogo
        Assert.Contains("Ver el catálogo completo", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PolaroidGiveawayCard_ShouldBePhysicalPolaroidWithoutEmojis()
    {
        var source = ReadSource(PolaroidPath);

        // Estilo polaroid física crema con sombra
        Assert.Contains("bg-[#FFF8EE]", source, StringComparison.Ordinal);
        Assert.Contains("shadow-[0_16px_30px_rgba(34,24,15,0.20)]", source, StringComparison.Ordinal);

        // Dimensiones intrínsecas anti-CLS
        Assert.Contains("width=\"220\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"220\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);

        // Icono timer y color de alerta (sin emoji)
        Assert.Contains("Icon Name=\"timer\"", source, StringComparison.Ordinal);
        Assert.Contains("#B23A12", source, StringComparison.Ordinal);
        Assert.DoesNotContain("⏱", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UpcomingEventsGrid_ShouldUsePastelPaletteAndBigDays()
    {
        var source = ReadSource(EventsGridPath);

        // Título de sección y enlace a agenda
        Assert.Contains("Ferias y grandes citas", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/eventos\"", source, StringComparison.Ordinal);

        // Tarjetas pastel con radio de 24px
        Assert.Contains("rounded-[24px]", source, StringComparison.Ordinal);
        Assert.Contains("var(--p-blue)", source, StringComparison.Ordinal);
        Assert.Contains("var(--p-pink)", source, StringComparison.Ordinal);
        Assert.Contains("var(--p-peach)", source, StringComparison.Ordinal);
        Assert.Contains("var(--p-mint)", source, StringComparison.Ordinal);

        // Día gigante
        Assert.Contains("StartDate.Day", source, StringComparison.Ordinal);
        Assert.Contains("RemainingDaysText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeGameCard_ShouldSupportQuickActionAndDurationBadge()
    {
        var source = ReadSource(HomeGameCardPath);

        // Botón de acción rápida para ludoteca / jugado
        Assert.Contains("OnQuickAction", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"plus\"", source, StringComparison.Ordinal);

        // Badges de comensales y tiempo
        Assert.Contains("FormatCompactPlayers", source, StringComparison.Ordinal);
        Assert.Contains("EstimatedPerPlayerMinutes", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"clock\"", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var repoRoot = GetRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"No se encontró el archivo: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("No se localizó la raíz del repositorio.");
    }
}
