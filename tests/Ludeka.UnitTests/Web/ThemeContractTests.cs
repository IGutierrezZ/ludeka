using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para los tokens semánticos de diseño editorial, la paleta Terracota/Salvia,
/// la tipografía Plus Jakarta Sans y el par canónico de temas Claro/Oscuro (INC-113).
/// </summary>
public class ThemeContractTests
{
    private const string InputCssPath = "src/Ludeka.Web/Styles/input.css";
    private const string AppCssPath = "src/Ludeka.Web/wwwroot/app.css";
    private const string AppRazorPath = "src/Ludeka.Web/Components/App.razor";
    private const string TailwindConfigPath = "src/Ludeka.Web/tailwind.config.js";

    [Fact]
    public void InputCss_ShouldContainEditorialPaletteAndSemanticTokens()
    {
        var css = ReadSource(InputCssPath);

        // Paleta Editorial Principal (INC-113)
        Assert.Contains("--color-terracota: #C85A32;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-salvia: #2D6A4F;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-arena: #F4EEDB;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-arena-light: #FAF6EE;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-tinta: #1A1A1A;", css, StringComparison.OrdinalIgnoreCase);

        // Estados Semánticos
        Assert.Contains("--semantic-verde: #2D6A4F;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--semantic-ambar: #C97A1E;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--semantic-carmin: #9E2A2B;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-semantic-verde: #2D6A4F;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-semantic-ambar: #C97A1E;", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--color-semantic-carmin: #9E2A2B;", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InputCss_ShouldDefineLightAndDarkCanonicalSelectors()
    {
        var css = ReadSource(InputCssPath);

        // Selectores canónicos de modo claro
        Assert.Contains(":root,", css, StringComparison.Ordinal);
        Assert.Contains(":root.light,", css, StringComparison.Ordinal);
        Assert.Contains("[data-theme=\"light\"]", css, StringComparison.Ordinal);

        // Selectores canónicos de modo oscuro
        Assert.Contains(":root.dark,", css, StringComparison.Ordinal);
        Assert.Contains("[data-theme=\"dark\"],", css, StringComparison.Ordinal);
        Assert.Contains("html.dark", css, StringComparison.Ordinal);
    }

    [Fact]
    public void InputCss_ShouldDefineShadowsAndEditorialUtilities()
    {
        var css = ReadSource(InputCssPath);

        // Tokens de sombra
        Assert.Contains("--shadow-card:", css, StringComparison.Ordinal);
        Assert.Contains("--shadow-polaroid:", css, StringComparison.Ordinal);
        Assert.Contains("--shadow-sticker:", css, StringComparison.Ordinal);

        // Utilidades editoriales
        Assert.Contains(".tracking-tight-display", css, StringComparison.Ordinal);
        Assert.Contains("letter-spacing: -0.055em;", css, StringComparison.Ordinal);
        Assert.Contains(".leading-tight-display", css, StringComparison.Ordinal);
        Assert.Contains("line-height: 0.88;", css, StringComparison.Ordinal);
        Assert.Contains(".rule-editorial", css, StringComparison.Ordinal);
        Assert.Contains("border-bottom: 3px solid var(--rule);", css, StringComparison.Ordinal);
        Assert.Contains(".safe-rotations", css, StringComparison.Ordinal);
        Assert.Contains("will-change: transform;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AppCss_CompiledOutput_ShouldContainCanonicalTokensAndClasses()
    {
        var css = ReadSource(AppCssPath);

        // Colores y tokens en el CSS compilado
        Assert.Contains("#c85a32", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#2d6a4f", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#f4eedb", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#1a1a1a", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#c97a1e", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#9e2a2b", css, StringComparison.OrdinalIgnoreCase);

        // Fuentes y utilidades
        Assert.Contains("Plus Jakarta Sans", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rule-editorial", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tracking-tight-display", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("leading-tight-display", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("safe-rotations", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppRazor_ShouldIncludePlusJakartaFontWithPreconnectAndZeroFoucScript()
    {
        var razor = ReadSource(AppRazorPath);

        // Preconexiones a Google Fonts
        Assert.Contains("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\" />", razor, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin />", razor, StringComparison.Ordinal);

        // Fuente Plus Jakarta Sans
        Assert.Contains("family=Plus+Jakarta+Sans:ital,wght@0,400;0,500;0,600;0,700;0,800;1,800", razor, StringComparison.Ordinal);

        // Raíz HTML con clase y tema por defecto
        Assert.Contains("<html lang=\"es\" data-theme=\"light\" class=\"light\">", razor, StringComparison.Ordinal);

        // Meta theme-color con Terracota
        Assert.Contains("<meta name=\"theme-color\" content=\"#C85A32\" />", razor, StringComparison.Ordinal);

        // Zero-FOUC con normalización determinista
        Assert.Contains("document.documentElement.setAttribute('data-theme', theme);", razor, StringComparison.Ordinal);
        Assert.Contains("document.documentElement.classList.add('dark');", razor, StringComparison.Ordinal);
        Assert.Contains("document.documentElement.classList.add('light');", razor, StringComparison.Ordinal);

        // Funciones JS globales
        Assert.Contains("window.setLudekaTheme", razor, StringComparison.Ordinal);
        Assert.Contains("window.getLudekaTheme", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void TailwindConfig_ShouldDefinePlusJakartaSansAndSemanticColorTokens()
    {
        var config = ReadSource(TailwindConfigPath);

        // Tipografía sans
        Assert.Contains("Plus Jakarta Sans", config, StringComparison.Ordinal);

        // Tokens semánticos de la paleta editorial
        Assert.Contains("terracota:", config, StringComparison.Ordinal);
        Assert.Contains("salvia:", config, StringComparison.Ordinal);
        Assert.Contains("arena:", config, StringComparison.Ordinal);
        Assert.Contains("tinta:", config, StringComparison.Ordinal);
        Assert.Contains("paper:", config, StringComparison.Ordinal);
        Assert.Contains("card:", config, StringComparison.Ordinal);
        Assert.Contains("brand:", config, StringComparison.Ordinal);
        Assert.Contains("mustard:", config, StringComparison.Ordinal);
        Assert.Contains("green:", config, StringComparison.Ordinal);
        Assert.Contains("'blue-band':", config, StringComparison.Ordinal);
        Assert.Contains("plum:", config, StringComparison.Ordinal);
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
