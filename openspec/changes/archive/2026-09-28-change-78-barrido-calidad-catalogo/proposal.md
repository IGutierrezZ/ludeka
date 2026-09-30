# Propuesta SDD: INC-78 — Barrido Completo de Calidad de Catálogo (~10.000 Juegos Promovidos)

## 1. Problema
Tras la entrega de INC-77, la lógica de inferencia de estilo, duraciones por jugador y preservación de votos comunitarios está completamente corregida. Sin embargo, el filtro de selección de candidatos para backfill en producción (`GetGamesPendingQualityBackfillAsync`) se apoya exclusivamente en la ausencia de votos comunitarios en escalabilidad (`s.Count == 0 || s.All(votos == 0)`).

En producción existen ~10.000 juegos ya promovidos a catálogo. Solo ~4.000 de ellos son seleccionados por dicho filtro. Los ~6.000 restantes conservan votos de escalabilidad pero mantienen el estilo fijado como `GameStyle.Eurogame` por la promoción histórica y la duración colapsada a 15 min. No se puede filtrar simplemente por `Style == Eurogame` porque los Eurogames legítimos (*Agrícola*, *Catán*) nunca saldrían de la condición, entrando en bucle infinito.

## 2. Solución Propuesta
Implementar un mecanismo de **Barrido Completo por Cursor (`BggId > afterBggId`)** que garantice la auditoría secuencial y exhaustiva del 100% de los juegos del catálogo:

1. **Repositorio (`SqliteGameRepository`):**
   - `GetGamesCursorPagedAsync(int afterBggId, int limit, CancellationToken ct)`: consulta `Games` ordenando por `BggId` ascendente con cláusula `g.BggId > afterBggId`. Paginación $O(1)$ sin offset costoso, determinista y finita.
   - `GetTotalCatalogCountAsync(CancellationToken ct)`: conteo total de títulos en catálogo para informar del progreso exacto.

2. **Servicio (`BggMassIngestionService`):**
   - `SweepCatalogQualityBatchAsync(int afterBggId, int batchSize, CancellationToken ct)` y `RunScheduledSweepCatalogQualityBatchAsync`.
   - Para cada juego del lote de 50:
     - Comprueba si en staging ya existe `<dna ` en `RawThingXml` para actualización instantánea en memoria.
     - Si no existe, consulta BGG XMLAPI2 para refrescar ADN, duraciones, fundas y escalabilidad.
     - Aplica cambios solo si existen diferencias (`UpdateDna`, `UpdateDuration`, etc.).
     - Si el juego ya está correcto, incrementa `SkippedCount` y omite el `UpdateAsync` a BD (idempotencia real).
     - Devuelve `BggQualitySweepBatchResultDto` con `LastBggIdProcessed` y `HasMore`.

3. **Superficie de Ejecución Web y Jobs:**
   - **Blazor Admin (`CatalogQueueAdmin.razor`):** Botón «Barrido Completo de Calidad (~10.000)» con ejecución reactiva en segundo plano, indicador de progreso `X/Total` y botón de parada.
   - **Cloud Run Jobs (`BackfillQualityJobRunner`):** Bucle continuo que recorre de `0` hasta el último `BggId`.

## 3. Impacto y Riesgos
- **Sin Migraciones de Esquema:** No añade columnas nuevas a `Games` ni altera la estructura de base de datos.
- **Rendimiento:** La estrategia *Staging-First* resuelve los juegos ya cacheados a velocidad de memoria/disco sin tocar BGG. Para los no cacheados, respeta la pausa de cortesía de BGG.
- **Trazabilidad:** Métricas segregadas de evaluados, actualizados, omitidos (ya correctos) y fallidos.
