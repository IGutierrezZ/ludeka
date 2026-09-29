using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Catálogo oficial de países soportados y utilidades de resolución geográfica en Ludeka.
/// </summary>
public static class CountryCatalog
{
    public static readonly CountryInfo Spain = new("ES", "España", "🇪🇸");
    public static readonly CountryInfo Mexico = new("MX", "México", "🇲🇽");
    public static readonly CountryInfo Argentina = new("AR", "Argentina", "🇦🇷");
    public static readonly CountryInfo Chile = new("CL", "Chile", "🇨🇱");
    public static readonly CountryInfo Colombia = new("CO", "Colombia", "🇨🇴");
    public static readonly CountryInfo Peru = new("PE", "Perú", "🇵🇪");
    public static readonly CountryInfo Uruguay = new("UY", "Uruguay", "🇺🇾");
    public static readonly CountryInfo International = new("INT", "Internacional", "🌎");

    public static IReadOnlyList<CountryInfo> All { get; } = new List<CountryInfo>
    {
        Spain,
        Mexico,
        Argentina,
        Chile,
        Colombia,
        Peru,
        Uruguay,
        International
    };

    /// <summary>
    /// Lista de países específicos sin incluir el valor genérico "Internacional".
    /// </summary>
    public static IReadOnlyList<CountryInfo> SpecificCountries { get; } = All
        .Where(c => c.Code != "INT")
        .ToList();

    /// <summary>
    /// Obtiene la lista completa de países soportados, incluyendo Internacional.
    /// </summary>
    public static IReadOnlyList<CountryInfo> GetAllCountries() => All;

    /// <summary>
    /// Obtiene la lista de países específicos territoriales.
    /// </summary>
    public static IReadOnlyList<CountryInfo> GetSpecificCountries() => SpecificCountries;

    /// <summary>
    /// Busca un país en el catálogo a partir de su nombre canónico, código ISO o variación lingüística (ignorando acentos y mayúsculas).
    /// </summary>
    public static CountryInfo? FindByNameOrCode(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var clean = RemoveDiacritics(query.Trim());

        return All.FirstOrDefault(c =>
            string.Equals(c.Code, query.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(RemoveDiacritics(c.Name), clean, StringComparison.OrdinalIgnoreCase) ||
            (c.Code == "INT" && (clean.Equals("global", StringComparison.OrdinalIgnoreCase) ||
                                 clean.Equals("mundial", StringComparison.OrdinalIgnoreCase) ||
                                 clean.Equals("worldwide", StringComparison.OrdinalIgnoreCase) ||
                                 clean.Equals("internacional", StringComparison.OrdinalIgnoreCase) ||
                                 clean.Equals("todos", StringComparison.OrdinalIgnoreCase))) ||
            (c.Code == "ES" && (clean.Equals("espana", StringComparison.OrdinalIgnoreCase) ||
                                clean.Equals("spain", StringComparison.OrdinalIgnoreCase) ||
                                clean.Equals("peninsula", StringComparison.OrdinalIgnoreCase) ||
                                clean.Equals("peninsula iberica", StringComparison.OrdinalIgnoreCase) ||
                                clean.Equals("baleares", StringComparison.OrdinalIgnoreCase) ||
                                clean.Equals("canarias", StringComparison.OrdinalIgnoreCase))) ||
            (c.Code == "MX" && clean.Equals("mexico", StringComparison.OrdinalIgnoreCase)) ||
            (c.Code == "PE" && clean.Equals("peru", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Obtiene la bandera emoji de un país dado su nombre o código, o "🌐" si es desconocido.
    /// </summary>
    public static string GetFlag(string? countryName)
    {
        var country = FindByNameOrCode(countryName);
        return country?.FlagEmoji ?? "🌐";
    }

    /// <summary>
    /// Normaliza un nombre de país devolviendo su nombre oficial canónico si está en el catálogo, o el texto limpio.
    /// </summary>
    public static string Normalize(string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName))
            return string.Empty;

        var matched = FindByNameOrCode(countryName);
        return matched?.Name ?? countryName.Trim();
    }

    /// <summary>
    /// Determina si un país o ámbito territorial corresponde a cobertura mundial/internacional.
    /// </summary>
    public static bool IsInternational(string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName))
            return false;

        var matched = FindByNameOrCode(countryName);
        return matched != null && matched.Code == "INT";
    }

    private static string RemoveDiacritics(string text)
    {
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
}
