using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
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
    private const string DefaultMalditoCatalogUrl = "https://tienda.malditogames.com/juegos?product_list_order=creation_time&product_list_dir=desc";

    private readonly HttpClient _httpClient;
    private readonly ILogger<MalditoReleasesExtractor> _logger;

    public MalditoReleasesExtractor(HttpClient httpClient, ILogger<MalditoReleasesExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Descargando novedades de Maldito Games desde portada y catálogo...");

        string? homeHtml = null;
        try
        {
            homeHtml = await FetchHtmlWithRetryAsync(DefaultMalditoHomeUrl, referer: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fallo al obtener la portada de Maldito Games.");
        }

        string? catalogHtml = null;
        try
        {
            catalogHtml = await FetchHtmlWithRetryAsync(DefaultMalditoCatalogUrl, referer: DefaultMalditoHomeUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fallo al obtener el catálogo de Maldito Games. Se continuará con los lanzamientos de portada.");
        }

        if (string.IsNullOrWhiteSpace(homeHtml) && string.IsNullOrWhiteSpace(catalogHtml))
        {
            _logger.LogError("No se pudo obtener contenido de ninguna fuente de Maldito Games.");
            return Array.Empty<EditorialReleaseItem>();
        }

        return ParseHtml(homeHtml ?? string.Empty, catalogHtml);
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

        // 1. Extraer bloques seccionales de la portada
        if (!string.IsNullOrWhiteSpace(homeHtml))
        {
            ParseHomeSections(homeHtml, itemsMap);
        }

        // 2. Extraer o enriquecer novedades recientes del catálogo
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

            bool isReprintSection = secTitle.Contains("Volver", StringComparison.OrdinalIgnoreCase) &&
                                    secTitle.Contains("disponible", StringComparison.OrdinalIgnoreCase);

            bool isLoQueSeViene = secTitle.Contains("Lo que se viene", StringComparison.OrdinalIgnoreCase) ||
                                  secTitle.Contains("Se viene", StringComparison.OrdinalIgnoreCase);

            if (isLoQueSeViene)
            {
                // Sección de banners inferiores con fecha año (ej. 2027)
                ParseSeVieneBanners(secContent, itemsMap);
            }
            else
            {
                // Secciones de cuadrícula con productos estructurados (Últimas novedades, A puntito de llegar, Volverán a estar disponibles)
                ParseProductGridSection(secTitle, secContent, isReprintSection, itemsMap);
            }
        }

        // Si la portada no contuviera h2 estructurados, fallback a buscar directamente los banners de "Se viene"
        if (sectionMatches.Count == 0)
        {
            ParseSeVieneBanners(homeHtml, itemsMap);
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

    private void ParseSeVieneBanners(string html, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        var matches = SeVieneRegex().Matches(html);

        foreach (Match match in matches)
        {
            var imgSrc = match.Groups["img"].Value.Trim();
            var rawDate = match.Groups["date"].Value;
            var dateText = CleanHtml(rawDate);

            var title = InferTitleFromFilename(imgSrc);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            // Si ya fue extraído de las secciones con productos reales, no sobreescribir con el banner
            if (itemsMap.ContainsKey(title))
                continue;

            var (releaseDate, isMonthOnly) = ParseSpanishDate(dateText);

            itemsMap[title] = new EditorialReleaseItem(
                Title: title,
                Publisher: "Maldito Games",
                ReleaseDate: releaseDate,
                TargetDateText: !string.IsNullOrWhiteSpace(dateText) ? dateText : null,
                EstimatedPvp: null,
                Ean: ExtractEanFromImageUrl(imgSrc),
                CoverImageUrl: imgSrc,
                Notes: "Próximamente en Maldito Games (Lo que se viene)",
                SourceUrl: DefaultMalditoHomeUrl,
                IsReprint: false,
                IsMonthOnly: isMonthOnly);
        }
    }

    private void ParseCatalog(string catalogHtml, Dictionary<string, EditorialReleaseItem> itemsMap)
    {
        var catalogMatches = CatalogProductRegex().Matches(catalogHtml);
        _logger.LogInformation("Productos encontrados en catálogo reciente de Maldito: {Count}", catalogMatches.Count);

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
            else
            {
                itemsMap[title] = new EditorialReleaseItem(
                    Title: title,
                    Publisher: "Maldito Games",
                    ReleaseDate: null,
                    TargetDateText: null,
                    EstimatedPvp: price,
                    Ean: ean,
                    CoverImageUrl: !string.IsNullOrWhiteSpace(imgSrc) ? imgSrc : null,
                    Notes: "Novedad en catálogo de Maldito Games",
                    SourceUrl: productUrl,
                    IsReprint: false,
                    IsMonthOnly: false);
            }
        }
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

    [GeneratedRegex(@"src=""(?<img>[^""]*media/wysiwyg/SQ[^""]+)"".*?<div class=""fecha-home""[^>]*>(?<date>.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SeVieneRegex();

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
}
