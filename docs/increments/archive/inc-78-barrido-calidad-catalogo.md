# INC-78: Barrido Completo de Calidad de Catálogo (~10.000 Juegos Promovidos)

> **Estado:** ✅ Completado y Archivado  
> **Fecha de Inicio:** 2026-09-28 · **Fecha de Cierre:** 2026-09-28  
> **Rama de Trabajo:** `inc/barrido-calidad-catalogo`  
> **Worktree:** `C:\repos\ludeka-wt\barrido-calidad-catalogo`  
> **Dependencias:** INC-73 (Enriquecimiento Masivo), INC-77 (Calidad Ingesta BGG)  
> **Pruebas Automatizadas Verificadas:** 2.037 pruebas (2.027 unitarias + 10 de integración) al 100% en verde  
> **Especificación Viva:** [`01. Catálogo y Ficha Inteligente`](../specs/sistema/01-catalogo-y-fichas.md) y [`45. Saneamiento de Calidad en Ingesta BGG`](../specs/sistema/45-saneamiento-calidad-ingesta-bgg-escalabilidad-adn.md)  
> **Metodología:** Spec-Driven Development (SDD) con verificación de suite de pruebas  

---

## 1. Contexto y Diagnóstico

En la base de datos de producción existen actualmente **~10.000 títulos ya promovidos** a la tabla `Games` (catálogo definitivo de Ludeka).
Al introducir INC-77 se saneó la lógica de inferencia de estilo (subdominios BGG), duración por jugador y escalabilidad. Sin embargo, el filtro de selección de títulos pendientes de enriquecimiento (`GetGamesPendingQualityBackfillAsync`) se limitaba a:

```csharp
g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)
```

Esto generaba la siguiente discordancia en producción:
1. **Solo ~4.000 títulos eran seleccionados:** Aquellos que sufrieron el reseteo de votos de escalabilidad o no tenían encuesta comunitaria en BGG.
2. **~6.000 títulos quedaban excluidos:** Al tener votos comunitarios (`BestVotes > 0`), el filtro los daba por «completos», pero al haberse catalogado antes de INC-77:
   - Siguen homogeneizados como `GameStyle.Eurogame` (falsos eurogames en títulos temáticos, wargames o party).
   - Su duración estimada sigue colapsada a 15 min/jugador en juegos de media y larga duración.
   - Posibles discrepancias en fundas o títulos en español.
3. No es viable filtrar simplemente por `Style == Eurogame` porque los Eurogames auténticos (*Agrícola*, *Catán*, *Concordia*) mantendrán legítimamente ese estilo tras auditarse, provocando bucles infinitos en cualquier filtro de condición estática.

---

## 2. Alcance y Arquitectura de la Solución (INC-78)

### Componente 1: Paginación Determinista por Cursor en Repositorio (`IGameRepository`)
- Añadido `GetGamesCursorPagedAsync(int afterBggId, int limit, CancellationToken ct)` en `IGameRepository` y su implementación en `SqliteGameRepository`.
- La consulta ordena estrictamente por `BggId` ascendente (`g.BggId > afterBggId`).
- Paginación $O(1)$ sin `Skip`, determinista, garantizando que cada juego del catálogo se visita exactamente una vez sin posibilidad de bucle infinito.
- Añadido `GetTotalCatalogCountAsync(CancellationToken ct)` para conocer el universo total a recorrer (~10.000 títulos).

### Componente 2: Servicio de Barrido Integral (`IBggMassIngestionService`)
- Incorporado `SweepCatalogQualityBatchAsync(int afterBggId, int batchSize, CancellationToken ct)` y su variante desatendida `RunScheduledSweepCatalogQualityBatchAsync`.
- Para cada juego del lote:
  1. **Estrategia Staging-First:** Si `BggCatalogStaging` ya contiene `<dna ` en `RawThingXml`, se enriquece en memoria a alta velocidad sin llamadas HTTP externas.
  2. **Fallback a BGG XMLAPI2:** Si no está en staging con ADN inferido, se consulta la API de BGG, actualizando estilo, confrontación, duración, escalabilidad, fundas y guardando el ADN en staging.
  3. **Escritura Idempotente:** Si el juego ya está 100% correcto (estilo coincide, duración coincide, etc.), se contabiliza como `Skipped` y no se ejecuta escritura redundante en base de datos.
- Retorna `BggQualitySweepBatchResultDto` con `EvaluatedCount`, `UpdatedCount`, `SkippedCount`, `FailedCount`, `LastBggIdProcessed` y `HasMore`.

### Componente 3: Interfaz Web y Cloud Run Jobs
- En `CatalogQueueAdmin.razor`:
  - Botón interactivo **«Barrido Total Catálogo (~10.000)»** con ejecución continua en segundo plano mediante `CancellationTokenSource`.
  - Indicador reactivo en vivo: `Evaluados X/Total (Y corregidos, Z ya correctos, W errores)`.
  - Botón accesible para detener el barrido en cualquier momento.
  - Botón para ejecución manual de un lote de 50 títulos.
- En `Ludeka.Jobs` (`BackfillQualityJobRunner`):
  - Actualizado para ejecutar el barrido por cursor secuencial de principio a fin hasta `!HasMore`.

---

## 3. Criterios de Aceptación y Verificación

1. **Recorrido del 100% del Catálogo:** El proceso visita secuencialmente los ~10.000 títulos promovidos desde `afterBggId = 0` hasta el último.
2. **Cero Bucles Infinitos:** Ningún título se reevalúa en la misma pasada gracias a la paginación por cursor.
3. **Corrección Integral:** Títulos como Dune, Nemesis o Codenames corrigen su estilo y tiempos independientemente de que ya tuvieran votos de escalabilidad.
4. **Idempotencia:** Los títulos ya correctos no generan escrituras en BD ni errores.
5. **Verificación Automatizada:** Suite de pruebas unitarias cubriendo cursor, servicio y runner con 2.037 pruebas pasando al 100% sin regresiones.
