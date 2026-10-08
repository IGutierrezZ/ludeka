# Especificación: INC-136 — Refresco de Versiones BGG de Novedades, Soporte Editorial Lúdilo y Saneamiento de Catálogo

## 1. Requerimientos Funcionales

- **RF-01 (Mapeo Editorial de Lúdilo):**
  - `RegionalPublisherMatcher.Match` debe reconocer `"Lúdilo"`, `"Ludilo"`, `"Lúdilo Games"` y `"Ludilo Games"` como editorial oficial española (`CountryCode = "ES"`, `OfficialName = "Lúdilo"`, `Slug = "ludilo"`).
  
- **RF-02 (Saneamiento Inmediato de Código 5):**
  - `CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync` (y `EnsureKnownPriorityGamesRepairedAsync`) debe reparar de forma prioritaria el juego BggId `453526` (*Got Five!*), estableciendo `SpanishTitle = "Código 5"` y `SpanishPublisher = "Lúdilo"`.
  - Esta operación debe ser idempotente y ejecutarse en O(1) durante el arranque del host web.

- **RF-03 (Detección de Snapshots Candidatos a Refresco):**
  - `IBggRawSnapshotRepository` debe proveer el método `GetBggIdsNeedingVersionRefreshAsync(int minYear, int limit, CancellationToken ct)`.
  - Debe seleccionar snapshots de juegos cuyo año de publicación sea mayor o igual a `minYear` (por defecto año actual - 1) que carezcan de versión en español en su subárbol `versions`.

- **RF-04 (Extracción y Resincronización de Versiones):**
  - `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson` debe procesar correctamente un subárbol de versión con título `"Código 5 - Spanish edition (2026)"` e idioma `"Spanish"`, devolviendo `Title = "Código 5"` y `Publisher = "Lúdilo"`.

## 2. Requerimientos No Funcionales

- **RNF-01 (Compatibilidad Dual SQLite / PostgreSQL):**
  - Las consultas de candidatos de refresco deben ejecutarse correctamente tanto en SQLite (desarrollo/tests) como en PostgreSQL (Supabase en producción).
- **RNF-02 (Cero Regresiones y Rendimiento de Arranque):**
  - El arranque del host web no debe degradar sus tiempos; la reparación prioritaria de juegos conocidos se ejecuta en memoria sobre las entidades rastreadas sin consultas pesadas adicionales.
- **RNF-03 (TDD y Cobertura Completa):**
  - La suite de 2.740 pruebas debe mantenerse en verde, añadiendo pruebas unitarias específicas para cada uno de los requerimientos funcionales.
