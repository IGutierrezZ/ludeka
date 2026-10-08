# INC-135: Saneamiento de Descriptores de Edición en Títulos BGG y Reparación Automática de Catálogo

**Estado:** ✅ Archivado  
**Rama:** `inc/saneamiento-titulos-edicion`  
**Fecha:** 2026-10-08  
**Autor:** Antigravity (ODD / SDD)  

---

## 1. Contexto y Diagnóstico del Defecto

En fichas del catálogo como *Queen Alice* (`/juegos/queen-alice`, BggId 456236), el título principal visible en cabecera se mostraba degradado como `"ENG/GER/FRE/SPA edition"`, relegando el nombre canónico al subtítulo `"Título original: Queen Alice"`.

### Causa Raíz
1. **Fallo de clasificación en el parser de versiones BGG:**  
   En la API de BoardGameGeek (XMLAPI2 `thing?id=...&versions=1`), las versiones multilingües europeas frecuentemente adoptan como nombre primario de la versión cadenas compuestas por acrónimos de idioma (`<name type="primary" value="ENG/GER/FRE/SPA edition" />`).
   El método `BggRawSnapshotParser.IsGenericEditionTitle` y su limpiador asociado `CleanVersionTitle` identificaban palabras completas en inglés/español/coreano, pero omitían combinaciones con barras (`/`), guiones (`-`) o acrónimos lingüísticos de 2 y 3 letras (`ENG`, `GER`, `FRE`, `SPA`, `ITA`, `POR`, etc.), así como descriptores genéricos puros (`Retail edition`, `Deluxe edition`, `Kickstarter edition`, `Multilingual edition`).
2. **Sobreescritura en la sincronización:**  
   Al catalogar o sincronizar snapshots en segundo plano, `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson` consideró `"ENG/GER/FRE/SPA edition"` como un título comercial traducido válido y sobreescribió `game.SpanishTitle`.
3. **Filtro SQL del saneador insuficiente:**  
   `CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync` disponía de un predicado SQL acotado a cadenas fijas (`"korean"`, `"angry lion"`, `"spanish edition"`, etc.), excluyendo cualquier juego afectado por acrónimos o descriptores genéricos con `"edition"` o `"version"`.

---

## 2. Alcance Técnico del Incremento

1. **Parser Analítico Puro (`Ludeka.Application.Features.Bgg.BggRawSnapshotParser`):**
   - Extender `IsGenericEditionTitle` para reconocer acrónimos lingüísticos (2 y 3 letras ISO/BGG), combinaciones con separadores (`/`, `-`, `&`, `+`), descriptores de tirada (`retail`, `deluxe`, `kickstarter`, `standard`, `multilingual`, `international`, `first/second/third edition`, etc.) y patrones donde, tras despojar los términos de edición, no reste un título sustantivo.
   - Ajustar `CleanVersionTitle` para retirar sufijos de edición multilingüe con acrónimos y devolver `null` cuando la cadena completa sea un descriptor genérico.
   - Asegurar que `ExtractSpanishVersionInfoFromJson` asigne `Title = null` en estos casos, preservando el título canónico original del juego.
2. **Parser XML Raíz (`Ludeka.Infrastructure.Bgg.BggXmlParser`):**
   - Validar con `IsGenericEditionTitle` en `ExtractSpanishTitle` para evitar promover nombres alternativos del nodo raíz que sean meros descriptores de edición.
3. **Saneador de Catálogo (`Ludeka.Infrastructure.Seeding.CatalogDataSanitizer`):**
   - Ampliar la consulta de candidatos en base de datos para evaluar cualquier juego cuyo `SpanishTitle` contenga `"edition"`, `"edicion"`, `"edición"`, `"version"`, `"versión"` o patrones de acrónimos lingüísticos con barras.
   - Evaluar en memoria con `BggRawSnapshotParser.IsGenericEditionTitle` y restaurar `SpanishTitle` a `game.OriginalTitle` (o al título localizado legítimo del snapshot si existiese uno no genérico).
   - Asegurar de forma prioritaria el caso de *Queen Alice* (BggId 456236) junto a *Ark Nova: Mundo Marino* (BggId 368966).
4. **Verificación Automatizada con Tests Unitarios:**
   - Pruebas analíticas en `BggRawSnapshotParserVersionsTests` para `ENG/GER/FRE/SPA edition`, combinaciones de acrónimos, descriptores de tirada y preservación de títulos reales.
   - Pruebas de integración en memoria en `CatalogDataSanitizerTests` para verificar el barrido y corrección de juegos contaminados con descriptores de edición en base de datos.
