# Incremento 134: Backfill Autónomo de Dureza BGG, Extrapolación de Peso Efectivo y Ordenación Resiliente en Catálogo

- **ID del Incremento:** `INC-134`
- **Slug:** `ordenacion-dureza-catalogo`
- **Rama:** `inc/ordenacion-dureza-catalogo`
- **Fecha:** 2026-10-08
- **Estado:** ⏳ En progreso (ODD / SDD)
- **Épica / Contexto:** Resiliencia de Datos, Dominio Canónico y Experiencia de Catálogo.

---

## 1. Descripción del Problema y Objetivos

Tras la incorporación de `BggWeight` en el INC-132, la ordenación por dureza (`ComplexityAsc` y `ComplexityDesc`) en el catálogo no produce la ordenación esperada debido a dos causas fundamentales:
1. **Falta de ejecución del backfill en arranque:** Las bases de datos operativas existentes (SQLite y PostgreSQL) albergan filas con `BggWeight = NULL` porque la columna se añadió como nula pero `BackfillBggWeightsFromSnapshotsAsync` no fue integrado en el ciclo de arranque (`Program.cs` / `SqliteSchemaMigrator`).
2. **Degradación del criterio de ordenación con nulos:** Tanto `ApplyQuerySorting` (SQL) como `ApplyIndexSorting` (memoria) sitúan los valores nulos al final (`g.BggWeight.HasValue ? 0 : 1`) y desempatan por `BggRank`. Al tener la totalidad o mayoría de registros con `NULL`, la lista resultante es idéntica a la ordenación por ranking BGG, inutilizando el criterio de ordenación por dureza. Además, los juegos sin votos en BGG quedan completamente desprovistos de criterio de ordenación en lugar de recibir un peso efectivo extrapolado.

### Objetivos Principales:
1. **Dominio Canónico (`Ludeka.Core`):**
   - Incorporar en `ComplexityCalculator` el cálculo de **peso efectivo continuo** (`GetEffectiveWeight`), devolviendo `BggWeight` si existe o extrapolando un valor numérico representativo (ej. 1.60 para Ligero, 2.70 para Medio, 3.80 para Duro) a partir del estilo, duración y edad comunitaria.
2. **Reconciliación y Backfill Autónomo en Arranque (`Ludeka.Infrastructure` / `Ludeka.Web`):**
   - Integrar la invocación automática y no bloqueante de `BackfillBggWeightsFromSnapshotsAsync` en el arranque de la aplicación (`Program.cs` / `SqliteSchemaMigrator`), de modo que todo juego con snapshot recupere su peso BGG real sin intervención manual.
3. **Ordenación Resiliente en Catálogo (`Ludeka.Infrastructure`):**
   - En `ApplyIndexSorting`, ordenar por `GetEffectiveWeight(...)`, garantizando que todos los títulos queden ordenados monotónicamente de forma ascendente o descendente.
   - En `ApplyQuerySorting`, ordenar usando `COALESCE` o condicionales SQL que asignen el peso efectivo extrapolado para filas con `BggWeight IS NULL`, eliminando la relegación ciega al final por `BggRank`.
4. **Presentación Editorial en Tarjetas de Catálogo (`Ludeka.Web`):**
   - Exponer en `GameCard.razor` y `GameListItem.razor` la dureza numérica/cualitativa de forma discreta y elegante, acorde a las directrices de diseño de Revista Lúdica y WCAG 2.2 AA.
5. **Verificación Estricta TDD:**
   - Pruebas unitarias de dominio para `GetEffectiveWeight`.
   - Pruebas de integración y repositorio verificando ordenación con mezcla de juegos con y sin `BggWeight`.
   - Pruebas de contratos de marcado para tarjetas de catálogo.
