using System;
using System.IO;
using System.Text.RegularExpressions;
using Ludeka.Web.Authentication;
using Ludeka.Web.Components.Shared;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class AffiliatesAdminWebTests
{
    [Fact]
    public void AffiliatesAdmin_RazorFile_ShouldDeclareRouteAndPolicy()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor");

        Assert.Contains("@page \"/admin/afiliados\"", source, StringComparison.Ordinal);
        Assert.Contains($"@attribute [Authorize(Policy = AuthorizationPolicies.{AuthorizationPolicies.PermisoGestionarTiendas})]", source, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AffiliatesAdmin_RazorFile_ShouldInjectRequiredContractsAndServices()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor");

        Assert.Contains("IAffiliateFeedSourceRepository", source, StringComparison.Ordinal);
        Assert.Contains("IAffiliateEanDiscrepancyRepository", source, StringComparison.Ordinal);
        Assert.Contains("ICatalogFeedSyncService", source, StringComparison.Ordinal);
        Assert.Contains("IGameRepository", source, StringComparison.Ordinal);
        Assert.Contains("ICurrentUserService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AffiliatesAdmin_RazorFile_ShouldContainFeedsAndDiscrepanciesTabsAndActions()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor");

        Assert.Contains("Fuentes de Catálogo", source, StringComparison.Ordinal);
        Assert.Contains("Discrepancias EAN", source, StringComparison.Ordinal);
        Assert.Contains("Promover a EAN principal", source, StringComparison.Ordinal);
        Assert.Contains("Sincronizar Feeds Activos", source, StringComparison.Ordinal);
        Assert.Contains("GoogleShoppingXml", source, StringComparison.Ordinal);
        Assert.Contains("ShopifyJson", source, StringComparison.Ordinal);
        Assert.Contains("Shopify Catálogo JSON", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_RazorFile_ShouldIncludeAffiliatesAdminLinkWithPermissionGuard()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Layout/MainLayout.razor");

        Assert.Contains("/admin/afiliados", source, StringComparison.Ordinal);
        Assert.Contains("ModeratorPermission.CanManageStoreLinks", source, StringComparison.Ordinal);
        Assert.Contains("Feeds & Afiliados", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AffiliatesAdmin_IconNames_ShouldAllBeRegisteredInIconCatalog()
    {
        var source = ReadSource("src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor");
        var matches = Regex.Matches(source, @"<Icon\s+[^>]*Name=""([^""]+)""");

        Assert.NotEmpty(matches);
        foreach (Match match in matches)
        {
            var iconName = match.Groups[1].Value;
            Assert.True(
                IconCatalog.Icons.ContainsKey(iconName),
                $"El icono '{iconName}' usado en AffiliatesAdmin.razor no existe en IconCatalog.");
        }
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
