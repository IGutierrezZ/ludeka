using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y verificación de interfaz para la navegación móvil y áreas seguras (INC-60).
/// Valida el principio Mobile-First Radical: barra inferior fija, safe-area-inset, 5 destinos
/// accesibles y ergonomía táctil en acciones de colección.
/// </summary>
public class MobileNavigationContractTests
{
    private const string AppPath = "src/Ludeka.Web/Components/App.razor";
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string MainLayoutCssPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor.css";
    private const string InputCssPath = "src/Ludeka.Web/Styles/input.css";
    private const string MobileBottomNavPath = "src/Ludeka.Web/Components/Shared/MobileBottomNav.razor";
    private const string CollectionActionBarPath = "src/Ludeka.Web/Components/Shared/CollectionActionBar.razor";
    private const string MobileNavJsPath = "src/Ludeka.Web/wwwroot/js/mobile-nav.js";

    [Fact]
    public void App_ViewportContract_ShouldIncludeViewportFitCoverForSafeArea()
    {
        var source = ReadSource(AppPath);

        // Habilita el cálculo nativo de safe-area-inset en WebKit/Blink
        Assert.Contains("viewport-fit=cover", source, StringComparison.Ordinal);
        Assert.Contains("name=\"viewport\"", source, StringComparison.Ordinal);
        Assert.Contains("width=device-width", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_MarkupContract_ShouldIncludeMobileBottomNavAndSpacers()
    {
        var source = ReadSource(MainLayoutPath);

        // Inclusión del componente móvil
        Assert.Contains("<MobileBottomNav />", source, StringComparison.Ordinal);

        // Clase de reserva de espacio inferior en el footer para evitar solapamiento
        Assert.Contains("mobile-nav-spacer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayoutCss_ShouldPositionBlazorErrorUiAboveMobileNav()
    {
        var source = ReadSource(MainLayoutCssPath);

        // #blazor-error-ui debe contemplar el espacio de la barra móvil
        Assert.Contains("bottom: calc(4.25rem + env(safe-area-inset-bottom, 0px));", source, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 1024px)", source, StringComparison.Ordinal);
        Assert.Contains("bottom: 0;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InputCss_ShouldDefineSafeAreaAndSpacerUtilities()
    {
        var source = ReadSource(InputCssPath);

        Assert.Contains(".mobile-safe-bottom", source, StringComparison.Ordinal);
        Assert.Contains(".mobile-nav-spacer", source, StringComparison.Ordinal);
        Assert.Contains("env(safe-area-inset-bottom, 0px)", source, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 1024px)", source, StringComparison.Ordinal);
        Assert.Contains("padding-bottom: 0 !important;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileBottomNav_MarkupContract_ShouldHaveAccessibleLandmarkAndResponsiveBreakpoints()
    {
        var source = ReadSource(MobileBottomNavPath);

        // Landmark semántico y etiquetado accesible WCAG 2.2 AA
        Assert.Contains("<nav aria-label=\"Navegación principal móvil\"", source, StringComparison.Ordinal);
        Assert.Contains("fixed bottom-0 inset-x-0 z-40", source, StringComparison.Ordinal);
        Assert.Contains("mobile-safe-bottom", source, StringComparison.Ordinal);
        Assert.Contains("lg:hidden", source, StringComparison.Ordinal);
        Assert.Contains("backdrop-blur-md", source, StringComparison.Ordinal);
        Assert.Contains("@implements IDisposable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileBottomNav_DestinationsContract_ShouldContainAllFivePrimaryDestinations()
    {
        var source = ReadSource(MobileBottomNavPath);

        // 1. Inicio
        Assert.Contains("href=\"/\"", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"house\"", source, StringComparison.Ordinal);
        Assert.Contains(">Inicio<", source, StringComparison.Ordinal);

        // 2. Catálogo
        Assert.Contains("href=\"/catalogo\"", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"dices\"", source, StringComparison.Ordinal);
        Assert.Contains(">Catálogo<", source, StringComparison.Ordinal);

        // 3. Sorteos
        Assert.Contains("href=\"/sorteos\"", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"gift\"", source, StringComparison.Ordinal);
        Assert.Contains(">Sorteos<", source, StringComparison.Ordinal);

        // 4. Mi Ludoteca o Acceso
        Assert.Contains("href=\"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/login\"", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"library\"", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"user\"", source, StringComparison.Ordinal);
        Assert.Contains(">Ludoteca<", source, StringComparison.Ordinal);
        Assert.Contains(">Entrar<", source, StringComparison.Ordinal);

        // 5. Más de Ludeka (disparador modal de 5 destinos principales, INC-114)
        Assert.Contains("aria-label=\"Más de Ludeka\"", source, StringComparison.Ordinal);
        Assert.Contains("ToggleSheet", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"menu\"", source, StringComparison.Ordinal);
        Assert.Contains(">Más<", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileBottomNav_MoreSheetContract_ShouldContainPastelGridAndAccessibility()
    {
        var source = ReadSource(MobileBottomNavPath);

        // Hoja modal accesible WCAG 2.2 AA
        Assert.Contains("role=\"dialog\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", source, StringComparison.Ordinal);
        Assert.Contains("rounded-[26px]", source, StringComparison.Ordinal);
        Assert.Contains("border-2 border-[var(--rule)]", source, StringComparison.Ordinal);
        Assert.Contains("e.Key == \"Escape\"", source, StringComparison.Ordinal);

        // Cuadrícula 2x4 con destinos secundarios y selector de tema
        Assert.Contains("href=\"/novedades\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/eventos\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/clasificaciones\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/editoriales\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/creadores\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/tiendas\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/transparencia\"", source, StringComparison.Ordinal);
        Assert.Contains("ToggleThemeAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileBottomNav_AccessibilityContract_ShouldSupportAriaCurrentAndLocationChanged()
    {
        var source = ReadSource(MobileBottomNavPath);

        // Atributo ARIA de estado activo
        Assert.Contains("aria-current=\"@(IsHomeActive ? \"page\" : null)\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsCatalogActive ? \"page\" : null)\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsLibraryActive ? \"page\" : null)\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsDrawsActive ? \"page\" : null)\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsAccountActive ? \"page\" : null)\"", source, StringComparison.Ordinal);

        // Ergonomía táctil: tamaño mínimo de 48px y foco visible
        Assert.Contains("min-h-[48px]", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);

        // Reactividad y ciclo de vida
        Assert.Contains("Navigation.LocationChanged += HandleLocationChanged;", source, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged -= HandleLocationChanged;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionActionBar_MobileErgonomicsContract_ShouldSupportThreeColumnSegmentedRow()
    {
        var source = ReadSource(CollectionActionBarPath);

        // Barra ergonómica en 3 columnas alcanzable con el pulgar
        Assert.Contains("grid grid-cols-3 gap-1.5 sm:gap-2", source, StringComparison.Ordinal);
        Assert.Contains("<span class=\"sm:hidden\">Tengo</span>", source, StringComparison.Ordinal);
        Assert.Contains("<span class=\"hidden sm:inline\">En mi ludoteca</span>", source, StringComparison.Ordinal);
        Assert.Contains("<span>Jugado</span>", source, StringComparison.Ordinal);
        Assert.Contains("<span>Comprar</span>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void App_ScriptContract_ShouldIncludeMobileNavScriptWithDefer()
    {
        var source = ReadSource(AppPath);

        Assert.Contains("<script src=\"/js/mobile-nav.js\" defer></script>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_TopMobileNav_ShouldHaveAccessibleDetailsContract()
    {
        var source = ReadSource(MainLayoutPath);

        // Menú móvil con identificador para descarte exterior accesible y semántica estándar
        Assert.Contains("<details id=\"mobile-nav-details\" data-mobile-nav class=\"lg:hidden relative\">", source, StringComparison.Ordinal);
        Assert.Contains("<summary aria-label=\"Menú de navegación\"", source, StringComparison.Ordinal);
        Assert.Contains("onclick=\"this.closest('details')?.removeAttribute('open')\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileNavJs_ShouldImplementClickOutsideEscapeAndResizeDismissal()
    {
        var source = ReadSource(MobileNavJsPath);

        // Soporte de pointerdown y click para compatibilidad móvil/escritorio sin bloquear otras acciones
        Assert.Contains("document.addEventListener('pointerdown'", source, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('click'", source, StringComparison.Ordinal);

        // Soporte WCAG 2.2 AA de tecla Escape y retorno de foco
        Assert.Contains("e.key === 'Escape'", source, StringComparison.Ordinal);
        Assert.Contains("summary.focus()", source, StringComparison.Ordinal);

        // Cierre al redimensionar a resolución de escritorio y en popstate
        Assert.Contains("window.addEventListener('resize'", source, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener('popstate'", source, StringComparison.Ordinal);
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

        return dir?.FullName ?? throw new InvalidOperationException("No se pudo localizar la raíz de la solución.");
    }
}
