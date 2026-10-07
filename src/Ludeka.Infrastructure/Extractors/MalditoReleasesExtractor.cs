using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Extractors;

/// <summary>
/// Extractor para los próximos lanzamientos y preventas de Maldito Games (https://tienda.malditogames.com/).
/// </summary>
public partial class MalditoReleasesExtractor : IMalditoReleasesExtractor
{
    private const string DefaultMalditoHomeUrl = "https://tienda.malditogames.com/";
    private const string DefaultMalditoCatalogUrl = "https://tienda.malditogames.com/juegos?product_list_order=creation_time";

    private readonly HttpClient _httpClient;
    private readonly ILogger<MalditoReleasesExtractor> _logger;

    public MalditoReleasesExtractor(HttpClient httpClient, ILogger<MalditoReleasesExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Descargando novedades de Maldito Games desde portada y catálogo...");
            var homeHtmlTask = _httpClient.GetStringAsync(DefaultMalditoHomeUrl, ct);
            var catalogHtmlTask = _httpClient.GetStringAsync(DefaultMalditoCatalogUrl, ct);

            await Task.WhenAll(homeHtmlTask, catalogHtmlTask).ConfigureAwait(false);

            return ParseHtml(await homeHtmlTask.ConfigureAwait(false), await catalogHtmlTask.ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer los lanzamientos de Maldito Games.");
            return Array.Empty<EditorialReleaseItem>();
        }
    }

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null)
    {
        var itemsMap = new Dictionary<string, EditorialReleaseItem>(StringComparer.OrdinalIgnoreCase);

        // 1. Extraer juegos de la sección "Se viene" en la portada
        if (!string.IsNullOrWhiteSpace(homeHtml))
        {
            var matches = SeVieneRegex().Matches(homeHtml);
            _logger.LogInformation("Juegos encontrados en 'Se viene' de Maldito: {Count}", matches.Count);

            foreach (Match match in matches)
            {
                var imgSrc = match.Groups["img"].Value.Trim();
                var rawDate = match.Groups["date"].Value;
                var dateText = CleanHtml(rawDate);

                var title = InferTitleFromFilename(imgSrc);
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                DateOnly? releaseDate = null;
                bool isMonthOnly = false;
                if (int.TryParse(dateText, out int y) && y >= 2024 && y <= 2035)
                {
                    releaseDate = new DateOnly(y, 1, 1);
                    isMonthOnly = true;
                }

                itemsMap[title] = new EditorialReleaseItem(
                    Title: title,
                    Publisher: "Maldito Games",
                    ReleaseDate: releaseDate,
                    TargetDateText: dateText,
                    EstimatedPvp: null,
                    Ean: null,
                    CoverImageUrl: imgSrc,
                    Notes: "Próximamente en Maldito Games (Se viene)",
                    SourceUrl: DefaultMalditoHomeUrl,
                    IsReprint: false,
                    IsMonthOnly: isMonthOnly);
            }
        }

        // 2. Extraer novedades y preventas recientes del catálogo con PVP
        if (!string.IsNullOrWhiteSpace(catalogHtml))
        {
            var catalogMatches = CatalogProductRegex().Matches(catalogHtml);
            _logger.LogInformation("Productos encontrados en catálogo reciente de Maldito: {Count}", catalogMatches.Count);

            foreach (Match match in catalogMatches)
            {
                var productUrl = match.Groups["url"].Value.Trim();
                var rawTitle = match.Groups["title"].Value.Trim();
                var rawPrice = match.Groups["price"].Value.Trim();

                if (string.IsNullOrWhiteSpace(rawTitle))
                    continue;

                decimal? price = ParsePrice(rawPrice);

                // Si ya estaba en "Se viene", enriquecemos con el precio y enlace exacto
                if (itemsMap.TryGetValue(rawTitle, out var existing))
                {
                    itemsMap[rawTitle] = existing with
                    {
                        EstimatedPvp = price ?? existing.EstimatedPvp,
                        SourceUrl = productUrl
                    };
                }
                else
                {
                    itemsMap[rawTitle] = new EditorialReleaseItem(
                        Title: rawTitle,
                        Publisher: "Maldito Games",
                        ReleaseDate: null,
                        TargetDateText: null,
                        EstimatedPvp: price,
                        Ean: null,
                        CoverImageUrl: null,
                        Notes: "Novedad en catálogo de Maldito Games",
                        SourceUrl: productUrl,
                        IsReprint: false,
                        IsMonthOnly: false);
                }
            }
        }

        return new List<EditorialReleaseItem>(itemsMap.Values);
    }

    private static string InferTitleFromFilename(string imageUrl)
    {
        try
        {
            var uri = new Uri(imageUrl);
            var filename = Path.GetFileNameWithoutExtension(uri.LocalPath);

            // Quitar prefijos comunes como SQ_, SQ-
            if (filename.StartsWith("SQ_", StringComparison.OrdinalIgnoreCase) ||
                filename.StartsWith("SQ-", StringComparison.OrdinalIgnoreCase))
            {
                filename = filename[3..];
            }

            // Quitar sufijos comunes como _1, -ESP
            filename = filename.Replace("-ESP", "", StringComparison.OrdinalIgnoreCase)
                               .Replace("_ESP", "", StringComparison.OrdinalIgnoreCase)
                               .Replace("_1", "", StringComparison.OrdinalIgnoreCase);

            // Reemplazar guiones y barras por espacios
            var title = filename.Replace('-', ' ').Replace('_', ' ').Trim();

            // Capitalizar palabras
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 1)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
                }
                else if (words[i].Length == 1)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]).ToString();
                }
            }

            return string.Join(" ", words);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return Regex.Replace(html, "<.*?>", " ").Trim();
    }

    private static decimal? ParsePrice(string rawPrice)
    {
        if (string.IsNullOrWhiteSpace(rawPrice))
            return null;

        // Limpiar símbolos de moneda y espacios: "27,00 €" -> "27.00"
        var cleaned = rawPrice.Replace("€", "")
                              .Replace("&nbsp;", "")
                              .Replace(" ", "")
                              .Trim();

        cleaned = cleaned.Replace(',', '.');

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
        {
            return val;
        }

        return null;
    }

    [GeneratedRegex(@"src=""(?<img>[^""]*media/wysiwyg/SQ[^""]+)"".*?<div class=""fecha-home""[^>]*>(?<date>.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SeVieneRegex();

    [GeneratedRegex(@"<a class=""product-item-link""\s+href=""(?<url>[^""]+)"">\s*(?<title>[^<]+)\s*</a>.*?<span class=""price"">(?<price>[^<]+)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CatalogProductRegex();
}
