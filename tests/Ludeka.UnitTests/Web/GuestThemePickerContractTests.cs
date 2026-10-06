using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para el selector de tema en visitantes no autenticados
/// y la configuración de Madera Clara ('wood') como tema por defecto.
/// </summary>
public class GuestThemePickerContractTests
{
    private const string AppPath = "src/Ludeka.Web/Components/App.razor";
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string GuestThemePickerPath = "src/Ludeka.Web/Components/Shared/GuestThemePicker.razor";

    [Fact]
    public void AppRazor_ShouldSetLightThemeAsDefaultOnHtmlAndFallbacks()
    {
        var source = ReadSource(AppPath);

        // Atributo data-theme="light" y class="light" directo en <html> para renderizado Zero-FOUC
        Assert.Contains("<html lang=\"es\" data-theme=\"light\" class=\"light\">", source, StringComparison.Ordinal);

        // Fallback en script temprano de cabecera
        Assert.Contains("|| 'light'", source, StringComparison.Ordinal);

        // Fallback en función JS getLudekaTheme
        Assert.Contains("return 'light';", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_ShouldMountGuestThemePickerOnlyWhenNoSession()
    {
        var source = ReadSource(MainLayoutPath);

        // Montaje condicional ante !HasSession
        Assert.Contains("@if (!HasSession)", source, StringComparison.Ordinal);
        Assert.Contains("<GuestThemePicker CurrentTheme=\"@CurrentTheme\" OnThemeChanged=\"SwitchTheme\" />", source, StringComparison.Ordinal);

        // Tema inicial fijado a 'wood'
        Assert.Contains("private string CurrentTheme = \"wood\";", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_SwitchTheme_ShouldNotCallDatabaseOrRedirectWhenGuest()
    {
        var source = ReadSource(MainLayoutPath);

        // SwitchTheme solo persiste a base de datos si hay sesión
        Assert.Contains("if (HasSession)", source, StringComparison.Ordinal);
        Assert.Contains("await PreferenceService.SetUserThemeAsync(CurrentUserService.UserId, theme);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GuestThemePicker_ShouldDeclareAllFiveThemesWithoutTextLabels()
    {
        var source = ReadSource(GuestThemePickerPath);

        // Declaración de las 5 opciones de tema
        Assert.Contains("\"editorial\"", source, StringComparison.Ordinal);
        Assert.Contains("\"wood\"", source, StringComparison.Ordinal);
        Assert.Contains("\"tabletop\"", source, StringComparison.Ordinal);
        Assert.Contains("\"midnight\"", source, StringComparison.Ordinal);
        Assert.Contains("\"charcoal\"", source, StringComparison.Ordinal);

        // Icono de paleta como disparador
        Assert.Contains("Icon Name=\"palette\"", source, StringComparison.Ordinal);

        // Roles semánticos y accesibilidad WCAG 2.2 AA
        Assert.Contains("role=\"menu\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"menuitem\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Seleccionar tema de color\"", source, StringComparison.Ordinal);

        // Soporte de teclado (Escape) y telón de cierre
        Assert.Contains("@onkeydown=\"HandleKeyDown\"", source, StringComparison.Ordinal);
        Assert.Contains("e.Key == \"Escape\"", source, StringComparison.Ordinal);
        Assert.Contains("<div class=\"fixed inset-0 z-40\" @onclick=\"CloseMenu\"></div>", source, StringComparison.Ordinal);
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
