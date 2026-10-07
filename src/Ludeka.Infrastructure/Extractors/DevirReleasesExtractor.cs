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

        // Regex para capturar cada bloque de juego en la página de Devir:
        // Bloque contiene:
        // 1. Imagen con src="..." alt="..."
        // 2. Parágrafo con fecha / año en strong
        // 3. Parágrafo con título en strong
        var regex = BlockRegex();
        var matches = regex.Matches(html);

        _logger.LogInformation("Coincidencias encontradas en HTML de Devir: {Count}", matches.Count);

        foreach (Match match in matches)
        {
            var imgSrc = match.Groups["img"].Value.Trim();
            var altText = match.Groups["alt"].Value.Trim();
            var dateText = match.Groups["date"].Value.Trim();
            var titleText = match.Groups["title"].Value.Trim();

            if (string.IsNullOrWhiteSpace(titleText))
                continue;

            // Limpieza y descarte de falsos positivos (por ejemplo, textos de año en el título)
            if (int.TryParse(titleText, out _))
            {
                // Si el título es solo un número de año, invertimos o descartamos
                continue;
            }

            // Extracción de EAN: Devir nombra sus archivos de portada con el EAN-13 (ej: 8436625615992-1200...)
            string? ean = null;
            var eanMatch = EanRegex().Match(imgSrc);
            if (eanMatch.Success)
            {
                ean = eanMatch.Groups[1].Value;
            }

            // Parseo de fecha si es posible
            DateOnly? releaseDate = ParseDevirDate(dateText);

            var item = new EditorialReleaseItem(
                Title: titleText,
                Publisher: "Devir",
                ReleaseDate: releaseDate,
                TargetDateText: dateText,
                EstimatedPvp: null, // Devir no siempre publica el PVP en esta vista agregada
                Ean: ean,
                CoverImageUrl: string.IsNullOrWhiteSpace(imgSrc) ? null : imgSrc,
                Notes: "Próximos lanzamientos Devir",
                SourceUrl: DefaultDevirUrl,
                IsReprint: false);

            results.Add(item);
        }

        return results;
    }

    private static DateOnly? ParseDevirDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return null;

        dateText = dateText.Trim();

        // Si es solo año: "2026", "2027"
        if (int.TryParse(dateText, out int year) && year >= 2024 && year <= 2035)
        {
            return new DateOnly(year, 1, 1);
        }

        // Si contiene mes y año: "Noviembre 2026", "Q1 2026", etc.
        var monthYearMatch = MonthYearRegex().Match(dateText);
        if (monthYearMatch.Success)
        {
            string monthStr = monthYearMatch.Groups["month"].Value.ToLowerInvariant();
            if (int.TryParse(monthYearMatch.Groups["year"].Value, out int y))
            {
                int m = MonthNameToNumber(monthStr);
                if (m > 0)
                {
                    return new DateOnly(y, m, 1);
                }
            }
        }

        return null;
    }

    private static int MonthNameToNumber(string month)
    {
        return month switch
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

    [GeneratedRegex(@"src=""(?<img>[^""]*Proximos-lanzamientos/[^""]+)"".*?alt=""(?<alt>[^""]*)"".*?<p[^>]*>.*?<strong>(?<date>[^<]+)</strong>.*?</p>.*?<p[^>]*>.*?<strong>(?<title>[^<]+)</strong>.*?</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"\b(84\d{11})\b")]
    private static partial Regex EanRegex();

    [GeneratedRegex(@"(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+(?<year>20\d\d)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthYearRegex();
}
