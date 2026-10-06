using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para el Backoffice Editorial «Ludeka Gestión» (INC-119).
/// Valida la estructura de AdminLayout: cabecera compacta de 56px con badge "Gestión",
/// barra lateral con las 11 bandejas operativas y atajos de teclado (⌘K / 1-9),
/// control de acceso para staff y vinculación en todas las páginas administrativas.
/// </summary>
public class AdminLayoutContractTests
{
    private const string AdminLayoutPath = "src/Ludeka.Web/Components/Layout/AdminLayout.razor";
    private const string AdminDashboardPath = "src/Ludeka.Web/Components/Pages/AdminDashboard.razor";

    public static TheoryData<string, string> AdminPages => new()
    {
        { "SocialInboxModeration.razor", "/admin/ingesta-social" },
        { "GameReportsModeration.razor", "/moderacion/reportes" },
        { "MediaModeration.razor", "/moderacion-media" },
        { "CatalogQueueAdmin.razor", "/admin/cola-catalogacion" },
        { "EventsManagement.razor", "/admin/eventos" },
        { "MonitoredAccountsDirectory.razor", "/admin/canales-monitorizados" },
        { "AffiliatesAdmin.razor", "/admin/afiliados" },
        { "InstagramModeration.razor", "/admin/instagram" },
        { "AdminNotifications.razor", "/admin/notificaciones" },
        { "UserManagement.razor", "/admin/usuarios" },
        { "AuditLogViewer.razor", "/admin/auditoria" },
    };

    [Fact]
    public void AdminLayout_HeaderStructure_ShouldBe56pxWithGestiónBadgeAndShortcuts()
    {
        var source = ReadSource(AdminLayoutPath);

        // Cabecera compacta de 56px
        Assert.Contains("h-[56px]", source, StringComparison.Ordinal);
        Assert.Contains("Gestión", source, StringComparison.Ordinal);

        // Botón de buscador global con ⌘K / Ctrl+K
        Assert.Contains("OpenQuickSearch", source, StringComparison.Ordinal);
        Assert.Contains("Ctrl+K", source, StringComparison.Ordinal);

        // Enlace para volver a la web pública
        Assert.Contains("Volver a la web", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminLayout_Sidebar_ShouldExpose11OperationalTraysAndShortcuts()
    {
        var source = ReadSource(AdminLayoutPath);

        // Ancho de 248px en escritorio
        Assert.Contains("w-[248px]", source, StringComparison.Ordinal);

        // 11 Bandejas contratadas
        Assert.Contains("/admin/cola-catalogacion", source, StringComparison.Ordinal);
        Assert.Contains("/moderacion/reportes", source, StringComparison.Ordinal);
        Assert.Contains("/moderacion/multimedia", source, StringComparison.Ordinal);
        Assert.Contains("/admin/ingesta-social", source, StringComparison.Ordinal);
        Assert.Contains("/admin/eventos", source, StringComparison.Ordinal);
        Assert.Contains("/admin/instagram", source, StringComparison.Ordinal);
        Assert.Contains("/admin/afiliados", source, StringComparison.Ordinal);
        Assert.Contains("/admin/canales-monitorizados", source, StringComparison.Ordinal);
        Assert.Contains("/admin/usuarios", source, StringComparison.Ordinal);
        Assert.Contains("/admin/auditoria", source, StringComparison.Ordinal);
        Assert.Contains("/admin/notificaciones", source, StringComparison.Ordinal);

        // Atajos de teclado 1-9
        Assert.Contains("OnGlobalShortcutTriggered", source, StringComparison.Ordinal);
        Assert.Contains("NavigateByShortcut", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminLayout_Security_ShouldContainRestrictedAccessGuard()
    {
        var source = ReadSource(AdminLayoutPath);

        // Guardia de personal editorial
        Assert.Contains("IsStaff", source, StringComparison.Ordinal);
        Assert.Contains("Acceso Restringido", source, StringComparison.Ordinal);
        Assert.Contains("Mesa Fundadora", source, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AdminPages))]
    public void AdminPages_ShouldDeclareAdminLayout(string pageFile, string expectedRoute)
    {
        var source = ReadSource($"src/Ludeka.Web/Components/Pages/{pageFile}");

        Assert.Contains($"@page \"{expectedRoute}\"", source, StringComparison.Ordinal);
        Assert.Contains("@layout AdminLayout", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboard_ShouldDeclareRoutesAndMetrics()
    {
        var source = ReadSource(AdminDashboardPath);

        Assert.Contains("@page \"/admin\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/admin/resumen\"", source, StringComparison.Ordinal);
        Assert.Contains("@layout AdminLayout", source, StringComparison.Ordinal);
        Assert.Contains("Resumen de Operaciones", source, StringComparison.Ordinal);
        Assert.Contains("Estado de los Sistemas", source, StringComparison.Ordinal);
        Assert.Contains("Actividad Editorial Reciente", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var root = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(root) && !File.Exists(Path.Combine(root, "Ludeka.sln")))
        {
            var parent = Directory.GetParent(root);
            root = parent?.FullName;
        }

        if (string.IsNullOrEmpty(root))
        {
            throw new InvalidOperationException("No se encontró la raíz del repositorio.");
        }

        var fullPath = Path.Combine(root, relativePath);
        Assert.True(File.Exists(fullPath), $"El archivo {relativePath} no existe en {fullPath}.");
        return File.ReadAllText(fullPath);
    }
}
