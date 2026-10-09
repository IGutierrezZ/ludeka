using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-143: Acredita el soporte de peticiones HTTP HEAD en el pipeline web de Ludeka
/// para que validadores y crawlers de afiliación (Awin, Google, etc.) verifiquen la
/// disponibilidad del sitio sin recibir 405 Method Not Allowed.
/// </summary>
public class HeadMethodSupportTests
{
    [Fact]
    public async Task UseHeadMethodSupport_PeticionHead_RespondeHttp200ConCuerpoVacioYCabeceras()
    {
        await using var harness = await HeadHostHarness.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Head, "/");
        using var response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body), $"Se esperaba cuerpo vacío para HEAD pero se recibió: '{body}'");
    }

    [Fact]
    public async Task UseHeadMethodSupport_PeticionGet_RespondeNormalConCuerpoCompleto()
    {
        await using var harness = await HeadHostHarness.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        using var response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("<!DOCTYPE html><html><body>Ludeka Live</body></html>", body);
    }

    [Fact]
    public async Task UseHeadMethodSupport_RutaInexistente_Responde404ConCuerpoVacio()
    {
        await using var harness = await HeadHostHarness.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Head, "/ruta-inexistente-404");
        using var response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body), "Se esperaba cuerpo vacío en 404 para HEAD.");
    }

    [Fact]
    public void AppRazor_ContieneMetaetiquetaVerificacionAwin()
    {
        // Localizar el fichero App.razor en el proyecto Ludeka.Web
        var solutionDir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(solutionDir) && !File.Exists(Path.Combine(solutionDir, "Ludeka.sln")))
        {
            var parent = Directory.GetParent(solutionDir)?.FullName;
            if (parent == solutionDir) break;
            solutionDir = parent;
        }

        var appRazorPath = Path.Combine(solutionDir!, "src", "Ludeka.Web", "Components", "App.razor");
        Assert.True(File.Exists(appRazorPath), $"No se encontró App.razor en: {appRazorPath}");

        var content = File.ReadAllText(appRazorPath);
        Assert.Contains("awin-site-verification", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("awin", content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HeadHostHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private HeadHostHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<HeadHostHarness> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();

            // Middleware bajo prueba antes de UseRouting
            app.UseHeadMethodSupport();
            app.UseRouting();

            // Simulación de endpoint GET como los de Blazor SSR / Razor
            app.MapGet("/", () => Results.Content("<!DOCTYPE html><html><body>Ludeka Live</body></html>", "text/html"));

            await app.StartAsync();

            var address = app.Urls.First();
            var client = new HttpClient { BaseAddress = new Uri(address) };
            return new HeadHostHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
