using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para favicon, iconos de marca del navegador y activos PWA.
/// Garantiza que no haya iconos por defecto en ningún navegador ni dispositivo.
/// </summary>
public class FaviconAndBrandIconsContractTests
{
    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.slnx")) && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void AppRazor_ContainsFaviconAndTouchIconLinks()
    {
        string appRazorPath = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "App.razor");
        Assert.True(File.Exists(appRazorPath), "No se encontró App.razor");

        string content = File.ReadAllText(appRazorPath);

        Assert.Contains("<link rel=\"icon\" type=\"image/svg+xml\" href=\"/favicon.svg\" />", content);
        Assert.Contains("<link rel=\"icon\" type=\"image/png\" sizes=\"32x32\" href=\"/favicon-32x32.png\" />", content);
        Assert.Contains("<link rel=\"shortcut icon\" href=\"/favicon.ico\" />", content);
        Assert.Contains("<link rel=\"apple-touch-icon\" sizes=\"180x180\" href=\"/apple-touch-icon.png\" />", content);
    }

    [Theory]
    [InlineData("favicon.ico")]
    [InlineData("favicon.svg")]
    [InlineData("favicon-16x16.png")]
    [InlineData("favicon-32x32.png")]
    [InlineData("favicon-48x48.png")]
    [InlineData("apple-touch-icon.png")]
    [InlineData("icons/icon-192.png")]
    [InlineData("icons/icon-512.png")]
    [InlineData("icons/icon-maskable.png")]
    public void Wwwroot_ContainsOfficialBrandIcons(string relativeIconPath)
    {
        string fullPath = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "wwwroot", relativeIconPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"El activo de icono no existe: {relativeIconPath}");
        var fileInfo = new FileInfo(fullPath);
        Assert.True(fileInfo.Length > 0, $"El archivo de icono está vacío: {relativeIconPath}");
    }

    [Fact]
    public void ServiceWorker_PrecacheIncludesFaviconAssets()
    {
        string swPath = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "wwwroot", "service-worker.js");
        Assert.True(File.Exists(swPath), "No se encontró service-worker.js");

        string content = File.ReadAllText(swPath);
        Assert.Contains("'/favicon.ico'", content);
        Assert.Contains("'/favicon.svg'", content);
    }

    [Fact]
    public void WebManifest_IncludesStandardAndMaskableIcons()
    {
        string manifestPath = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "wwwroot", "manifest.webmanifest");
        Assert.True(File.Exists(manifestPath), "No se encontró manifest.webmanifest");

        string content = File.ReadAllText(manifestPath);
        Assert.Contains("/icons/icon-192.png", content);
        Assert.Contains("/icons/icon-512.png", content);
        Assert.Contains("maskable", content);
    }
}
