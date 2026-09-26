using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y accesibilidad para las pantallas dedicadas de cuenta:
/// Apariencia (/cuenta/apariencia), País (/cuenta/pais) y Privacidad (/cuenta/privacidad) (INC-62).
/// </summary>
public class AccountPreferencesContractTests
{
    private const string AppearancePath = "src/Ludeka.Web/Components/Pages/AccountAppearance.razor";
    private const string CountryPath = "src/Ludeka.Web/Components/Pages/AccountCountry.razor";
    private const string PrivacyPath = "src/Ludeka.Web/Components/Pages/AccountPrivacy.razor";
    private const string PublicProfilePath = "src/Ludeka.Web/Components/Pages/PublicProfile.razor";

    [Fact]
    public void AccountAppearance_ShouldDeclareRouteAndAuthorizeAndMountSectionNav()
    {
        var source = ReadSource(AppearancePath);

        Assert.Contains("@page \"/cuenta/apariencia\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.Contains("<AccountSectionNav Active=\"apariencia\" />", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeTheme", source, StringComparison.Ordinal);
        Assert.Contains("setLudekaTheme", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountCountry_ShouldDeclareRouteAndAuthorizeAndMountSectionNav()
    {
        var source = ReadSource(CountryPath);

        Assert.Contains("@page \"/cuenta/pais\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.Contains("<AccountSectionNav Active=\"pais\" />", source, StringComparison.Ordinal);
        Assert.Contains("CountryCatalog", source, StringComparison.Ordinal);
        Assert.Contains("detectUserCountry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPrivacy_ShouldDeclareRouteAndAuthorizeAndMountSectionNav()
    {
        var source = ReadSource(PrivacyPath);

        Assert.Contains("@page \"/cuenta/privacidad\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.Contains("<AccountSectionNav Active=\"privacidad\" />", source, StringComparison.Ordinal);
        Assert.Contains("IsPublicProfileHiddenAsync", source, StringComparison.Ordinal);
        Assert.Contains("SetPublicProfileHiddenAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPrivacy_ShouldDeclareLeaderboardOptInAnonymousAndPseudonymControls()
    {
        // INC-68: Ajustes de privacidad para gamificación, clasificaciones públicas y anonimato
        var source = ReadSource(PrivacyPath);

        // Inyecciones y dependencias
        Assert.Contains("ILeaderboardService LeaderboardService", source, StringComparison.Ordinal);
        Assert.Contains("PseudonymGenerator", source, StringComparison.Ordinal);

        // Controles de Opt-In y Anonimato
        Assert.Contains("leaderboard-optin-switch", source, StringComparison.Ordinal);
        Assert.Contains("ToggleLeaderboardOptInAsync", source, StringComparison.Ordinal);
        Assert.Contains("leaderboard-anon-switch", source, StringComparison.Ordinal);
        Assert.Contains("ToggleLeaderboardAnonymousAsync", source, StringComparison.Ordinal);

        // Control y validación de seudónimo personalizado
        Assert.Contains("custom-pseudonym-input", source, StringComparison.Ordinal);
        Assert.Contains("save-pseudonym-btn", source, StringComparison.Ordinal);
        Assert.Contains("SavePseudonymAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsValidCustomPseudonym", source, StringComparison.Ordinal);

        // Enlace a la página pública de clasificaciones
        Assert.Contains("href=\"/clasificaciones\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicProfile_ShouldGuardAgainstHiddenProfilesForVisitors()
    {
        var source = ReadSource(PublicProfilePath);

        Assert.Contains("IsPublicProfileHiddenAsync", source, StringComparison.Ordinal);
        Assert.Contains("_isPrivateProfile", source, StringComparison.Ordinal);
        Assert.Contains("Perfil privado", source, StringComparison.Ordinal);
        Assert.Contains("Tu perfil público está en modo PRIVADO", source, StringComparison.Ordinal);
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

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
