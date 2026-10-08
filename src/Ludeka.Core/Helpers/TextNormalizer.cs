using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ludeka.Core.Helpers;

/// <summary>
/// Proporciona métodos puros y deterministas para normalizar texto,
/// remover diacríticos y tildes, y construir patrones de búsqueda insensibles a acentos.
/// </summary>
public static partial class TextNormalizer
{
    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    /// <summary>
    /// Remueve acentos, diacríticos y marcas de combinación mediante descomposición canónica (FormD).
    /// Por ejemplo: "Código 5" -> "Codigo 5", "Agrícola" -> "Agricola", "Borgoña" -> "Borgona".
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Genera un patrón de búsqueda SQL LIKE para comparar contra columnas de tipo slug (ej. Game.Slug),
    /// normalizando diacríticos, convirtiendo a minúsculas y reemplazando separadores por comodines '%'.
    /// Por ejemplo: "Código 5" -> "%codigo%5%", "catan" -> "%catan%".
    /// </summary>
    public static string ToSearchSlugPattern(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var clean = RemoveDiacritics(text).Trim().ToLowerInvariant();
        var sanitized = NonAlphanumericRegex().Replace(clean, "%");
        return $"%{sanitized}%";
    }
}
