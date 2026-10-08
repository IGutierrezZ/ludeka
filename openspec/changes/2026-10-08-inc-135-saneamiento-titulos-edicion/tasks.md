# Tareas: INC-135 Saneamiento de Descriptores de Edición en Títulos BGG y Reparación Automática de Catálogo

- [x] 1. **Pruebas Unitarias TDD en Rojo (Red Phase):**
  - [x] 1.1 Añadir en `BggRawSnapshotParserVersionsTests.cs` casos de prueba para acrónimos (`ENG/GER/FRE/SPA edition`, `ENG/SPA edition`), descriptores de tirada (`Retail edition`, `Deluxe edition`, `Multilingual edition`) y sufijos compuestos.
  - [x] 1.2 Añadir en `CatalogDataSanitizerTests.cs` una prueba que verifique la reparación de un juego con `SpanishTitle = "ENG/GER/FRE/SPA edition"` devolviéndolo a `OriginalTitle = "Queen Alice"`.
- [x] 2. **Implementación de Lógica Analítica en `BggRawSnapshotParser`:**
  - [x] 2.1 Ampliar `IsGenericEditionTitle` para soportar acrónimos lingüísticos, combinaciones con barras y descriptores de tirada.
  - [x] 2.2 Ampliar `CleanVersionTitle` para retirar sufijos de edición multilingüe con acrónimos.
  - [x] 2.3 Validar que `ExtractSpanishVersionInfoFromJson` conserve `Title = null` cuando el título de versión sea genérico.
- [x] 3. **Blindaje en `BggXmlParser`:**
  - [x] 3.1 Proteger `ExtractSpanishTitle` con `IsGenericEditionTitle`.
- [x] 4. **Actualización de `CatalogDataSanitizer`:**
  - [x] 4.1 Ampliar el filtro de selección SQL para incluir `edition`, `edición`, `version`, `versión` y BggId 456236.
  - [x] 4.2 Saneamiento y restauración en base de datos.
- [x] 5. **Verificación Completa y Cierre:**
  - [x] 5.1 Ejecutar suite completa de tests (`dotnet test`).
  - [x] 5.2 Actualizar `ROADMAP.md` y especificaciones vivas en `docs/specs/sistema/`.
  - [x] 5.3 Abrir PR y verificar CI/CD.
