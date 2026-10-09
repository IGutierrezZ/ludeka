using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
/// Extractor para los próximos lanzamientos, novedades y preventas de Maldito Games (https://tienda.malditogames.com/).
/// </summary>
public partial class MalditoReleasesExtractor : IMalditoReleasesExtractor
{
    private const string DefaultMalditoHomeUrl = "https://tienda.malditogames.com/";
    private const string DefaultMalditoCatalogUrl = "https://tienda.malditogames.com/juegos";

    private readonly HttpClient _httpClient;
    private readonly ILogger<MalditoReleasesExtractor> _logger;

    public MalditoReleasesExtractor(HttpClient httpClient, ILogger<MalditoReleasesExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Descargando novedades de Maldito Games desde la portada oficial...");

        string? homeHtml = null;
        try
        {
            homeHtml = await FetchHtmlWithRetryAsync(DefaultMalditoHomeUrl, referer: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fallo al obtener la portada de Maldito Games.");
        }

        if (string.IsNullOrWhiteSpace(homeHtml))
        {
            _logger.LogError("No se pudo obtener la portada de Maldito Games.");
            return Array.Empty<EditorialReleaseItem>();
        }

        var releases = ParseHtml(homeHtml, null);

        // Enriquecer con galería de producto (caja 3D, mesa, contraportada, EAN y PVP) para aquellos ítems con SourceUrl de ficha
        var enrichedItems = new List<EditorialReleaseItem>(releases.Count);
        foreach (var item in releases)
        {
            if (ct.IsCancellationRequested) break;

            if (!string.IsNullOrWhiteSpace(item.SourceUrl) &&
                item.SourceUrl.StartsWith("https://tienda.malditogames.com/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.SourceUrl, DefaultMalditoHomeUrl, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var gallery = await ExtractProductGalleryAsync(item.SourceUrl, ct).ConfigureAwait(false);
                    if (gallery != null)
                    {
                        var enriched = item with
                        {
                            CoverImageUrl = gallery.CoverImageUrl ?? item.CoverImageUrl,
                            TableImageUrl = gallery.TableImageUrl ?? gallery.FrontFlatImageUrl ?? item.TableImageUrl,
                            BackCoverImageUrl = gallery.BackCoverImageUrl ?? item.BackCoverImageUrl,
                            Ean = !string.IsNullOrWhiteSpace(gallery.Ean) ? gallery.Ean : item.Ean,
                            EstimatedPvp = gallery.Pvp ?? item.EstimatedPvp
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

    private async Task<string?> FetchHtmlWithRetryAsync(string url, string? referer, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                using var request = CreateBrowserNavRequest(HttpMethod.Get, url, referer);
                var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                }

                _logger.LogWarning("HTTP {StatusCode} al consultar '{Url}' (intento {Attempt}/2).", response.StatusCode, url, attempt);

                if (attempt == 1 && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                {
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    continue;
                }

                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error al consultar '{Url}' (intento {Attempt}/2).", url, attempt);
                if (attempt == 1)
                {
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    continue;
                }

                return null;
            }
        }

        return null;
    }

    private static HttpRequestMessage CreateBrowserNavRequest(HttpMethod method, string targetUrl, string? referer = null)
    {
        var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", referer != null ? "same-origin" : "none");

        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
        {
            request.Headers.Referrer = refUri;
        }

        return request;
    }

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null)
    {
        var itemsMap = new Dictionary<string, EditorialReleaseItem>(StringComparer.OrdinalIgnoreCase);

        // 1. Extraer bloques seccionales de la portada («A puntito de llegar» y «Volverán a estar disponibles»)
        if (!string.IsNullOrWhiteSpace(homeHtml))
        {
            ParseHomeSections(homeHtml, itemsMap);
        }

        // 2. Si se proporciona catálogo, solo enriquecer los productos ya identificados en la portada (no crear novedades ajenas)
        if (!string.IsNullOrWhiteSpace(catalogHtml))
        {
            ParseCatalog(catalogHtml, itemsMap);
        }

        return new List<EditorialReleaseItem>(itemsMap.Values);
    }

    private void ParseHomeSections(string homeHtml, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        var sectionMatches = HomeSectionRegex().Matches(homeHtml);
        _logger.LogInformation("Secciones encontradas en portada de Maldito Games: {Count}", sectionMatches.Count);

        foreach (Match secMatch in sectionMatches)
        {
            var rawSecTitle = secMatch.Groups["title"].Value;
            var secTitle = WebUtility.HtmlDecode(rawSecTitle).Trim();
            var secContent = secMatch.Groups["content"].Value;

            // Descartar explícitamente secciones que ya salieron a la venta ("Últimas novedades") o anuncios lejanos sin datos ("Lo que se viene")
            if (secTitle.Contains("últimas novedades", StringComparison.OrdinalIgnoreCase) ||
                secTitle.Contains("ultimas novedades", StringComparison.OrdinalIgnoreCase) ||
                secTitle.Contains("lo que se viene", StringComparison.OrdinalIgnoreCase) ||
                secTitle.Contains("se viene", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isPuntito = secTitle.Contains("puntito", StringComparison.OrdinalIgnoreCase) ||
                             secTitle.Contains("llegar", StringComparison.OrdinalIgnoreCase);

            bool isReprintSection = secTitle.Contains("volver", StringComparison.OrdinalIgnoreCase) &&
                                    secTitle.Contains("disponible", StringComparison.OrdinalIgnoreCase);

            // Solo procesamos las dos secciones de interés
            if (!isPuntito && !isReprintSection)
            {
                continue;
            }

            ParseProductGridSection(secTitle, secContent, isReprintSection, itemsMap);
        }
    }

    private void ParseProductGridSection(
        string sectionTitle,
        string sectionHtml,
        bool isReprint,
        Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        var itemMatches = ProductItemBlockRegex().Matches(sectionHtml);

        foreach (Match match in itemMatches)
        {
            var itemHtml = match.Value;

            var linkMatch = ProductLinkRegex().Match(itemHtml);
            var title = linkMatch.Success ? CleanTitle(linkMatch.Groups["title"].Value) : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var productUrl = linkMatch.Groups["url"].Value.Trim();
            var imgMatch = ProductImageRegex().Match(itemHtml);
            var imgSrc = imgMatch.Success ? imgMatch.Groups["src"].Value.Trim() : null;

            var fechaMatch = FechaHomeRegex().Match(itemHtml);
            var rawFecha = fechaMatch.Success ? fechaMatch.Groups["fecha"].Value : null;
            var (releaseDate, isMonthOnly) = ParseSpanishDate(rawFecha);
            var dateText = !string.IsNullOrWhiteSpace(rawFecha) ? WebUtility.HtmlDecode(rawFecha).Trim() : null;

            var priceMatch = PriceRegex().Match(itemHtml);
            var rawPrice = priceMatch.Success ? priceMatch.Groups["price"].Value : null;
            decimal? price = ParsePrice(rawPrice);

            string? ean = ExtractEanFromImageUrl(imgSrc);

            string notes = isReprint
                ? "Reimpresión oficial en Maldito Games"
                : $"Lanzamiento en Maldito Games ({sectionTitle})";

            var releaseItem = new EditorialReleaseItem(
                Title: title,
                Publisher: "Maldito Games",
                ReleaseDate: releaseDate,
                TargetDateText: dateText,
                EstimatedPvp: price,
                Ean: ean,
                CoverImageUrl: imgSrc,
                Notes: notes,
                SourceUrl: !string.IsNullOrWhiteSpace(productUrl) ? productUrl : DefaultMalditoHomeUrl,
                IsReprint: isReprint,
                IsMonthOnly: isMonthOnly);

            // Si ya existía, priorizamos la información más completa
            if (itemsMap.TryGetValue(title, out var existing))
            {
                itemsMap[title] = existing with
                {
                    ReleaseDate = releaseDate ?? existing.ReleaseDate,
                    TargetDateText = dateText ?? existing.TargetDateText,
                    EstimatedPvp = price ?? existing.EstimatedPvp,
                    Ean = ean ?? existing.Ean,
                    CoverImageUrl = imgSrc ?? existing.CoverImageUrl,
                    SourceUrl = !string.IsNullOrWhiteSpace(productUrl) ? productUrl : existing.SourceUrl,
                    IsReprint = existing.IsReprint || isReprint,
                    IsMonthOnly = isMonthOnly || existing.IsMonthOnly
                };
            }
            else
            {
                itemsMap[title] = releaseItem;
            }
        }
    }

    private void ParseCatalog(string catalogHtml, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        var catalogMatches = CatalogProductRegex().Matches(catalogHtml);

        foreach (Match match in catalogMatches)
        {
            var productUrl = match.Groups["url"].Value.Trim();
            var rawTitle = match.Groups["title"].Value.Trim();
            var title = CleanTitle(rawTitle);
            var rawPrice = match.Groups["price"].Value.Trim();
            var imgSrc = match.Groups["img"].Value.Trim();

            if (string.IsNullOrWhiteSpace(title))
                continue;

            decimal? price = ParsePrice(rawPrice);
            string? ean = ExtractEanFromImageUrl(imgSrc);

            // Solo enriquecer si ya estaba en las novedades de portada (no añadir productos viejos del catálogo)
            if (itemsMap.TryGetValue(title, out var existing))
            {
                itemsMap[title] = existing with
                {
                    EstimatedPvp = price ?? existing.EstimatedPvp,
                    Ean = ean ?? existing.Ean,
                    CoverImageUrl = existing.CoverImageUrl ?? (!string.IsNullOrWhiteSpace(imgSrc) ? imgSrc : null),
                    SourceUrl = !string.IsNullOrWhiteSpace(productUrl) ? productUrl : existing.SourceUrl
                };
            }
        }
    }

    public async Task<MalditoProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl) || !productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = CreateBrowserNavRequest(HttpMethod.Get, productUrl, DefaultMalditoHomeUrl);
            var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP {StatusCode} al obtener la galería de Maldito Games en '{Url}'.", response.StatusCode, productUrl);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return ParseProductGalleryHtml(html);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo extraer la galería de producto de Maldito Games desde '{Url}'.", productUrl);
            return null;
        }
    }

    public MalditoProductGalleryDto? ParseProductGalleryHtml(string html)
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

                        if (ean == null)
                        {
                            var eanM = ExtractEanFromImageUrl(targetUrl);
                            if (!string.IsNullOrWhiteSpace(eanM))
                            {
                                ean = eanM;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error al parsear el JSON de galería de producto Maldito Games.");
            }
        }

        // Si no se detectó imagen de mesa pero hay frontal plano, usar frontal plano
        tableUrl ??= frontFlatUrl;

        // 2. Extraer EAN desde el SKU del formulario si faltaba
        if (ean == null)
        {
            var skuMatch = ProductSkuRegex().Match(html);
            if (skuMatch.Success)
            {
                var sku = skuMatch.Groups["sku"].Value.Trim();
                if (sku.Length == 13 && long.TryParse(sku, out _))
                {
                    ean = sku;
                }
            }
        }

        // 3. Extraer PVP (Meta property o price span)
        var metaPriceMatch = ProductMetaPriceRegex().Match(html);
        if (metaPriceMatch.Success)
        {
            var pStr = metaPriceMatch.Groups["price"].Value.Replace(',', '.');
            if (decimal.TryParse(pStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            {
                pvp = p;
            }
        }

        if (!pvp.HasValue)
        {
            var priceM = PriceRegex().Match(html);
            if (priceM.Success)
            {
                pvp = ParsePrice(priceM.Groups["price"].Value);
            }
        }

        if (coverUrl == null && tableUrl == null && backCoverUrl == null && frontFlatUrl == null && ean == null && !pvp.HasValue)
        {
            return null;
        }

        return new MalditoProductGalleryDto(coverUrl, tableUrl, backCoverUrl, frontFlatUrl, ean, pvp);
    }

    public async Task<MalditoCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
    {
        var targetUrl = page <= 1
            ? DefaultMalditoCatalogUrl
            : $"{DefaultMalditoCatalogUrl}?p={page}";

        var referer = page > 1 ? DefaultMalditoCatalogUrl : DefaultMalditoHomeUrl;

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                using var request = CreateBrowserNavRequest(HttpMethod.Get, targetUrl, referer);
                var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    return ParseCatalogPageHtml(html);
                }

                _logger.LogWarning("HTTP {StatusCode} al obtener la página {Page} del catálogo de Maldito Games (intento {Attempt}/2).", response.StatusCode, page, attempt);

                if (attempt == 1 && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                {
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    continue;
                }

                return new MalditoCatalogPageResultDto(Array.Empty<MalditoCatalogItemDto>(), HasNextPage: false, Success: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error al extraer la página {Page} del catálogo general de Maldito Games (intento {Attempt}/2).", page, attempt);
                if (attempt == 1)
                {
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    continue;
                }

                return new MalditoCatalogPageResultDto(Array.Empty<MalditoCatalogItemDto>(), HasNextPage: false, Success: false);
            }
        }

        return new MalditoCatalogPageResultDto(Array.Empty<MalditoCatalogItemDto>(), HasNextPage: false, Success: false);
    }

    public MalditoCatalogPageResultDto ParseCatalogPageHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new MalditoCatalogPageResultDto(Array.Empty<MalditoCatalogItemDto>(), false);
        }

        var items = new List<MalditoCatalogItemDto>();
        var itemMatches = ProductItemBlockRegex().Matches(html);

        foreach (Match match in itemMatches)
        {
            var itemHtml = match.Value;

            var linkMatch = ProductLinkRegex().Match(itemHtml);
            if (!linkMatch.Success) continue;

            var productUrl = linkMatch.Groups["url"].Value.Trim();
            var title = CleanTitle(linkMatch.Groups["title"].Value);
            var imgMatch = ProductImageRegex().Match(itemHtml);
            var imgSrc = imgMatch.Success ? imgMatch.Groups["src"].Value.Trim() : null;

            string? ean = ExtractEanFromImageUrl(imgSrc);

            items.Add(new MalditoCatalogItemDto(
                ProductUrl: productUrl,
                Title: !string.IsNullOrWhiteSpace(title) ? title : null,
                Ean: ean,
                CoverImageUrl: imgSrc));
        }

        bool hasNextPage = html.Contains("pages-item-next", StringComparison.OrdinalIgnoreCase) ||
                           Regex.IsMatch(html, @"\baction\s+next\b", RegexOptions.IgnoreCase);

        return new MalditoCatalogPageResultDto(items, hasNextPage);
    }

    public static string? ExtractEanFromImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var match = EanFromImageRegex().Match(imageUrl);
        if (match.Success)
        {
            return match.Groups["ean"].Value;
        }

        var genericMatch = GenericEanRegex().Match(imageUrl);
        if (genericMatch.Success)
        {
            return genericMatch.Groups["ean"].Value;
        }

        return null;
    }

    public static (DateOnly? Date, bool IsMonthOnly) ParseSpanishDate(string? rawDate, int? referenceYear = null)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
            return (null, false);

        var cleaned = CleanHtml(rawDate).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return (null, false);

        int currentYear = referenceYear ?? DateTime.UtcNow.Year;

        // Caso 1: Solo año de 4 dígitos, ej. "2027"
        if (Regex.IsMatch(cleaned, @"^\d{4}$") && int.TryParse(cleaned, out int yearOnly) && yearOnly >= 2020 && yearOnly <= 2035)
        {
            return (new DateOnly(yearOnly, 1, 1), true);
        }

        // Caso 2: Día y mes en español, ej. "8 de octubre", "15 de octubre"
        var dayMonthMatch = DayMonthRegex().Match(cleaned);
        if (dayMonthMatch.Success)
        {
            int day = int.Parse(dayMonthMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
            string monthName = dayMonthMatch.Groups["month"].Value;
            int month = MonthNameToNumber(monthName);
            if (month > 0 && day >= 1 && day <= DateTime.DaysInMonth(currentYear, month))
            {
                return (new DateOnly(currentYear, month, day), false);
            }
        }

        // Caso 3: "Mes Año", ej. "Octubre 2026" o "Octubre de 2026"
        var monthYearMatch = MonthYearRegex().Match(cleaned);
        if (monthYearMatch.Success)
        {
            string monthName = monthYearMatch.Groups["month"].Value;
            int y = int.Parse(monthYearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            int m = MonthNameToNumber(monthName);
            if (m > 0 && y >= 2020 && y <= 2035)
            {
                return (new DateOnly(y, m, 1), true);
            }
        }

        // Fallback: Parse con cultura española
        if (DateTime.TryParse(cleaned, new CultureInfo("es-ES"), DateTimeStyles.None, out var dt))
        {
            return (DateOnly.FromDateTime(dt), false);
        }

        return (null, false);
    }

    private static int MonthNameToNumber(string monthName)
    {
        if (string.IsNullOrWhiteSpace(monthName))
            return 0;

        var m = monthName.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");

        return m switch
        {
            "enero" => 1,
            "febrero" => 2,
            "marzo" => 3,
            "abril" => 4,
            "mayo" => 5,
            "junio" => 6,
            "julio" => 7,
            "agosto" => 8,
            "septiembre" or "setiembre" => 9,
            "octubre" => 10,
            "noviembre" => 11,
            "diciembre" => 12,
            _ => 0
        };
    }

    private static string CleanTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(raw).Trim();
        // Colapsar espacios múltiples y saltos de línea
        return Regex.Replace(decoded, @"\s+", " ");
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return Regex.Replace(html, "<.*?>", " ").Trim();
    }

    private static decimal? ParsePrice(string? rawPrice)
    {
        if (string.IsNullOrWhiteSpace(rawPrice))
            return null;

        var decoded = WebUtility.HtmlDecode(rawPrice);

        // Limpiar símbolos de moneda y espacios: "27,00 €" -> "27.00"
        var cleaned = decoded.Replace("€", "")
                             .Replace(" ", "")
                             .Replace("\u00A0", "")
                             .Trim();

        cleaned = cleaned.Replace(',', '.');

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
        {
            return val;
        }

        return null;
    }

    [GeneratedRegex(@"(?si)<h2[^>]*class=""[^""]*titulo-home[^""]*""[^>]*>\s*(?<title>.*?)\s*</h2>(?<content>.*?)(?=(?:<h2[^>]*class=""[^""]*titulo-home[^""]*""|<footer|\z))")]
    private static partial Regex HomeSectionRegex();

    [GeneratedRegex(@"(?si)<li[^>]*class=""[^""]*product-item[^""]*""[^>]*>.*?</li>")]
    private static partial Regex ProductItemBlockRegex();

    [GeneratedRegex(@"(?si)<a class=""product-item-link""\s+href=""(?<url>[^""]+)"">\s*(?<title>[^<]+)\s*</a>")]
    private static partial Regex ProductLinkRegex();

    [GeneratedRegex(@"(?si)<img[^>]*class=""[^""]*product-image-photo[^""]*""[^>]*src=""(?<src>[^""]+)""")]
    private static partial Regex ProductImageRegex();

    [GeneratedRegex(@"(?si)<span class=""fecha_home""[^>]*>(?<fecha>[^<]+)</span>")]
    private static partial Regex FechaHomeRegex();

    [GeneratedRegex(@"(?si)<span class=""price""[^>]*>(?<price>[^<]+)</span>")]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"(?si)<div class=""product-item-info"".*?<a class=""product-item-link""\s+href=""(?<url>[^""]+)"">\s*(?<title>[^<]+)\s*</a>.*?(?:<img[^>]*src=""(?<img>[^""]+)""|).*?<span class=""price"">(?<price>[^<]+)</span>")]
    private static partial Regex CatalogProductRegex();

    [GeneratedRegex(@"/(?<ean>\d{13})(?:-[^/]+)?\.(?:jpg|jpeg|png|webp)", RegexOptions.IgnoreCase)]
    private static partial Regex EanFromImageRegex();

    [GeneratedRegex(@"[^\d](?<ean>\d{13})[^\d]")]
    private static partial Regex GenericEanRegex();

    [GeneratedRegex(@"^(?<day>\d{1,2})\s+de\s+(?<month>[a-zA-ZáéíóúÁÉÍÓÚ]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DayMonthRegex();

    [GeneratedRegex(@"^(?<month>[a-zA-ZáéíóúÁÉÍÓÚ]+)(?:\s+de)?\s+(?<year>\d{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex MonthYearRegex();

    [GeneratedRegex(@"<script[^>]*type=""text/x-magento-init""[^>]*>\s*(?<json>\{.*?data-gallery-role=gallery-placeholder.*?\})\s*</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex GalleryScriptRegex();

    [GeneratedRegex(@"data-product-sku=""(?<sku>\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ProductSkuRegex();

    [GeneratedRegex(@"<meta\s+property=""product:price:amount""\s+content=""(?<price>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ProductMetaPriceRegex();
}
