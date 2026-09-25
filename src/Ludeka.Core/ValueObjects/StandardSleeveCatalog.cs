using System;
using System.Collections.Generic;
using System.Linq;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa la especificación de un formato estándar de fundas reconocido en la industria de juegos de mesa.
/// </summary>
public record StandardSleeveFormat(
    string Name,
    double WidthMm,
    double HeightMm,
    double ToleranceMm = 1.5,
    string? Description = null,
    string? PopularGamesExample = null
)
{
    public string DimensionText => $"{WidthMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} x {HeightMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} mm";

    public bool Matches(double widthMm, double heightMm)
    {
        var minGiven = Math.Min(widthMm, heightMm);
        var maxGiven = Math.Max(widthMm, heightMm);
        var minFormat = Math.Min(WidthMm, HeightMm);
        var maxFormat = Math.Max(WidthMm, HeightMm);

        return Math.Abs(minFormat - minGiven) <= ToleranceMm &&
               Math.Abs(maxFormat - maxGiven) <= ToleranceMm;
    }
}

/// <summary>
/// Catálogo universal de formatos de fundas estándar más habituales en el hobby.
/// </summary>
public static class StandardSleeveCatalog
{
    private static readonly Dictionary<string, string> CommonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mini European"] = "Mini Euro",
        ["Standard European"] = "Euro Standard",
        ["Chimera Standard"] = "Chimera / USA"
    };

    public static readonly IReadOnlyList<StandardSleeveFormat> Formats =
    [
        new("Mini USA", 41, 63, 1.0, "Formato compacto habitual en cartas de daño, objetos y eventos", "Arkham Horror, Twilight Imperium, Star Wars: X-Wing"),
        new("Mini Euro", 44, 68, 1.0, "Formato europeo pequeño muy extendido en juegos clásicos y modernos", "7 Wonders Duel, Catan, Scythe, Ticket to Ride"),
        new("Estándar USA", 56, 87, 1.5, "Estándar americano clásico utilizado en cartas tradicionales", "Bang!, Ticket to Ride USA, Munchkin"),
        new("Chimera / USA", 57, 89, 1.5, "Formato Chimera estándar ligeramente más ancho que el americano", "Wingspan, Ark Nova, Concordia, Viticulture"),
        new("Euro Standard", 59, 92, 1.5, "Formato europeo tradicional muy habitual en juegos de gestión", "Dominion, Agricola, Puerto Rico, Los Castillos de Borgoña"),
        new("Standard Card Game", 63.5, 88, 1.5, "El estándar universal para TCG/CCG/LCG y juegos modernos", "Terraforming Mars, Dune Imperium, Magic, Heat, Marvel Champions"),
        new("Tarot / 7 Wonders", 65, 100, 2.0, "Cartas grandes de formato maravilla, personajes o roles", "7 Wonders, 7 Wonders Duel (Maravillas), Coup"),
        new("Tarot Grande", 70, 120, 2.0, "Cartas extra grandes para visiones y ambientación narrativa", "Century: La Ruta de las Especias, Eldritch Horror"),
        new("Cuadrada", 70, 70, 2.0, "Formato cuadrado para losetas y pistas", "Código Secreto, Power Grid"),
        new("Magnum / Dixit", 80, 120, 2.0, "Formato gigante para cartas con ilustraciones panorámicas", "Dixit, Mysterium, Stella")
    ];

    /// <summary>
    /// Encuentra el formato estándar más próximo a unas dimensiones dadas dentro de su tolerancia.
    /// </summary>
    public static StandardSleeveFormat? Match(double widthMm, double heightMm)
    {
        return Formats.FirstOrDefault(f => f.Matches(widthMm, heightMm));
    }

    /// <summary>
    /// Busca un formato estándar por su nombre (búsqueda insensible a mayúsculas, alias o coincidencia parcial).
    /// </summary>
    public static StandardSleeveFormat? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();

        // 1. Coincidencia directa con alias conocido
        if (CommonAliases.TryGetValue(trimmed, out var canonicalTarget))
        {
            var direct = Formats.FirstOrDefault(f => string.Equals(f.Name, canonicalTarget, StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct;
        }

        // 2. Si el texto contiene alguno de los alias conocidos
        foreach (var (alias, target) in CommonAliases)
        {
            if (trimmed.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                var matched = Formats.FirstOrDefault(f => string.Equals(f.Name, target, StringComparison.OrdinalIgnoreCase));
                if (matched != null) return matched;
            }
        }

        // 3. Búsqueda por subcadena insensible a mayúsculas
        var normalized = trimmed.ToLowerInvariant();
        return Formats.FirstOrDefault(f => f.Name.ToLowerInvariant().Contains(normalized) ||
                                           normalized.Contains(f.Name.ToLowerInvariant()));
    }
}

