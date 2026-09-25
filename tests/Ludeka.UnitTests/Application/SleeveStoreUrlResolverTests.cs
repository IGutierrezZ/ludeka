using System.Collections.Generic;
using System.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Application.Features.Sleeves;
using Ludeka.Application.Options;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class SleeveStoreUrlResolverTests
{
    private readonly SleeveStoreUrlResolver _resolver = new();

    [Theory]
    [InlineData("Zacatrus", 63.5, 88.0, "https://zacatrus.es/catalogsearch/result/?q=fundas+63.5x88&ref=ludeka")]
    [InlineData("Dungeon Marvels", 56.0, 87.0, "https://dungeonmarvels.com/buscar?controller=search&s=fundas+56x87&ref=ludeka")]
    [InlineData("Cuarto de Juegos", 44.0, 68.0, "https://cuartodejuegos.es/buscar?q=fundas+44x68&ref=ludeka")]
    [InlineData("Tablerum", 59.0, 92.0, "https://tablerum.es/buscar?q=fundas+59x92&partner=ludeka")]
    [InlineData("Amazon", 63.5, 88.0, "https://www.amazon.es/s?k=fundas+63.5x88&tag=ludeka-21")]
    public void ResolveStoreUrl_BuildsAccurateAffiliateUrls(string store, double width, double height, string expectedUrl)
    {
        var url = _resolver.ResolveStoreUrl(store, width, height);
        Assert.Equal(expectedUrl, url);
    }

    [Fact]
    public void ResolveStoreUrl_WithInjectedAffiliateResolver_AppliesCorrectParameters()
    {
        var affiliateOptions = Options.Create(new AffiliateOptions());
        var affiliateResolver = new AffiliateUrlResolver(affiliateOptions);
        var resolver = new SleeveStoreUrlResolver(affiliateResolver);

        var zacatrusUrl = resolver.ResolveStoreUrl("Zacatrus", 63.5, 88.0);
        Assert.Equal("https://zacatrus.es/catalogsearch/result/?q=fundas+63.5x88&ref=ludeka", zacatrusUrl);

        var dungeonMarvelsUrl = resolver.ResolveStoreUrl("Dungeon Marvels", 56.0, 87.0);
        Assert.Equal("https://dungeonmarvels.com/buscar?controller=search&s=fundas+56x87&ref=ludeka", dungeonMarvelsUrl);

        var cuartoJuegosUrl = resolver.ResolveStoreUrl("Cuarto de Juegos", 44.0, 68.0);
        Assert.Equal("https://cuartodejuegos.es/buscar?q=fundas+44x68&ref=ludeka", cuartoJuegosUrl);

        var tablerumUrl = resolver.ResolveStoreUrl("Tablerum", 59.0, 92.0);
        Assert.Equal("https://tablerum.es/buscar?q=fundas+59x92&partner=ludeka", tablerumUrl);

        var amazonUrl = resolver.ResolveStoreUrl("Amazon", 63.5, 88.0);
        Assert.Equal("https://www.amazon.es/s?k=fundas+63.5x88&tag=ludeka-21", amazonUrl);
    }

    [Fact]
    public void ResolveStoreUrl_UsesCustomAffiliateCode_WhenProvided()
    {
        var url = _resolver.ResolveStoreUrl("Zacatrus", 63.5, 88.0, "customtag");
        Assert.Contains("ref=customtag", url);
    }

    [Fact]
    public void ResolvePurchaseOptions_IncludesPartnerStores_AndFiltersByCountry()
    {
        var sleeve = new SleeveItem(
            FormatName: "Standard Card Game",
            WidthMm: 63.5,
            HeightMm: 88.0,
            CardCount: 110,
            AffiliateUrl: null
        );

        // Sin filtro de país: devuelve opciones de España (Zacatrus, Dungeon Marvels, Cuarto de Juegos, Tablerum, Amazon)
        var optionsGlobal = _resolver.ResolvePurchaseOptions(sleeve);
        Assert.NotEmpty(optionsGlobal);
        Assert.Contains(optionsGlobal, o => o.StoreName == "Zacatrus");
        Assert.Contains(optionsGlobal, o => o.StoreName == "Dungeon Marvels");
        Assert.Contains(optionsGlobal, o => o.StoreName == "Cuarto de Juegos");
        Assert.Contains(optionsGlobal, o => o.StoreName == "Tablerum");
        Assert.Contains(optionsGlobal, o => o.StoreName == "Amazon");

        // Con filtro país España: compatible
        var optionsSpain = _resolver.ResolvePurchaseOptions(sleeve, "España");
        Assert.NotEmpty(optionsSpain);
        Assert.Contains(optionsSpain, o => o.StoreName == "Zacatrus");
        Assert.Contains(optionsSpain, o => o.StoreName == "Cuarto de Juegos");
        Assert.Contains(optionsSpain, o => o.StoreName == "Tablerum");
        Assert.Contains(optionsSpain, o => o.StoreName == "Amazon");

        // Con filtro país Colombia (las tiendas asociadas de España solo envían a España y Portugal, pero Amazon envía internacional)
        var optionsColombia = _resolver.ResolvePurchaseOptions(sleeve, "Colombia");
        Assert.DoesNotContain(optionsColombia, o => o.StoreName == "Zacatrus");
        Assert.DoesNotContain(optionsColombia, o => o.StoreName == "Dungeon Marvels");
        Assert.DoesNotContain(optionsColombia, o => o.StoreName == "Cuarto de Juegos");
        Assert.DoesNotContain(optionsColombia, o => o.StoreName == "Tablerum");
        Assert.Contains(optionsColombia, o => o.StoreName == "Amazon");
    }

    [Fact]
    public void ResolvePurchaseOptions_IncludesDirectAffiliateUrl_WhenSpecifiedOnSleeve()
    {
        var sleeve = new SleeveItem(
            FormatName: "Chimera",
            WidthMm: 57.0,
            HeightMm: 89.0,
            CardCount: 90,
            AffiliateUrl: "https://tienda-custom.com/fundas-wingspan?aff=direct",
            StoreName: "Tienda Custom",
            Country: "España"
        );

        var options = _resolver.ResolvePurchaseOptions(sleeve, "España");

        Assert.Contains(options, o => o.StoreName == "Tienda Custom" && o.PurchaseUrl.Contains("aff=direct"));
        Assert.Contains(options, o => o.StoreName == "Zacatrus");
    }

    [Fact]
    public void MatchStandardFormat_MatchesAccurately()
    {
        var format = _resolver.MatchStandardFormat(63.5, 88.0);
        Assert.NotNull(format);
        Assert.Equal("Standard Card Game", format.Name);
    }
}
