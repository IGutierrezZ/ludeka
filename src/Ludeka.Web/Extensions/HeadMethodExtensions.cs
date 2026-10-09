using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Ludeka.Web.Extensions;

/// <summary>
/// Proporciona soporte transparente para peticiones HTTP HEAD en todo el pipeline de Ludeka,
/// evitando respuestas 405 Method Not Allowed ante validadores y crawlers de afiliación (Awin, Google, etc.).
/// Reescribe internamente el método a GET para el enrutamiento de Blazor SSR y endpoints, y descarta
/// el cuerpo de la respuesta con Stream.Null para devolver cabeceras 200 OK con 0 bytes de cuerpo.
/// </summary>
public static class HeadMethodExtensions
{
    public static IApplicationBuilder UseHeadMethodSupport(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (HttpMethods.IsHead(context.Request.Method))
            {
                context.Request.Method = HttpMethods.Get;
                var originalBodyStream = context.Response.Body;
                context.Response.Body = Stream.Null;
                try
                {
                    await next();
                }
                finally
                {
                    context.Request.Method = HttpMethods.Head;
                    context.Response.Body = originalBodyStream;
                }
            }
            else
            {
                await next();
            }
        });
    }
}
