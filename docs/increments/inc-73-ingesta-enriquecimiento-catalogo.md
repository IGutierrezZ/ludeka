# INC-73: Ampliación de Ingesta Masiva BGG (>100 opiniones), Descarte de Duplicados y Enriquecimiento Integral de Metadatos

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-27 · **Fecha de Cierre:** Pendiente  
> **Rama de Trabajo:** `inc/ingesta-enriquecimiento-catalogo`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-enriquecimiento-catalogo`  
> **Pruebas Automatizadas:** 1.945 pruebas unitarias en verde al inicio (línea base de INC-72)  
> **Dependencias:** INC-41/INC-53 (Staging y Ranks Dump BGG), INC-26/INC-66 (Fundas de Cartas), INC-59/INC-72 (Filtros, Dureza y Huella en Mesa)  
> **Especificación Viva:** `docs/specs/sistema/01-catalogo-juegos.md` y `docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`  

---

## 1. Contexto y Objetivos del Incremento

Tras la consolidación del catálogo inicial (~4.000 títulos más destacados con umbral >1.000 opiniones) y la mejora de los filtros y fichas de juego en INC-72, se requiere una segunda fase masiva orientada tanto a la expansión del fondo editorial como a la calidad profunda de los metadatos de mesa:

1. **Ampliación del umbral de ingesta BGG (>100 opiniones):**
   - El umbral previo (`minUsersRated = 1000`) dejaba fuera grandes joyas lúdicas de nicho, juegos recientes con alta valoración y producciones nacionales relevantes.
   - Se ajusta el umbral por defecto a 100 opiniones (`minUsersRated = 100`), permitiendo absorber decenas de miles de títulos preservando un estándar mínimo de tracción y fiabilidad comunitaria.

2. **Blindaje anti-duplicados y actualización incremental:**
   - La ingesta masiva del nuevo lote debe convivir limpiamente con los ~4.000 títulos ya existentes en las bases de datos (SQLite en desarrollo, PostgreSQL en producción).
   - El pipeline debe comprobar la existencia previa por `BggId` (tanto en staging como en la tabla principal `Games`):
     - Si el título es nuevo: se inserta y promueve con normalidad.
     - Si ya existe: se descarta de la inserción para no provocar errores de clave única o se actualiza de forma incremental enriqueciendo campos previamente vacíos (escalabilidad, fundas, huella, tiempos).

3. **Enriquecimiento de escalabilidad comunitaria (Best / Recommended):**
   - Eliminar de raíz las fichas con «Sin datos de escalabilidad».
   - Parsear y mapear la encuesta comunitaria de BGG (`poll name="suggested_numplayers"`).
   - Si la encuesta carece de votos o es insuficiente, aplicar un fallback determinista y heurístico (apoyado en síntesis IA o en los rangos de jugadores del diseño original) asignando recomendaciones coherentes (`Recommended` para el rango base, `Best` según recuento óptimo).

4. **Ingesta y normalización de fundas (Sleeves):**
   - Capturar las especificaciones de fundas de cartas devueltas por los enlaces `boardgamecardsleeve` de la API/scraping de BGG.
   - Persistir las dimensiones (ancho, alto, cantidad) en el registro de juego para alimentar la guía de fundas y enlaces de compra contextuales de INC-66 sin dejar la sección vacía.

5. **Corrección de tamaño en mesa (TableFootprint):**
   - Subsanar la asignación por defecto uniforme (`StandardTable`) en el catálogo actual.
   - Determinar analíticamente la huella real (`SmallTable`, `StandardTable`, `TableMonster`) combinando categorías BGG (ej. *Card Game*, *Wargame*, *Miniatures*, *Economic*), peso del juego y duración, complementado con síntesis IA.

6. **Consistencia de tiempos de juego y por jugador:**
   - Corregir el cálculo distorsionado de `Duration`: extraer `MinPlayTimeMinutes`, `MaxPlayTimeMinutes` reales de BGG y calcular de forma lógica `EstimatedPerPlayerMinutes` en función de la media de jugadores y escala temporal.
   - Proceso aplicable tanto a las nuevas promociones como a los títulos ya existentes vía backfill.

---

## 2. Requerimientos Técnicos Detallados

### 2.1. Configuración y Streaming de Volcado (BggDumpParser & BggMassIngestionService)
- Actualizar `BggMassIngestionOptions.MinUsersRated` a 100 (configurable vía `appsettings.json`).
- Asegurar que `BggDumpParser.ParseRanksDumpAsync` filtre eficientemente con `minUsersRated >= 100`.
- En `IngestRanksDumpAsync`, comprobar `seenBggIds` en memoria y hacer `UpsertBatchAsync` idempotente en `IBggCatalogStagingRepository`.

### 2.2. Esquema de Staging y Persistencia Dual
- Garantizar que `BggCatalogStagingItem` almacene `ScalabilityJson`, `SleevesJson`, `MinPlayTimeMinutes`, `MaxPlayTimeMinutes` y `InferredFootprint`.
- En `ProcessPendingDetailsBatchAsync`: invocar `FetchGameByBggIdAsync` y volcar de forma fidedigna escalabilidad, fundas y tiempos calculados al staging item.

### 2.3. Promoción y Deduplicación en Catálogo
- En `PromoteReadyToCatalogBatchAsync`:
  - Si `_gameRepo.GetByBggIdAsync(item.BggId)` es nulo: instanciar `new Game(...)` asignando escalabilidad real (o fallback), fundas, huella inferida y duraciones precisas.
  - Si el juego ya existe: ejecutar actualización aditiva (`existing.UpdateQualityMetadata(...)` o actualización de medios, escalabilidad si estaba vacía, fundas y huella si era `StandardTable` por defecto).
- En ningún caso debe producirse una excepción de clave duplicada (`UNIQUE constraint failed: Games.BggId`).

### 2.4. Servicio de Backfill Retroactivo para el Catálogo Existente
- Proporcionar método `BackfillCatalogQualityBatchAsync(int batchSize)` en `IBggMassIngestionService`.
- Consultar juegos en catálogo que carezcan de escalabilidad, fundas o tengan tiempos/huella por defecto.
- Enriquecerlos prioritariamente desde staging si ya se dispone de los datos o consultando BGG Thing si se requiere.

---

## 3. Criterios de Aceptación

1. **Umbral >100 opiniones:** El volcado de clasificación y las opciones de ingesta admiten juegos con `UsersRated >= 100`.
2. **Cero colisiones o duplicados:** La ingestión y promoción maneja registros existentes de forma idempotente sin duplicar filas en `Games` ni lanzar violaciones de unicidad.
3. **Escalabilidad siempre presente:** Todo juego importado o enriquecido dispone de al menos una entrada de escalabilidad con estado semafórico (`MustPlay`, `Recommended`, `NotRecommended`).
4. **Fundas importadas:** Cuando BGG informa fundas, estas se persisten en `Game.Sleeves` con dimensiones válidas y cantidad.
5. **Huella en mesa diferenciada:** Juegos compactos (ej. cartas) se catalogan como `SmallTable`; juegos densos/miniaturas como `TableMonster`; juegos de tablero estándar como `StandardTable`.
6. **Tiempos coherentes:** `MinMinutes <= MaxMinutes` y `EstimatedPerPlayerMinutes` derivado de forma proporcional y realista.
7. **Suite de pruebas:** 100% de pruebas unitarias en verde, incorporando tests de regresión y cobertura para cada nuevo escenario.
