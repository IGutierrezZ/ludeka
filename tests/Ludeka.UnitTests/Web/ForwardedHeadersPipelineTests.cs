using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Web;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using AuthenticationOptions = Ludeka.Application.Features.Identity.AuthenticationOptions;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-52 (Fase 2 / PR #2), especificación <c>reverse-proxy-forwarded-headers</c>, requisito
/// "Procesamiento incondicional de <c>X-Forwarded-Proto</c> sin restricción por origen del
/// proxy" (spec.md). Esta clase acredita la puerta de decisión H4/D4 del diseño (design.md
/// §7.2, §8.3): si las listas de confianza de <see cref="ForwardedHeadersOptions"/>
/// (<c>KnownProxies</c>, <c>KnownIPNetworks</c>) deben vaciarse para que el esquema se
/// reescriba a <c>https</c> detrás de un proxy inverso de salto único (Cloud Run / Nginx).
///
/// El arnés (<see cref="ForwardedHeadersHostHarness"/>) es un host ASP.NET Core real y
/// deliberadamente mínimo (Kestrel en <c>127.0.0.1</c>, puerto efímero), calcado de
/// <c>MinimalMediaHostHarness</c> (<c>MediaStaticFilesDeliveryTests.cs:166-204</c>) por la
/// misma razón que allí: la referencia de proyecto a <c>Ludeka.Web.csproj</c> (SDK
/// <c>Microsoft.NET.Sdk.Web</c>) ya arrastra el <c>FrameworkReference</c> implícito necesario
/// para que <see cref="WebApplication.CreateBuilder(WebApplicationOptions)"/> resuelva y
/// arranque, sin añadir <c>Microsoft.AspNetCore.TestHost</c> (decisión cerrada del
/// maintainer, diseño D13).
///
/// La pieza que decide todo el incremento es forzar <c>Connection.RemoteIpAddress</c> a
/// <c>203.0.113.10</c> (TEST-NET-3, RFC 5737) ANTES de <c>UseForwardedHeaders</c>: un
/// <see cref="HttpClient"/> contra <c>127.0.0.1</c> produce una dirección remota de bucle
/// invertido, que es justo la que se sospecha que las listas de confianza aceptan por
/// defecto (diseño D4, hueco H4). Sin forzar una dirección que no sea de bucle invertido, el
/// caso negativo de abajo saldría verde por el motivo equivocado y no probaría nada.
/// </summary>
public class ForwardedHeadersPipelineTests
{
    // N2 (caso negativo) — design.md §4.3, spec.md "Sin vaciar las listas de confianza, el
    // esquema no cambia" (líneas 23-28). Esta prueba decide la puerta H4/D4 (design.md §7.2,
    // §8.3): si sale roja porque el esquema SÍ cambia sin vaciar las listas, el supuesto de
    // D4 queda refutado y NO debe reescribirse para que pase — hay que volver a sdd-design.
    //
    // No depende de ForwardedHeadersConfiguration (Fase 3, todavía no existe): construye sus
    // propias opciones inline, con las listas de confianza en su estado por defecto.
    [Fact]
    public async Task UseForwardedHeaders_ListasDeConfianzaSinVaciar_ElEsquemaPermaneceEnHttp()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto
        };

        await using var harness = await ForwardedHeadersHostHarness.StartAsync(options);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await harness.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var scheme = body.Split('|')[0];

        Assert.Equal("http", scheme);
    }

    // N1 (caso positivo) — design.md §4.3, spec.md "Proxy no loopback corrige el esquema a
    // https" (líneas 17-21). Parametrizada por EnvironmentName para cubrir también "Mismo
    // comportamiento en un entorno distinto de Production" (líneas 30-34): la corrección no
    // depende de ASPNETCORE_ENVIRONMENT, solo de la cabecera y de las listas de confianza
    // vacías de ForwardedHeadersConfiguration.Build() (Fase 3).
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task UseForwardedHeaders_ProxyNoLoopbackConCabeceraHttps_CorrigeElEsquemaAHttps(string environmentName)
    {
        await using var harness = await ForwardedHeadersHostHarness.StartAsync(
            ForwardedHeadersConfiguration.Build(),
            environmentName);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await harness.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var scheme = body.Split('|')[0];

        Assert.Equal("https", scheme);
    }

    // N3 — design.md §4.3, spec.md "Sin proxy delante, el comportamiento local no cambia"
    // (líneas 36-40). Mismas opciones de producción que N1, pero sin la cabecera: el esquema
    // debe conservar el valor real que Kestrel determinó para la conexión (http, en este
    // arnés), sin alteración.
    [Fact]
    public async Task UseForwardedHeaders_SinCabeceraXForwardedProto_ElEsquemaPermaneceEnHttp()
    {
        await using var harness = await ForwardedHeadersHostHarness.StartAsync(ForwardedHeadersConfiguration.Build());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");

        var response = await harness.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var scheme = body.Split('|')[0];

        Assert.Equal("http", scheme);
    }

    // N4 — design.md §4.3 y §7 (fila H5, líneas 187 y 602): cierre empírico de D7. Monta
    // UseForwardedHeaders y UseHttpsRedirection() en el mismo orden que Program.cs
    // (:202,:211), sin la cabecera X-Forwarded-Proto. Este arnés solo publica un puerto HTTP
    // efímero (sin puerto HTTPS resoluble): si UseHttpsRedirection() no puede determinar un
    // puerto HTTPS, la documentación del propio framework dice que se apaga en vez de
    // redirigir. Si esta prueba viera 307/308, el hueco H5 quedaría refutado — el hallazgo se
    // registra, no se corrige aquí (decisión del maintainer).
    [Fact]
    public async Task UseForwardedHeadersYUseHttpsRedirection_SinCabeceraXForwardedProto_NoRedirige()
    {
        await using var harness = await ForwardedHeadersHostHarness.StartAsync(
            ForwardedHeadersConfiguration.Build(),
            useHttpsRedirection: true,
            allowAutoRedirect: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");

        var response = await harness.Client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
    }

    // N5 — design.md §4.3, spec.md "X-Forwarded-Host no altera el host" (líneas 48-52), fila
    // A3 de la matriz de amenazas: XForwardedHost queda fuera de las banderas de
    // ForwardedHeadersConfiguration.Build() (D2), así que un origen que suplante esa cabecera
    // no consigue que la aplicación adopte el host falso.
    [Fact]
    public async Task UseForwardedHeaders_ConXForwardedHostSuplantado_ElHostRealNoCambia()
    {
        await using var harness = await ForwardedHeadersHostHarness.StartAsync(ForwardedHeadersConfiguration.Build());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "dominio-suplantado.ejemplo");

        var response = await harness.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var host = body.Split('|')[1];

        Assert.Equal(harness.Client.BaseAddress!.Authority, host);
        Assert.DoesNotContain("dominio-suplantado.ejemplo", host, StringComparison.Ordinal);
    }

    // N6 — design.md §4.3, spec.md "Acceso social detrás del proxy genera un redirect_uri en
    // https" (líneas 60-64): el manejador de Google construye el redirect_uri a partir del
    // Request.Scheme ya corregido por UseForwardedHeaders, no del esquema interno con el que
    // reenvía el proxy (ExternalAuthenticationSchemes.cs:35).
    [Fact]
    public async Task DesafioGoogle_ConCabeceraHttps_ElRedirectUriEmpiezaPorHttpsYTerminaEnSigninGoogle()
    {
        await using var harness = await ForwardedHeadersAuthenticationHostHarness.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await harness.Client.SendAsync(request);
        var redirectUri = ExtractRedirectUri(response);

        Assert.StartsWith("https://", redirectUri, StringComparison.Ordinal);
        Assert.EndsWith(ExternalAuthenticationSchemes.GoogleCallbackPath, redirectUri, StringComparison.Ordinal);
    }

    // Variante de N6 — spec.md "Desarrollo local sin proxy sigue construyendo el redirect_uri
    // en http" (líneas 66-70): mismo arnés de autenticación, sin la cabecera; el redirect_uri
    // conserva el esquema real de Kestrel (http), coherente con el acceso local real. Junto
    // con la prueba anterior, prueba que el esquema depende de la cabecera y no de otra cosa.
    [Fact]
    public async Task DesafioGoogle_SinCabeceraXForwardedProto_ElRedirectUriConservaHttp()
    {
        await using var harness = await ForwardedHeadersAuthenticationHostHarness.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");

        var response = await harness.Client.SendAsync(request);
        var redirectUri = ExtractRedirectUri(response);

        Assert.StartsWith("http://", redirectUri, StringComparison.Ordinal);
        Assert.EndsWith(ExternalAuthenticationSchemes.GoogleCallbackPath, redirectUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extrae el parámetro <c>redirect_uri</c> (ya decodificado) de la cabecera
    /// <c>Location</c> del desafío OAuth. El <see cref="HttpClient"/> del arnés de
    /// autenticación se construye con <c>AllowAutoRedirect = false</c> precisamente para que
    /// esta cabecera llegue intacta, en vez de que el cliente siga la redirección hacia Google.
    /// </summary>
    private static string ExtractRedirectUri(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = QueryHelpers.ParseQuery(location!.Query);
        Assert.True(query.TryGetValue("redirect_uri", out var redirectUri), "La URL de autorización debe incluir redirect_uri.");

        return redirectUri.ToString();
    }

    /// <summary>
    /// Host ASP.NET Core real y deliberadamente mínimo (Kestrel en <c>127.0.0.1</c>, puerto
    /// efímero) que compone el middleware de cabeceras reenviadas bajo prueba y, cuando se
    /// pide explícitamente (Fase 4, prueba N4), también <c>UseHttpsRedirection()</c> en el
    /// mismo orden que <c>Program.cs</c> (<c>:202</c>, <c>:211</c>). Punto final de la
    /// tubería: devuelve <c>Request.Scheme</c> y <c>Request.Host</c> en el cuerpo de la
    /// respuesta, separados por <c>|</c>, como texto plano.
    /// </summary>
    private sealed class ForwardedHeadersHostHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ForwardedHeadersHostHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<ForwardedHeadersHostHarness> StartAsync(
            ForwardedHeadersOptions options,
            string environmentName = "Production",
            bool useHttpsRedirection = false,
            bool allowAutoRedirect = true)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();

            // Pieza que decide todo el incremento (diseño D13): forzar una dirección remota
            // que NO es de bucle invertido, ANTES de UseForwardedHeaders. 203.0.113.10 es
            // TEST-NET-3 (RFC 5737): nunca enrutable, nunca privada, nunca de bucle
            // invertido, y estable en cualquier máquina y en cualquier entorno de CI.
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
                await next();
            });

            app.UseForwardedHeaders(options);

            if (useHttpsRedirection)
            {
                // Prueba N4 (design.md §4.3, §7 fila H5): mismo orden relativo que
                // Program.cs, UseForwardedHeaders antes de UseHttpsRedirection.
                app.UseHttpsRedirection();
            }

            app.Run(async context =>
            {
                await context.Response.WriteAsync($"{context.Request.Scheme}|{context.Request.Host}");
            });

            await app.StartAsync();

            var address = app.Urls.First();
            var handler = new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect };
            var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            return new ForwardedHeadersHostHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Host ASP.NET Core real y deliberadamente mínimo (Kestrel en <c>127.0.0.1</c>, puerto
    /// efímero), variante de <see cref="ForwardedHeadersHostHarness"/> para la prueba N6 y su
    /// variante de desarrollo (Fase 4): compone el middleware de cabeceras reenviadas,
    /// <c>AddLudekaAuthentication</c> con un Google utilizable (mismo patrón que
    /// <c>OptionsWithUsableGoogle</c>, <c>WebAuthenticationRegistrationTests.cs:36-41</c>) y
    /// <c>UseAuthentication()</c>, en el mismo orden relativo que <c>Program.cs</c>
    /// (<c>:202</c>, <c>:224</c>). Data Protection efímera
    /// (<c>AddDataProtection().UseEphemeralDataProtectionProvider()</c>, diseño D13) evita que
    /// el desafío OAuth persista en disco las claves que protegen el parámetro <c>state</c>.
    /// Punto final de la tubería: ejecuta directamente <c>Results.Challenge</c> contra el
    /// esquema de Google (sin necesidad de enrutamiento) y deja que la redirección llegue
    /// intacta al cliente (<c>AllowAutoRedirect = false</c>) para poder leer el
    /// <c>redirect_uri</c> de la cabecera <c>Location</c>.
    /// </summary>
    private sealed class ForwardedHeadersAuthenticationHostHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ForwardedHeadersAuthenticationHostHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<ForwardedHeadersAuthenticationHostHarness> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Production"
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            // Google utilizable: mismo patrón que OptionsWithUsableGoogle
            // (WebAuthenticationRegistrationTests.cs:36-41, read-only).
            var authenticationOptions = new AuthenticationOptions();
            authenticationOptions.Providers[ExternalProviderNames.Google] = new ExternalProviderOptions
            {
                Enabled = true,
                ClientId = "google-client-id",
                ClientSecret = "google-client-secret"
            };

            builder.Services.AddLudekaAuthentication(authenticationOptions);

            // Data Protection efímera (diseño D13): el desafío protege el parámetro `state`
            // con claves que, por defecto, se persistirían en disco.
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

            var app = builder.Build();

            // Misma pieza que ForwardedHeadersHostHarness (diseño D13): dirección remota que
            // NO es de bucle invertido, ANTES de UseForwardedHeaders.
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
                await next();
            });

            app.UseForwardedHeaders(ForwardedHeadersConfiguration.Build());
            app.UseAuthentication();

            app.Run(async context =>
            {
                var challenge = Results.Challenge(new AuthenticationProperties(), [GoogleDefaults.AuthenticationScheme]);
                await challenge.ExecuteAsync(context);
            });

            await app.StartAsync();

            var address = app.Urls.First();
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            return new ForwardedHeadersAuthenticationHostHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
