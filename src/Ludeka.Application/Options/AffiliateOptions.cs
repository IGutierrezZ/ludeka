using System;
using System.Collections.Generic;

namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para el motor privado de afiliación de tiendas.
/// </summary>
public class AffiliateOptions
{
    public const string SectionName = "Affiliates";

    /// <summary>
    /// Activa o desactiva de forma global la inyección de parámetros de afiliado.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Diccionario de reglas de afiliación por clave de tienda (case-insensitive).
    /// </summary>
    public Dictionary<string, StoreAffiliateRule> Stores { get; set; } = CreateDefaultRules();

    public static Dictionary<string, StoreAffiliateRule> CreateDefaultRules()
    {
        return new Dictionary<string, StoreAffiliateRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zacatrus"] = new()
            {
                ParamName = "ref",
                AffiliateTag = "ludeka",
                DomainMatch = "zacatrus.es"
            },
            ["Mathom"] = new()
            {
                ParamName = "aff",
                AffiliateTag = "ludeka",
                DomainMatch = "mathom.es"
            },
            ["DungeonMarvels"] = new()
            {
                ParamName = "ref",
                AffiliateTag = "ludeka",
                DomainMatch = "dungeonmarvels.com"
            },
            ["CuartoDeJuegos"] = new()
            {
                ParamName = "ref",
                AffiliateTag = "ludeka",
                DomainMatch = "cuartodejuegos.es"
            },
            ["Tablerum"] = new()
            {
                ParamName = "partner",
                AffiliateTag = "ludeka",
                DomainMatch = "tablerum.es"
            },
            ["Amazon"] = new()
            {
                ParamName = "tag",
                AffiliateTag = "ludeka-21",
                DomainMatch = "amazon.es"
            }
        };
    }
}

/// <summary>
/// Regla específica de afiliación para una tienda colaboradora.
/// </summary>
public class StoreAffiliateRule
{
    /// <summary>
    /// Nombre del parámetro query (ej: 'ref', 'aff', 'partner', 'tag').
    /// </summary>
    public string ParamName { get; set; } = "ref";

    /// <summary>
    /// Código o identificador de afiliado privado.
    /// </summary>
    public string AffiliateTag { get; set; } = string.Empty;

    /// <summary>
    /// Dominio o fragmento de host para coincidencia heurística en URLs (ej: 'zacatrus.es').
    /// </summary>
    public string? DomainMatch { get; set; }

    /// <summary>
    /// Indica si esta regla está habilitada.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
