using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Parser especializado para extraer especificaciones de fundas de cartas desde el XML de BGG.
/// </summary>
public static class BggSleeveParser
{
    private static readonly Regex DimensionRegex = new(
        @"(?<width>\d+(?:[\.,]\d+)?)\s*""?\s*[xX*×]\s*(?<height>\d+(?:[\.,]\d+)?)\s*""?\s*(?:mm|inches|inch)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex QuantityRegex = new(
        @"(?<qty>\d+)\s*(?:cards?|cartas?|uds?|unidades?|ct|pieces?|piezas?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extrae una lista de fundas SleeveItem a partir de un elemento XElement de BGG.
    /// Busca enlaces con type="boardgamecardsleeve" y cualquier texto con especificación de medidas.
    /// </summary>
    public static IReadOnlyList<SleeveItem> ParseSleeves(XElement item)
    {
        if (item == null) return [];

        var results = new List<SleeveItem>();

        var sleeveLinks = item.Elements("link")
            .Where(l => string.Equals(l.Attribute("type")?.Value, "boardgamecardsleeve", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var link in sleeveLinks)
        {
            var rawValue = link.Attribute("value")?.Value;
            if (string.IsNullOrWhiteSpace(rawValue)) continue;

            var sleeve = ParseSingleSleeve(rawValue, link.Attribute("qty")?.Value);
            if (sleeve != null)
            {
                // Si no está ya añadida una funda de dimensiones equivalentes
                if (!results.Any(r => Math.Abs(r.WidthMm - sleeve.WidthMm) < 0.5 && Math.Abs(r.HeightMm - sleeve.HeightMm) < 0.5))
                {
                    results.Add(sleeve);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Parsea una cadena de texto individual que describe una funda y opcionalmente su atributo de cantidad.
    /// </summary>
    public static SleeveItem? ParseSingleSleeve(string text, string? explicitQty = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var dimMatch = DimensionRegex.Match(text);
        if (!dimMatch.Success) return null;

        var wStr = dimMatch.Groups["width"].Value.Replace(',', '.');
        var hStr = dimMatch.Groups["height"].Value.Replace(',', '.');

        if (!double.TryParse(wStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double width) ||
            !double.TryParse(hStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double height))
        {
            return null;
        }

        if (width <= 0 || height <= 0) return null;

        bool hasInches = text.Contains('"') || text.Contains("inches", StringComparison.OrdinalIgnoreCase);
        if (hasInches && width < 15.0 && height < 15.0)
        {
            width = Math.Round(width * 25.4, 1);
            height = Math.Round(height * 25.4, 1);
        }

        // Asegurar que ancho sea menor o igual que el alto para formato vertical estándar
        if (width > height)
        {
            (width, height) = (height, width);
        }

        if (width < 30.0 || width > 250.0 || height < 30.0 || height > 250.0)
        {
            return null;
        }

        int count = 0;

        if (!string.IsNullOrWhiteSpace(explicitQty) && int.TryParse(explicitQty, out int qty) && qty > 0)
        {
            count = qty;
        }
        else
        {
            var qtyMatch = QuantityRegex.Match(text);
            if (qtyMatch.Success && int.TryParse(qtyMatch.Groups["qty"].Value, out int detectedQty) && detectedQty > 0)
            {
                count = detectedQty;
            }
        }

        var matchedFormat = StandardSleeveCatalog.Match(width, height);
        string formatName = matchedFormat != null ? matchedFormat.Name : $"Formato ({width:0.#} x {height:0.#} mm)";

        return new SleeveItem(
            FormatName: formatName,
            WidthMm: width,
            HeightMm: height,
            CardCount: count,
            AffiliateUrl: null
        );
    }
}
