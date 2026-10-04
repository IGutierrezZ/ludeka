using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ludeka.Web.Endpoints;

public static class AffiliateRedirectEndpoints
{
    public static IEndpointRouteBuilder MapAffiliateRedirectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Ruta canónica limpia por slugs: /r/comprar/wingspan/zacatrus o /r/wingspan/zacatrus
        endpoints.MapGet("/r/{gameSlug}/{storeSlug}", HandleGameStoreRedirectAsync)
            .AllowAnonymous()
            .WithName("AffiliateGameStoreRedirect");

        endpoints.MapGet("/r/comprar/{gameSlug}/{storeSlug}", HandleGameStoreRedirectAsync)
            .AllowAnonymous()
            .WithName("AffiliateBuyGameStoreRedirect");

        // Ruta de redirección directa con comprobación de seguridad Open Redirect: /r/deal?url=...&store=...
        endpoints.MapGet("/r/deal", async (
            [FromQuery] string? url,
            [FromQuery] string? store,
            [FromQuery] string? gameSlug,
            [FromQuery] string? gameTitle,
            [FromQuery] string? country,
            [FromServices] IAffiliateClickService clickService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(store))
            {
                return Results.Redirect("/catalogo");
            }

            var targetUrl = await clickService.TrackDirectRedirectAsync(
                gameId: null,
                gameSlug: gameSlug,
                gameTitle: gameTitle ?? gameSlug ?? string.Empty,
                storeName: store,
                targetUrl: url,
                country: country,
                ct: ct
            );

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                // Rechazado por seguridad (dominio no autorizado)
                return Results.BadRequest(new { error = "Dominio o enlace de tienda no autorizado." });
            }

            return Results.Redirect(targetUrl, permanent: false);
        }).AllowAnonymous().WithName("AffiliateDirectDealRedirect");

        return endpoints;
    }

    private static async Task<IResult> HandleGameStoreRedirectAsync(
        string gameSlug,
        string storeSlug,
        [FromQuery] string? country,
        [FromServices] IAffiliateClickService clickService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(gameSlug) || string.IsNullOrWhiteSpace(storeSlug))
        {
            return Results.Redirect("/catalogo");
        }

        var targetUrl = await clickService.ResolveAndTrackRedirectAsync(
            gameSlug: gameSlug,
            storeSlug: storeSlug,
            country: country,
            ct: ct
        );

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            // Fallback elegante a la propia ficha del juego si no se pudo resolver
            return Results.Redirect($"/juego/{Uri.EscapeDataString(gameSlug)}");
        }

        return Results.Redirect(targetUrl, permanent: false);
    }
}
