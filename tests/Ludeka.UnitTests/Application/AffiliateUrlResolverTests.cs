using System.Collections.Generic;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Application.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class AffiliateUrlResolverTests
{
    private readonly AffiliateUrlResolver _resolver;

    public AffiliateUrlResolverTests()
    {
        var options = new AffiliateOptions
        {
            Enabled = true,
            Stores = new Dictionary<string, StoreAffiliateRule>
            {
                ["Zacatrus"] = new()
                {
                    ParamName = "ref",
                    AffiliateTag = "ludeka-21",
                    DomainMatch = "zacatrus.es"
                },
                ["Mathom"] = new()
                {
                    ParamName = "aff",
                    AffiliateTag = "ludeka-mathom",
                    DomainMatch = "mathom.es"
                },
                ["Tablerum"] = new()
                {
                    ParamName = "partner",
                    AffiliateTag = "ludeka-tab",
                    DomainMatch = "tablerum.es"
                }
            }
        };

        _resolver = new AffiliateUrlResolver(Options.Create(options));
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenDisabled_ReturnsRawUrl()
    {
        var disabledResolver = new AffiliateUrlResolver(Options.Create(new AffiliateOptions { Enabled = false }));
        var result = disabledResolver.ResolveAffiliateUrl("https://zacatrus.es/catan.html", "Zacatrus");
        Assert.Equal("https://zacatrus.es/catan.html", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveAffiliateUrl_WhenEmptyUrl_ReturnsOriginal(string? url)
    {
        var result = _resolver.ResolveAffiliateUrl(url!);
        Assert.Equal(url, result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenStoreMatchesByStoreName_AppendsParam()
    {
        var result = _resolver.ResolveAffiliateUrl("https://zacatrus.es/juegos-de-mesa/catan.html", "Zacatrus");
        Assert.Equal("https://zacatrus.es/juegos-de-mesa/catan.html?ref=ludeka-21", result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenStoreMatchesByDomain_AppendsParamWithoutStoreName()
    {
        var result = _resolver.ResolveAffiliateUrl("https://mathom.es/es/juegos-de-mesa/ark-nova.html");
        Assert.Equal("https://mathom.es/es/juegos-de-mesa/ark-nova.html?aff=ludeka-mathom", result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenUrlAlreadyHasQueryParams_AppendsWithAmpersand()
    {
        var result = _resolver.ResolveAffiliateUrl("https://tablerum.es/buscar?q=fundas", "Tablerum");
        Assert.Equal("https://tablerum.es/buscar?q=fundas&partner=ludeka-tab", result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenUrlAlreadyHasTargetParam_ReplacesValue()
    {
        var result = _resolver.ResolveAffiliateUrl("https://zacatrus.es/catan.html?ref=old-tag&lang=es", "Zacatrus");
        Assert.Equal("https://zacatrus.es/catan.html?ref=ludeka-21&lang=es", result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenUrlHasHashFragment_PreservesFragmentAtEnd()
    {
        var result = _resolver.ResolveAffiliateUrl("https://zacatrus.es/catan.html#reviews", "Zacatrus");
        Assert.Equal("https://zacatrus.es/catan.html?ref=ludeka-21#reviews", result);
    }

    [Fact]
    public void ResolveAffiliateUrl_WhenUnknownStore_ReturnsOriginalUrl()
    {
        var result = _resolver.ResolveAffiliateUrl("https://tiendadesconocida.com/juego.html", "Desconocida");
        Assert.Equal("https://tiendadesconocida.com/juego.html", result);
    }
}
