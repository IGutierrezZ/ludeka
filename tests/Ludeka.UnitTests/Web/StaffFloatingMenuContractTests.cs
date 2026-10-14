using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para el Menú Flotante Global de Moderación y Staff (INC-146).
/// Valida:
/// 1. MainLayout ya no renderiza StaffBar fija superior y monta StaffFloatingMenu.
/// 2. StaffFloatingMenu contiene el botón flotante (FAB) posicionado a bottom-20 con área táctil accesible y Lucide zap.
/// 3. StaffFloatingMenu expone diálogo accesible (role="dialog", aria-label="Panel rápido de moderación y gestión", Escape, etc.).
/// 4. StaffFloatingMenu incluye el enlace permanente a /admin («Panel de gestión»).
/// 5. StaffFloatingMenu provee soporte tanto para acciones dinámicas vía IStaffActionService como fallback de ruta.
/// </summary>
public class StaffFloatingMenuContractTests
{
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string StaffFloatingMenuPath = "src/Ludeka.Web/Components/Shared/StaffFloatingMenu.razor";
    private const string GameDetailPath = "src/Ludeka.Web/Components/Pages/GameDetail.razor";

    [Fact]
    public void MainLayout_ShouldMountStaffFloatingMenu_AndNotContainStaffBar()
    {
        var source = ReadSource(MainLayoutPath);

        // Ya no debe contener la antigua barra fija superior
        Assert.DoesNotContain("<StaffBar />", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<StaffActionBar />", source, StringComparison.Ordinal);

        // Debe montar el componente global flotante
        Assert.Contains("<StaffFloatingMenu />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffFloatingMenu_Structure_ShouldComplyWithResponsiveAndAccessibilityRequirements()
    {
        var source = ReadSource(StaffFloatingMenuPath);

        // Guarda de rol staff
        Assert.Contains("CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole(\"Moderator\")", source, StringComparison.Ordinal);

        // Botón FAB flotante elevado para no colisionar con MobileBottomNav (bottom-20)
        Assert.Contains("fixed bottom-20 right-4", source, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"zap\"", source, StringComparison.Ordinal);

        // Diálogo accesible WCAG 2.2 AA
        Assert.Contains("role=\"dialog\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Panel rápido de moderación y gestión\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", source, StringComparison.Ordinal);

        // Enlace canónico permanente al panel de administración
        Assert.Contains("href=\"/admin\"", source, StringComparison.Ordinal);
        Assert.Contains("Panel de gestión", source, StringComparison.Ordinal);

        // Fallbacks canónicos de ruta cuando no hay acciones personalizadas
        Assert.Contains("/admin/cola-catalogacion", source, StringComparison.Ordinal);
        Assert.Contains("/admin/ingesta-social", source, StringComparison.Ordinal);
        Assert.Contains("/admin/eventos", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldNotContainRedundantStaffPanels_AndShouldRegisterActions()
    {
        var source = ReadSource(GameDetailPath);

        // Retirada del panel flotante local redundante
        Assert.DoesNotContain("<GameStaffToolsPanel", source, StringComparison.Ordinal);

        // Retirada del botón BGG redundante junto a la foto hero
        Assert.DoesNotContain("title=\"Sincronizar datos y versiones desde BoardGameGeek\"", source, StringComparison.Ordinal);

        // Retirada de la caja mostaza incrustada en veredicto
        Assert.DoesNotContain("border-2 border-dashed border-[var(--mustard)] mt-2", source, StringComparison.Ordinal);

        // Retirada de botones incrustados en la pestaña multimedia
        Assert.DoesNotContain("Hub Multimedia (Moderar)", source, StringComparison.Ordinal);

        // Registro de acciones en IStaffActionService con notificación reactiva StateHasChanged()
        Assert.Contains("StaffActionService.SetCustomActions", source, StringComparison.Ordinal);
        Assert.Contains("Editar Ficha\", \"pen-line\", () => { _isEditorModalOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
        Assert.Contains("Buscar en YouTube\", \"tv\", () => { _isYouTubeSearchModalOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
        Assert.Contains("Ingesta Flash Vídeo\", \"zap\", () => { _isFlashIngestOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
        Assert.Contains("Asociar BGG\", \"globe\", () => { _isAssociateBggModalOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
        Assert.Contains("Veredicto Fundador\", \"award\", () => { _isFoundingModalOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
        Assert.Contains("Incidencias / Erratas\", \"flag\", () => { _isReportModalOpen = true; StateHasChanged(); }", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/CreatorDetail.razor", "_isEditModalOpen = true; StateHasChanged();")]
    [InlineData("src/Ludeka.Web/Components/Pages/PublisherDetail.razor", "_isEditModalOpen = true; StateHasChanged();")]
    [InlineData("src/Ludeka.Web/Components/Pages/StoreDetail.razor", "_isEditModalOpen = true; StateHasChanged();")]
    [InlineData("src/Ludeka.Web/Components/Pages/ReleaseDetail.razor", "_isDeleteConfirmOpen = true; StateHasChanged();")]
    public void DetailPages_ShouldTriggerStateHasChanged_WhenOpeningStaffModals(string relativePath, string expectedPattern)
    {
        var source = ReadSource(relativePath);
        Assert.Contains(expectedPattern, source, StringComparison.Ordinal);
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
