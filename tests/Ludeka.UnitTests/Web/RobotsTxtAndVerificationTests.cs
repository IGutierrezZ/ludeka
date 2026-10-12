using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class RobotsTxtAndVerificationTests
{
    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Ludeka.sln")) || Directory.Exists(Path.Combine(current, "src", "Ludeka.Web")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        throw new InvalidOperationException("No se pudo localizar la raíz del repositorio.");
    }

    [Fact]
    public void RobotsTxt_ShouldExistAndDeclareCorrectDirectives()
    {
        var repoRoot = FindRepositoryRoot();
        var robotsPath = Path.Combine(repoRoot, "src", "Ludeka.Web", "wwwroot", "robots.txt");

        Assert.True(File.Exists(robotsPath), $"El archivo {robotsPath} no existe.");

        var content = File.ReadAllText(robotsPath);
        Assert.Contains("User-agent: *", content);
        Assert.Contains("Allow: /", content);
        Assert.Contains("Disallow: /admin", content);
        Assert.Contains("Disallow: /cuenta/", content);
        Assert.Contains("Disallow: /login", content);
        Assert.Contains("Sitemap: https://ludeka.es/sitemap.xml", content);
    }

    [Fact]
    public void AppRazor_ShouldDeclareGoogleSiteVerificationAndSeoMetaTags()
    {
        var repoRoot = FindRepositoryRoot();
        var appRazorPath = Path.Combine(repoRoot, "src", "Ludeka.Web", "Components", "App.razor");

        Assert.True(File.Exists(appRazorPath), $"El archivo {appRazorPath} no existe.");

        var content = File.ReadAllText(appRazorPath);
        Assert.Contains("google-site-verification", content);
        Assert.Contains("wr4JpkGdk8SvvoxnHOeGBuT-OvzoEmngGbKCzT-PnCU", content);
        Assert.Contains("<meta name=\"description\"", content);
        Assert.Contains("og:site_name", content);
        Assert.Contains("index, follow", content);
    }
}
