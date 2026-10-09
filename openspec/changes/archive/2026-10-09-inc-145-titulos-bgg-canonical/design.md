# Diseño Técnico: INC-145 Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos de Catálogo

## 1. Arquitectura y Componentes Afectados

```
┌────────────────────────────────────────────────────────┐
│  BggRawSnapshotParser (Ludeka.Application)             │
│  - ParseVersionInfo: priorizar canonicalname sobre     │
│    name en <item type="boardgameversion">              │
│  - IsGenericEditionTitle: incorporar z-man, iberian,   │
│    códigos de idioma y formatos pnp/cube box           │
│  - CleanVersionTitle: descartar descriptores genéricos │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│  CatalogDataSanitizer (Ludeka.Infrastructure)          │
│  - EnsureKnownPriorityGamesRepairedAsync:              │
│    BggId 221107 -> "Pandemic Legacy: Segunda temporada"│
│    y SpanishPublisher -> "Devir"                       │
│  - Barrido ampliado de títulos genéricos y refresco    │
│    desde snapshots crudos existentes                   │
└────────────────────────────────────────────────────────┘
```

## 2. Decisiones de Diseño

### A. Priorización de `canonicalname` en `BggRawSnapshotParser`
En `src/Ludeka.Application/Features/Bgg/BggRawSnapshotParser.cs`:
El método `ParseVersionInfo` y un método auxiliar dedicado `ExtractVersionTitle` procesarán la versión:
```csharp
private static string? ExtractVersionTitle(JsonElement versionElem)
{
    // 1. Priorizar canonicalname: título traducido limpio oficial en BGG
    if (versionElem.TryGetProperty("canonicalname", out var canonicalProp))
    {
        string? canonical = ExtractStringValue(canonicalProp);
        string? cleanedCanonical = CleanVersionTitle(canonical);
        if (!string.IsNullOrWhiteSpace(cleanedCanonical) && !IsGenericEditionTitle(cleanedCanonical))
        {
            return cleanedCanonical;
        }
    }

    // 2. Fallback a name de la versión (ej. "Código 5 - Spanish edition")
    string? rawName = ExtractRawVersionName(versionElem);
    string? cleanedName = CleanVersionTitle(rawName);
    if (!string.IsNullOrWhiteSpace(cleanedName) && !IsGenericEditionTitle(cleanedName))
    {
        return cleanedName;
    }

    return null;
}
```

### B. Robustecimiento de `IsGenericEditionTitle`
En `IsGenericEditionTitle`:
1. **Editoriales conocidas:**
   Añadir a la lista de reemplazo:
   `z-man(?:\s+games)?|zman(?:\s+games)?|lúdilo|ludilo|salt\s+&\s+pepper(?:\s+games)?|salt\s+and\s+pepper|tranjis(?:\s+games)?|gdm(?:\s+games)?|bumby\s+games|bumby|doit\s+games|doit|2tomatoes(?:\s+games)?|masqueoca|másqueoca|falomir(?:\s+juegos)?|mercurio(?:\s+distribuciones)?|loki|playte|two\s+acorns(?:\s+games)?|underdog(?:\s+games)?|jocus(?:\s+\(ii\))?|mattel(?:\s*,\s*inc\.?)?`
2. **Tokens lingüísticos y geográficos:**
   Añadir:
   `iberian|ibérica|iberica|ibérico|iberico|chilean|chileno|chilena|colombian|colombiano|colombiana|latin\s+american|latinoamericana|latinoamericano|latam`
   Añadir códigos lingüísticos en la sustitución de 2-4 letras:
   `cat|sp|ge|ja|ko`
3. **Formatos de tirada:**
   `print\s*&\s*play|pnp|cube\s+box`
4. **Desbloqueo de guiones:**
   En la regla de frases cortas de edición, relajar la exclusión si tras limpiar separadores y palabras clave solo quedan tokens de edición y editorial.

### C. Saneamiento Prioritario en `CatalogDataSanitizer`
En `EnsureKnownPriorityGamesRepairedAsync`:
```csharp
var pandemicLegacy2 = await context.Games.FirstOrDefaultAsync(g => g.BggId == 221107, ct);
if (pandemicLegacy2 != null)
{
    bool updated = false;
    const string targetTitle = "Pandemic Legacy: Segunda temporada";
    const string targetPublisher = "Devir";

    if (pandemicLegacy2.SpanishTitle != targetTitle)
    {
        pandemicLegacy2.UpdateSpanishTitle(targetTitle);
        updated = true;
    }
    if (pandemicLegacy2.SpanishPublisher != targetPublisher)
    {
        pandemicLegacy2.UpdateSpanishPublisher(targetPublisher);
        updated = true;
    }
    if (updated)
    {
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Saneamiento prioritario: BggId 221107 actualizado a '{Title}' ({Publisher}).", targetTitle, targetPublisher);
    }
}
```

Además, en la consulta de candidatos de `SanitizeCorruptedSpanishTitlesAsync`, incorporar detección de títulos que contengan `"iberian"`, `"z-man"`, `"print & play"`, `"cube box"` o similares, y en el bucle principal de saneamiento, cuando `vInfo.Title` esté disponible mediante `canonicalname`, aplicarlo sobre `SpanishTitle`.

## 3. Plan de Pruebas

1. `BggRawSnapshotParserVersionsTests`:
   - Verificar que `IsGenericEditionTitle("Z-Man Spanish edition")` devuelve `true`.
   - Verificar que `IsGenericEditionTitle("Iberian edition")` devuelve `true`.
   - Verificar que `IsGenericEditionTitle("CAT/ENG/ITA/POR/SPA edition")` devuelve `true`.
   - Verificar que `IsGenericEditionTitle("Print & Play edition")` devuelve `true`.
   - Verificar que `ExtractSpanishVersionInfoFromJson` sobre el snapshot real de BggId 221107 extrae `Title = "Pandemic Legacy: Segunda temporada"` y `Publisher = "Devir"`.
   - Verificar que `CleanVersionTitle("Z-Man Spanish edition")` devuelve `null`.
2. `CatalogDataSanitizerTests`:
   - Verificar la reparación prioritaria de BggId 221107 (*Pandemic Legacy: Season 2*) a `"Pandemic Legacy: Segunda temporada"` y editorial `"Devir"`.
   - Verificar el saneamiento de candidatos con `"Iberian edition"` hacia su título canónico.
