# Propuesta: change-73-ingesta-enriquecimiento-catalogo (Incremento 73: Ampliación de Ingesta Masiva BGG >100 opiniones, Descarte de Duplicados y Enriquecimiento Integral de Metadatos)

## 1. Resumen Ejecutivo y Motivación

En Ludeka, el catálogo de juegos de mesa es el pilar central sobre el que se articulan la ludoteca personal, las estadísticas de jugador, los filtros de búsqueda, las fichas con ADN lúdico y los enlaces a tiendas afiliadas.

Tras la ingesta masiva inicial (~4.000 títulos mediante el umbral `usersrated >= 1000`) y las mejoras de experiencia de usuario del Incremento 72 (filtros multiselección, carrusel de tres fotos, retiro de textos en inglés), se evidencia una doble necesidad crítica:

1. **Ampliación del fondo de catálogo (Umbral >100 opiniones):**
   - El umbral previo de 1.000 votos dejaba fuera miles de títulos excelentes: juegos de editoriales españolas independientes, novedades de los últimos 2-3 años con gran valoración pero aún en crecimiento de votos, y juegos de nicho de enorme calidad lúdica.
   - Modificar el filtro del volcado masivo de BGG (`bg_ranks`) para admitir juegos con más de 100 opiniones (`minUsersRated = 100`) permite ampliar el catálogo a decenas de miles de juegos con tracción comunitaria contrastada.

2. **Blindaje anti-duplicados y actualización incremental:**
   - La ingesta masiva del nuevo lote debe convivir limpiamente con los ~4.000 títulos ya existentes en las bases de datos (SQLite y PostgreSQL).
   - Debe blindarse el flujo para que cualquier título existente sea detectado por su `BggId` único: no debe intentarse una inserción duplicada (evitando violaciones de unicidad en `Games.BggId`), sino una actualización aditiva de sus metadatos (incorporando campos que antes estaban vacíos).

3. **Enriquecimiento de escalabilidad por jugadores (Best / Recommended):**
   - En el catálogo previo, muchos títulos carecían de semáforo de escalabilidad («A cuántos jugadores funciona bien»), mostrando «Sin datos de escalabilidad» en la ficha.
   - Se debe parsear la encuesta comunitaria de BGG (`poll name="suggested_numplayers"`). Si la encuesta no está informada o tiene 0 votos, se debe complementar con una heurística determinista basada en el rango oficial de jugadores (y opcionalmente refinada por síntesis IA) para que ningún juego quede sin semáforo.

4. **Ingesta de tamaños de fundas (Sleeves):**
   - Extraer e informar las dimensiones de fundas cuando vengan reportadas en los enlaces `boardgamecardsleeve` de la API de BGG.
   - Almacenar las fundas en staging y trasladarlas a `Game.Sleeves` para que la guía de fundas y tiendas afiliadas (INC-26 / INC-66) disponga de datos reales.

5. **Corrección de tamaño en mesa (TableFootprint):**
   - Erradicar la asignación estática plana de `StandardTable`. Asignar el tamaño real (`SmallTable`, `StandardTable`, `TableMonster`) analizando categorías, mecánicas, componentes, duración y síntesis de IA.

6. **Consistencia de tiempos de juego y tiempos por jugador:**
   - Eliminar el multiplicador artificial `PlayingTimeMinutes * 1.5`. Parsear `MinPlayTimeMinutes` y `MaxPlayTimeMinutes` reales de BGG y computar un `EstimatedPerPlayerMinutes` realista.

7. **Proceso de enriquecimiento retroactivo (Backfill):**
   - Incorporar un servicio de backfill por lotes (`BackfillCatalogQualityBatchAsync`) para actualizar los títulos ya presentes en el catálogo sin necesidad de una reingesta destructiva.

---

## 2. Arquitectura y Alcance por Capas

### 2.1 Dominio (`Ludeka.Core`)
- **`BggCatalogStagingItem`**:
  - Incorporar campos de persistencia de calidad:
    - `MinPlayTimeMinutes` (int) y `MaxPlayTimeMinutes` (int).
    - `InferredFootprint` (`TableFootprint`).
    - `ScalabilityJson` (string?) y `SleevesJson` (string?).
  - Métodos utilitarios deserializadores: `GetScalability()` y `GetSleeves()`.
  - Sobrecarga ampliada en `MarkFetched(...)` para recibir estos metadatos.
  - Método `UpdateInferredFootprint(TableFootprint footprint)`.
- **`Game`**:
  - Métodos aditivos para mutación controlada en backfill y promoción incremental:
    - `UpdateScalability(IEnumerable<ScalabilityEntry> scalability)`
    - `UpdateDuration(GameDuration duration)`
    - `UpdateFootprint(TableFootprint footprint)`
    - `UpdateSleeves(IEnumerable<SleeveItem> sleeves)`

### 2.2 Aplicación (`Ludeka.Application`)
- **`BggMassIngestionOptions`**:
  - Modificar el valor por defecto: `MinUsersRated = 100` (anteriormente 1000).
- **`BggDumpParser`**:
  - Actualizar parámetro por defecto a `minUsersRated = 100`.
- **`IBggMassIngestionService`**:
  - Añadir contratos para el backfill retroactivo de calidad:
    - `Task<int> BackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default);`
    - `Task<int> RunScheduledBackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default);`
- **`BggMassIngestionService`**:
  - En `ProcessPendingDetailsBatchAsync`: capturar `fetchedGame.Scalability`, `fetchedGame.Sleeves`, `fetchedGame.Duration` y `fetchedGame.Footprint` y persistirlos en `BggCatalogStagingItem`.
  - En `PromoteReadyToCatalogBatchAsync`:
    - Al crear un `new Game(...)`, asignar la escalabilidad, fundas, huella inferida y duraciones reales.
    - Si el juego ya existe en `Games` (detección por `BggId`): actualizar aditivamente escalabilidad, fundas, duración y huella si no las tenía o si eran valores por defecto.
    - Manejo defensivo para garantizar cero errores por clave única duplicada.
  - Implementar `RunScheduledBackfillCatalogQualityBatchAsync`: localiza juegos candidatos y los enriquece desde staging (o consultando BGG Thing si no están en staging).
- **`IGameRepository`**:
  - Añadir contrato `Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default);`

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`BggXmlParser`**:
  - Parsear `MinPlayTimeMinutes`, `MaxPlayTimeMinutes` y derivar `EstimatedPerPlayerMinutes` balanceado.
  - Extraer `ParseScalability` con fallback determinista cuando la encuesta comunitaria no tiene votos (generando recomendaciones para el rango oficial de jugadores).
  - Inferir analíticamente `TableFootprint` combinando categorías de BGG (*Card Game*, *Microgame*, *Miniatures*, *Wargame*, *Big Box*, etc.) y duración máxima.
- **`LudekaDbContext`**:
  - Configurar las nuevas propiedades de `BggCatalogStagingItem` con valores por defecto.
  - Generar migración EF Core `AddStagingQualityFields` (compatible con SQLite y PostgreSQL).
- **`SqliteGameRepository`**:
  - Implementar `GetGamesPendingQualityBackfillAsync`: consulta juegos que carecen de escalabilidad, o cuya huella es `StandardTable` sin clasificar, o con duraciones estáticas `MinMinutes == MaxMinutes`.

### 2.4 Tareas y Procesos (`Ludeka.Jobs`)
- **`SeedStagingJobRunner`**:
  - Consume automáticamente `_options.Value.MinUsersRated` (100).
- Preparación para ejecución del enriquecimiento en cola nocturna / trabajos programados.

---

## 3. Criterios de Aceptación

1. **Umbral de Ingesta:** `BggMassIngestionOptions.MinUsersRated` se fija en 100 y `BggDumpParser` procesa juegos con `UsersRated >= 100`.
2. **Idempotencia y Cero Duplicados:** Al ingerir volcados o promover registros, los títulos preexistentes en `Games` se actualizan de forma aditiva por `BggId` sin generar excepciones de clave duplicada.
3. **Escalabilidad Comunitaria con Fallback:** Todo juego ingestado o actualizado dispone de registros en `Scalability`. Si BGG no incluye votos, se aplica el fallback determinista garantizando que la ficha muestre semáforo.
4. **Fundas de Cartas Persistidas:** Los datos de fundas extraídos de BGG se almacenan en staging y se persisten en `Game.Sleeves`.
5. **Huella en Mesa Diferenciada:** Juegos de cartas y microjuegos reciben `SmallTable`; wargames y miniaturas reciben `TableMonster`; juegos intermedios reciben `StandardTable`.
6. **Tiempos Reales:** `Duration` refleja los valores mínimos y máximos de BGG y un tiempo por jugador realista.
7. **Backfill Operativo:** `BackfillCatalogQualityBatchAsync` enriquece con éxito juegos existentes en catálogo.
8. **Pruebas Automatizadas:** 100% de la suite de pruebas en verde, con tests unitarios específicos para cada componente.
