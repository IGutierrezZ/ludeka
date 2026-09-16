using System;
using System.IO;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de las escrituras con identidad en la interfaz (INC-46, F4): cada superficie que escribe
/// con identidad traduce la denegación por falta de sesión en una redirección al acceso, y Mi
/// Ludoteca no construye enlaces de perfil con una identidad vacía.
/// </summary>
public class SessionDenialUiContractTests
{
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

    private static string ReadComponent(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el componente {relativePath} en {path}");
        return File.ReadAllText(path);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/GameDetail.razor")]
    [InlineData("src/Ludeka.Web/Components/Pages/MyLibrary.razor")]
    [InlineData("src/Ludeka.Web/Components/Shared/RuleQuestionsSection.razor")]
    [InlineData("src/Ludeka.Web/Components/Shared/GameReportModal.razor")]
    [InlineData("src/Ludeka.Web/Components/Shared/BggImportModal.razor")]
    [InlineData("src/Ludeka.Web/Components/Shared/BggSearchModal.razor")]
    [InlineData("src/Ludeka.Web/Components/Shared/LocationSelectorModal.razor")]
    [InlineData("src/Ludeka.Web/Components/Layout/MainLayout.razor")]
    public void EveryIdentityWriter_TranslatesTheSessionDenialIntoALoginRedirect(string relativePath)
    {
        var source = ReadComponent(relativePath);

        Assert.Contains("TryRedirectToLogin", source);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/MyLibrary.razor")]
    public void MyLibrary_InvitesAnonymousVisitorsToLogin(string relativePath)
    {
        var source = ReadComponent(relativePath);

        Assert.Contains("ExternalAuthenticationSchemes.LoginPath", source);
        Assert.Contains("Inicia sesión", source);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/MyLibrary.razor")]
    public void MyLibrary_NeverBuildsAProfileLinkWithAnEmptyIdentity(string relativePath)
    {
        var source = ReadComponent(relativePath);

        Assert.DoesNotContain("href=\"/u/@CurrentUserService.UserId\"", source);
        Assert.DoesNotContain("/u/{CurrentUserService.UserId}", source);
        Assert.DoesNotContain("/u/\", CurrentUserService.UserId", source);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Shared/CollectionActionBar.razor")]
    public void CollectionActionBar_InvitesAnonymousVisitorsToLoginInsteadOfCollecting(string relativePath)
    {
        var source = ReadComponent(relativePath);

        Assert.Contains("ExternalAuthenticationSchemes.LoginPath", source);
        Assert.Contains("Inicia sesión", source);
    }
}
