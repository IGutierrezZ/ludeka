# Propuesta: INC-145 Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos de Catálogo

## 1. Motivación y Problema

En BoardGameGeek (BGG), el subárbol `<versions>` de cada juego contiene las ediciones físicas publicadas internacionalmente. Cada elemento `<item type="boardgameversion">` incluye dos campos de texto esenciales:
1. `<name type="primary">`: Describe habitualmente la edición física o el formato de caja para coleccionistas y catalogadores (ejemplos: `"Z-Man Spanish edition"`, `"Devir Spanish edition"`, `"Iberian edition"`, `"CAT/ENG/ITA/POR/SPA edition"`).
2. `<canonicalname>`: Contiene el título comercial traducido oficial y limpio asignado a esa versión concreta (ejemplos: `"Pandemic Legacy: Segunda temporada"`, `"Beacon Patrol"`, `"The White Castle Duel"`, `"Código 5"`).

Hasta ahora, nuestro parser analítico (`BggRawSnapshotParser.cs`) presentaba tres deficiencias críticas:
1. **Omisión de `canonicalname`:** Leía exclusivamente el campo `name` de la versión y nunca consultaba `canonicalname`. Como consecuencia, dependía de heurísticas de limpieza de sufijos sobre cadenas como `"Z-Man Spanish edition"` o `"Iberian edition"`.
2. **Huecos en el filtro de descriptores genéricos (`IsGenericEditionTitle`):**
   - No contemplaba editoriales internacionales con sellos reconocidos en España como `z-man` / `zman` (con guion), permitiendo que `"Z-Man Spanish edition"` se colara como título comercial válido para *Pandemic Legacy: Season 2* (BggId `221107`).
   - No reconocía descriptores geográficos o multilingües como `"Iberian edition"` (*Beacon Patrol*, *Piña Coladice*), acrónimos catalanes/regionales como `cat` en `"CAT/ENG/ITA/POR/SPA edition"`, ni ediciones tipo `"Print & Play edition"`.
   - La comprobación `!t.Contains('-')` descartaba incorrectamente cadenas cortas compuestas que contenían guiones.
3. **Filtro restrictivo en nombres alternativos raíz (`ResolveSpanishTitleFromRootNames`):**
   Exigía que el título alternativo contuviera explícitamente las palabras `"español"`, `"spanish"` o `"castellano"`, descartando traducciones limpias legítimas que no llevan esa etiqueta.

## 2. Propuesta de Solución

1. **Priorización de `canonicalname` en `BggRawSnapshotParser`:**
   - En `ParseVersionInfo`, consultar en primer lugar `canonicalname`. Si está presente, limpiarlo con `CleanVersionTitle` y, si no es genérico, adoptarlo como el título principal de la versión en español.
   - Usar `name` como fallback únicamente si `canonicalname` no existe o resulta vacío/genérico.

2. **Fortalecimiento de `IsGenericEditionTitle` y `CleanVersionTitle`:**
   - Incorporar editoriales pendientes: `z-man`, `zman`, `lúdilo`, `ludilo`, `salt & pepper`, `tranjis`, `gdm`, `bumby games`, `doit games`, `2tomatoes`, `masqueoca`, `falomir`, `mercurio`, `loki`, `playte`, `two acorns`, `underdog`, `mattel`, etc.
   - Incorporar tokens geográficos y lingüísticos: `iberian`, `ibérica`, `iberica`, `ibérico`, `iberico`, `chilean`, `colombian`, `latin american`, `latam`, `cat`, `sp`, `ge`, `ja`, `ko`.
   - Incorporar formatos de edición genéricos: `print & play`, `pnp`, `cube box`.
   - Eliminar el bloqueo de guiones en la heurística de frases cortas de edición, permitiendo limpiar expresiones como `Z-Man Spanish edition` o `Salt & Pepper edition`.

3. **Saneamiento Automático de Catálogo (`CatalogDataSanitizer`):**
   - Asegurar la reparación prioritaria de *Pandemic Legacy: Season 2* (BggId `221107`) asignando su título legítimo `"Pandemic Legacy: Segunda temporada"` y su editorial `"Devir"`.
   - Actualizar la detección de títulos corruptos para abarcar descriptores como `"Iberian edition"` y re-evaluar contra los snapshots crudos existentes.
   - Saneamiento en memoria restableciendo el título a `validSpanishTitle` (obtenido de `canonicalname`) o a `OriginalTitle` si no hay traducción.

## 3. Criterios de Aceptación y No-Regresión

- `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson` para BggId `221107` devuelve `Title = "Pandemic Legacy: Segunda temporada"` y `Publisher = "Devir"`.
- `BggRawSnapshotParser.IsGenericEditionTitle("Z-Man Spanish edition")` evalúa a `true`.
- `BggRawSnapshotParser.IsGenericEditionTitle("Iberian edition")` evalúa a `true`.
- `BggRawSnapshotParser.IsGenericEditionTitle("CAT/ENG/ITA/POR/SPA edition")` evalúa a `true`.
- `BggRawSnapshotParser.IsGenericEditionTitle("Print & Play edition")` evalúa a `true`.
- Títulos auténticos con subtítulos legítimos (ej. `"Alta Tensión"`, `"Código 5"`, `"Ark Nova: Mundo Marino"`, `"Pandemic Legacy: Segunda temporada"`) continúan evaluando a `false`.
- La suite completa de pruebas unitarias pasa al 100% en verde sin regresiones.
- La ejecución del saneador en la réplica de base de datos corrige *Pandemic Legacy: Season 2*, *Beacon Patrol* y *The White Castle Duel*.
