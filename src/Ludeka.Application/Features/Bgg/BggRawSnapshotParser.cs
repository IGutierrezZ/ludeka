using System;
using System.Collections.Generic;
using System.Text.Json;
using Ludeka.Application.DTOs;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Bgg;

/// <summary>
/// Utilidades analíticas puras para extraer metadatos de tipo y enlaces de expansión
/// desde la representación JSON normalizada de snapshots de BGG.
/// </summary>
public static class BggRawSnapshotParser
{
    private static JsonElement GetEffectiveItemElement(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("item", out var itemElem))
        {
            return itemElem;
        }
        return doc.RootElement;
    }

    /// <summary>
    /// Determina si el payload JSON del snapshot corresponde a una expansión en BGG (@type="boardgameexpansion").
    /// </summary>
    public static bool IsExpansionTypeFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (root.TryGetProperty("@type", out var typeProp))
            {
                return string.Equals(typeProp.GetString(), "boardgameexpansion", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Extrae el BggId del juego base desde un enlace entrante (inbound="true" con type="boardgameexpansion").
    /// </summary>
    public static int? ExtractInboundBaseGameBggIdFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("link", out var linkProp)) return null;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckInboundExpansionLink(item, out int baseId))
                        return baseId;
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckInboundExpansionLink(linkProp, out int baseId))
                    return baseId;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Extrae los enlaces entrantes (inbound="true" con type="boardgameexpansion") hacia juegos base.
    /// </summary>
    public static List<BggExpansionLinkDto> ExtractInboundBaseGameLinks(string rawJson)
    {
        var results = new List<BggExpansionLinkDto>();
        if (string.IsNullOrWhiteSpace(rawJson)) return results;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("link", out var linkProp)) return results;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckInboundExpansionLink(item, out int baseId, out string title))
                        results.Add(new BggExpansionLinkDto(baseId, title, IsInbound: true));
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckInboundExpansionLink(linkProp, out int baseId, out string title))
                    results.Add(new BggExpansionLinkDto(baseId, title, IsInbound: true));
            }
        }
        catch
        {
            // Salida silenciosa
        }

        return results;
    }

    /// <summary>
    /// Extrae los enlaces salientes (hacia expansiones hijas) desde un juego base.
    /// </summary>
    public static List<BggExpansionLinkDto> ExtractOutboundExpansionLinks(string rawJson)
    {
        var results = new List<BggExpansionLinkDto>();
        if (string.IsNullOrWhiteSpace(rawJson)) return results;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("link", out var linkProp)) return results;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckOutboundExpansionLink(item, out int expId, out string title))
                        results.Add(new BggExpansionLinkDto(expId, title, IsInbound: false));
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckOutboundExpansionLink(linkProp, out int expId, out string title))
                    results.Add(new BggExpansionLinkDto(expId, title, IsInbound: false));
            }
        }
        catch
        {
            // Salida silenciosa
        }

        return results;
    }

    /// <summary>
    /// Extrae los enlaces salientes a expansiones hijas desde el payload JSON de un juego base (como tuplas BggId, Title).
    /// </summary>
    public static List<(int BggId, string Title)> ExtractOutboundExpansionLinksFromJson(string rawJson)
    {
        var results = new List<(int, string)>();
        if (string.IsNullOrWhiteSpace(rawJson)) return results;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("link", out var linkProp)) return results;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckOutboundExpansionLink(item, out int expId, out string title))
                        results.Add((expId, title));
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckOutboundExpansionLink(linkProp, out int expId, out string title))
                    results.Add((expId, title));
            }
        }
        catch
        {
            // Salida silenciosa ante JSON no conforme
        }

        return results;
    }

    private static bool CheckInboundExpansionLink(JsonElement elem, out int baseId)
        => CheckInboundExpansionLink(elem, out baseId, out _);

    private static bool CheckInboundExpansionLink(JsonElement elem, out int baseId, out string title)
    {
        baseId = 0;
        title = string.Empty;
        if (elem.TryGetProperty("@type", out var typeProp) &&
            typeProp.GetString() == "boardgameexpansion" &&
            elem.TryGetProperty("@inbound", out var inProp) &&
            inProp.GetString() == "true" &&
            elem.TryGetProperty("@id", out var idProp) &&
            int.TryParse(idProp.GetString(), out int parsedId) &&
            parsedId > 0)
        {
            baseId = parsedId;
            title = elem.TryGetProperty("@value", out var valProp) ? valProp.GetString() ?? $"Juego #{baseId}" : $"Juego #{baseId}";
            return true;
        }
        return false;
    }

    private static bool CheckOutboundExpansionLink(JsonElement elem, out int expId, out string title)
    {
        expId = 0;
        title = string.Empty;
        if (elem.TryGetProperty("@type", out var typeProp) &&
            typeProp.GetString() == "boardgameexpansion")
        {
            bool isInbound = elem.TryGetProperty("@inbound", out var inProp) && inProp.GetString() == "true";
            if (!isInbound &&
                elem.TryGetProperty("@id", out var idProp) &&
                int.TryParse(idProp.GetString(), out int parsedId) &&
                parsedId > 0)
            {
                expId = parsedId;
                title = elem.TryGetProperty("@value", out var valProp) ? valProp.GetString() ?? $"Expansión #{expId}" : $"Expansión #{expId}";
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Comprueba si el payload JSON del snapshot incluye información del subárbol de versiones de BGG (&lt;versions&gt;).
    /// </summary>
    public static bool HasVersionsFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("versions", out var versionsProp)) return false;

            if (versionsProp.ValueKind == JsonValueKind.Object)
            {
                if (versionsProp.TryGetProperty("item", out var itemProp))
                {
                    if (itemProp.ValueKind == JsonValueKind.Array)
                        return itemProp.GetArrayLength() > 0;
                    return itemProp.ValueKind == JsonValueKind.Object;
                }
                return false;
            }
            if (versionsProp.ValueKind == JsonValueKind.Array)
            {
                return versionsProp.GetArrayLength() > 0;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Analiza el subárbol de versiones en el snapshot JSON y extrae metadatos de la edición en español (título, editorial, año, EAN, product code).
    /// Si existen múltiples versiones en español, prioriza la versión con código de barras (EAN-13) válido.
    /// </summary>
    public static BggSpanishVersionInfoDto? ExtractSpanishVersionInfoFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("versions", out var versionsProp)) return null;

            var versionElements = new List<JsonElement>();
            if (versionsProp.ValueKind == JsonValueKind.Object && versionsProp.TryGetProperty("item", out var itemProp))
            {
                if (itemProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in itemProp.EnumerateArray())
                        versionElements.Add(v);
                }
                else if (itemProp.ValueKind == JsonValueKind.Object)
                {
                    versionElements.Add(itemProp);
                }
            }
            else if (versionsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in versionsProp.EnumerateArray())
                    versionElements.Add(v);
            }

            var spanishCandidates = new List<BggSpanishVersionInfoDto>();

            foreach (var vElem in versionElements)
            {
                if (IsSpanishVersion(vElem))
                {
                    var info = ParseVersionInfo(vElem);
                    if (info != null)
                    {
                        spanishCandidates.Add(info);
                    }
                }
            }

            if (spanishCandidates.Count == 0) return null;

            // Priorizar candidata que tenga EAN normalizado válido
            var withEan = spanishCandidates.Find(c => !string.IsNullOrWhiteSpace(c.Ean));
            if (withEan != null) return withEan;

            // De lo contrario, devolver la primera encontrada
            return spanishCandidates[0];
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSpanishVersion(JsonElement versionElem)
    {
        if (!versionElem.TryGetProperty("link", out var linkProp)) return false;

        if (linkProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linkProp.EnumerateArray())
            {
                if (IsSpanishLanguageLink(link)) return true;
            }
        }
        else if (linkProp.ValueKind == JsonValueKind.Object)
        {
            if (IsSpanishLanguageLink(linkProp)) return true;
        }

        return false;
    }

    private static bool IsSpanishLanguageLink(JsonElement link)
    {
        if (link.TryGetProperty("@type", out var typeProp) &&
            string.Equals(typeProp.GetString(), "language", StringComparison.OrdinalIgnoreCase))
        {
            if (link.TryGetProperty("@id", out var idProp) && idProp.GetString() == "2195")
                return true;

            if (link.TryGetProperty("@value", out var valProp))
            {
                string? val = valProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) &&
                    (val.IndexOf("Spanish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     val.IndexOf("Español", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static BggSpanishVersionInfoDto? ParseVersionInfo(JsonElement versionElem)
    {
        string? title = ExtractVersionTitle(versionElem);
        if (string.IsNullOrWhiteSpace(title)) return null;

        string? publisher = ExtractVersionPublisher(versionElem);
        int? year = ExtractVersionYear(versionElem);
        string? productCode = ExtractStringValue(versionElem, "productcode");
        string? rawBarcode = ExtractStringValue(versionElem, "barcode");

        string? normalizedEan = null;
        if (!string.IsNullOrWhiteSpace(rawBarcode) && BarcodeValidator.TryNormalizeEan13(rawBarcode, out var norm1))
        {
            normalizedEan = norm1;
        }
        else if (!string.IsNullOrWhiteSpace(productCode) && BarcodeValidator.TryNormalizeEan13(productCode, out var norm2))
        {
            normalizedEan = norm2;
        }

        return new BggSpanishVersionInfoDto(
            Title: title,
            Publisher: publisher,
            YearPublished: year,
            Ean: normalizedEan,
            ProductCode: productCode
        );
    }

    private static string? ExtractVersionTitle(JsonElement versionElem)
    {
        if (!versionElem.TryGetProperty("name", out var nameProp)) return null;

        if (nameProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in nameProp.EnumerateArray())
            {
                if (n.TryGetProperty("@type", out var typeProp) &&
                    string.Equals(typeProp.GetString(), "primary", StringComparison.OrdinalIgnoreCase))
                {
                    var val = ExtractStringValue(n);
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }

            // Fallback al primer nombre del array
            foreach (var n in nameProp.EnumerateArray())
            {
                var val = ExtractStringValue(n);
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        else if (nameProp.ValueKind == JsonValueKind.Object)
        {
            return ExtractStringValue(nameProp);
        }
        else if (nameProp.ValueKind == JsonValueKind.String)
        {
            return nameProp.GetString();
        }

        return null;
    }

    private static string? ExtractVersionPublisher(JsonElement versionElem)
    {
        if (!versionElem.TryGetProperty("link", out var linkProp)) return null;

        if (linkProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linkProp.EnumerateArray())
            {
                if (link.TryGetProperty("@type", out var typeProp) &&
                    string.Equals(typeProp.GetString(), "boardgamepublisher", StringComparison.OrdinalIgnoreCase))
                {
                    if (link.TryGetProperty("@value", out var valProp))
                        return valProp.GetString();
                }
            }
        }
        else if (linkProp.ValueKind == JsonValueKind.Object)
        {
            if (linkProp.TryGetProperty("@type", out var typeProp) &&
                string.Equals(typeProp.GetString(), "boardgamepublisher", StringComparison.OrdinalIgnoreCase))
            {
                if (linkProp.TryGetProperty("@value", out var valProp))
                    return valProp.GetString();
            }
        }

        return null;
    }

    private static int? ExtractVersionYear(JsonElement versionElem)
    {
        string? val = ExtractStringValue(versionElem, "yearpublished");
        if (!string.IsNullOrWhiteSpace(val) && int.TryParse(val, out int y) && y > 0)
        {
            return y;
        }
        return null;
    }

    private static string? ExtractStringValue(JsonElement parent, string propName)
    {
        if (!parent.TryGetProperty(propName, out var prop)) return null;
        return ExtractStringValue(prop);
    }

    private static string? ExtractStringValue(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String)
            return elem.GetString();

        if (elem.ValueKind == JsonValueKind.Number)
            return elem.GetRawText();

        if (elem.ValueKind == JsonValueKind.Object)
        {
            if (elem.TryGetProperty("@value", out var v1) && v1.ValueKind == JsonValueKind.String)
                return v1.GetString();
            if (elem.TryGetProperty("#text", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
            if (elem.TryGetProperty("value", out var v3) && v3.ValueKind == JsonValueKind.String)
                return v3.GetString();
        }

        return null;
    }
}

