# Diseño: INC-135 Saneamiento de Descriptores de Edición en Títulos BGG y Reparación Automática de Catálogo

## 1. Arquitectura y Componentes Modificados

```
┌────────────────────────────────────────────────────────┐
│             BGG XMLAPI2 / Snapshots                    │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│  BggRawSnapshotParser (Ludeka.Application)             │
│  - IsGenericEditionTitle(title) [AMPLIADO]             │
│    * Acrónimos (ENG, GER, FRE, SPA, ES, EN, etc.)      │
│    * Conectores y barras (/ , - , & , +)               │
│    * Descriptores de tirada (retail, deluxe, ks...)    │
│  - CleanVersionTitle(rawTitle) [AMPLIADO]              │
│  - ExtractSpanishVersionInfoFromJson                   │
│    * Devuelve Title = null si es descriptor genérico   │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│  CatalogDataSanitizer (Ludeka.Infrastructure)          │
│  - SanitizeCorruptedSpanishTitlesAsync                 │
│    * Predicado SQL amplio (edition, versión, etc.)     │
│    * Aseguramiento prioritario BggId 456236 y 368966   │
│    * Restauración limpia a OriginalTitle               │
└────────────────────────────────────────────────────────┘
```

## 2. Decisiones de Diseño Técnico

### A. Detección de Descriptores Genéricos (`IsGenericEditionTitle`)
En `BggRawSnapshotParser.cs`:
1. **Normalización y descarte de acrónimos lingüísticos y descriptores:**
   Construir una expresión regular que contemple tanto nombres de idiomas en inglés, español y lenguas autonómicas (`spanish`, `castellano`, `catalan`, `english`, `german`, `french`, `italian`, `portuguese`, `korean`, `japanese`, etc.) como sus acrónimos habituales en BGG (códigos ISO 639-1 y 639-2: `eng`, `spa`, `esp`, `ger`, `deu`, `fre`, `fra`, `ita`, `por`, `dut`, `nld`, `pol`, `cze`, `ces`, `rus`, `kor`, `jpn`, `chi`, `zho`, `en`, `es`, `fr`, `de`, `it`, `pt`, `nl`, `pl`, `cs`, `ru`, `ko`, `ja`, `zh`, etc.).
2. **Tokens de edición y tirada:**
   `edition`, `edición`, `edicion`, `version`, `versión`, `retail`, `deluxe`, `kickstarter`, `gamefound`, `backer`, `standard`, `special`, `collector['’]?s?`, `limited`, `first`, `second`, `third`, `fourth`, `fifth`, `1st`, `2nd`, `3rd`, `4th`, `5th`, `multilingual`, `multilingüe`, `international`, `internacional`, `big box`, `pocket`, `travel`, `promo`, `essential`, `anniversary`.
3. **Editoriales y preposiciones:**
   `combo games`, `angry lion`, `lotus frog`, `board m`, `popcorn games`, `mandoo games`, `devir`, `maldito games`, `edge entertainment`, `asmodee`, `zacatrus`, `sd games`, `tcg factory`, `ludist`, `arrakis`, `gen x`, `2f spiele`, `pegasus`, `feuerland`, `en`, `de`, `del`, `la`, `el`, `los`, `las`, `in`, `for`, `with`, `and`, `y`.
4. Si al eliminar estos tokens y separadores (`/`, `-`, `&`, `+`, `(`, `)`, `:`, etc.) la cadena resultante queda vacía o no contiene letras sustantivas, el título es **100% genérico** y debe ser rechazado.

### B. Limpieza de Sufijos (`CleanVersionTitle`)
Permitir que títulos como `"Queen Alice - ENG/GER/FRE/SPA edition"` pierdan el sufijo y queden en `"Queen Alice"`, pero si el título entero era `"ENG/GER/FRE/SPA edition"`, devuelva `null`.

### C. Filtro en `CatalogDataSanitizer`
En `CatalogDataSanitizer.cs`:
```csharp
var candidates = await db.Games
    .Where(g => g.BggId > lastBggId && (
        g.BggId == 456236 || // Queen Alice
        g.BggId == 368966 || // Ark Nova: Mundo Marino
        (g.SpanishTitle != null && (
            g.SpanishTitle.ToLower().Contains("korean") ||
            g.SpanishTitle.ToLower().Contains("edition") ||
            g.SpanishTitle.ToLower().Contains("edicion") ||
            g.SpanishTitle.ToLower().Contains("edición") ||
            g.SpanishTitle.ToLower().Contains("version") ||
            g.SpanishTitle.ToLower().Contains("versión") ||
            g.SpanishTitle.ToLower().Contains("angry lion") ||
            g.SpanishTitle.ToLower().Contains("lotus frog") ||
            g.SpanishTitle.ToLower().Contains("board m") ||
            g.SpanishTitle.ToLower().Contains("popcorn games") ||
            g.SpanishTitle.ToLower().Contains("mandoo games")
        )) ||
        (g.SpanishPublisher != null && ...
```
Una vez recuperados los candidatos, la lógica en memoria ejecuta `BggRawSnapshotParser.IsGenericEditionTitle(game.SpanishTitle)`. Si es genérico, se restaura a `OriginalTitle` (o al título localizado legítimo del snapshot).
