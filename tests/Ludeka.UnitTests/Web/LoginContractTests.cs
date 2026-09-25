using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para la vista de acceso (INC-64):
/// Verifica que la página de Login elimine la contradicción histórica sobre "sin correo de confirmación",
/// ofrezca el acceso interactivo con enlace mágico (Magic Link) y mantenga la integración con proveedores sociales.
/// </summary>
public class LoginContractTests
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

    private static string ReadLoginRazor()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Pages", "Login.razor");
        Assert.True(File.Exists(path), $"No se encontró Login.razor en {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void LoginRazor_DoesNotContainContradictoryNoEmailClaim()
    {
        var source = ReadLoginRazor();

        Assert.DoesNotContain("sin registro, sin contraseña y sin correo de confirmación", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sin correo de confirmación", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoginRazor_ContainsMagicLinkFormAndEmailInput()
    {
        var source = ReadLoginRazor();

        Assert.Contains("IMagicLinkService", source);
        Assert.Contains("id=\"magic-link-email\"", source);
        Assert.Contains("type=\"email\"", source);
        Assert.Contains("HandleMagicLinkSubmitAsync", source);
        Assert.Contains("Acceso directo con enlace mágico", source);
    }

    [Fact]
    public void LoginRazor_PreservesSocialAuthenticationProviders()
    {
        var source = ReadLoginRazor();

        Assert.Contains("ExternalAuthenticationSchemes.GetEnabledProviders", source);
        Assert.Contains("action=\"/login/external\"", source);
        Assert.Contains("data-enhance=\"false\"", source);
        Assert.Contains("Continuar con", source);
    }

    [Fact]
    public void LoginRazor_HandlesClosedAvisoQueryParameters()
    {
        var source = ReadLoginRazor();

        Assert.Contains("[SupplyParameterFromQuery(Name = \"aviso\")]", source);
        Assert.Contains("LoginRedirect.ResolveAccountCollisionNotice(Aviso)", source);
    }
}
