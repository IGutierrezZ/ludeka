using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de fuente del área de cuenta (INC-50, diseño §D4.4). Igual que
/// <c>AccountConnectionsPageContractTests</c> y <c>AuthorizationPipelineContractTests</c>, se lee
/// el código fuente como texto (el repo no tiene bUnit) con marcadores sin posiciones de línea.
/// </summary>
public class AccountAreaContractTests
{
    private const string HubPath = "src/Ludeka.Web/Components/Pages/Account.razor";
    private const string LayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string MyLibraryPath = "src/Ludeka.Web/Components/Pages/MyLibrary.razor";
    private const string ConnectionsPath = "src/Ludeka.Web/Components/Pages/AccountConnections.razor";
    private const string AppearancePath = "src/Ludeka.Web/Components/Pages/AccountAppearance.razor";
    private const string CountryPath = "src/Ludeka.Web/Components/Pages/AccountCountry.razor";
    private const string PrivacyPath = "src/Ludeka.Web/Components/Pages/AccountPrivacy.razor";

    [Fact]
    public void Hub_ShouldDeclareItsRouteAndPlainAuthorize()
    {
        // INC-50, diseño §D1/D4.1: el hub raíz estrena la ruta /cuenta con [Authorize] plano
        // (patrón AccountConnections.razor), sin política granular — basta con sesión iniciada.
        var source = ReadSource(HubPath);

        Assert.Contains("@page \"/cuenta\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[Authorize(Policy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_ShouldInventoryAccountSections()
    {
        // INC-50 e INC-62: inventario de entradas del área de cuenta hacia sus rutas dedicadas
        var source = ReadSource(HubPath);

        Assert.Contains("href=\"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/cuenta/conexiones\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/cuenta/apariencia\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/cuenta/pais\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/cuenta/privacidad\"", source, StringComparison.Ordinal);
        Assert.Contains("Ludoteca", source, StringComparison.Ordinal);
        Assert.Contains("Conexiones", source, StringComparison.Ordinal);
        Assert.Contains("Tema", source, StringComparison.Ordinal);
        Assert.Contains("País", source, StringComparison.Ordinal);
        Assert.Contains("Privacidad", source, StringComparison.Ordinal);
    }

    private const string AccountMenuPath = "src/Ludeka.Web/Components/Shared/AccountMenu.razor";

    [Fact]
    public void HeaderGate_ShouldDistinguishSessionStatesAndLinkLogin()
    {
        // INC-50, INC-61 e INC-62: MainLayout monta AccountMenu, que distingue estados de sesión
        // con sesión → destinos canónicos + UserName; sin sesión → /login. HasSession sigue el patrón MyLibrary.razor.
        var layoutSource = ReadSource(LayoutPath);
        Assert.Contains("<AccountMenu />", layoutSource, StringComparison.Ordinal);

        var source = ReadSource(AccountMenuPath);
        Assert.Contains("href=\"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/login\"", source, StringComparison.Ordinal);
        Assert.Contains("Entrar", source, StringComparison.Ordinal);
        Assert.Contains("@CurrentUserService.UserName", source, StringComparison.Ordinal);
        Assert.Contains("HasSession", source, StringComparison.Ordinal);
        Assert.Contains("!string.IsNullOrWhiteSpace(CurrentUserService.UserId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderGate_ShouldMeetWcagFocusAndTargetSizeMarkers()
    {
        // INC-50 y INC-61: estado con texto en todos los tamaños (sr-only sm:not-sr-only),
        // foco visible con teclado (focus-visible:ring) y área objetivo ≥24×24 (WCAG 2.2 AA 2.5.8).
        var source = ReadSource(AccountMenuPath);

        Assert.Contains("sr-only sm:not-sr-only", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]", source, StringComparison.Ordinal);
        Assert.Contains("min-h-[24px]", source, StringComparison.Ordinal);
        Assert.Contains("min-w-[24px]", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MyLibrary_ShouldDeclareCanonicalRouteAndAlias()
    {
        // INC-50, diseño §D4.1: ruta canónica /cuenta/ludoteca convive con el alias vivo
        // /mi-ludoteca (doble @page, precedente Radar.razor). Sin [Authorize]: preserva el
        // comportamiento anónimo actual del alias.
        var source = ReadSource(MyLibraryPath);

        Assert.Contains("@page \"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/mi-ludoteca\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MyLibrary_ShouldAcceptTheAppearanceDeepLink()
    {
        // INC-50, diseño §D1: parámetro de query de código cerrado ?seccion= (precedente
        // resultado/aviso) mapeado a la pestaña Apariencia, único panel con temas y país.
        var source = ReadSource(MyLibraryPath);

        Assert.Contains("SupplyParameterFromQuery(Name = \"seccion\")", source, StringComparison.Ordinal);
        Assert.Contains("TabType.Appearance", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MyLibraryPath)]
    [InlineData(ConnectionsPath)]
    [InlineData(AppearancePath)]
    [InlineData(CountryPath)]
    [InlineData(PrivacyPath)]
    public void ChildPages_ShouldNotMountRedundantSectionHeader(string relativePath)
    {
        // Las rutas hijas del área de cuenta no montan submenú redundante, ya que todas las opciones
        // canónicas se encuentran en el menú de usuario de la cabecera.
        var source = ReadSource(relativePath);

        Assert.DoesNotContain("<AccountSectionNav", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccountMenuPath)]
    [InlineData("src/Ludeka.Web/Components/Pages/GameDetail.razor")]
    public void RewrittenLinks_ShouldTargetTheAccountArea(string relativePath)
    {
        // INC-50 e INC-62: reescritura selectiva del menú de cuenta en cabecera y de los enlaces
        // de GameDetail hacia la ruta canónica /cuenta/ludoteca. El alias sigue vivo, por lo
        // que esta reescritura es solo de enlaces, no de rutas.
        var source = ReadSource(relativePath);

        Assert.Contains("href=\"/cuenta/ludoteca\"", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/PublicProfile.razor")]
    [InlineData("src/Ludeka.Web/wwwroot/manifest.webmanifest")]
    [InlineData("src/Ludeka.Web/wwwroot/offline.html")]
    public void OutOfScopeFiles_ShouldKeepTheAliveAlias(string relativePath)
    {
        // INC-50, diseño §D4.1 (pines hacia adelante, nacen en verde): perfil público, manifest y
        // offline.html quedan fuera de alcance y conservan /mi-ludoteca mientras el alias viva.
        var source = ReadSource(relativePath);

        Assert.Contains("/mi-ludoteca", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountConnections_ShouldKeepInc49LogicUntouched()
    {
        // INC-50, diseño §D4.1 (pin hacia adelante, nace en verde): la tarea 2.4 solo monta la
        // cabecera de sección; los marcadores de lógica INC-49 deben seguir intactos.
        var source = ReadSource(ConnectionsPath);

        Assert.Contains("Connections.InvalidateCache()", source, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", source, StringComparison.Ordinal);
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
