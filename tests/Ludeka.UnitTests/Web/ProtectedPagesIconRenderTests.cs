using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ludeka.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Render real del componente <see cref="Icon"/> con los atributos que usan las tres páginas
/// protegidas que quedaron con <c>class</c>/<c>Class</c> sobre <c>&lt;Icon&gt;</c> (handoff de F3 de
/// INC-46): sin el paso directo de atributos adicionales esas páginas vuelven a fallar al renderizar.
/// No hay credenciales OAuth en el repositorio, así que el render se ejerce sobre el componente y se
/// alimenta con los atributos reales extraídos del marcado de cada página.
/// </summary>
public class ProtectedPagesIconRenderTests
{
    private const string FallbackIconName = "shield";

    private static readonly string[] ProtectedPagesWithIconAttributes =
    [
        "src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor",
        "src/Ludeka.Web/Components/Pages/SocialInboxModeration.razor",
        "src/Ludeka.Web/Components/Pages/MonitoredAccountsDirectory.razor"
    ];

    private static readonly HashSet<string> DeclaredParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Size",
        "StrokeWidth",
        "Title"
    };

    private static HtmlRenderer CreateRenderer()
        => new(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance);

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

    /// <summary>Nombres de atributo de cada etiqueta <c>&lt;Icon …/&gt;</c> del marcado de una página.</summary>
    private static IEnumerable<IReadOnlyList<string>> ReadIconAttributeSets(string relativePath)
    {
        string path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró la página protegida {relativePath}");

        string source = File.ReadAllText(path);
        foreach (Match tag in Regex.Matches(source, @"<Icon\s+([^>]*?)/?>", RegexOptions.Singleline))
        {
            var names = Regex.Matches(tag.Groups[1].Value, @"(?<=[\s])([A-Za-z][A-Za-z0-9_\-]*)\s*=\s*""")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count > 0)
            {
                yield return names;
            }
        }
    }

    /// <summary>
    /// Los iconos de las tres páginas protegidas se renderizan con sus atributos adicionales reales:
    /// ninguna combinación lanza y los atributos ajenos al componente llegan al SVG.
    /// </summary>
    [Fact]
    public async Task EveryIconOfTheProtectedPages_RendersWithItsExtraAttributes()
    {
        var attributeSets = ProtectedPagesWithIconAttributes
            .SelectMany(ReadIconAttributeSets)
            .ToList();

        Assert.True(attributeSets.Count >= 70,
            $"Se esperaban al menos 70 usos de <Icon> en las páginas protegidas, se leyeron {attributeSets.Count}.");

        var renderer = CreateRenderer();
        var renderedUsages = 0;
        var usagesWithExtraAttributes = 0;

        foreach (var attributeNames in attributeSets)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["Name"] = FallbackIconName,
                ["Size"] = 14,
                ["StrokeWidth"] = 2
            };

            var extraAttributes = new List<string>();
            foreach (var attributeName in attributeNames)
            {
                if (DeclaredParameters.Contains(attributeName))
                {
                    if (attributeName.Equals("Size", StringComparison.OrdinalIgnoreCase))
                    {
                        parameters["Size"] = 14;
                    }
                    else if (attributeName.Equals("StrokeWidth", StringComparison.OrdinalIgnoreCase))
                    {
                        parameters["StrokeWidth"] = 2;
                    }
                    else if (attributeName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                    {
                        parameters["Title"] = "Icono con significado";
                    }

                    continue;
                }

                extraAttributes.Add(attributeName);
                parameters[attributeName] = "valor-de-prueba";
            }

            string html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync<Icon>(ParameterView.FromDictionary(parameters));
                return root.ToHtmlString();
            });

            Assert.Contains("<svg", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("viewBox=\"0 0 24 24\"", html, StringComparison.OrdinalIgnoreCase);
            foreach (var attributeName in extraAttributes)
            {
                Assert.Contains($"{attributeName}=\"", html, StringComparison.OrdinalIgnoreCase);
            }

            renderedUsages++;
            if (extraAttributes.Count > 0)
            {
                usagesWithExtraAttributes++;
            }
        }

        Assert.Equal(attributeSets.Count, renderedUsages);
        Assert.True(usagesWithExtraAttributes > 0,
            "Las páginas protegidas deben ejercer el paso directo de atributos adicionales (class/Class/aria-*).");
    }

    [Fact]
    public void TheThreeProtectedPages_DeclareTheAuthorizeAttributeOfTheirPolicy()
    {
        foreach (var relativePath in ProtectedPagesWithIconAttributes)
        {
            string path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = File.ReadAllText(path);

            Assert.Contains("@attribute [Authorize", source);
        }
    }

    [Fact]
    public async Task Icon_WithoutTitle_IsDecorativeAndItsOwnAriaHiddenWinsOverTheCallerValue()
    {
        var renderer = CreateRenderer();

        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<Icon>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Name"] = FallbackIconName,
                ["class"] = "text-emerald-500",
                ["aria-hidden"] = "false"
            }));

            return root.ToHtmlString();
        });

        // El paso directo aplica la clase del llamador y la semántica decorativa del componente manda.
        Assert.Contains("class=\"text-emerald-500\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-hidden=\"true\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role=\"img\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Icon_WithUnknownName_RendersNothingInsteadOfThrowing()
    {
        var renderer = CreateRenderer();

        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<Icon>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Name"] = "icono-inexistente-de-prueba"
            }));

            return root.ToHtmlString();
        });

        Assert.DoesNotContain("<svg", html, StringComparison.OrdinalIgnoreCase);
    }
}

