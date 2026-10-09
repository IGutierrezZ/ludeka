# Tareas de Implementación: INC-145 Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos de Catálogo

- [x] 1. Pruebas Unitarias de Parser (TDD Rojo)
  - [x] 1.1 Añadir en `BggRawSnapshotParserVersionsTests.cs` casos para `IsGenericEditionTitle`: `"Z-Man Spanish edition"`, `"Iberian edition"`, `"CAT/ENG/ITA/POR/SPA edition"`, `"Print & Play edition"`.
  - [x] 1.2 Añadir prueba de extracción `ExtractSpanishVersionInfoFromJson` con snapshot de *Pandemic Legacy: Season 2* (BggId 221107) verificando `Title = "Pandemic Legacy: Segunda temporada"` y `Publisher = "Devir"`.
  - [x] 1.3 Añadir prueba de extracción con `canonicalname` presente frente a `name` genérico (`"Iberian edition"` -> `"Beacon Patrol"`).

- [x] 2. Implementación de Parser (`BggRawSnapshotParser.cs`)
  - [x] 2.1 Actualizar `ExtractVersionTitle` para priorizar `canonicalname` sobre `name`.
  - [x] 2.2 Ampliar `IsGenericEditionTitle` con tokens de editoriales (`z-man`, `lúdilo`, etc.), regiones (`iberian`, etc.), códigos lingüísticos y formatos de edición.
  - [x] 2.3 Refinar `CleanVersionTitle` para garantizar que descriptores como `"Z-Man Spanish edition"` devuelvan `null`.

- [x] 3. Saneamiento en Catálogo (`CatalogDataSanitizer.cs`)
  - [x] 3.1 Añadir en `EnsureKnownPriorityGamesRepairedAsync` la corrección forzada de BggId 221107 (*Pandemic Legacy: Season 2*) a `"Pandemic Legacy: Segunda temporada"` y editorial `"Devir"`.
  - [x] 3.2 Ampliar la consulta de candidatos en `SanitizeCorruptedSpanishTitlesAsync` para detectar `"iberian"` y otros descriptores genéricos.
  - [x] 3.3 Añadir prueba unitaria en `CatalogDataSanitizerTests.cs` validando la reparación de BggId 221107.

- [x] 4. Verificación y Saneamiento en Local
  - [x] 4.1 Ejecutar suite completa de tests (`dotnet test`).
  - [x] 4.2 Probar saneamiento contra la base de datos PostgreSQL local (Docker) y verificar estado de `pandemic-legacy-season-2`, `beacon-patrol`, etc.
