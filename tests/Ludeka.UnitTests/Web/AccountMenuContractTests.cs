using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para el menú de cuenta y estado de sesión en cabecera (INC-61).
/// Valida el comportamiento para visitantes e identificados, los 6 destinos canónicos,
/// el cierre de sesión limpio vía /logout, el soporte WCAG 2.2 AA y la composición con avisos de correo.
/// </summary>
public class AccountMenuContractTests
{
    private const string AccountMenuPath = "src/Ludeka.Web/Components/Shared/AccountMenu.razor";
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";

    [Fact]
    public void MainLayout_ShouldMountAccountMenuInHeaderUtilities()
    {
        var source = ReadSource(MainLayoutPath);

        Assert.Contains("<AccountMenu />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountMenu_GuestContract_ShouldRenderLoginLinkWithUserIconAndText()
    {
        var source = ReadSource(AccountMenuPath);

        // Sin sesión: enlace accesible a /login con texto "Entrar"
        Assert.Contains("href=\"/login\"", source, StringComparison.Ordinal);
        Assert.Contains("title=\"Entrar\"", source, StringComparison.Ordinal);
        Assert.Contains(">Entrar<", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"user\"", source, StringComparison.Ordinal);
        Assert.Contains("min-h-[24px]", source, StringComparison.Ordinal);
        Assert.Contains("min-w-[24px]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountMenu_AuthenticatedTriggerContract_ShouldRenderMustardInitialAvatarAndEditorialDropdown()
    {
        var source = ReadSource(AccountMenuPath);

        // Con sesión: botón disparador con atributos ARIA y semántica
        Assert.Contains("aria-expanded=\"@_isOpen\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-haspopup=\"menu\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"@($\"Menú de cuenta de {CurrentUserService.UserName}\")\"", source, StringComparison.Ordinal);

        // Avatar en píldora con inicial mostaza (INC-114, Revista Lúdica)
        Assert.Contains("@UserInitial", source, StringComparison.Ordinal);
        Assert.Contains("bg-[var(--mustard)]", source, StringComparison.Ordinal);
        Assert.Contains("w-[290px]", source, StringComparison.Ordinal);
        Assert.Contains("rounded-[22px]", source, StringComparison.Ordinal);
        Assert.Contains("border-2 border-[var(--rule)]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Icon Name=\"chevron-down\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountMenu_DropdownDestinationsContract_ShouldExposeAllCanonicalEndpoints()
    {
        var source = ReadSource(AccountMenuPath);

        // 1. Perfil Público
        Assert.Contains("href=\"@PublicProfileUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("Mi Perfil Público", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"user\"", source, StringComparison.Ordinal);

        // 2. Mi Ludoteca
        Assert.Contains("href=\"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
        Assert.Contains("Mi Ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"library\"", source, StringComparison.Ordinal);

        // 3. Apariencia & Tema (Pantalla dedicada INC-62)
        Assert.Contains("href=\"/cuenta/apariencia\"", source, StringComparison.Ordinal);
        Assert.Contains("Apariencia & Tema", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"palette\"", source, StringComparison.Ordinal);

        // 4. Ubicación & País (Pantalla dedicada INC-62)
        Assert.Contains("href=\"/cuenta/pais\"", source, StringComparison.Ordinal);
        Assert.Contains("Ubicación & País", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"globe\"", source, StringComparison.Ordinal);

        // 5. Privacidad (Pantalla dedicada INC-62)
        Assert.Contains("href=\"/cuenta/privacidad\"", source, StringComparison.Ordinal);
        Assert.Contains("Privacidad", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"shield\"", source, StringComparison.Ordinal);

        // 6. Conexiones OAuth
        Assert.Contains("href=\"/cuenta/conexiones\"", source, StringComparison.Ordinal);
        Assert.Contains("Conexiones OAuth", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"link\"", source, StringComparison.Ordinal);

        // 7. Cierre de Sesión Limpio
        Assert.Contains("href=\"/logout\"", source, StringComparison.Ordinal);
        Assert.Contains("Cerrar Sesión", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"log-out\"", source, StringComparison.Ordinal);

        // No debe contener el enlace redundante a Área de Cuenta dentro del desplegable
        Assert.DoesNotContain(">Área de Cuenta<", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountMenu_AccessibilityAndDismissContract_ShouldSupportEscapeBackdropAndLocationChanged()
    {
        var source = ReadSource(AccountMenuPath);

        // Telón de fondo invisible para descarte al hacer clic fuera
        Assert.Contains("<div class=\"fixed inset-0 z-40\" @onclick=\"CloseMenu\"></div>", source, StringComparison.Ordinal);

        // Soporte de roles semánticos WCAG 2.2 AA
        Assert.Contains("role=\"menu\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"menuitem\"", source, StringComparison.Ordinal);

        // Manejo de teclado (Escape)
        Assert.Contains("@onkeydown=\"HandleKeyDown\"", source, StringComparison.Ordinal);
        Assert.Contains("e.Key == \"Escape\"", source, StringComparison.Ordinal);

        // Descarte reactivo ante cambios de navegación
        Assert.Contains("Navigation.LocationChanged += HandleLocationChanged;", source, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged -= HandleLocationChanged;", source, StringComparison.Ordinal);
        Assert.Contains("@implements IDisposable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountMenu_EmailVerificationNoticeContract_ShouldObserveConnectionsServiceInvalidation()
    {
        var source = ReadSource(AccountMenuPath);

        // Detección reactiva de correo no verificado mediante IAccountConnectionsService
        Assert.Contains("ConnectionsService.HasVerifiedProviderEmailAsync()", source, StringComparison.Ordinal);
        Assert.Contains("ConnectionsService.Invalidated += HandleConnectionsInvalidated;", source, StringComparison.Ordinal);
        Assert.Contains("ConnectionsService.Invalidated -= HandleConnectionsInvalidated;", source, StringComparison.Ordinal);
        Assert.Contains("bg-amber-500", source, StringComparison.Ordinal);
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
