using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

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

    /// <summary>
    /// Host ASP.NET Core real y deliberadamente mínimo (Kestrel en <c>127.0.0.1</c>, puerto
    /// efímero) que compone EXCLUSIVAMENTE el middleware de cabeceras reenviadas bajo prueba.
    /// Punto final de la tubería: devuelve <c>Request.Scheme</c> y <c>Request.Host</c> en el
    /// cuerpo de la respuesta, separados por <c>|</c>, como texto plano.
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

        public static async Task<ForwardedHeadersHostHarness> StartAsync(ForwardedHeadersOptions options)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
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

            app.Run(async context =>
            {
                await context.Response.WriteAsync($"{context.Request.Scheme}|{context.Request.Host}");
            });

            await app.StartAsync();

            var address = app.Urls.First();
            var client = new HttpClient { BaseAddress = new Uri(address) };
            return new ForwardedHeadersHostHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
