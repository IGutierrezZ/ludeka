using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Sleeves;
using Ludeka.Core.ValueObjects;
using Ludeka.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class SleeveGuideCardTests
{
    private sealed class StubUserLocationService : IUserLocationService
    {
        public string? CurrentCountry { get; set; } = "España";
        public string? DetectedCountry { get; set; } = "España";
        public string? EffectiveCountry { get; set; } = "España";

        public void SetUserCountry(string? country) => CurrentCountry = country;
        public void SetDetectedCountry(string? country) => DetectedCountry = country;

        public Task<string?> GetEffectiveCountryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(EffectiveCountry);

        public Task<string?> GetDetectedCountryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(DetectedCountry);

        public Task<UserLocationState> GetUserLocationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UserLocationState(CurrentCountry, DetectedCountry, EffectiveCountry));

        public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, Func<T, string?> countrySelector, string? preferredCountry = null)
            => items;

        public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, string? preferredCountry = null, Func<T, string?>? countrySelector = null)
            => items;
    }

    private static IServiceProvider CreateServiceProvider(
        IUserLocationService? locationService = null,
        ISleeveStoreUrlResolver? sleeveStoreUrlResolver = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserLocationService>(locationService ?? new StubUserLocationService());
        services.AddSingleton<ISleeveStoreUrlResolver>(sleeveStoreUrlResolver ?? new SleeveStoreUrlResolver());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SleeveGuideCard_WhenNoSleevesAndNotExplicitlyNoCards_RendersHonestMissingDataState()
    {
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<SleeveGuideCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Sleeves"] = null,
                ["HasNoCards"] = false
            }));
            return root.ToHtmlString();
        });

        Assert.True(
            html.Contains("Sin especificación registrada de fundas") || html.Contains("Información de fundas no disponible"),
            "Debe contener 'Sin especificación registrada de fundas' o 'Información de fundas no disponible'.");
        Assert.DoesNotContain("¡Buenas noticias!", html);
        Assert.DoesNotContain("Este juego no contiene cartas", html);
    }

    [Fact]
    public async Task SleeveGuideCard_WhenHasNoCardsIsTrue_RendersNoCardsMessage()
    {
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<SleeveGuideCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Sleeves"] = Array.Empty<SleeveItem>(),
                ["HasNoCards"] = true
            }));
            return root.ToHtmlString();
        });

        Assert.Contains("¡Buenas noticias!", html);
        Assert.Contains("Este juego no contiene cartas", html);
    }

    [Fact]
    public async Task SleeveGuideCard_WhenCardCountIsZero_RendersUnknownCardCountMessage()
    {
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var sleeve = new SleeveItem("Mini European", 44.0, 68.0, 0, null);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<SleeveGuideCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Sleeves"] = new[] { sleeve },
                ["HasNoCards"] = false
            }));
            return root.ToHtmlString();
        });

        Assert.True(
            html.Contains("Recuento no especificado") || html.Contains("Recuento sin especificar"),
            "Debe contener 'Recuento no especificado' o 'Recuento sin especificar'.");
        Assert.DoesNotContain("0 cartas", html);
    }

    [Fact]
    public async Task SleeveGuideCard_WhenSleevesProvided_RendersCardsAndStoreOptions()
    {
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var sleeve = new SleeveItem("Standard Card Game", 63.5, 88.0, 110, null);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<SleeveGuideCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Sleeves"] = new[] { sleeve },
                ["HasNoCards"] = false
            }));
            return root.ToHtmlString();
        });

        Assert.Contains("Standard Card Game", html);
        Assert.Contains("110 cartas", html);
        Assert.True(Regex.IsMatch(html, @"3(</strong>)?\s*packs"), "Debe mostrar 3 packs.");
        Assert.True(html.Contains("Zacatrus") || html.Contains("Amazon"), "Debe contener enlaces a tiendas como Zacatrus o Amazon.");
    }

    [Fact]
    public async Task SleeveGuideCard_WhenNoSleevesAndOnOpenEditorProvided_RendersAddButton()
    {
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);
        var callback = EventCallback.Factory.Create(this, () => { });

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<SleeveGuideCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Sleeves"] = null,
                ["HasNoCards"] = false,
                ["OnOpenEditor"] = callback
            }));
            return root.ToHtmlString();
        });

        Assert.Contains("Añadir fundas", html);
    }
}
