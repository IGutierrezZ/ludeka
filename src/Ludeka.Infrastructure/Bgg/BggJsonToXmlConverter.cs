using System;
using System.Text.Json;
using System.Xml.Linq;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Reconstituye un elemento XElement de BGG (ej. el elemento &lt;item&gt;) a partir del JSON
/// normalizado generado por BggXmlToJsonConverter y almacenado en BggRawSnapshots.
/// </summary>
public static class BggJsonToXmlConverter
{
    /// <summary>
    /// Convierte el payload JSON en un XElement con nombre &lt;item&gt;.
    /// Devuelve null si el json es nulo, vacío o representa un snapshot ausente (notFound: true).
    /// </summary>
    public static XElement? ConvertToItemElement(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Contains("\"notFound\":true"))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("item", out var itemElem) && itemElem.ValueKind == JsonValueKind.Object)
            {
                root = itemElem;
            }

            return ConvertObjectToElement("item", root);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convierte un JsonElement a un XElement con el nombre especificado, reconstituyendo
    /// atributos (@attr), texto interno (#text) y elementos hijos recursivos.
    /// </summary>
    public static XElement ConvertObjectToElement(string elementName, JsonElement jsonElem)
    {
        var element = new XElement(elementName);

        if (jsonElem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in jsonElem.EnumerateObject())
            {
                if (prop.Name.StartsWith('@'))
                {
                    string attrName = prop.Name[1..];
                    string attrVal = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        _ => prop.Value.GetRawText()
                    };
                    element.SetAttributeValue(attrName, attrVal);
                }
                else if (prop.Name == "#text")
                {
                    element.Value = prop.Value.GetString() ?? string.Empty;
                }
                else
                {
                    AppendChildElements(element, prop.Name, prop.Value);
                }
            }
        }
        else if (jsonElem.ValueKind == JsonValueKind.String)
        {
            element.Value = jsonElem.GetString() ?? string.Empty;
        }
        else
        {
            element.Value = jsonElem.GetRawText();
        }

        return element;
    }

    private static void AppendChildElements(XElement parent, string childName, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                parent.Add(ConvertObjectToElement(childName, item));
            }
        }
        else
        {
            parent.Add(ConvertObjectToElement(childName, value));
        }
    }
}
