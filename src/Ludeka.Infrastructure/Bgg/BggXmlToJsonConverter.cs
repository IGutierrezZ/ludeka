using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Convierte payloads XML de BGG XMLAPI2 en documentos JSON fieles y normalizados para persistencia en Postgres jsonb o SQLite TEXT.
/// </summary>
public static class BggXmlToJsonConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Convierte un elemento XML de BGG (ej. el elemento &lt;item&gt;) a una cadena JSON estructurada.
    /// </summary>
    public static string ConvertToJson(XElement element)
    {
        if (element == null) return "{}";

        var dict = ElementToDictionary(element);
        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    /// <summary>
    /// Parsea una cadena de texto XML completa y extrae/convierte su elemento &lt;item&gt; (o el nodo raíz) a JSON.
    /// </summary>
    public static string ConvertXmlStringToJson(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return "{}";

        var doc = XDocument.Parse(xmlContent);
        var item = doc.Root?.Element("item") ?? doc.Root;
        if (item == null) return "{}";

        return ConvertToJson(item);
    }

    private static object ElementToDictionary(XElement element)
    {
        var dict = new Dictionary<string, object?>();

        // Atributos con prefijo '@' para distinguir metadatos de elementos hijos
        foreach (var attr in element.Attributes())
        {
            dict["@" + attr.Name.LocalName] = attr.Value;
        }

        // Si no tiene elementos hijos pero contiene texto
        if (!element.HasElements)
        {
            if (!string.IsNullOrWhiteSpace(element.Value))
            {
                if (dict.Count == 0)
                {
                    return element.Value;
                }
                dict["#text"] = element.Value;
            }
            return dict;
        }

        // Elementos hijos agrupados por nombre local
        var grouped = element.Elements().GroupBy(e => e.Name.LocalName);
        foreach (var group in grouped)
        {
            var list = group.Select(ElementToDictionary).ToList();
            if (list.Count == 1)
            {
                dict[group.Key] = list[0];
            }
            else
            {
                dict[group.Key] = list;
            }
        }

        return dict;
    }
}
