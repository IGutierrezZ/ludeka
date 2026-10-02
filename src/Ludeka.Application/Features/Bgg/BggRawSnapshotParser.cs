using System;
using System.Collections.Generic;
using System.Text.Json;
using Ludeka.Application.DTOs;

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
}
