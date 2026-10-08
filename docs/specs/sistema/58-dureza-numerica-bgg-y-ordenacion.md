# 58. Dureza Numérica de BGG, Extrapolación de Complejidad y Ordenación Precisa en Catálogo

## 1. Propósito y Resumen

Este módulo incorpora en Ludeka el valor cuantitativo y continuo de peso/dureza de BoardGameGeek (`averageweight`, escala decimal de 1.00 a 5.00), sustituyendo la dependencia exclusiva de la heurística cualitativa de tres niveles (`Light`, `Medium`, `Heavy`). Proporciona una ordenación matemática exacta en el catálogo (`ComplexityAsc` y `ComplexityDesc`) tanto a nivel de motor SQL nativo (SQLite y PostgreSQL) como en el índice en memoria (`GameFilterIndexItem`), garantiza la persistencia normalizada con migraciones de base de datos e índices dedicados, procesa el backfill retroactivo desde los datos crudos de `BggRawSnapshots`, y traslada la precisión del dato a la interfaz editorial de la ficha de juego (`GameDetail.razor`) sin perder la legibilidad accesible para cualquier tipo de jugador.

---

## 2. Dominio y Reglas de Negocio (`Ludeka.Core`)

### 2.1 Propiedades en `Game`
Se añaden a la entidad principal `Game` las siguientes definiciones:
- `double? BggWeight`: Valor decimal continuo [1.00 - 5.00] con redondeo bancario a dos posiciones decimales. Es nullable para contemplar juegos pendientes de votación suficiente en BGG.
- `GameComplexity Complexity`: Propiedad calculada de solo lectura que delega en el motor canónico `ComplexityCalculator.CalculateComplexity(BggWeight, Style, Duration.MaxMinutes, Age.CommunityAge)`.

### 2.2 Método de Actualización y Clamping
- `UpdateBggWeight(double? weight)`: Aplica clamping defensivo entre `1.0` y `5.0` a cualquier valor positivo, y redondea a dos cifras decimales (`Math.Round(w.Value, 2, MidpointRounding.AwayFromZero)`). Trata valores menores o iguales a `0` como `null`.

### 2.3 Motor Canónico de Complejidad (`ComplexityCalculator`)
Ubicado en `Ludeka.Core/Helpers/ComplexityCalculator.cs`:
1. **Umbrales Estándar de la Comunidad BGG:**
   - `< 2.20`: `GameComplexity.Light` (Ligero)
   - `2.20` a `< 3.25`: `GameComplexity.Medium` (Medio)
   - `≥ 3.25`: `GameComplexity.Heavy` (Duro / Complejo)
2. **Fallback Heurístico Resiliente:** Si `BggWeight` es nulo o no disponible, evalúa de forma determinista `GameStyle`, `Duration` y `Age`.
3. **Extractor Defensivo de Snapshots:** `ExtractBggWeightFromJson(string? rawJson)` procesa payloads crudos JSON de BGG buscando de forma segura `poll-summary` o `statistics.ratings.averageweight`.

---

## 3. Persistencia e Infraestructura (`Ludeka.Infrastructure`)

### 3.1 Extracción XML en `BggXmlParser`
Se actualiza el analizador de XML de BGG para extraer de `<statistics><ratings>` el elemento:
```xml
<averageweight value="2.4789" />
```
El valor es parseado defensivamente con cultura invariante y asignado a `BggGameDto.BggWeight`.

### 3.2 Migraciones de Base de Datos e Índices
- Columna `BggWeight` de tipo `REAL` (SQLite) / `double precision` (PostgreSQL) en la tabla `Games`.
- Índice `IX_Games_BggWeight` para garantizar consultas y ordenaciones de alto rendimiento.
- Migración oficial EF Core: `20261008143509_AddGameBggWeight.cs`.
- Migración idempotente en `SqliteSchemaMigrator` para entornos locales y tests en memoria.
- Ajuste del canario de integración `PostgresSchemaVerificationTests` a 24 migraciones esperadas.

### 3.3 Repositorio y Consultas SQL (`SqliteGameRepository`)
- **Proyección de Catálogo:** `GameFilterIndexItem` incluye `double? BggWeight = null`, proyectado en la primera fase de paginación (`SearchAsync`).
- **Filtrado por Facetas:** Se evalúa la pertenencia a las categorías seleccionadas (`criteria.Complexities`) evaluando en memoria la complejidad calculada por `ComplexityCalculator`.
- **Ordenación SQL (`ApplyQuerySorting`):**
  - `ComplexityAsc`: `g.BggWeight.HasValue ? 0 : 1`, seguido de `g.BggWeight ASC`, situando los juegos sin dureza al final de la consulta.
  - `ComplexityDesc`: `g.BggWeight.HasValue ? 0 : 1`, seguido de `g.BggWeight DESC`, situando los nulos al final.
- **Ordenación en Índice L1 (`ApplyIndexSorting`):** Ordenación monotónica decimal por `g.BggWeight` con nulos al final y desempates deterministas por `BggRank` y nombre.
- **Backfill Autónomo:** Método `BackfillBggWeightsFromSnapshotsAsync(int batchSize, CancellationToken ct)` para enriquecer juegos preexistentes a partir de `BggRawSnapshots`.

---

## 4. Transferencia y Presentación Web (`Ludeka.Application` y `Ludeka.Web`)

### 4.1 DTOs
- `GameFilterIndexItem.BggWeight`
- `GameDetailDto.BggWeight`
- `GameSummaryDto.BggWeight`

### 4.2 Interfaz de Ficha Editorial (`GameDetail.razor`)
1. **Eyebrow Editorial:** Muestra la dureza compacta con decimal y categoría en mayúsculas (ej. `2.5/5 MEDIO`), respetando el tono sobrio de Revista Lúdica.
2. **Bloque Técnico (ADN Lúdico):** Bajo el acordeón `_isTechOpen`, expone la etiqueta `Dureza / Peso` formateada a dos decimales y categoría en español (`2.50 / 5 (Medio)`), apoyándose en `ComplexityCalculator.FormatWeight(dto.BggWeight, dto.Complexity)`.

---

## 5. Verificación y Cobertura Automatizada

- **Pruebas de Dominio:** [`GameBggWeightAndComplexityTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Domain/GameBggWeightAndComplexityTests.cs) (12 pruebas cubriendo clamping, redondeo, umbrales y fallbacks).
- **Pruebas de Ingesta XML:** [`BggXmlParserTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Bgg/BggXmlParserTests.cs) (22 pruebas verificando extracción de peso y resistencia a XMLs incompletos).
- **Pruebas de Persistencia y Repositorio:** [`SqliteGameRepositoryTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs) (27 pruebas comprobando ordenación ascendente y descendente con nulos al final).
- **Pruebas de Contratos de Interfaz:** [`GameDetailEditorialBlocksContractTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Web/GameDetailEditorialBlocksContractTests.cs) (13 pruebas certificando el renderizado en el eyebrow y bloque técnico).
- **Pruebas de Integración PostgreSQL:** [`PostgresSchemaVerificationTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs) (24 migraciones verificadas en Testcontainers).
- **Suite Completa del Monorepo:** 2.696 pruebas unitarias e integración pasando al 100% en verde (0 errores, 0 fallos).
