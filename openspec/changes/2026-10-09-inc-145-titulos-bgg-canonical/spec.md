# Especificación: INC-145 Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos de Catálogo

## Requisitos Funcionales

- **RF-01 (Priorización de `canonicalname` en Versiones BGG):**
  - `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(string rawJson)` debe inspeccionar prioritariamente la propiedad `canonicalname` de cada subárbol de versión (`boardgameversion`).
  - Si `canonicalname` contiene un valor no nulo ni vacío, se somete a `CleanVersionTitle`. Si el resultado no es genérico (`!IsGenericEditionTitle`), se establece como el `Title` de la versión.
  - Solo en caso de que `canonicalname` no exista o sea clasificado como genérico, se evaluará el campo `name` de la versión como fallback.

- **RF-02 (Ampliación y Blindaje de `IsGenericEditionTitle`):**
  - `BggRawSnapshotParser.IsGenericEditionTitle(string? title)` debe retornar `true` para:
    - Expresiones con sellos y editoriales con o sin guiones (ej. `"Z-Man Spanish edition"`, `"Z-Man edition"`, `"Devir Spanish edition"`, `"Lúdilo Spanish edition"`).
    - Descriptores regionales y territoriales de tirada (ej. `"Iberian edition"`, `"Chilean/Colombian edition"`).
    - Descriptores multilingües con códigos de idioma adicionales como `cat`, `sp`, `ge`, `ja`, `ko` (ej. `"CAT/ENG/ITA/POR/SPA edition"`, `"EN/FR/GE/IT/NL/SP edition"`, `"EN/JA/KO Cube box edition"`).
    - Formatos de producción como `"Print & Play edition"`, `"PnP edition"`.
  - Debe eliminarse la exclusión por presencia de guiones en heurísticas de frases cortas de edición, para que términos compuestos con guiones no evadan la detección.

- **RF-03 (Limpieza de Título en Versiones):**
  - `BggRawSnapshotParser.CleanVersionTitle(string? rawTitle)` debe retornar `null` para cadenas puramente genéricas como `"Z-Man Spanish edition"` o `"Iberian edition"`.
  - Títulos con prefijo legítimo seguido de sufijo de edición deben ser limpiados adecuadamente conservando el prefijo.

- **RF-04 (Saneamiento Prioritario y Sistemático en `CatalogDataSanitizer`):**
  - `EnsureKnownPriorityGamesRepairedAsync` debe reparar explícitamente *Pandemic Legacy: Season 2* (BggId `221107`), fijando su `SpanishTitle` en `"Pandemic Legacy: Segunda temporada"` y su `SpanishPublisher` en `"Devir"`.
  - El barrido de candidatos de `CatalogDataSanitizer` debe detectar juegos cuyo título contenga descriptores de edición como `"iberian"` o `"z-man"`.
  - Para cada candidato, si el snapshot crudo dispone de una versión con `canonicalname` limpio o un `validSpanishTitle`, se actualizará el `SpanishTitle` con ese valor; en su defecto, si el título actual es genérico, se restablecerá a `OriginalTitle`.
  - Si el snapshot identifica una editorial en español legítima como `"Devir"` y la actual es incongruente o genérica, debe actualizarse `SpanishPublisher`.

## Requisitos No Funcionales

- **RNF-01 (Cero Regresiones):** Todos los tests existentes en `tests/Ludeka.UnitTests` deben seguir pasando en verde al 100%.
- **RNF-02 (Idempotencia en Base de Datos):** La ejecución repetida de `SanitizeCorruptedSpanishTitlesAsync` debe ser idempotente y no realizar modificaciones sobre juegos que ya tengan títulos y editoriales saneados.
- **RNF-03 (Rendimiento en Arranque):** Las verificaciones de `CatalogDataSanitizer` deben ejecutarse en memoria constante O(100) sin penalizar el tiempo de arranque de la aplicación.
