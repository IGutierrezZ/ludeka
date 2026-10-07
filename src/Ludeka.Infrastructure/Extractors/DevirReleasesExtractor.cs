using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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
            return ParseHtml(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer los lanzamientos de Devir Iberia.");
            return Array.Empty<EditorialReleaseItem>();
        }
    }

    public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Array.Empty<EditorialReleaseItem>();

        var results = new List<EditorialReleaseItem>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Dividir por secciones si existen cabeceras principales (ej. "Octubre 2026 - Juegos de mesa", "Noviembre 2026 - Juegos de rol", etc.)
        var sectionHeaders = SectionHeaderRegex().Matches(html);

        if (sectionHeaders.Count > 0)
        {
            for (int i = 0; i < sectionHeaders.Count; i++)
            {
                var match = sectionHeaders[i];
                var headerText = match.Groups["header"].Value.Trim();

                // Descartar explícitamente secciones que sean de Juegos de Rol o no sean Juegos de Mesa
                if (headerText.Contains("juegos de rol", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("rol", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (sectionDate, isMonthOnly) = ParseSectionDate(headerText);

                int startIdx = match.Index;
                int endIdx = (i + 1 < sectionHeaders.Count) ? sectionHeaders[i + 1].Index : html.Length;
                string sectionHtml = html.Substring(startIdx, endIdx - startIdx);

                ParseSectionItems(sectionHtml, headerText, sectionDate, isMonthOnly, results, seenTitles);
            }
        }
        else
        {
            ParseSectionItems(html, null, null, false, results, seenTitles);
        }

        _logger.LogInformation("Lanzamientos válidos de juegos de mesa extraídos de Devir: {Count}", results.Count);
        return results;
    }

    private void ParseSectionItems(
        string sectionHtml,
        string? headerText,
        DateOnly? sectionDate,
        bool sectionIsMonthOnly,
        List<EditorialReleaseItem> results,
        HashSet<string> seenTitles)
    {
        // 1. Formato A: Productos detallados con Precio (ej. "Precio: 25€")
        ParseDetailedPriceItems(sectionHtml, headerText, sectionDate, sectionIsMonthOnly, results, seenTitles);

        // 2. Formato B: Tarjetas simples con imagen, fecha y título
        ParseTileCardItems(sectionHtml, headerText, sectionDate, sectionIsMonthOnly, results, seenTitles);
    }

    private void ParseDetailedPriceItems(
        string sectionHtml,
        string? headerText,
        DateOnly? sectionDate,
        bool sectionIsMonthOnly,
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

            // Descartar si dentro del bloque se menciona juego de rol
            if (precedingHtml.Contains("Juego de rol", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extraer título del bloque precedente
            string? title = ExtractTitleFromPreceding(precedingHtml);
            if (string.IsNullOrWhiteSpace(title) || int.TryParse(title, out _) || !seenTitles.Add(title))
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
                imgSrc = lastImg.Groups["img"].Value.Trim();
                if (lastImg.Groups["url"].Success)
                {
                    productUrl = lastImg.Groups["url"].Value.Trim();
                }

                var eanM = EanRegex().Match(imgSrc);
                if (eanM.Success)
                {
                    ean = eanM.Groups[1].Value;
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
                titleText.Equals("Juego de mesa", StringComparison.OrdinalIgnoreCase) ||
                titleText.Equals("Juegos de mesa", StringComparison.OrdinalIgnoreCase) ||
                titleText.Equals("NOVEDAD", StringComparison.OrdinalIgnoreCase) ||
                titleText.Equals("REIMPRESIÓN", StringComparison.OrdinalIgnoreCase) ||
                titleText.Equals("REIMPRESION", StringComparison.OrdinalIgnoreCase))
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

            var item = new EditorialReleaseItem(
                Title: titleText,
                Publisher: "Devir",
                ReleaseDate: parsedDate,
                TargetDateText: !string.IsNullOrWhiteSpace(dateText) ? dateText : headerText,
                EstimatedPvp: null,
                Ean: ean,
                CoverImageUrl: string.IsNullOrWhiteSpace(imgSrc) ? null : imgSrc,
                Notes: headerText != null ? $"Próximos lanzamientos Devir ({headerText})" : "Próximos lanzamientos Devir",
                SourceUrl: DefaultDevirUrl,
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
            if (!string.IsNullOrWhiteSpace(raw) && 
                !raw.Equals("Juego de mesa", StringComparison.OrdinalIgnoreCase) &&
                !raw.Equals("Juegos de mesa", StringComparison.OrdinalIgnoreCase) &&
                !raw.Equals("NOVEDAD", StringComparison.OrdinalIgnoreCase) &&
                !raw.Equals("REIMPRESIÓN", StringComparison.OrdinalIgnoreCase) &&
                !raw.Equals("REIMPRESION", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }
        }

        // 2. Título en <strong> dentro de los últimos párrafos
        var strongMatches = StrongTagRegex().Matches(precedingHtml);
        for (int i = strongMatches.Count - 1; i >= 0; i--)
        {
            var candidate = Regex.Replace(strongMatches[i].Groups["text"].Value, "<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(candidate) ||
                candidate.Equals("Juego de mesa", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Juegos de mesa", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("NOVEDAD", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("REIMPRESIÓN", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("REIMPRESION", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Autor:", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Ilustrador:", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Tipo:", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Edad:", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Nº Jugadores:", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Tiempo", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Precio", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static (DateOnly? Date, bool IsMonthOnly) ParseCardDate(string dateText, DateOnly? fallbackDate, bool fallbackIsMonthOnly)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return (fallbackDate, fallbackIsMonthOnly);

        dateText = dateText.Trim();

        // Año solo: "2026", "2027"
        if (int.TryParse(dateText, out int year) && year >= 2024 && year <= 2035)
        {
            return (new DateOnly(year, 1, 1), true);
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

        return (fallbackDate, fallbackIsMonthOnly);
    }

    private static (DateOnly? Date, bool IsMonthOnly) ParseSectionDate(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
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

        var yearMatch = Regex.Match(header, @"\b(20\d\d)\b");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int year))
        {
            return (new DateOnly(year, 1, 1), true);
        }

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

    [GeneratedRegex(@"(?:<a[^>]+href=""(?<url>https://devir\.es/[^""]+)""[^>]*>)?\s*<img[^>]+src=""(?<img>https?://[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PrecedingImageRegex();

    [GeneratedRegex(@"<span[^>]*font-size:\s*(?:24|20)px[^>]*><strong>(?:<span[^>]*>)?(?<title>[^<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BigTitleRegex();

    [GeneratedRegex(@"<strong>(?<text>[^<]+)</strong>", RegexOptions.IgnoreCase)]
    private static partial Regex StrongTagRegex();

    [GeneratedRegex(@"src=""(?<img>[^""]*(?:Proximos-lanzamientos|product)/[^""]+)"".*?alt=""(?<alt>[^""]*)"".*?<p[^>]*>.*?<strong>(?<date>[^<]+)</strong>.*?</p>.*?<p[^>]*>.*?<strong>(?<title>[^<]+)</strong>.*?</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TileCardRegex();

    [GeneratedRegex(@"\b(84\d{11})\b")]
    private static partial Regex EanRegex();

    [GeneratedRegex(@"(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+(?<year>20\d\d)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthYearRegex();
}
