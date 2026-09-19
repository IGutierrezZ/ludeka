using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Services;
using Ludeka.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-48 (PR1b), requisito <c>media-storage-precedence</c> "Entrega HTTP del fallback en disco"
/// (spec.md). <c>MapStaticAssets()</c> (<c>Program.cs</c>) solo sirve el manifiesto de compilación,
/// no ficheros escritos en tiempo de ejecución por <see cref="PhysicalFileImageStorageService"/>
/// (diseño D2) — estas pruebas lo demuestran con un servidor HTTP real: Kestrel escuchando en
/// <c>127.0.0.1</c> con puerto efímero, primer uso de un host ASP.NET Core real dentro de
/// <c>Ludeka.UnitTests</c> (tasks.md, tarea 2.1). Contingencia del arnés CONFIRMADA RESUELTA por
/// este mismo fichero al compilar y ejecutar: <c>Ludeka.UnitTests.csproj</c> referencia
/// <c>Ludeka.Web.csproj</c> (SDK <c>Microsoft.NET.Sdk.Web</c>), y esa referencia de proyecto
/// arrastra el <c>FrameworkReference</c> implícito a <c>Microsoft.AspNetCore.App</c> lo bastante
/// para que <see cref="WebApplication.CreateBuilder(WebApplicationOptions)"/> resuelva en
/// tiempo de compilación y arranque en tiempo de ejecución, sin añadir paquete ni
/// <c>FrameworkReference</c> propios al csproj de pruebas.
///
/// Se descarta <c>WebApplicationFactory&lt;Program&gt;</c> por los motivos ya documentados en
/// <see cref="WebHostHostedServiceCompositionTests"/> (manifiesto de activos, resolución de
/// <i>content root</i> cruzada entre proyectos, claves de <c>DataProtection</c> en disco): el host
/// de estas pruebas es deliberadamente mínimo y compone EXCLUSIVAMENTE
/// <see cref="MediaStaticFilesExtensions.UseLudekaMediaFiles"/>, sin enrutamiento, autenticación ni
/// ningún otro middleware de <c>Program.cs</c>.
/// </summary>
public class MediaStaticFilesDeliveryTests
{
    [Fact]
    public async Task UseLudekaMediaFiles_ImagenGuardadaPorElAlmacenEnDisco_SeDescargaConHttp200()
    {
        var mediaRoot = CreateTempDirectory();
        try
        {
            var mediaOptions = new MediaOptions { LocalStoragePath = mediaRoot };
            var storageService = new PhysicalFileImageStorageService(env: null, customPath: mediaRoot);

            var contentBytes = Encoding.UTF8.GetBytes("contenido-binario-de-prueba-portada");
            using var contentStream = new MemoryStream(contentBytes);
            var uploadResult = await storageService.SaveGameCoverAsync(
                slug: "gloomhaven",
                contentStream: contentStream,
                originalFileName: "portada.jpg",
                contentType: "image/jpeg");

            Assert.True(uploadResult.Success, uploadResult.ErrorMessage);
            Assert.NotNull(uploadResult.RelativePath);

            await using var harness = await MinimalMediaHostHarness.StartAsync(mediaOptions);

            var response = await harness.Client.GetAsync(uploadResult.RelativePath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(contentBytes, downloadedBytes);
        }
        finally
        {
            Directory.Delete(mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task UseLudekaMediaFiles_UrlDeImagenNuncaGuardada_Devuelve404()
    {
        var mediaRoot = CreateTempDirectory();
        try
        {
            var mediaOptions = new MediaOptions { LocalStoragePath = mediaRoot };
            await using var harness = await MinimalMediaHostHarness.StartAsync(mediaOptions);

            var response = await harness.Client.GetAsync("/images/games/no-existe-12345.jpg");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            Directory.Delete(mediaRoot, recursive: true);
        }
    }

    // Matriz de amenazas (diseño §8, fila "Exposición de rutas"), tasks.md 2.4: dos codificaciones
    // distintas del mismo recorrido de directorio deben quedar bloqueadas. Confirmado por ejecución
    // real (no asumido): ambas variantes devuelven 404, coincidiendo con la predicción del diseño —
    // PhysicalFileProvider decodifica, normaliza y descarta cualquier ruta que resuelva fuera de su
    // raíz antes de que el fichero de fuera pueda servirse.
    [Theory]
    [InlineData("/images/..%2f..%2fSECRETO.txt")]
    [InlineData("/images/%2e%2e/%2e%2e/SECRETO.txt")]
    public async Task UseLudekaMediaFiles_PeticionConRecorridoDeDirectorio_NoSirveFicheroFueraDeLaRaiz(string payloadPath)
    {
        var mediaRoot = CreateTempDirectory();
        var secretContent = $"secreto-fuera-de-la-raiz-{Guid.NewGuid():N}";
        var secretPath = Path.Combine(Directory.GetParent(mediaRoot)!.FullName, "SECRETO.txt");
        await File.WriteAllTextAsync(secretPath, secretContent);
        try
        {
            var mediaOptions = new MediaOptions { LocalStoragePath = mediaRoot };
            await using var harness = await MinimalMediaHostHarness.StartAsync(mediaOptions);

            var response = await harness.Client.GetAsync(payloadPath);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(secretContent, body);
        }
        finally
        {
            File.Delete(secretPath);
            Directory.Delete(mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task UseLudekaMediaFiles_RutaLocalRelativaConfigurada_ResuelveDentroDeContentRootPath()
    {
        var contentRoot = CreateTempDirectory();
        try
        {
            const string relativeSegment = "media-relativa";
            var mediaOptions = new MediaOptions { LocalStoragePath = relativeSegment };

            var expectedRoot = Path.Combine(contentRoot, relativeSegment);
            Directory.CreateDirectory(expectedRoot);
            const string fileContent = "contenido-en-ruta-relativa";
            await File.WriteAllTextAsync(Path.Combine(expectedRoot, "hola.txt"), fileContent);

            await using var harness = await MinimalMediaHostHarness.StartAsync(mediaOptions, contentRoot);

            var response = await harness.Client.GetAsync("/images/hola.txt");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(fileContent, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ludeka-media-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Host ASP.NET Core real y deliberadamente mínimo (Kestrel en <c>127.0.0.1</c>, puerto
    /// efímero) que compone EXCLUSIVAMENTE
    /// <see cref="MediaStaticFilesExtensions.UseLudekaMediaFiles"/>.
    /// </summary>
    private sealed class MinimalMediaHostHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private MinimalMediaHostHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<MinimalMediaHostHarness> StartAsync(MediaOptions mediaOptions, string? contentRootPath = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentRootPath,
                EnvironmentName = Environments.Production
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();
            app.UseLudekaMediaFiles(mediaOptions, app.Environment);

            await app.StartAsync();

            var address = app.Urls.First();
            var client = new HttpClient { BaseAddress = new Uri(address) };
            return new MinimalMediaHostHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
