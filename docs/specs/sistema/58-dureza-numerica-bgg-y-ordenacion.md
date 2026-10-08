# 58. Dureza Numérica de BGG, Extrapolación de Complejidad y Ordenación Precisa en Catálogo

## 1. Propósito y Resumen

Este módulo incorpora en Ludeka el valor cuantitativo y continuo de peso/dureza de BoardGameGeek (`averageweight`, escala decimal de 1.00 a 5.00), sustituyendo la dependencia exclusiva de la heurística cualitativa de tres niveles (`Light`, `Medium`, `Heavy`). Proporciona una ordenación matemática exacta en el catálogo (`ComplexityAsc` y `ComplexityDesc`) tanto a nivel de motor SQL nativo (SQLite y PostgreSQL) como en el índice en memoria (`GameFilterIndexItem`), garantiza la persistencia normalizada con migraciones de base de datos e índices dedicados, procesa el backfill retroactivo automático desde los datos crudos de `BggRawSnapshots` en el pipeline de arranque, y traslada la precisión del dato a la interfaz editorial tanto en la ficha de juego (`GameDetail.razor`) como en las tarjetas de catálogo (`GameCard.razor` y `GameListItem.razor`) cumpliendo con las directrices de accesibilidad WCAG 2.2 AA.

---

## 2. Dominio y Reglas de Negocio (`Ludeka.Core`)

### 2.1 Propiedades en `Game`
Se añaden a la entidad principal `Game` las siguientes definiciones:
- `double? BggWeight`: Valor decimal continuo [1.00 - 5.00] con redondeo bancario a dos posiciones decimales. Es nullable para contemplar juegos pendientes de votación suficiente en BGG.
- `GameComplexity Complexity`: Propiedad calculada de solo lectura que delega en el motor canónico `ComplexityCalculator.Calculate(BggWeight, Style, Duration.MaxMinutes, Age.CommunityAge)`.

### 2.2 Método de Actualización y Clamping
- `UpdateBggWeight(double? weight)`: Aplica clamping defensivo entre `1.0` y `5.0` a cualquier valor positivo, y redondea a dos cifras decimales (`Math.Round(w.Value, 2, MidpointRounding.AwayFromZero)`). Trata valores menores o iguales a `0` como `null`.

### 2.3 Motor Canónico de Complejidad (`ComplexityCalculator`)
Ubicado en `Ludeka.Core/Helpers/ComplexityCalculator.cs`:
1. **Umbrales Estándar de la Comunidad BGG:**
   - `< 2.20`: `GameComplexity.Light` (Ligero)
   - `2.20` a `< 3.25`: `GameComplexity.Medium` (Medio)
   - `≥ 3.25`: `GameComplexity.Heavy` (Duro / Complejo)
2. **Fallback Heurístico Resiliente:** Si `BggWeight` es nulo o no disponible, evalúa de forma determinista `GameStyle`, `Duration` y `Age`.
3. **Cálculo Canónico de Peso Efectivo Continuo (INC-134):** `GetEffectiveWeight(...)` devuelve el valor de `BggWeight` si está presente, o extrapola un valor numérico decimal continuo (`1.60` para Ligero, `2.70` para Medio, `3.80` para Duro). Esto garantiza un ordenamiento monotónico y continuo de todos los juegos del catálogo, evitando que los títulos sin peso votado queden relegados al final de la lista.
4. **Extractor Defensivo de Snapshots:** `ExtractWeightFromJson(string? rawJson)` procesa payloads crudos JSON de BGG buscando de forma segura `poll-summary` o `statistics.ratings.averageweight`.

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
- **Ordenación SQL Continua (`ApplyQuerySorting`):**
  - Proyecta mediante expresión condicional (`CASE WHEN ... THEN ... ELSE ... END`) el cálculo exacto del peso efectivo continuo (usando `BggWeight` o las extrapolaciones `1.60` / `2.70` / `3.80`), eliminando la agrupación de nulos al final y desempatando por ranking BGG y valoración.
- **Ordenación en Índice L1 (`ApplyIndexSorting`):** Ordenación monotónica decimal utilizando `ComplexityCalculator.GetEffectiveWeight(g.BggWeight, g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge)` con desempates por `BggRank` y valoración.
- **Backfill Autónomo en Arranque (`Program.cs`):** Invocación de `BackfillBggWeightsFromSnapshotsAsync` dentro del bloque de inicialización de `Program.cs`. Procesa en lotes de 250 elementos sin degradar la memoria y de forma estrictamente idempotente (`g.BggWeight == null`).

---

## 4. Transferencia y Presentación Web (`Ludeka.Application` y `Ludeka.Web`)

### 4.1 DTOs
- `GameFilterIndexItem.BggWeight`
- `GameDetailDto.BggWeight`
- `GameSummaryDto.BggWeight`, con propiedades calculadas `Complexity`, `EffectiveWeight`, `ComplexityDisplayBadge` y `ComplexityTooltip`.

### 4.2 Interfaz de Ficha Editorial (`GameDetail.razor`)
1. **Eyebrow Editorial:** Muestra la dureza compacta con decimal y categoría en mayúsculas (ej. `2.5/5 MEDIO`), respetando el tono sobrio de Revista Lúdica.
2. **Bloque Técnico (ADN Lúdico):** Bajo el acordeón `_isTechOpen`, expone la etiqueta `Dureza / Peso` formateada a dos decimales y categoría en español (`2.50 / 5 (Medio)`).

### 4.3 Tarjetas de Catálogo (`GameCard.razor` y `GameListItem.razor`)
- Incorporación de la píldora textual accesible de dureza (`· 2.4/5` o `· Medio`), con atributos semánticos `title` y `aria-label` WCAG 2.2 AA (`Dureza: 2.4/5 (Medio)` o `Dureza estimada: Medio`), permitiendo al usuario corroborar visualmente la ordenación seleccionada.

---

## 5. Verificación y Cobertura Automatizada

- **Pruebas de Dominio:** [`GameBggWeightAndComplexityTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Domain/GameBggWeightAndComplexityTests.cs) (16 pruebas cubriendo clamping, redondeo, umbrales, fallbacks y cálculo continuo de `GetEffectiveWeight`).
- **Pruebas de DTO y Presentación:** [`GameSummaryDtoComplexityTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Domain/GameSummaryDtoComplexityTests.cs) (pruebas de badges y tooltips accesibles).
- **Pruebas de Ingesta XML:** [`BggXmlParserTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Bgg/BggXmlParserTests.cs) (22 pruebas verificando extracción de peso y resistencia a XMLs incompletos).
- **Pruebas de Persistencia y Repositorio:** [`SqliteGameRepositoryTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs) (28 pruebas comprobando ordenación ascendente, descendente y en dos fases por peso continuo).
- **Pruebas de Contratos de Interfaz:** [`WebMarkupContractTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/WebMarkupContractTests.cs) (111 pruebas) y [`CatalogPaginationContractTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.UnitTests/Web/CatalogPaginationContractTests.cs) (18 pruebas certificando el renderizado en tarjetas y listas).
- **Pruebas de Integración PostgreSQL:** [`PostgresSchemaVerificationTests.cs`](file:///f:/repos/Ludeka/tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs) (24 migraciones verificadas en Testcontainers).
- **Suite Completa del Monorepo:** 2.712 pruebas unitarias pasando al 100% en verde (0 errores, 0 fallos).
