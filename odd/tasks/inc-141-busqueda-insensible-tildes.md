# Tareas ODD — INC-141: Búsqueda Insensible a Tildes y Diacríticos en Catálogo y Búsqueda Rápida

- [x] **1. Core & Normalización (`Ludeka.Core`)**
  - [x] 1.1 Crear `TextNormalizer.cs` en `src/Ludeka.Core/Helpers/TextNormalizer.cs` con métodos puros `RemoveDiacritics(string text)` y `ToSearchSlugPattern(string text)`.
  - [x] 1.2 Crear tests unitarios en `tests/Ludeka.UnitTests/Core/Helpers/TextNormalizerTests.cs` cubriendo remoción de diacríticos, mayúsculas, cadenas vacías y generación de patrones de slug.
  - [x] 1.3 Reutilizar `TextNormalizer.RemoveDiacritics` en `CountryCatalog.cs` para evitar código duplicado.

- [x] **2. TDD - Fase Roja (`SqliteGameRepositoryTests`)**
  - [x] 2.1 Escribir pruebas unitarias en `SqliteGameRepositoryTests.cs`:
    - `SearchAsync_WithSearchTermWithoutDiacritics_ShouldFindAccentedGameTitle` (ej. buscar `"codigo 5"`, encontrar `"Código 5"`).
    - `QuickSearchAsync_WithSearchTermWithoutDiacritics_ShouldFindAccentedGameTitle`.
    - `SearchAsync_WithSearchTermDiacriticsMismatch_ShouldFindGame` (ej. buscar `"catan"` o `"borgona"`).
  - [x] 2.2 Ejecutar las nuevas pruebas y verificar que fallen en rojo con la implementación actual.

- [x] **3. Repositorio e Infraestructura (`SqliteGameRepository`)**
  - [x] 3.1 En `SqliteGameRepository.SearchAsync`, calcular `slugPattern = TextNormalizer.ToSearchSlugPattern(term)` y `cleanPattern = $"%{TextNormalizer.RemoveDiacritics(term).Trim()}%"`.
  - [x] 3.2 Añadir al predicado `WHERE` de SQLite y PostgreSQL la comprobación sobre `g.Slug` con `slugPattern` y sobre los campos de texto con `cleanPattern`.
  - [x] 3.3 Replicar la misma lógica en `SqliteGameRepository.QuickSearchAsync`.

- [x] **4. Verificación y Suite Completa**
  - [x] 4.1 Ejecutar pruebas unitarias de `SqliteGameRepositoryTests` y verificar que pasen a verde.
  - [x] 4.2 Ejecutar la suite completa de pruebas unitarias (`dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj`) y constatar 0 regresiones (2.788 pruebas superadas al 100%).
  - [x] 4.3 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
