# Propuesta: INC-135 Saneamiento de Descriptores de Edición en Títulos BGG y Reparación Automática de Catálogo

## 1. Motivación y Problema

En BoardGameGeek, los nombres de versión (`<name type="primary">` dentro de `<versions>`) suelen ser rótulos técnicos de producción o tirada en lugar de títulos comerciales auténticos (por ejemplo: `"ENG/GER/FRE/SPA edition"`, `"Retail edition"`, `"Multilingual edition"`, `"Spanish/English edition"`).

En la ficha de *Queen Alice* (`/juegos/queen-alice`, BggId 456236) y títulos similares, nuestro motor de sincronización extrajo `"ENG/GER/FRE/SPA edition"` como título oficial en español, degradando la presentación del catálogo y las búsquedas. Además, el saneador existente en `CatalogDataSanitizer` solo examinaba cadenas específicas como `"korean"` o `"spanish edition"`, ignorando acrónimos y variantes de tirada.

## 2. Propuesta de Solución

1. **Parser Analítico Puro (`BggRawSnapshotParser`):**
   - Generalizar `IsGenericEditionTitle` y `CleanVersionTitle` para reconocer acrónimos lingüísticos (códigos ISO/BGG de 2 y 3 letras: `ENG`, `SPA`, `GER`, `FRE`, `FRA`, `ITA`, `POR`, `DUT`, `POL`, `CZE`, `RUS`, `KOR`, `JPN`, `CHI`, `EN`, `ES`, `FR`, `DE`, `IT`, `PT`, etc.) en cualquier combinación con `/`, `-`, `&`, `+` o espacios, acompañados o no de `edition` o `versión`.
   - Incluir términos genéricos de tirada (`retail`, `deluxe`, `kickstarter`, `standard`, `special`, `collector`, `limited`, `multilingual`, `international`, etc.).
   - Si una versión analizada tiene un título genérico, `ExtractSpanishVersionInfoFromJson` debe devolver `Title = null`, permitiendo conservar el título canónico del juego o acudir a alternativas legítimas en español.

2. **Parser XML Raíz (`BggXmlParser`):**
   - Proteger `ExtractSpanishTitle` para descartar nombres alternativos del nodo raíz que sean clasificados como genéricos por `IsGenericEditionTitle`.

3. **Saneador Autónomo de Catálogo (`CatalogDataSanitizer`):**
   - Ampliar la consulta de EF Core para que capture cualquier juego en la base de datos cuyo `SpanishTitle` contenga `"edition"`, `"edicion"`, `"edición"`, `"version"`, `"versión"`, o acrónimos con barras.
   - Saneamiento en memoria restableciendo el título a `OriginalTitle` cuando no exista una traducción legítima.
   - Reparación inmediata prioritaria para BggId 456236 (*Queen Alice*) además de BggId 368966 (*Ark Nova: Mundo Marino*).

## 3. Criterios de Aceptación y No-Regresión

- `BggRawSnapshotParser.IsGenericEditionTitle("ENG/GER/FRE/SPA edition")` evalúa a `true`.
- `BggRawSnapshotParser.IsGenericEditionTitle("Retail edition")` evalúa a `true`.
- `BggRawSnapshotParser.IsGenericEditionTitle("Multilingual edition")` evalúa a `true`.
- Nombres de juegos legítimos con subtítulos (ej. `"Alta Tensión"`, `"Ciudadelas"`, `"Ark Nova: Mundo Marino"`, `"Terraforming Mars: Preludio"`) continúan evaluando a `false`.
- `SanitizeCorruptedSpanishTitlesAsync` corrige en base de datos cualquier juego cuyo `SpanishTitle` contenga descriptores genéricos, restableciéndolo a su título original si no hay versión comercial específica.
- Cero regresiones en la suite completa de pruebas unitarias (2.712 pruebas existentes).
