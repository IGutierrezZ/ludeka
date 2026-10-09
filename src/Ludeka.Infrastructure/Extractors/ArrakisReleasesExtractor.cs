using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Extractors;

/// <summary>
/// Extractor especializado para los próximos lanzamientos, reimpresiones y catálogo de Arrakis Games (https://arrakisgames.com/).
/// </summary>
public partial class ArrakisReleasesExtractor : IArrakisReleasesExtractor
{
    private const string DefaultHomeUrl = "https://arrakisgames.com/";
    private const string DefaultNewsUrl = "https://arrakisgames.com/noticias/";
    private const string DefaultCatalogUrl = "https://arrakisgames.com/catalogo/";
    private const string DefaultProductSitemapUrl = "https://arrakisgames.com/product-sitemap.xml";

    private static readonly Regex HomeItemRegex = new(
        @"(?si)<h2[^>]*class=""[^""]*elementor-heading-title[^""]*""[^>]*>\s*(?:<a\s+[^>]*href=""(?<url>[^""]+)""[^>]*>)?(?<title>[^<]+?)(?:</a>)?\s*</h2>(?<between>(?:(?!<h2).)*?)<div[^>]*class=""[^""]*elementor-widget-text-editor[^""]*""[^>]*>\s*(?:<div[^>]*class=""[^""]*elementor-widget-container[^""]*""[^>]*>\s*)?<p>(?<status>[^<]+?)</p>",
        RegexOptions.Compiled);

    private static readonly Regex ImageSrcRegex = new(
        @"(?si)<img[^>]+(?:src|data-src)=""(?<src>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex NewsRegex = new(
        @"(?si)<h2[^>]*class=""[^""]*elementor-heading-title[^""]*""[^>]*>\s*LANZAMIENTO:\s*(?<fecha>[^<]+?)\s*</h2>(?<between>(?:(?!<h2).)*?)<h2[^>]*class=""[^""]*elementor-heading-title[^""]*""[^>]*>\s*<a\s+[^>]*href=""(?<url>[^""]+)""[^>]*>(?<title>[^<]+?)</a>\s*</h2>",
        RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ArrakisReleasesExtractor> _logger;

    public ArrakisReleasesExtractor(HttpClient httpClient, ILogger<ArrakisReleasesExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Descargando novedades y próximos lanzamientos de Arrakis Games...");

        string? homeHtml = null;
        string? newsHtml = null;

        try
        {
            homeHtml = await FetchHtmlWithRetryAsync(DefaultHomeUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fallo al obtener la portada de Arrakis Games.");
        }

        try
        {
            newsHtml = await FetchHtmlWithRetryAsync(DefaultNewsUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fallo al obtener la sección de noticias/próximamente de Arrakis Games.");
        }

        if (string.IsNullOrWhiteSpace(homeHtml) && string.IsNullOrWhiteSpace(newsHtml))
        {
            _logger.LogError("No se pudo obtener información de lanzamientos de Arrakis Games.");
            return Array.Empty<EditorialReleaseItem>();
        }

        var releases = ParseHtml(homeHtml ?? string.Empty, newsHtml);

        // Enriquecer cada ítem desde su ficha de producto (EAN, PVP, BGG ID y portada en alta resolución)
        var enrichedItems = new List<EditorialReleaseItem>(releases.Count);
        foreach (var item in releases)
        {
            if (ct.IsCancellationRequested) break;

            if (!string.IsNullOrWhiteSpace(item.SourceUrl) &&
                item.SourceUrl.StartsWith("https://arrakisgames.com/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.SourceUrl, DefaultHomeUrl, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.SourceUrl, DefaultNewsUrl, StringComparison.OrdinalIgnoreCase))
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
                            EstimatedPvp = gallery.Pvp ?? item.EstimatedPvp,
                            BggId = gallery.BggId ?? item.BggId,
                            Notes = !string.IsNullOrWhiteSpace(gallery.StatusText) ? gallery.StatusText : item.Notes
                        };
                        enrichedItems.Add(enriched);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "No se pudo extraer la ficha detallada para '{Title}' desde '{Url}'.", item.Title, item.SourceUrl);
                }
            }

            enrichedItems.Add(item);
        }

        return enrichedItems;
    }

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? newsHtml = null)
    {
        var itemsMap = new Dictionary<string, EditorialReleaseItem>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(homeHtml))
        {
            ParseHomeSections(homeHtml, itemsMap);
        }

        if (!string.IsNullOrWhiteSpace(newsHtml))
        {
            ParseNewsSections(newsHtml, itemsMap);
        }

        return itemsMap.Values.ToList();
    }

    private void ParseHomeSections(string html, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        if (string.IsNullOrWhiteSpace(html)) return;

        // Acotar la búsqueda a la sección de próximos lanzamientos si está presente en el documento
        int sectionIdx = html.IndexOf("ximos lanzamientos", StringComparison.OrdinalIgnoreCase);
        string searchHtml = sectionIdx >= 0 ? html.Substring(sectionIdx) : html;

        var matches = HomeItemRegex.Matches(searchHtml);
        foreach (Match match in matches)
        {
            var rawTitle = match.Groups["title"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();
            var rawStatus = match.Groups["status"].Value.Trim();

            string? imgUrl = null;
            var between = match.Groups["between"].Value;
            var imgMatch = ImageSrcRegex.Match(between);
            if (imgMatch.Success)
            {
                imgUrl = imgMatch.Groups["src"].Value.Trim();
            }

            ProcessExtractedEntry(rawTitle, url, imgUrl, rawStatus, itemsMap);
        }
    }

    private void ParseNewsSections(string html, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        if (string.IsNullOrWhiteSpace(html)) return;

        var matches = NewsRegex.Matches(html);
        foreach (Match match in matches)
        {
            var rawFecha = match.Groups["fecha"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();
            var rawTitle = match.Groups["title"].Value.Trim();

            string? imgUrl = null;
            var between = match.Groups["between"].Value;
            var imgMatch = ImageSrcRegex.Match(between);
            if (imgMatch.Success)
            {
                imgUrl = imgMatch.Groups["src"].Value.Trim();
            }

            ProcessExtractedEntry(rawTitle, url, imgUrl, rawFecha, itemsMap);
        }
    }

    private void ProcessExtractedEntry(
        string rawTitle,
        string url,
        string? imgUrl,
        string rawStatus,
        Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return;

        // Descartar cabeceras y elementos del sistema
        if (IsSystemHeading(rawTitle) || IsSystemHeading(rawStatus)) return;

        // REGLA FUNDAMENTAL DE USUARIO: descartar aquellos que ya están en tienda ("Ya disponible!", "Ya disponible")
        if (IsAlreadyAvailable(rawStatus))
        {
            return;
        }

        bool isReprint = rawTitle.Contains("Reimpresión", StringComparison.OrdinalIgnoreCase) ||
                         rawTitle.Contains("Reimpresion", StringComparison.OrdinalIgnoreCase) ||
                         rawStatus.Contains("Reimpresión", StringComparison.OrdinalIgnoreCase) ||
                         rawStatus.Contains("Reimpresion", StringComparison.OrdinalIgnoreCase);

        var cleanTitle = CleanTitle(rawTitle);
        if (string.IsNullOrWhiteSpace(cleanTitle) || IsSystemHeading(cleanTitle)) return;

        var (date, isMonthOnly) = ParseSpanishDate(rawStatus);

        string? targetDateText = !string.IsNullOrWhiteSpace(rawStatus) ? rawStatus.Trim() : null;

        var releaseItem = new EditorialReleaseItem(
            Title: cleanTitle,
            Publisher: "Arrakis Games",
            ReleaseDate: date,
            TargetDateText: targetDateText,
            EstimatedPvp: null,
            Ean: null,
            CoverImageUrl: !string.IsNullOrWhiteSpace(imgUrl) ? imgUrl : null,
            Notes: isReprint ? "Reimpresión oficial anunciada" : "Próximo lanzamiento oficial",
            SourceUrl: !string.IsNullOrWhiteSpace(url) ? url : null,
            IsReprint: isReprint,
            IsMonthOnly: isMonthOnly);

        var key = cleanTitle.ToLowerInvariant();
        if (!itemsMap.ContainsKey(key))
        {
            itemsMap[key] = releaseItem;
        }
        else
        {
            // Combinar inteligentemente si ya existía (ej. preferir con URL y con imagen)
            var existing = itemsMap[key];
            itemsMap[key] = existing with
            {
                SourceUrl = existing.SourceUrl ?? releaseItem.SourceUrl,
                CoverImageUrl = existing.CoverImageUrl ?? releaseItem.CoverImageUrl,
                IsReprint = existing.IsReprint || releaseItem.IsReprint,
                ReleaseDate = existing.ReleaseDate ?? releaseItem.ReleaseDate,
                TargetDateText = existing.TargetDateText ?? releaseItem.TargetDateText
            };
        }
    }

    private static bool IsAlreadyAvailable(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim().ToLowerInvariant();
        return s.StartsWith("ya disponible") || s.Equals("disponible") || s.Contains("ya en tiendas");
    }

    private static bool IsSystemHeading(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = text.Trim().ToLowerInvariant();
        return t.Contains("síguenos") ||
               t.Contains("siguenos") ||
               t.Contains("contacto") ||
               t.Contains("últimas novedades") ||
               t.Contains("ultimas novedades") ||
               t.Contains("próximos lanzamientos") ||
               t.Contains("proximos lanzamientos") ||
               t.Contains("aviso legal") ||
               t.Contains("política") ||
               t.Contains("politica") ||
               t.Contains("redes sociales");
    }

    private static string CleanTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var t = WebUtility.HtmlDecode(raw);
        t = Regex.Replace(t, @"(?i)\s*\(Reimpresi[oó]n\)", string.Empty);
        t = Regex.Replace(t, @"(?i)<br\s*/?>", " ");
        t = Regex.Replace(t, @"\s+", " ").Trim();

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(t.ToLowerInvariant());
    }

    public async Task<ArrakisProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl)) return null;

        var html = await FetchHtmlWithRetryAsync(productUrl, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(html)) return null;

        return ParseProductFichaHtml(html, productUrl);
    }

    public ArrakisProductGalleryDto? ParseProductFichaHtml(string html, string productUrl)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // 1. EAN
        string? ean = null;
        var eanMatch = Regex.Match(html, @"EAN:\s*([0-9]{10,14})", RegexOptions.IgnoreCase);
        if (eanMatch.Success)
        {
            ean = eanMatch.Groups[1].Value;
        }

        // 2. PVP
        decimal? pvp = null;
        var pvpMatch = Regex.Match(html, @"PVPr?:\s*([0-9]+[.,][0-9]{2})\s*€?", RegexOptions.IgnoreCase);
        if (pvpMatch.Success)
        {
            var pvpStr = pvpMatch.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(pvpStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedPvp))
            {
                pvp = parsedPvp;
            }
        }

        // 3. Enlace a BGG y BGG ID
        int? bggId = null;
        string? bggUrl = null;
        var bggMatch = Regex.Match(html, @"href=""(https?://(?:www\.)?boardgamegeek\.com/boardgame/(?<bggid>\d+)[^""]*)""", RegexOptions.IgnoreCase);
        if (bggMatch.Success)
        {
            bggUrl = bggMatch.Groups[1].Value;
            if (int.TryParse(bggMatch.Groups["bggid"].Value, out int id))
            {
                bggId = id;
            }
        }

        // 4. Portada de alta calidad
        string? coverUrl = null;
        var coverMatch = Regex.Match(html, @"<img[^>]+src=""(?<src>https://arrakisgames\.com/wp-content/uploads/[^""]+)""[^>]+class=""[^""]*(?:attachment-woocommerce_thumbnail|wp-post-image|size-full)", RegexOptions.IgnoreCase);
        if (!coverMatch.Success)
        {
            coverMatch = Regex.Match(html, @"<img[^>]+class=""[^""]*(?:attachment-woocommerce_thumbnail|wp-post-image|size-full)[^""]*""[^>]+src=""(?<src>https://arrakisgames\.com/wp-content/uploads/[^""]+)""", RegexOptions.IgnoreCase);
        }
        if (coverMatch.Success)
        {
            coverUrl = coverMatch.Groups["src"].Value;
        }

        // 5. Estado / Reimpresión
        string? statusText = null;
        var statusMatch = Regex.Match(html, @"<h2 class=""elementor-heading-title[^""]*"">\s*(?:Reimpresi[oó\u00f3]n:\s*)?(?<text>[^<]*?(?:202[0-9]|Noviembre|Diciembre|Enero|Febrero|Marzo|Abril|Mayo|Junio|Julio|Agosto|Septiembre|Octubre)[^<]*?)\s*</h2>", RegexOptions.IgnoreCase);
        if (statusMatch.Success)
        {
            statusText = statusMatch.Groups["text"].Value.Trim();
        }

        // 6. Galería (mesa, componentes)
        string? tableUrl = null;
        var galleryMatch = Regex.Match(html, @"href=""(?<src>https://arrakisgames\.com/wp-content/uploads/[^""]+?(?:figures|resources|table|stats|components|detalle)[^""]*?\.(?:jpg|png|webp))""", RegexOptions.IgnoreCase);
        if (galleryMatch.Success)
        {
            tableUrl = galleryMatch.Groups["src"].Value;
        }

        return new ArrakisProductGalleryDto(
            CoverImageUrl: coverUrl,
            TableImageUrl: tableUrl,
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null,
            Ean: ean,
            Pvp: pvp,
            BggId: bggId,
            BggUrl: bggUrl,
            Title: null,
            StatusText: statusText);
    }

    public async Task<IReadOnlyList<ArrakisCatalogItemDto>> ExtractFullCatalogAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Extrayendo catálogo completo de Arrakis Games...");

        var resultsMap = new Dictionary<string, ArrakisCatalogItemDto>(StringComparer.OrdinalIgnoreCase);

        // Intentar primero por sitemap oficial de productos (formato XML directo y veloz)
        try
        {
            var sitemapXml = await FetchHtmlWithRetryAsync(DefaultProductSitemapUrl, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(sitemapXml))
            {
                ParseProductSitemap(sitemapXml, resultsMap);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el product-sitemap.xml de Arrakis Games.");
        }

        // Complementar con la página de catálogo HTML
        try
        {
            var catalogHtml = await FetchHtmlWithRetryAsync(DefaultCatalogUrl, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(catalogHtml))
            {
                var catalogItems = ParseCatalogHtml(catalogHtml);
                foreach (var item in catalogItems)
                {
                    if (!resultsMap.ContainsKey(item.ProductUrl))
                    {
                        resultsMap[item.ProductUrl] = item;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el catálogo HTML de Arrakis Games.");
        }

        _logger.LogInformation("Se localizaron {Count} productos en el catálogo de Arrakis Games.", resultsMap.Count);
        return resultsMap.Values.ToList();
    }

    public IReadOnlyList<ArrakisCatalogItemDto> ParseCatalogHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<ArrakisCatalogItemDto>();

        var results = new List<ArrakisCatalogItemDto>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Elementor columns con enlaces a fichas de juego
        var itemRegex = new Regex(
            @"(?si)<a\s+[^>]*href=""(?<url>https://arrakisgames\.com/[a-z0-9\-]+/?)""[^>]*>.*?" +
            @"(?:<img[^>]+src=""(?<imgUrl>[^""]+)""[^>]*>)?.*?" +
            @"<h2 class=""elementor-heading-title[^""]*"">\s*<a[^>]*>(?<title>[^<]+?)</a>",
            RegexOptions.Compiled);

        var matches = itemRegex.Matches(html);
        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value.Trim();
            var title = match.Groups["title"].Value.Trim();
            var img = match.Groups["imgUrl"].Value.Trim();

            if (IsSystemHeading(title)) continue;
            if (url.EndsWith("/catalogo/", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith("/tienda/", StringComparison.OrdinalIgnoreCase)) continue;

            if (seenUrls.Add(url))
            {
                results.Add(new ArrakisCatalogItemDto(
                    ProductUrl: url,
                    Title: CleanTitle(title),
                    CoverImageUrl: !string.IsNullOrWhiteSpace(img) ? img : null));
            }
        }

        return results;
    }

    private void ParseProductSitemap(string xml, Dictionary<string, ArrakisCatalogItemDto> resultsMap)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace imageNs = "http://www.google.com/schemas/sitemap-image/1.1";

            var urls = doc.Descendants(ns + "url");
            foreach (var u in urls)
            {
                var loc = u.Element(ns + "loc")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(loc)) continue;

                // Solo productos reales, ignorando lotes genéricos si aplica
                if (loc.Contains("/lote-juegos-", StringComparison.OrdinalIgnoreCase)) continue;

                var firstImage = u.Descendants(imageNs + "loc").FirstOrDefault()?.Value?.Trim();

                // Deducir título a partir del slug de producto
                var slug = loc.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
                var derivedTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slug.Replace('-', ' '));

                if (!resultsMap.ContainsKey(loc))
                {
                    resultsMap[loc] = new ArrakisCatalogItemDto(
                        ProductUrl: loc,
                        Title: derivedTitle,
                        CoverImageUrl: firstImage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al parsear el XML de product-sitemap.");
        }
    }

    public static (DateOnly? Date, bool IsMonthOnly) ParseSpanishDate(string? rawDate, int? referenceYear = null)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
            return (null, false);

        var cleaned = rawDate.Trim();
        int currentYear = referenceYear ?? DateTime.UtcNow.Year;

        // Caso 1: "Mes Año", ej. "Junio 2027", "Noviembre 2026"
        var monthYearMatch = Regex.Match(cleaned, @"(?i)(?:En\s+)?(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+(?:de\s+)?(?<year>202[0-9])");
        if (monthYearMatch.Success)
        {
            int y = int.Parse(monthYearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            int m = MonthNameToNumber(monthYearMatch.Groups["month"].Value);
            if (m > 0)
            {
                return (new DateOnly(y, m, 1), true);
            }
        }

        // Caso 2: Día, mes y año, ej. "1 de octubre de 2026"
        var fullDateMatch = Regex.Match(cleaned, @"(?i)(?<day>\d{1,2})\s+de\s+(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+(?:de\s+)?(?<year>202[0-9])");
        if (fullDateMatch.Success)
        {
            int d = int.Parse(fullDateMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
            int m = MonthNameToNumber(fullDateMatch.Groups["month"].Value);
            int y = int.Parse(fullDateMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            if (m > 0 && d >= 1 && d <= DateTime.DaysInMonth(y, m))
            {
                return (new DateOnly(y, m, d), false);
            }
        }

        // Caso 3: Solo mes sin año, ej. "En Noviembre", "Noviembre"
        var monthOnlyMatch = Regex.Match(cleaned, @"(?i)(?:En\s+)?(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)$");
        if (monthOnlyMatch.Success)
        {
            int m = MonthNameToNumber(monthOnlyMatch.Groups["month"].Value);
            if (m > 0)
            {
                int targetYear = currentYear;
                // Si el mes ya pasó en el año en curso, asumimos el año siguiente
                if (m < DateTime.UtcNow.Month)
                {
                    targetYear++;
                }
                return (new DateOnly(targetYear, m, 1), true);
            }
        }

        return (null, false);
    }

    private static int MonthNameToNumber(string monthName)
    {
        return monthName.ToLowerInvariant() switch
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
    }

    private async Task<string?> FetchHtmlWithRetryAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                using var request = CreateBrowserNavRequest(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                }

                _logger.LogWarning("HTTP {StatusCode} al consultar '{Url}' en Arrakis Games (intento {Attempt}/2).", response.StatusCode, url, attempt);

                if (attempt == 1 && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                {
                    await Task.Delay(1500, ct).ConfigureAwait(false);
                    continue;
                }

                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error al consultar '{Url}' en Arrakis Games (intento {Attempt}/2).", url, attempt);
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

    private static HttpRequestMessage CreateBrowserNavRequest(HttpMethod method, string targetUrl)
    {
        var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36 Ludeka/1.0");

        return request;
    }
}
