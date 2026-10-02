# Propuesta: INC-102 — Barrido y Auditoría Integral de Calidad de Catálogo desde Snapshots Locales de BGG y Saneamiento Anti-Bucle en Pendientes Sin Votos

## 1. Contexto y Problema Detectado

Durante la operación del panel administrativo `/admin/cola-catalogacion` en producción, se han identificado dos problemas críticos que bloquean la calidad y el rendimiento del catálogo:

1. **Bucle Infinito en «Pendientes Sin Votos» (`GetGamesPendingQualityBackfillAsync`):**
   - El panel muestra ~421 títulos pendientes de enriquecimiento (juegos cuya escalabilidad carece de votos comunitarios). La gran mayoría son títulos nicho o antiguos que en la propia BGG no tienen votos de la comunidad en la encuesta de número de jugadores.
   - Al pulsar «Pendientes Sin Votos», el enriquecedor aplica un *fallback* sintético (1J–4J recomendado), donde `BestVotes` y `RecommendedVotes` son 0.
   - La consulta `GetGamesPendingQualityBackfillAsync` busca títulos con `Scalability.Count == 0 || Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)` mediante `Take(50)` sin cursor.
   - Como esos títulos siguen teniendo 0 votos tras el *fallback*, la siguiente iteración del bucle vuelve a seleccionar exactamente los mismos 50 juegos.
   - Adicionalmente, `EnrichSingleGameQualityAsync` comprueba `game.Scalability.All(s => s.BestVotes == 0)`, por lo que considera siempre modificado el juego (`enriched = true`), ejecutando escrituras innecesarias en base de datos y manteniendo un bucle infinito que sube indefinidamente el contador de evaluaciones (ej. 1.200 evaluados / 1.128 actualizados sobre los mismos 50 juegos) sin avanzar.

2. **Desconexión entre el Barrido de Calidad y los 17.505 Snapshots Locales:**
   - La base de datos dispone de 17.505 registros íntegros en la tabla satélite `BggRawSnapshots` (100% de cobertura del catálogo).
   - Sin embargo, `EnrichSingleGameQualityAsync` en `BggMassIngestionService` solo consulta la tabla `BggCatalogStagingItem` y, si no encuentra la marca `<dna `, recurre a llamadas HTTP individuales por internet a la API de BGG (`_bggClient.FetchGameByBggIdAsync`) con una pausa de 1,5 segundos entre llamada y llamada.
   - Esto hace que auditar el catálogo completo requiera más de 7 horas de peticiones HTTP redundantes, con riesgo de bloqueo por rate limiting en BGG y saturación de recursos en Cloud Run, ignorando que los datos completos ya residen en la base de datos local.

## 2. Objetivos del Incremento

1. **Saneamiento Anti-Bucle en Pendientes de Calidad:**
   - Modificar `GetGamesPendingQualityBackfillAsync` para aceptar cursor `afterBggId`, garantizando progreso monotónico $O(1)$ sin posibilidad de ciclo infinito.
   - Refinar la condición de modificación en `EnrichSingleGameQualityAsync`: no marcar como modificado ni reescribir si la escalabilidad ya tiene el mismo número de entradas y ambas carecen de votos comunitarios.
   - Considerar pendientes de enriquecimiento prioritario únicamente aquellos juegos que carecen totalmente de escalabilidad (`Scalability.Count == 0`) o con cursor acotado.

2. **Estrategia Snapshot-First en Enriquecimiento de Calidad:**
   - Conectar `_snapshotRepo` (`IBggRawSnapshotRepository`) en `BggMassIngestionService.EnrichSingleGameQualityAsync`.
   - Incorporar un conversor bidireccional determinista `BggJsonToXmlConverter` (o extensión en `BggRawSnapshotParser`) para reconstituir el elemento XML `<item>` a partir del `RawJson` del snapshot satélite.
   - Reutilizar los parsers de calidad y ADN lúdico de `BggXmlParser` (`InferGameDna`, `ParseQualityMetadata`) directamente sobre el snapshot en memoria.
   - Preservar la llamada HTTP externa a BGG como último recurso únicamente para títulos que no dispongan de snapshot satélite local.

3. **Auditoría Masiva Ultrarrápida y CLI:**
   - Permitir auditar y corregir el 100% del catálogo (~17.505 juegos) en pocos minutos y con 0 llamadas a la red, tanto desde el botón web «Barrido Total Catálogo» como desde el runner desatendido `BackfillQualityJobRunner` (`dotnet run --project src/Ludeka.Jobs -- backfill-quality`).
   - Exponer telemetría clara en el panel web y en consola de los campos específicos corregidos (ADN lúdico, duraciones, escalabilidad, fundas, huella).
