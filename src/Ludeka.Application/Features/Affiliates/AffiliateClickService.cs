using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Affiliates;

public class AffiliateClickService : IAffiliateClickService
{
    private readonly IGameRepository _gameRepository;
    private readonly IAffiliateUrlResolver _affiliateResolver;
    private readonly IAffiliateClickRepository _clickRepository;
    private readonly ILogger<AffiliateClickService> _logger;

    public AffiliateClickService(
        IGameRepository gameRepository,
        IAffiliateUrlResolver affiliateResolver,
        IAffiliateClickRepository clickRepository,
        ILogger<AffiliateClickService> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _affiliateResolver = affiliateResolver ?? throw new ArgumentNullException(nameof(affiliateResolver));
        _clickRepository = clickRepository ?? throw new ArgumentNullException(nameof(clickRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> ResolveAndTrackRedirectAsync(
        string gameSlug,
        string storeSlug,
        string? country = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameSlug) || string.IsNullOrWhiteSpace(storeSlug))
            return null;

        var cleanGameSlug = gameSlug.Trim().ToLowerInvariant();
        var cleanStoreSlug = storeSlug.Trim().ToLowerInvariant();

        var game = await _gameRepository.GetBySlugAsync(cleanGameSlug, ct);
        if (game == null)
        {
            _logger.LogWarning("Redirección de afiliado fallida: Juego no encontrado para slug '{GameSlug}'", cleanGameSlug);
            return null;
        }

        // 1. Buscar si el juego tiene oferta explícita para esta tienda
        var matchingOffer = game.PurchaseLinks.FirstOrDefault(o =>
            Game.GenerateSlug(o.StoreName) == cleanStoreSlug ||
            o.StoreName.Equals(cleanStoreSlug, StringComparison.OrdinalIgnoreCase));

        string targetUrl;
        string storeDisplayName;

        if (matchingOffer != null && !string.IsNullOrWhiteSpace(matchingOffer.AffiliateUrl))
        {
            storeDisplayName = matchingOffer.StoreName;
            targetUrl = _affiliateResolver.ResolveAffiliateUrl(matchingOffer.AffiliateUrl, matchingOffer.StoreName);
        }
        else
        {
            // 2. Fallback: búsqueda asistida en la tienda colaboradora para este juego
            storeDisplayName = cleanStoreSlug switch
            {
                "amazon" => "Amazon",
                "zacatrus" => "Zacatrus",
                "cuarto-de-juegos" or "cuartodejuegos" => "Cuarto de Juegos",
                "dungeon-marvels" or "dungeonmarvels" => "Dungeon Marvels",
                "tablerum" => "Tablerum",
                "mathom" => "Mathom",
                "dracotienda" => "Dracotienda",
                "jugamos-otra" or "jugamosotra" => "Jugamos Otra",
                _ => cleanStoreSlug
            };

            targetUrl = _affiliateResolver.BuildSearchUrl(storeDisplayName, game.SpanishTitle);
        }

        // Registrar clic para auditoría y analítica
        try
        {
            var log = new AffiliateClickLog(
                gameId: game.Id,
                gameTitle: game.SpanishTitle,
                gameSlug: game.Slug,
                storeName: storeDisplayName,
                targetUrl: targetUrl,
                country: country
            );
            await _clickRepository.RecordClickAsync(log, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error no bloqueante al registrar clic de afiliado para {GameSlug} en {Store}", game.Slug, storeDisplayName);
        }

        return targetUrl;
    }

    public async Task<string?> TrackDirectRedirectAsync(
        Guid? gameId,
        string? gameSlug,
        string gameTitle,
        string storeName,
        string targetUrl,
        string? country = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
            return null;

        // Comprobación de seguridad contra Open Redirect
        if (!_affiliateResolver.IsAllowedStoreUrl(targetUrl))
        {
            _logger.LogWarning("Redirección rechazada por seguridad: Dominio no autorizado en URL '{Url}'", targetUrl);
            return null;
        }

        var enrichedUrl = _affiliateResolver.ResolveAffiliateUrl(targetUrl, storeName);

        try
        {
            var log = new AffiliateClickLog(
                gameId: gameId,
                gameTitle: gameTitle,
                gameSlug: gameSlug ?? string.Empty,
                storeName: storeName,
                targetUrl: enrichedUrl,
                country: country
            );
            await _clickRepository.RecordClickAsync(log, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error no bloqueante al registrar clic directo para {Store}", storeName);
        }

        return enrichedUrl;
    }
}
