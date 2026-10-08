using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Extractors;

/// <summary>
/// Extractor para los próximos lanzamientos de Devir Iberia (https://devir.es/proximos-lanzamientos).
/// </summary>
public partial class DevirReleasesExtractor : IDevirReleasesExtractor
{
    private const string DefaultDevirUrl = "https://devir.es/proximos-lanzamientos";
    private readonly HttpClient _httpClient;
    private readonly ILogger<DevirReleasesExtractor> _logger;

    public DevirReleasesExtractor(HttpClient httpClient, ILogger<DevirReleasesExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Descargando calendario de próximos lanzamientos de Devir desde '{Url}'...", DefaultDevirUrl);
            var html = await _httpClient.GetStringAsync(DefaultDevirUrl, ct).ConfigureAwait(false);
            var items = ParseHtml(html);

            // Enriquecer con galería de producto (mesa y contraportada) para aquellos ítems con SourceUrl de ficha de producto
            var enrichedItems = new List<EditorialReleaseItem>(items.Count);
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.SourceUrl) &&
                    item.SourceUrl.StartsWith("https://devir.es/", StringComparison.OrdinalIgnoreCase) &&
                    !item.SourceUrl.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var gallery = await ExtractProductGalleryAsync(item.SourceUrl, ct).ConfigureAwait(false);
                        if (gallery != null)
                        {
                            var enriched = item with
                            {
                                CoverImageUrl = gallery.CoverImageUrl ?? item.CoverImageUrl,
                                TableImageUrl = gallery.TableImageUrl ?? item.TableImageUrl,
                                BackCoverImageUrl = gallery.BackCoverImageUrl ?? item.BackCoverImageUrl,
                                Ean = !string.IsNullOrWhiteSpace(gallery.Ean) ? gallery.Ean : item.Ean,
                                EstimatedPvp = item.EstimatedPvp ?? gallery.Pvp
                            };
                            enrichedItems.Add(enriched);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No se pudo extraer la galería para '{Title}' desde '{Url}'.", item.Title, item.SourceUrl);
                    }
                }

                enrichedItems.Add(item);
            }

            return enrichedItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer los lanzamientos de Devir Iberia.");
            return Array.Empty<EditorialReleaseItem>();
        }
    }

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html)
        => ParseHtml(html, null);

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html, DateOnly? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Array.Empty<EditorialReleaseItem>();

        var results = new List<EditorialReleaseItem>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var currentMonthStart = referenceDate ?? new DateOnly(now.Year, now.Month, 1);

        // 1. Dividir por secciones si existen cabeceras principales (ej. "Octubre 2026 - Juegos de mesa", "Noviembre 2026 - Juegos de rol", etc.)
        var sectionHeaders = SectionHeaderRegex().Matches(html);

        if (sectionHeaders.Count > 0)
        {
            for (int i = 0; i < sectionHeaders.Count; i++)
            {
                var match = sectionHeaders[i];
                var headerText = match.Groups["header"].Value.Trim();

                // Descartar explícitamente secciones que sean de Juegos de Rol, no sean Juegos de Mesa o estén en desarrollo
                if (headerText.Contains("juegos de rol", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("rol", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("rpg", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("desarrollo", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (sectionDate, isMonthOnly) = ParseSectionDate(headerText);
                if (!sectionDate.HasValue)
                {
                    continue;
                }

                // Descartar meses anteriores al mes de referencia actual
                if (sectionDate.Value < currentMonthStart)
                {
                    continue;
                }

                int startIdx = match.Index;
                int endIdx = (i + 1 < sectionHeaders.Count) ? sectionHeaders[i + 1].Index : html.Length;
                string sectionHtml = html.Substring(startIdx, endIdx - startIdx);

                ParseSectionItems(sectionHtml, headerText, sectionDate, isMonthOnly, currentMonthStart, results, seenTitles);
            }
        }
        else
        {
            ParseSectionItems(html, null, null, false, currentMonthStart, results, seenTitles);
        }

        _logger.LogInformation("Lanzamientos válidos de juegos de mesa extraídos de Devir: {Count}", results.Count);
        return results;
    }

    private void ParseSectionItems(
        string sectionHtml,
        string? headerText,
        DateOnly? sectionDate,
        bool sectionIsMonthOnly,
        DateOnly currentMonthStart,
        List<EditorialReleaseItem> results,
        HashSet<string> seenTitles)
    {
        int countBefore = results.Count;

        // 1. Formato A: Productos detallados con Precio (ej. "Precio: 25€")
        ParseDetailedPriceItems(sectionHtml, headerText, sectionDate, sectionIsMonthOnly, currentMonthStart, results, seenTitles);

        int detailedCount = results.Count - countBefore;

        // 2. Formato B: Tarjetas simples SOLO si la sección no contenía productos detallados con precio
        if (detailedCount == 0)
        {
            ParseTileCardItems(sectionHtml, headerText, sectionDate, sectionIsMonthOnly, currentMonthStart, results, seenTitles);
        }
    }

    private void ParseDetailedPriceItems(
        string sectionHtml,
        string? headerText,
        DateOnly? sectionDate,
        bool sectionIsMonthOnly,
        DateOnly currentMonthStart,
        List<EditorialReleaseItem> results,
        HashSet<string> seenTitles)
    {
        var priceMatches = PriceRegex().Matches(sectionHtml);
        foreach (Match priceMatch in priceMatches)
        {
            int priceIdx = priceMatch.Index;

            // Retroceder hasta encontrar el inicio del bloque de texto o columna (hasta 1500 chars)
            int searchStart = Math.Max(0, priceIdx - 1500);
            string precedingHtml = sectionHtml.Substring(searchStart, priceIdx - searchStart);

            // Descartar si dentro del bloque se menciona juego de rol o libro básico
            if (precedingHtml.Contains("Juego de rol", StringComparison.OrdinalIgnoreCase) ||
                precedingHtml.Contains("Juegos de rol", StringComparison.OrdinalIgnoreCase) ||
                precedingHtml.Contains("Libro básico", StringComparison.OrdinalIgnoreCase) ||
                precedingHtml.Contains("Pantalla del director", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extraer título del bloque precedente
            string? title = ExtractTitleFromPreceding(precedingHtml);
            if (string.IsNullOrWhiteSpace(title) || int.TryParse(title, out _) || !IsValidGameTitle(title) || !seenTitles.Add(title))
            {
                continue;
            }

            // Extraer precio
            decimal? price = null;
            if (decimal.TryParse(priceMatch.Groups["price"].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            {
                price = p;
            }

            // Buscar la imagen más cercana antes del bloque de texto
            string? imgSrc = null;
            string? productUrl = null;
            string? ean = null;

            var imgMatch = PrecedingImageRegex().Matches(precedingHtml);
            if (imgMatch.Count > 0)
            {
                var lastImg = imgMatch[imgMatch.Count - 1];
                Match chosenImg = lastImg;

                // Buscar si entre las imágenes contiguas a la del producto (dentro de 400 chars) hay una con face3d/3d/caja
                for (int i = imgMatch.Count - 1; i >= 0; i--)
                {
                    var m = imgMatch[i];
                    if (lastImg.Index - m.Index > 400)
                    {
                        break; // Pertenece a un producto anterior en el HTML precedente
                    }

                    var src = m.Groups["img"].Value;
                    if (src.Contains("face3d", StringComparison.OrdinalIgnoreCase) ||
                        src.Contains("3d", StringComparison.OrdinalIgnoreCase) ||
                        src.Contains("caja", StringComparison.OrdinalIgnoreCase))
                    {
                        chosenImg = m;
                        break;
                    }
                }

                imgSrc = chosenImg.Groups["img"].Value.Trim();
                if (chosenImg.Groups["url"].Success)
                {
                    var u = chosenImg.Groups["url"].Value.Trim();
                    if (u.StartsWith('/'))
                    {
                        u = "https://devir.es" + u;
                    }
                    if (!u.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                    {
                        productUrl = u;
                    }
                }

                if (string.IsNullOrWhiteSpace(productUrl))
                {
                    for (int i = imgMatch.Count - 1; i >= 0; i--)
                    {
                        var m = imgMatch[i];
                        if (lastImg.Index - m.Index > 400) break;

                        if (m.Groups["url"].Success)
                        {
                            var u = m.Groups["url"].Value.Trim();
                            if (u.StartsWith('/')) u = "https://devir.es" + u;
                            if (!u.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                            {
                                productUrl = u;
                                break;
                            }
                        }
                    }
                }

                var eanM = EanRegex().Match(imgSrc);
                if (eanM.Success)
                {
                    ean = eanM.Groups[1].Value;
                }
            }

            if (string.IsNullOrWhiteSpace(productUrl))
            {
                var linkM = Regex.Match(precedingHtml, @"href=""(?<url>(?:https://devir\.es)?/[^""]+)""", RegexOptions.IgnoreCase);
                if (linkM.Success)
                {
                    var u = linkM.Groups["url"].Value.Trim();
                    if (u.StartsWith('/'))
                    {
                        u = "https://devir.es" + u;
                    }
                    if (!u.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                    {
                        productUrl = u;
                    }
                }
            }

            bool isReprint = precedingHtml.Contains("REIMPRESIÓN", StringComparison.OrdinalIgnoreCase) ||
                             precedingHtml.Contains("REIMPRESION", StringComparison.OrdinalIgnoreCase);

            var item = new EditorialReleaseItem(
                Title: title,
                Publisher: "Devir",
                ReleaseDate: sectionDate,
                TargetDateText: headerText ?? (sectionDate?.ToString("yyyy-MM") ?? null),
                EstimatedPvp: price,
                Ean: ean,
                CoverImageUrl: imgSrc,
                Notes: headerText != null ? $"Próximos lanzamientos Devir ({headerText})" : "Próximos lanzamientos Devir",
                SourceUrl: !string.IsNullOrWhiteSpace(productUrl) ? productUrl : DefaultDevirUrl,
                IsReprint: isReprint,
                IsMonthOnly: sectionIsMonthOnly || (sectionDate.HasValue && sectionDate.Value.Day == 1));

            results.Add(item);
        }
    }

    private void ParseTileCardItems(
        string sectionHtml,
        string? headerText,
        DateOnly? sectionDate,
        bool sectionIsMonthOnly,
        DateOnly currentMonthStart,
        List<EditorialReleaseItem> results,
        HashSet<string> seenTitles)
    {
        var cardMatches = TileCardRegex().Matches(sectionHtml);
        foreach (Match match in cardMatches)
        {
            var titleText = match.Groups["title"].Value.Trim();
            var dateText = match.Groups["date"].Value.Trim();
            var imgSrc = match.Groups["img"].Value.Trim();

            titleText = Regex.Replace(titleText, "<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(titleText) || 
                int.TryParse(titleText, out _) ||
                !IsValidGameTitle(titleText))
            {
                continue;
            }

            if (!seenTitles.Add(titleText))
            {
                continue;
            }

            // Extraer EAN si el nombre de archivo contiene EAN-13
            string? ean = null;
            var eanMatch = EanRegex().Match(imgSrc);
            if (eanMatch.Success)
            {
                ean = eanMatch.Groups[1].Value;
            }

            var (parsedDate, isMonthOnly) = ParseCardDate(dateText, sectionDate, sectionIsMonthOnly);
            if (!parsedDate.HasValue || parsedDate.Value < currentMonthStart)
            {
                continue;
            }

            string? productUrl = null;
            if (match.Groups["url"].Success)
            {
                var u = match.Groups["url"].Value.Trim();
                if (u.StartsWith('/'))
                {
                    u = "https://devir.es" + u;
                }
                if (!u.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                {
                    productUrl = u;
                }
            }

            if (string.IsNullOrWhiteSpace(productUrl))
            {
                var linkMatch = Regex.Match(match.Value, @"href=""(?<url>(?:https://devir\.es)?/[^""]+)""", RegexOptions.IgnoreCase);
                if (linkMatch.Success)
                {
                    var u = linkMatch.Groups["url"].Value.Trim();
                    if (u.StartsWith('/'))
                    {
                        u = "https://devir.es" + u;
                    }
                    if (!u.EndsWith("/proximos-lanzamientos", StringComparison.OrdinalIgnoreCase))
                    {
                        productUrl = u;
                    }
                }
            }

            var item = new EditorialReleaseItem(
                Title: titleText,
                Publisher: "Devir",
                ReleaseDate: parsedDate,
                TargetDateText: !string.IsNullOrWhiteSpace(dateText) ? dateText : headerText,
                EstimatedPvp: null,
                Ean: ean,
                CoverImageUrl: string.IsNullOrWhiteSpace(imgSrc) ? null : imgSrc,
                Notes: headerText != null ? $"Próximos lanzamientos Devir ({headerText})" : "Próximos lanzamientos Devir",
                SourceUrl: !string.IsNullOrWhiteSpace(productUrl) ? productUrl : DefaultDevirUrl,
                IsReprint: false,
                IsMonthOnly: isMonthOnly);

            results.Add(item);
        }
    }

    private static string? ExtractTitleFromPreceding(string precedingHtml)
    {
        // 1. Título en span grande font-size: 24px o 20px (tomar el último antes del precio)
        var bigSpanMatches = BigTitleRegex().Matches(precedingHtml);
        for (int i = bigSpanMatches.Count - 1; i >= 0; i--)
        {
            var raw = Regex.Replace(bigSpanMatches[i].Groups["title"].Value, "<[^>]+>", "").Trim();
            if (!string.IsNullOrWhiteSpace(raw) && IsValidGameTitle(raw))
            {
                return raw;
            }
        }

        // 2. Título en <strong> dentro de los últimos párrafos
        var strongMatches = StrongTagRegex().Matches(precedingHtml);
        for (int i = strongMatches.Count - 1; i >= 0; i--)
        {
            var candidate = Regex.Replace(strongMatches[i].Groups["text"].Value, "<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(candidate) || !IsValidGameTitle(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool IsValidGameTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.Trim();
        if (t.Length < 2) return false;

        if (t.Equals("Juego de mesa", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Juegos de mesa", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("NOVEDAD", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("REIMPRESIÓN", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("REIMPRESION", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Juego de rol", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Juegos de rol", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Libro básico", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Libro basico", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (t.StartsWith("Autor", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Ilustrador", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Editorial", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Precio", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Libro", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Pantalla", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Tipo:", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Edad:", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Nº Jugadores", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("Tiempo", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith(":"))
        {
            return false;
        }

        return true;
    }

    public async Task<DevirProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl) || !productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var html = await _httpClient.GetStringAsync(productUrl, timeoutCts.Token).ConfigureAwait(false);
            return ParseProductGalleryHtml(html);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo extraer la galería de producto de Devir desde '{Url}'.", productUrl);
            return null;
        }
    }

    public DevirProductGalleryDto? ParseProductGalleryHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        string? coverUrl = null;
        string? tableUrl = null;
        string? backCoverUrl = null;
        string? frontFlatUrl = null;
        string? ean = null;
        decimal? pvp = null;

        // 1. Extraer JSON de galería Magento
        var galleryMatch = GalleryScriptRegex().Match(html);
        if (galleryMatch.Success)
        {
            try
            {
                var jsonText = galleryMatch.Groups["json"].Value;
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("[data-gallery-role=gallery-placeholder]", out var ph) &&
                    ph.TryGetProperty("mage/gallery/gallery", out var mg) &&
                    mg.TryGetProperty("data", out var dataArr) &&
                    dataArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var imgObj in dataArr.EnumerateArray())
                    {
                        var full = imgObj.TryGetProperty("full", out var f) ? f.GetString() : null;
                        var img = imgObj.TryGetProperty("img", out var im) ? im.GetString() : null;
                        var targetUrl = !string.IsNullOrWhiteSpace(full) ? full : img;
                        if (string.IsNullOrWhiteSpace(targetUrl)) continue;

                        if (targetUrl.Contains("face3d", StringComparison.OrdinalIgnoreCase) ||
                            targetUrl.Contains("3d", StringComparison.OrdinalIgnoreCase) ||
                            targetUrl.Contains("caja", StringComparison.OrdinalIgnoreCase))
                        {
                            coverUrl = targetUrl;
                        }
                        else if (targetUrl.Contains("components", StringComparison.OrdinalIgnoreCase) ||
                                 targetUrl.Contains("mesa", StringComparison.OrdinalIgnoreCase) ||
                                 targetUrl.Contains("contenido", StringComparison.OrdinalIgnoreCase))
                        {
                            tableUrl = targetUrl;
                        }
                        else if (targetUrl.Contains("backflat", StringComparison.OrdinalIgnoreCase) ||
                                 targetUrl.Contains("trasera", StringComparison.OrdinalIgnoreCase) ||
                                 targetUrl.Contains("contra", StringComparison.OrdinalIgnoreCase))
                        {
                            backCoverUrl = targetUrl;
                        }
                        else if (targetUrl.Contains("frontflat", StringComparison.OrdinalIgnoreCase))
                        {
                            frontFlatUrl = targetUrl;
                        }

                        if (coverUrl == null && imgObj.TryGetProperty("isMain", out var isMain) && isMain.GetBoolean())
                        {
                            coverUrl = targetUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error al parsear el JSON de galería de producto Devir.");
            }
        }

        // 2. Extraer EAN (del HTML o SKU)
        var eanM = EanRegex().Match(html);
        if (eanM.Success)
        {
            ean = eanM.Groups[1].Value;
        }

        // 3. Extraer PVP
        var pvpM = ProductPriceRegex().Match(html);
        if (pvpM.Success)
        {
            var priceStr = pvpM.Groups["price"].Value.Replace(',', '.');
            if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            {
                pvp = p;
            }
        }

        if (coverUrl == null && tableUrl == null && backCoverUrl == null && frontFlatUrl == null && ean == null && !pvp.HasValue)
        {
            return null;
        }

        return new DevirProductGalleryDto(coverUrl, tableUrl, backCoverUrl, frontFlatUrl, ean, pvp);
    }

    public async Task<DevirCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
    {
        var targetUrl = page <= 1 
            ? "https://devir.es/catalogo/juegos-de-mesa" 
            : $"https://devir.es/catalogo/juegos-de-mesa?p={page}";

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} al obtener la página {Page} del catálogo de Devir.", response.StatusCode, page);
                return new DevirCatalogPageResultDto(Array.Empty<DevirCatalogItemDto>(), false);
            }

            var html = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return ParseCatalogPageHtml(html);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Error al extraer la página {Page} del catálogo general de Devir.", page);
            return new DevirCatalogPageResultDto(Array.Empty<DevirCatalogItemDto>(), false);
        }
    }

    public DevirCatalogPageResultDto ParseCatalogPageHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new DevirCatalogPageResultDto(Array.Empty<DevirCatalogItemDto>(), false);
        }

        var items = new List<DevirCatalogItemDto>();
        var matches = CatalogCardRegex().Matches(html);

        foreach (Match match in matches)
        {
            var productUrl = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(productUrl)) continue;

            var rawAlt = match.Groups["alt"].Value;
            var title = !string.IsNullOrWhiteSpace(rawAlt)
                ? WebUtility.HtmlDecode(rawAlt).Trim()
                : null;

            var imageUrl = match.Groups["img"].Value.Trim();
            string? ean = null;
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                var eanMatch = EanRegex().Match(imageUrl);
                if (eanMatch.Success)
                {
                    ean = eanMatch.Groups[1].Value;
                }
            }

            items.Add(new DevirCatalogItemDto(
                ProductUrl: productUrl,
                Title: title,
                Ean: ean,
                CoverImageUrl: !string.IsNullOrWhiteSpace(imageUrl) ? imageUrl : null));
        }

        bool hasNextPage = html.Contains("pages-item-next", StringComparison.OrdinalIgnoreCase) ||
                           html.Contains("class=\"action  next\"", StringComparison.OrdinalIgnoreCase);

        return new DevirCatalogPageResultDto(items, hasNextPage);
    }

    private static (DateOnly? Date, bool IsMonthOnly) ParseCardDate(string dateText, DateOnly? fallbackDate, bool fallbackIsMonthOnly)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return (fallbackDate, fallbackIsMonthOnly);

        dateText = dateText.Trim();

        // Descartar si el texto de fecha indica "desarrollo"
        if (dateText.Contains("desarrollo", StringComparison.OrdinalIgnoreCase))
        {
            return (null, false);
        }

        // Mes y año: "Noviembre 2026"
        var m = MonthYearRegex().Match(dateText);
        if (m.Success)
        {
            string monthStr = m.Groups["month"].Value.ToLowerInvariant();
            if (int.TryParse(m.Groups["year"].Value, out int y))
            {
                int monthNum = MonthNameToNumber(monthStr);
                if (monthNum > 0)
                {
                    return (new DateOnly(y, monthNum, 1), true);
                }
            }
        }

        // Si la tarjeta solo tiene un año numérico (ej. "2026", "2027") sin mes,
        // no se acepta salvo que la sección padre (fallbackDate) tenga un mes cerrado válido.
        if (fallbackDate.HasValue && fallbackDate.Value.Month > 0)
        {
            return (fallbackDate, fallbackIsMonthOnly);
        }

        return (null, false);
    }

    private static (DateOnly? Date, bool IsMonthOnly) ParseSectionDate(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return (null, false);

        if (header.Contains("desarrollo", StringComparison.OrdinalIgnoreCase))
            return (null, false);

        var m = MonthYearRegex().Match(header);
        if (m.Success)
        {
            string monthStr = m.Groups["month"].Value.ToLowerInvariant();
            if (int.TryParse(m.Groups["year"].Value, out int y))
            {
                int monthNum = MonthNameToNumber(monthStr);
                if (monthNum > 0)
                {
                    return (new DateOnly(y, monthNum, 1), true);
                }
            }
        }

        // Si solo contiene año (ej. "2027") sin mes, no se considera una sección con mes cerrado
        return (null, false);
    }

    private static int MonthNameToNumber(string month) => month switch
    {
        "enero" => 1,
        "febrero" => 2,
        "marzo" => 3,
        "abril" => 4,
        "mayo" => 5,
        "junio" => 6,
        "julio" => 7,
        "agosto" => 8,
        "septiembre" => 9,
        "octubre" => 10,
        "noviembre" => 11,
        "diciembre" => 12,
        _ => 0
    };

    [GeneratedRegex(@"<span[^>]*font-size:\s*38px[^>]*>\s*<strong>(?<header>[^<]+)</strong>", RegexOptions.IgnoreCase)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(@"Precio:\s*(?:</strong>)?\s*(?<price>\d+(?:[.,]\d+)?)\s*€", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"(?:<a[^>]+href=""(?<url>(?:https://devir\.es)?/[^""]+)""[^>]*>\s*)?<img[^>]+src=""(?<img>https?://[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PrecedingImageRegex();

    [GeneratedRegex(@"<span[^>]*font-size:\s*(?:24|20)px[^>]*><strong>(?:<span[^>]*>)?(?<title>[^<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BigTitleRegex();

    [GeneratedRegex(@"<strong>(?<text>[^<]+)</strong>", RegexOptions.IgnoreCase)]
    private static partial Regex StrongTagRegex();

    [GeneratedRegex(@"(?:<a[^>]+href=""(?<url>(?:https://devir\.es)?/[^""]+)""[^>]*>\s*)?<img[^>]+src=""(?<img>[^""]*(?:Proximos-lanzamientos|product)/[^""]+)"".*?alt=""(?<alt>[^""]*)"".*?<p[^>]*>.*?<strong>(?<date>[^<]+)</strong>.*?</p>.*?<p[^>]*>.*?<strong>(?<title>[^<]+)</strong>.*?</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TileCardRegex();

    [GeneratedRegex(@"\b(84\d{11})\b")]
    private static partial Regex EanRegex();

    [GeneratedRegex(@"(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+(?<year>20\d\d)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthYearRegex();

    [GeneratedRegex(@"<script[^>]*type=""text/x-magento-init""[^>]*>\s*(?<json>\{.*?data-gallery-role=gallery-placeholder.*?)\s*</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex GalleryScriptRegex();

    [GeneratedRegex(@"(?:data-price-amount=""(?<price>\d+(?:\.\d+)?)"")|(?:<span[^>]*class=""price""[^>]*>(?<price>\d+(?:[.,]\d+)?)(?:\s|&nbsp;)*€)", RegexOptions.IgnoreCase)]
    private static partial Regex ProductPriceRegex();

    [GeneratedRegex(@"<a[^>]+href=""(?<url>https://devir\.es/[^""]+)""[^>]*class=""product photo product-item-photo""[^>]*>[\s\S]*?<img[^>]*src=""(?<img>[^""]+)""[^>]*alt=""(?<alt>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex CatalogCardRegex();
}
