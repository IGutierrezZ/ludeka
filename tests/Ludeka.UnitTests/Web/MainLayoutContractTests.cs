using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para el Shell Global «Revista Lúdica» (INC-114).
/// Valida la estructura de MainLayout: cabecera reactiva de 72px teñida por sección,
/// utilidades de cabecera (/buscar, Sun/Moon, soporte, OfflineIndicator y AccountMenu),
/// montaje de StaffBar para roles de gestión y cumplimiento WCAG 2.2 AA.
/// </summary>
public class MainLayoutContractTests
{
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string StaffBarPath = "src/Ludeka.Web/Components/Shared/StaffBar.razor";

    [Fact]
    public void MainLayout_HeaderStructure_ShouldBe72pxWithReactiveSectionTinting()
    {
        var source = ReadSource(MainLayoutPath);

        // Cabecera compacta de 72px
        Assert.Contains("h-[72px]", source, StringComparison.Ordinal);
        Assert.Contains("max-w-[1280px]", source, StringComparison.Ordinal);

        // Lógica reactiva de colores por sección editorial
        Assert.Contains("GetHeaderColors()", source, StringComparison.Ordinal);
        Assert.Contains("HeaderColors.Bg", source, StringComparison.Ordinal);
        Assert.Contains("HeaderColors.Fg", source, StringComparison.Ordinal);
        Assert.Contains("HeaderColors.Accent", source, StringComparison.Ordinal);

        // Secciones contratadas en INC-114
        Assert.Contains("catalogo", source, StringComparison.Ordinal);
        Assert.Contains("sorteos", source, StringComparison.Ordinal);
        Assert.Contains("novedades", source, StringComparison.Ordinal);
        Assert.Contains("eventos", source, StringComparison.Ordinal);
        Assert.Contains("clasificaciones", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_Utilities_ShouldContainQuickSearchThemeToggleAndOfflineIndicator()
    {
        var source = ReadSource(MainLayoutPath);

        // 1. Acceso a búsqueda rápida /buscar con icono Lucide
        Assert.Contains("href=\"/buscar\"", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"search\"", source, StringComparison.Ordinal);

        // 2. Conmutador claro/oscuro (Sun/Moon)
        Assert.Contains("ToggleTheme", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"@(CurrentTheme == \"dark\" ? \"sun\" : \"moon\")\"", source, StringComparison.Ordinal);

        // 3. Montaje dual de OfflineIndicator (píldora en cabecera y banner reactivo)
        Assert.Contains("<OfflineIndicator AsBanner=\"false\" />", source, StringComparison.Ordinal);
        Assert.Contains("<OfflineIndicator AsBanner=\"true\" />", source, StringComparison.Ordinal);

        // 4. Montaje de menú de cuenta
        Assert.Contains("<AccountMenu />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_StaffBar_ShouldMountUnderHeaderAndExposeShortcuts()
    {
        var source = ReadSource(MainLayoutPath);

        // Montaje condicional o directo del componente de gestión staff bajo la cabecera
        Assert.Contains("<StaffBar />", source, StringComparison.Ordinal);

        // Contrato del componente StaffBar.razor
        var staffSource = ReadSource(StaffBarPath);
        Assert.Contains("CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole(\"Moderator\")", staffSource, StringComparison.Ordinal);
        Assert.Contains("role=\"region\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Barra de gestión staff\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("border-dashed border-[var(--mustard)]", staffSource, StringComparison.Ordinal);

        // Enlace al panel central de administración y atajos
        Assert.Contains("href=\"/admin\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("href=\"/moderacion/reportes\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/cola-catalogacion\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/afiliados\"", staffSource, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/ingesta-social\"", staffSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_AccessibilityLandmarks_ShouldSatisfyWcag22Aa()
    {
        var source = ReadSource(MainLayoutPath);

        // Skip-link accesible al contenido principal
        Assert.Contains("href=\"#main-content\"", source, StringComparison.Ordinal);
        Assert.Contains("Saltar al contenido principal", source, StringComparison.Ordinal);

        // Landmark de contenido principal etiquetado y enfocable
        Assert.Contains("<main class=\"flex-1\" id=\"main-content\" tabindex=\"-1\">", source, StringComparison.Ordinal);

        // Landmark semántico de cabecera y navegación
        Assert.Contains("<header", source, StringComparison.Ordinal);
        Assert.Contains("<nav class=\"hidden lg:flex items-center gap-5 text-sm font-extrabold flex-1\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Navegación principal\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_DesktopNavigation_ShouldContainPrimaryDestinationsAndMoreMenu()
    {
        var source = ReadSource(MainLayoutPath);

        // Destinos principales de escritorio
        Assert.Contains("href=\"/sorteos\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/novedades\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/eventos\"", source, StringComparison.Ordinal);

        // Desplegable «Más ▾» con destinos secundarios
        Assert.Contains("ToggleMore", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/editoriales\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/creadores\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/tiendas\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/transparencia\"", source, StringComparison.Ordinal);
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
