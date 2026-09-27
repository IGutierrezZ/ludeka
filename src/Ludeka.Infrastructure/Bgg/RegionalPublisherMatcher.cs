using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Mapeador analítico de sellos editoriales de BGG hacia las editoriales locales oficiales de España y Latinoamérica.
/// </summary>
public static class RegionalPublisherMatcher
{
    private record KnownPublisher(
        string MatchName,
        string CountryCode,
        string OfficialName,
        string Slug
    );

    private static readonly List<KnownPublisher> KnownPublishers =
    [
        // España (ES)
        new("Maldito Games", "ES", "Maldito Games", "maldito-games"),
        new("Devir", "ES", "Devir Iberia", "devir-iberia"),
        new("Devir Iberia", "ES", "Devir Iberia", "devir-iberia"),
        new("Tranjis Games", "ES", "Tranjis Games", "tranjis-games"),
        new("Asmodee", "ES", "Asmodee Ibérica", "asmodee-iberica"),
        new("Asmodee Ibérica", "ES", "Asmodee Ibérica", "asmodee-iberica"),
        new("Asmodee Spain", "ES", "Asmodee Ibérica", "asmodee-iberica"),
        new("Zacatrus", "ES", "Zacatrus!", "zacatrus"),
        new("Zacatrus!", "ES", "Zacatrus!", "zacatrus"),
        new("SD Games", "ES", "SD Games", "sd-games"),
        new("TCG Factory", "ES", "TCG Factory", "tcg-factory"),
        new("GDM Games", "ES", "GDM Games", "gdm-games"),
        new("Doit Games", "ES", "Doit Games", "doit-games"),
        new("Arrakis Games", "ES", "Arrakis Games", "arrakis-games"),
        new("2Tomatoes Games", "ES", "2Tomatoes Games", "2tomatoes-games"),
        new("Ludonova", "ES", "Ludonova", "ludonova"),
        new("Gen-X Games", "ES", "Gen-X Games", "gen-x-games"),
        new("Edge Entertainment", "ES", "Edge Entertainment", "edge-entertainment"),
        new("MasQueOca", "ES", "Ediciones MasQueOca", "masqueoca"),
        new("Ediciones MasQueOca", "ES", "Ediciones MasQueOca", "masqueoca"),
        new("Eclipse Editorial", "ES", "Eclipse Editorial", "eclipse-editorial"),
        new("Salt & Pepper Games", "ES", "Salt & Pepper Games", "salt-and-pepper-games"),
        new("Cacahuete Games", "ES", "Cacahuete Games", "cacahuete-games"),
        new("Looping Games", "ES", "Looping Games", "looping-games"),
        new("Venatus Ediciones", "ES", "Venatus Ediciones", "venatus-ediciones"),
        new("Melmac Games", "ES", "Melmac Games", "melmac-games"),
        new("Perro Loko Games", "ES", "Perro Loko Games", "perro-loko-games"),
        new("Second Gate Games", "ES", "Second Gate Games", "second-gate-games"),
        new("Primigenia Games", "ES", "Primigenia Games", "primigenia-games"),
        new("Moidean Games", "ES", "Moidean Games", "moidean-games"),
        new("Mont Tábora", "ES", "Mont Tábora", "mont-tabora"),
        new("Bumble3EE Interactive", "ES", "Bumble3EE Interactive", "bumble3ee-interactive"),
        new("Tang de Naranja", "ES", "Tang de Naranja", "tang-de-naranja"),

        // Argentina (AR)
        new("Bureau de Juegos", "AR", "Bureau de Juegos", "bureau-de-juegos"),
        new("Maldón", "AR", "Maldón", "maldon"),
        new("Maldon", "AR", "Maldón", "maldon"),
        new("Ruibal", "AR", "Ruibal Juegos", "ruibal-juegos"),
        new("Juegos Ruibal", "AR", "Ruibal Juegos", "ruibal-juegos"),
        new("Ruibal Juegos", "AR", "Ruibal Juegos", "ruibal-juegos"),

        // Chile (CL)
        new("Fractal Juegos", "CL", "Fractal Juegos", "fractal-juegos"),
        new("Dentro de la Caja", "CL", "Dentro de la Caja", "dentro-de-la-caja"),
        new("Devir Chile", "CL", "Devir Chile", "devir-chile"),

        // México (MX)
        new("Devir México", "MX", "Devir México", "devir-mexico"),
        new("Devir Mexico", "MX", "Devir México", "devir-mexico"),
        new("El Troquel", "MX", "El Troquel", "el-troquel"),
        new("Aldebarán Games", "MX", "Aldebarán Games", "aldebaran-games"),
        new("Detestable Games", "MX", "Detestable Games", "detestable-games"),

        // Colombia (CO)
        new("Devir Colombia", "CO", "Devir Colombia", "devir-colombia"),
        new("Ouroboros Games", "CO", "Ouroboros Games", "ouroboros-games"),

        // Perú (PE)
        new("Devir Perú", "PE", "Devir Perú", "devir-peru"),
        new("Devir Peru", "PE", "Devir Perú", "devir-peru"),

        // Uruguay (UY)
        new("Matufia", "UY", "Matufia Juegos", "matufia-juegos"),
        new("Matufia Juegos", "UY", "Matufia Juegos", "matufia-juegos")
    ];

    /// <summary>
    /// Resuelve las editoriales regionales y la editorial española a partir de los nombres de editoriales presentes en BGG.
    /// </summary>
    public static (string? SpanishPublisher, List<RegionalPublisherEntry> RegionalPublishers) Match(
        IEnumerable<string>? bggPublisherNames)
    {
        if (bggPublisherNames == null)
            return (null, []);

        var names = bggPublisherNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToList();

        if (names.Count == 0)
            return (null, []);

        var regionalList = new List<RegionalPublisherEntry>();
        var seenCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool hasDevir = false;

        foreach (var name in names)
        {
            var match = KnownPublishers.FirstOrDefault(kp =>
                string.Equals(kp.MatchName, name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                // Búsqueda por subcadena aproximada si contiene el nombre de la editorial
                match = KnownPublishers.FirstOrDefault(kp =>
                    name.Contains(kp.MatchName, StringComparison.OrdinalIgnoreCase) ||
                    kp.MatchName.Contains(name, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
            {
                if (string.Equals(match.MatchName, "Devir", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Devir", StringComparison.OrdinalIgnoreCase))
                {
                    hasDevir = true;
                }

                if (!seenCountries.Contains(match.CountryCode))
                {
                    seenCountries.Add(match.CountryCode);
                    regionalList.Add(new RegionalPublisherEntry(
                        match.CountryCode,
                        ResolveCountryName(match.CountryCode),
                        match.OfficialName,
                        match.Slug
                    ));
                }
            }
        }

        // Si incluye Devir y algunos países de Latinoamérica no tienen una editorial específica en BGG,
        // registramos las filiales de Devir para dar cobertura real a las ediciones en español
        if (hasDevir)
        {
            var devirSubs = new (string CountryCode, string Name, string Slug)[]
            {
                ("MX", "Devir México", "devir-mexico"),
                ("CL", "Devir Chile", "devir-chile"),
                ("CO", "Devir Colombia", "devir-colombia"),
                ("PE", "Devir Perú", "devir-peru")
            };

            foreach (var (code, name, slug) in devirSubs)
            {
                if (!seenCountries.Contains(code))
                {
                    seenCountries.Add(code);
                    regionalList.Add(new RegionalPublisherEntry(code, ResolveCountryName(code), name, slug));
                }
            }
        }

        // Obtener la editorial española
        var esEntry = regionalList.FirstOrDefault(r => string.Equals(r.CountryCode, "ES", StringComparison.OrdinalIgnoreCase));
        string? spanishPublisher = esEntry?.PublisherName;

        return (spanishPublisher, regionalList);
    }

    private static string ResolveCountryName(string code) => code.ToUpperInvariant() switch
    {
        "ES" => "España",
        "MX" => "México",
        "AR" => "Argentina",
        "CL" => "Chile",
        "CO" => "Colombia",
        "PE" => "Perú",
        "UY" => "Uruguay",
        _ => "Internacional"
    };
}
