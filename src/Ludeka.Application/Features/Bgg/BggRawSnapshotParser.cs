using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    /// Extrae las URLs canónicas de imagen de portada y miniatura del juego raíz desde el payload JSON del snapshot.
    /// </summary>
    public static (string? CoverImageUrl, string? ThumbnailUrl) ExtractRootImagesFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            string? cover = NormalizeUrl(ExtractStringValue(root, "image"));
            string? thumb = NormalizeUrl(ExtractStringValue(root, "thumbnail"));
            return (cover, thumb);
        }
        catch
        {
            return (null, null);
        }
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
            var best = spanishCandidates.Find(c => !string.IsNullOrWhiteSpace(c.Ean)) ?? spanishCandidates[0];

            // Si la candidata seleccionada carece de título pero otra candidata española dispone de uno no genérico, enriquecer
            if (string.IsNullOrWhiteSpace(best.Title))
            {
                var withTitle = spanishCandidates.Find(c => !string.IsNullOrWhiteSpace(c.Title));
                if (withTitle != null)
                {
                    best = best with { Title = withTitle.Title };
                }
                else
                {
                    string? altTitle = ResolveSpanishTitleFromRootNames(root);
                    if (!string.IsNullOrWhiteSpace(altTitle))
                    {
                        best = best with { Title = altTitle };
                    }
                }
            }

            // Si la candidata seleccionada carece de editorial pero otra candidata española dispone de ella, enriquecer
            if (string.IsNullOrWhiteSpace(best.Publisher))
            {
                var withPub = spanishCandidates.Find(c => !string.IsNullOrWhiteSpace(c.Publisher));
                if (withPub != null)
                {
                    best = best with { Publisher = withPub.Publisher };
                }
            }

            // Si la candidata seleccionada carece de portada pero otra candidata española dispone de ella, enriquecer
            if (string.IsNullOrWhiteSpace(best.CoverImageUrl))
            {
                var withCover = spanishCandidates.Find(c => !string.IsNullOrWhiteSpace(c.CoverImageUrl));
                if (withCover != null)
                {
                    best = best with { CoverImageUrl = withCover.CoverImageUrl, ThumbnailUrl = withCover.ThumbnailUrl };
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveSpanishTitleFromRootNames(JsonElement root)
    {
        if (!root.TryGetProperty("name", out var namesProp)) return null;

        var nameElements = new List<JsonElement>();
        if (namesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in namesProp.EnumerateArray()) nameElements.Add(n);
        }
        else if (namesProp.ValueKind == JsonValueKind.Object)
        {
            nameElements.Add(namesProp);
        }

        foreach (var n in nameElements)
        {
            if (n.TryGetProperty("@type", out var typeProp) &&
                string.Equals(typeProp.GetString(), "alternate", StringComparison.OrdinalIgnoreCase))
            {
                string? val = ExtractStringValue(n);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    if (val.IndexOf("español", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        val.IndexOf("spanish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        val.IndexOf("castellano", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string cleaned = CleanVersionTitle(val) ?? val;
                        cleaned = Regex.Replace(cleaned, @"\s*\([^)]*(español|spanish|castellano)[^)]*\)", "", RegexOptions.IgnoreCase).Trim();
                        if (!string.IsNullOrWhiteSpace(cleaned) && !IsGenericEditionTitle(cleaned))
                        {
                            return cleaned;
                        }
                    }
                }
            }
        }

        return null;
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
            if (link.TryGetProperty("@value", out var valProp))
            {
                string? val = valProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) &&
                    (val.IndexOf("Spanish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     val.IndexOf("Español", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     val.IndexOf("Castellano", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Determina si un título corresponde a un descriptor genérico de edición (ej. "Spanish edition", "Korean edition", "Edición en español")
    /// y no a un título comercial auténtico de juego o expansión.
    /// </summary>
    public static bool IsGenericEditionTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return true;

        string t = title.Trim();

        // 1. Descriptores puros de idioma
        if (Regex.IsMatch(t, @"^(?:spanish|español|española|castellano|castellana|english|korean|coreana|german|alemana|french|francesa|italian|italiana|multilingual|internacional)$", RegexOptions.IgnoreCase))
            return true;

        // 2. Títulos que consisten únicamente en [editorial/idioma/ordinal/adjetivo] + edition/edición/version/versión
        if (Regex.IsMatch(t, @"\b(edition|edici[oó]n|versi[oó]n|version)\b", RegexOptions.IgnoreCase))
        {
            string stripped = Regex.Replace(t, @"\b(spanish|español|española|españoles|españolas|castellano|castellana|castellanos|castellanas|english|korean|coreana|german|alemana|french|francesa|italian|italiana|multilingual|international|internacional|first|second|third|1st|2nd|3rd|deluxe|collector['’]?s?|limited|retail|kickstarter|special|edition|edici[oó]n|versi[oó]n|version|en|de|la|el|los|las|(?:19|20)\d{2})\b", "", RegexOptions.IgnoreCase);
            stripped = Regex.Replace(stripped, @"[-_–—/:(),.']", " ").Trim();

            // Si no queda nada, era un descriptor genérico puro (ej. "Spanish edition", "Edición en español")
            if (string.IsNullOrWhiteSpace(stripped)) return true;

            // Si lo que queda coincide con nombres de editoriales conocidas o palabras breves que acompañan a edition (ej. "Angry Lion", "Devir", "Maldito Games")
            if (Regex.IsMatch(stripped, @"^(?:angry\s+lion|lotus\s+frog|board\s+m|popcorn\s+games|mandoo\s+games|devir|maldito\s+games|edge\s+entertainment|asmodee|zacatrus|sd\s+games|tcg\s+factory|ludist|arrakis|gen\s+x|2f[\s-]spiele|pegasus|feuerland|hans\s+im\s+glück|stonemaier|czech\s+games|rebel|phalanx)$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // Si no contiene separadores de subtítulo y es una frase corta de edición (ej. "Angry Lion Korean edition")
            if (!t.Contains(':') && !t.Contains('-') && !t.Contains('—') && !t.Contains('–'))
            {
                var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length <= 4 && Regex.IsMatch(t, @"\b(korean|angry\s+lion|spanish|español|castellano|english|german|french)\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Limpia sufijos o coletillas de edición de un título de versión (ej. "Alta Tensión (Edición en español)" -> "Alta Tensión",
    /// "Ark Nova: Mundo Marino - Spanish edition (2024)" -> "Ark Nova: Mundo Marino").
    /// Si el título resultante es un descriptor genérico (ej. "Spanish edition"), devuelve null para evitar sobreescribir el título canónico del juego.
    /// </summary>
    public static string? CleanVersionTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return null;

        // Limpiar sufijos que contengan "edición", "edition", "versión" o "version" tras separadores (, -, :, —, etc.),
        // contemplando posibles años asociados antes o después del término de edición (ej. " - Spanish edition (2024)").
        string cleaned = Regex.Replace(rawTitle.Trim(),
            @"\s*[\(\[\-:–—]\s*(?:(?:primera|segunda|tercera|cuarta|quinta|first|second|third|fourth|fifth|1st|2nd|3rd|4th|5th|deluxe|collector['’]?s?|limited|retail|special|spanish|español|castellano|english|korean|german|french|italian|multilingual|internacional|(?:19|20)\d{2})\s+)*(?:edici[oó]n|edition|versi[oó]n|version)(?:\s+(?:en\s+)?(?:español|castellano|spanish|multilingual|internacional|deluxe|special|collector['’]?s?|limited|retail))?[\)\]]?(?:\s*[\(\[]?(?:19|20)\d{2}[\)\]]?)?\s*$",
            "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrWhiteSpace(cleaned) || IsGenericEditionTitle(cleaned))
        {
            return null;
        }

        return cleaned;
    }

    private static BggSpanishVersionInfoDto? ParseVersionInfo(JsonElement versionElem)
    {
        string? rawTitle = ExtractVersionTitle(versionElem);
        string? title = CleanVersionTitle(rawTitle);

        string? publisher = ExtractVersionPublisher(versionElem);
        int? year = ExtractVersionYear(versionElem);
        string? productCode = ExtractStringValue(versionElem, "productcode");
        string? rawBarcode = ExtractStringValue(versionElem, "barcode");
        string? coverImageUrl = NormalizeUrl(ExtractStringValue(versionElem, "image"));
        string? thumbnailUrl = NormalizeUrl(ExtractStringValue(versionElem, "thumbnail"));

        string? normalizedEan = null;
        if (!string.IsNullOrWhiteSpace(rawBarcode) && BarcodeValidator.TryNormalizeEan13(rawBarcode, out var norm1))
        {
            normalizedEan = norm1;
        }
        else if (!string.IsNullOrWhiteSpace(productCode) && BarcodeValidator.TryNormalizeEan13(productCode, out var norm2))
        {
            normalizedEan = norm2;
        }

        // Si no contiene título válido, ni editorial, ni EAN, ni imagen, la versión no aporta datos útiles
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(publisher) && string.IsNullOrWhiteSpace(normalizedEan) && string.IsNullOrWhiteSpace(coverImageUrl))
        {
            return null;
        }

        return new BggSpanishVersionInfoDto(
            Title: title,
            Publisher: publisher,
            YearPublished: year,
            Ean: normalizedEan,
            ProductCode: productCode,
            CoverImageUrl: coverImageUrl,
            ThumbnailUrl: thumbnailUrl
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

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (url.StartsWith("//")) return "https:" + url;
        return url;
    }

    private static readonly Regex PromoOrAccessoryRegex = new(
        @"(?i)\b(promo|promos|promopack|promo-pack|bonus\s+card[s]?|bonus\s+tile[s]?|bonus\s+pack|upgrade\s+pack|upgrade\s+kit|deluxe\s+upgrade|metal\s+coins|dice\s+set|custom\s+dice|card\s+sleeves|playmat|neoprene\s+mat|miniatures?\s+pack|pin\s+set|sticker\s+pack|acrylic\s+tokens|wooden\s+tokens|resource\s+pack|coin\s+set|event\s+card[s]?|promo\s+box)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Determina heurísticamente si el título de un ítem corresponde a una promo, pack promocional de cartas o accesorio de juego.
    /// </summary>
    public static bool IsProbablePromoOrAccessory(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        return PromoOrAccessoryRegex.IsMatch(title);
    }

    /// <summary>
    /// Comprueba si el snapshot JSON contiene el bloque de estadísticas comunitarias de BGG.
    /// </summary>
    public static bool HasStatisticsFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            return root.TryGetProperty("statistics", out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extrae las métricas comunitarias de BGG (usersrated y owned) del snapshot JSON.
    /// </summary>
    public static (int UsersRated, int Owned) ExtractCommunityStatsFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return (0, 0);

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = GetEffectiveItemElement(doc);
            if (!root.TryGetProperty("statistics", out var statsProp)) return (0, 0);

            if (!statsProp.TryGetProperty("ratings", out var ratingsProp)) return (0, 0);

            int usersRated = 0;
            int owned = 0;

            if (ratingsProp.TryGetProperty("usersrated", out var usersRatedProp))
            {
                string? val = ExtractStringValue(usersRatedProp);
                if (int.TryParse(val, out int ur)) usersRated = ur;
            }

            if (ratingsProp.TryGetProperty("owned", out var ownedProp))
            {
                string? val = ExtractStringValue(ownedProp);
                if (int.TryParse(val, out int ow)) owned = ow;
            }

            return (usersRated, owned);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Determina si una expansión cumple el umbral comunitario mínimo o dispone de edición comercial en español.
    /// Si el snapshot contiene estadísticas de BGG, exige al menos minUsersRated (30) o minOwned (100) salvo que
    /// cuente con edición confirmada en español. Si el snapshot no contiene bloque de estadísticas, se admite condicionalmente.
    /// </summary>
    public static bool MeetsExpansionCommunityThresholdFromJson(string rawJson, int minUsersRated = 30, int minOwned = 100)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        // Si dispone de edición comercial en español (con editorial, EAN o título específico), se admite
        var spanishInfo = ExtractSpanishVersionInfoFromJson(rawJson);
        if (spanishInfo != null && (!string.IsNullOrWhiteSpace(spanishInfo.Publisher) || !string.IsNullOrWhiteSpace(spanishInfo.Ean) || !string.IsNullOrWhiteSpace(spanishInfo.Title)))
        {
            return true;
        }

        // Si no tiene estadísticas en el snapshot, no se puede descartar por falta de métricas
        if (!HasStatisticsFromJson(rawJson))
        {
            return true;
        }

        var (usersRated, owned) = ExtractCommunityStatsFromJson(rawJson);
        return usersRated >= minUsersRated || owned >= minOwned;
    }
}

