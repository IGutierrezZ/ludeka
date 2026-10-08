# Especificación: INC-135 Saneamiento de Descriptores de Edición en Títulos BGG y Reparación Automática de Catálogo

## Requisitos Funcionales

- **RF-01 (Clasificación de Descriptores Genéricos de Edición):**
  `BggRawSnapshotParser.IsGenericEditionTitle(string? title)` debe retornar `true` para:
  - Cadenas con combinaciones de códigos o nombres de idioma con o sin separadores (ej. `"ENG/GER/FRE/SPA edition"`, `"ENG/SPA edition"`, `"ES/EN version"`, `"Multilingual edition"`, `"English / Spanish edition"`).
  - Cadenas de formato o tirada que carecen de nombre de juego (ej. `"Retail edition"`, `"Deluxe edition"`, `"Kickstarter edition"`, `"First edition"`, `"2nd edition"`, `"Special edition"`, `"Collector's edition"`).
  - Cadenas formadas únicamente por editoriales o términos de edición (ej. `"Combo Games edition"`, `"Devir Spanish edition"`).

- **RF-02 (Limpieza de Sufijos y Supresión de Títulos Genéricos en Versiones):**
  - `BggRawSnapshotParser.CleanVersionTitle(string? rawTitle)` debe devolver `null` si el título completo es un descriptor genérico clasificado por `IsGenericEditionTitle`.
  - Debe limpiar sufijos como `" - ENG/GER/FRE/SPA edition"` o `" (Retail edition)"`, preservando la porción legítima del título si no es genérica.
  - `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(string rawJson)` debe asignar `Title = null` cuando la versión en español solo ofrezca un descriptor genérico, y no deba sobreescribir el título canónico del juego.

- **RF-03 (Protección en Parseo XML Raíz):**
  - `BggXmlParser.ExtractSpanishTitle` no debe promover nombres alternativos (`type="alternate"`) que clasifiquen como `IsGenericEditionTitle`.

- **RF-04 (Saneamiento Determinista en Base de Datos):**
  - `CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync` debe ampliar su selección de candidatos en EF Core para incluir juegos cuyo `SpanishTitle` contenga `"edition"`, `"edicion"`, `"edición"`, `"version"`, `"versión"`, o combinaciones con barras (`/`).
  - Todo juego cuyo `SpanishTitle` sea detectado como genérico debe ser restaurado a `OriginalTitle` (o al título localizado legítimo si el snapshot contiene uno válido).
  - Debe asegurar la reparación prioritaria de *Queen Alice* (BggId 456236).

## Requisitos No Funcionales

- **RNF-01 (Idempotencia y Eficiencia):** La consulta y saneamiento por lotes de `CatalogDataSanitizer` deben ser idempotentes, sin degradar el tiempo de arranque del host web.
- **RNF-02 (Integridad de Títulos Legítimos):** Títulos comerciales reales que contienen palabras como `"Edición"` o `"Deluxe"` como parte legítima de su nombre comercial (ej. `"Ciudadelas: Edición Deluxe"`, `"Alta Tensión"`) no deben ser alterados indebidamente si contienen un título sustantivo.
