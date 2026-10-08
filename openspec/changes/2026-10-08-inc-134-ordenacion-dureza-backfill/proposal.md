# Propuesta: INC-134 — Backfill Autónomo de Dureza BGG, Extrapolación de Peso Efectivo y Ordenación Resiliente en Catálogo

## Metadatos
- **Fecha:** 2026-10-08
- **Incremento:** INC-134
- **Rama:** `inc/ordenacion-dureza-catalogo`
- **Slug:** `ordenacion-dureza-catalogo`
- **Estado:** En curso (ODD / SDD)

## Contexto y Motivación
Tras la incorporación inicial de `BggWeight` en el INC-132, la ordenación por dureza en el catálogo (`ComplexityAsc` y `ComplexityDesc`) presenta fallos de funcionamiento en bases de datos operativas reales (tanto SQLite local como PostgreSQL en producción):
1. **Ausencia de ejecución del backfill en arranque:** Las filas existentes en `Games` poseen `BggWeight = NULL` porque la migración añadió la columna sin rellenarla, y `BackfillBggWeightsFromSnapshotsAsync` no fue enlazado en el ciclo de arranque (`Program.cs` / `SqliteSchemaMigrator`).
2. **Degradación del criterio de ordenación SQL y memoria:** Al ordenar por dureza, los nulos se agrupan al final (`g.BggWeight.HasValue ? 0 : 1`) y se desempatan por `BggRank`. Al tener todos los juegos `BggWeight = NULL`, la lista resultante es idéntica a la ordenación por ranking BGG. Además, los juegos sin votos en BGG pierden la extrapolación heurística de dureza de `ComplexityCalculator`.
3. **Falta de visualización en tarjetas de catálogo:** `GameCard.razor` y `GameListItem.razor` no muestran el peso numérico ni la insignia de dureza, impidiendo que el usuario perciba el criterio de ordenación seleccionado.

## Objetivos
1. **Peso Efectivo en Dominio (`ComplexityCalculator.GetEffectiveWeight`):** Proveer un método canónico que devuelva `BggWeight` o extrapole un valor decimal continuo determinista (1.60 Ligero, 2.70 Medio, 3.80 Duro) basado en estilo, duración y edad.
2. **Backfill Autónomo no bloqueante en Arranque:** Ejecutar `BackfillBggWeightsFromSnapshotsAsync` en el arranque de la aplicación web (`Program.cs` y `SqliteSchemaMigrator`).
3. **Ordenación Resiliente en Repositorio:** Actualizar `ApplyQuerySorting` y `ApplyIndexSorting` para emplear el peso efectivo en `ComplexityAsc` y `ComplexityDesc`, evitando que los registros sin peso de BGG queden relegados al final agrupados por ranking BGG.
4. **Presentación Editorial en Catálogo:** Exhibición visual de la dureza en `GameCard.razor` y `GameListItem.razor`.
5. **Cobertura Automatizada TDD:** Pruebas unitarias de dominio, repositorio, arranque y contratos web.

## No Objetivos
- No se fuerza la llamada a APIs externas de BGG en el arranque; el backfill opera estrictamente contra `BggRawSnapshots` locales.
- No se elimina el soporte de `BggWeight == null` en la base de datos (sigue siendo nullable para reflejar con fidelidad la ausencia de votos en BGG).
