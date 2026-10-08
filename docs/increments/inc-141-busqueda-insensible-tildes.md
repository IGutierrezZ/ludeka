# INC-141: Búsqueda Insensible a Tildes y Diacríticos en Catálogo y Búsqueda Rápida

## 1. Contexto y Problema Detectado

Al realizar búsquedas en el catálogo (`/catalogo`, `IGameRepository.SearchAsync`) y en los modales de búsqueda rápida (`IGameRepository.QuickSearchAsync`), el motor relacional exige coincidencias exactas en caracteres diacríticos o con tilde. Por ejemplo, al buscar `"codigo 5"` sin tilde, no se localiza el juego *"Código 5"*, obligando al usuario a escribir explícitamente `"código 5"` con tilde. De igual forma ocurre con títulos como *"Agrícola"* (no encontrado al escribir `"agricola"`), *"Los Castillos de Borgoña"* (no encontrado al escribir `"borgona"`), o con nombres de diseñadores y editoriales.

### Causas Raíz Técnicas Identificadas:
1. **Comportamiento de `EF.Functions.ILike` (PostgreSQL) y `EF.Functions.Like` (SQLite):**
   - Aunque ambos operadores ignoran mayúsculas y minúsculas (insensibilidad a mayúsculas), son estrictos respecto a acentos y diacríticos (`"codigo"` != `"código"`).
2. **Incompatibilidad de `EF.Functions.Unaccent` con SQLite:**
   - La extensión `unaccent` de PostgreSQL / Npgsql no es compatible con SQLite en memoria o disco empleado en el entorno local y en los más de 2.760 tests unitarios y de integración de la solución.
3. **Columna `Slug` normalizada y desprovista de tildes:**
   - Toda entidad `Game` ya genera y almacena su `Slug` canónico (`IX_Games_Slug`) aplicando descomposición canónica (`NormalizationForm.FormD`) que elimina tildes y diacríticos (ejemplo: `"Código 5"` -> `"codigo-5"`, `"Agrícola"` -> `"agricola"`).

---

## 2. Objetivos del Incremento

1. **Helper Centralizado en Dominio (`Ludeka.Core.Helpers.TextNormalizer`):**
   - Centralizar la función pura `RemoveDiacritics(string text)` y utilidades de normalización en `Ludeka.Core.Helpers.TextNormalizer`.
   - Generar patrones de búsqueda sobre slugs (`ToSearchSlugPattern(string text)`), transformando secuencias de caracteres no alfanuméricos en comodines `%` (ejemplo: `"codigo 5"` -> `"%codigo%5%"`).

2. **Búsqueda Insensible a Tildes en `SqliteGameRepository.SearchAsync` y `QuickSearchAsync`:**
   - Enriquecer el predicado `WHERE` de búsqueda para evaluar `g.Slug` contra el patrón normalizado del término (`slugPattern`).
   - Generar el término limpio sin tildes (`cleanTerm`) y su patrón `cleanPattern` (`$"%{cleanTerm}%"`).
   - Realizar la búsqueda tanto con el término original `pattern` como con `cleanPattern` sobre `SpanishTitle`, `OriginalTitle`, `SpanishPublisher`, `Publisher` y `Designer`.
   - Mantener la compatibilidad nativa dual entre SQLite (desarrollo, CI y tests) y PostgreSQL (producción en Google Cloud Run / Supabase).

3. **Pruebas de Regresión y Contrato (TDD Estricto):**
   - Pruebas unitarias en `SqliteGameRepositoryTests.cs` verificando que buscar `"codigo 5"`, `"agricola"` o `"borgona"` devuelva los juegos con tildes y diacríticos en `SearchAsync` y `QuickSearchAsync`.
   - Pruebas unitarias de `TextNormalizer` en `Ludeka.UnitTests/Core/Helpers/TextNormalizerTests.cs`.
   - Suite completa en verde con 0 regresiones.

---

## 3. Plan de Tareas (ODD)

- [ ] **Tarea 1 (Core & Normalización):** Crear `TextNormalizer` en `src/Ludeka.Core/Helpers/TextNormalizer.cs` y sus tests unitarios.
- [ ] **Tarea 2 (TDD - Fase Roja):** Escribir pruebas unitarias en `SqliteGameRepositoryTests.cs` buscando juegos acentuados con términos sin tildes, comprobando su fallo antes de la implementación.
- [ ] **Tarea 3 (Implementación en Repositorio):** Modificar `SqliteGameRepository.SearchAsync` y `QuickSearchAsync` para incorporar la búsqueda sobre `g.Slug` y `cleanPattern`.
- [ ] **Tarea 4 (Verificación):** Confirmar fase verde en `SqliteGameRepositoryTests`, ejecutar la suite completa de tests de la solución y actualizar el roadmap.
