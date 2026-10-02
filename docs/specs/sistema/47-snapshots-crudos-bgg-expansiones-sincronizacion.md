# 47. Snapshots Crudos BGG, Extracción de Expansiones y Sincronización Defensiva con Respeto de Límites

> **Estado:** Implementado y Verificado  
> **Fecha:** 2026-10-02  
> **Incremento Asociado:** INC-90 (`inc/bgg-raw-snapshots`), INC-91 (`inc/bgg-raw-runner`), INC-97 (`inc/bgg-batch-fetch`) e INC-98 (`inc/persistencia-automatica-snapshots`)  
> **Pruebas Verificadas:** 2.257 unitarias en verde  

---

## 1. Propósito y Contexto del Dominio

Históricamente, al importar o actualizar un juego desde la API XML2 de BoardGameGeek (`/xmlapi2/thing?id={bggId}`), el documento XML se parseaba para hidratar la entidad de dominio `Game` y el payload original se descartaba. Esta pérdida de información bruta impedía:
1. Recalcular deterministamente categorizaciones, mecánicas, duraciones o escalabilidad comunitaria ante futuras mejoras heurísticas sin tener que volver a consultar los servidores de BGG.
2. Descubrir y mapear expansiones vinculadas (`boardgameexpansion`) que BGG expone en los elementos `<link type="boardgameexpansion">` y `<link type="boardgameversion">`.
3. Contar con una pista de auditoría inmutable de los datos de origen de la API externa.

El incremento **INC-90** resuelve esta limitación introduciendo la tabla satélite `BggRawSnapshots`, su integración en el pipeline de importación, un servicio de sincronización defensiva por lotes con limitación de tasa (~1.200 ms) y telemetría en `/admin/cola-catalogacion`, la auto-vinculación y descubrimiento de expansiones, y un barrido de refinamientos editoriales y de catálogo.

---

## 2. Modelo de Datos y Persistencia

### 2.1 Entidad `BggRawSnapshot`
Ubicada en `Ludeka.Core.Entities.BggRawSnapshot`:
- `BggId` (`int`, PK): Identificador único en BoardGameGeek.
- `RawXml` (`string`): Documento XML íntegro devuelto por XMLAPI2.
- `JsonData` (`string`): Representación JSON equivalente generada deterministamente mediante `BggXmlToJsonConverter` para indexación y consultas en bases de datos relacionales (`jsonb` en PostgreSQL).
- `FetchedAt` (`DateTime` UTC): Marca temporal de obtención.
- `Version` (`int`): Contador secuencial de versión del snapshot.

### 2.2 Esquema Dual (PostgreSQL / SQLite)
- **PostgreSQL (`docs/database/supabase_schema.sql`):**
  ```sql
  CREATE TABLE IF NOT EXISTS public."BggRawSnapshots" (
      "BggId" integer NOT NULL,
      "RawXml" text NOT NULL,
      "JsonData" jsonb NOT NULL,
      "FetchedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
      "Version" integer NOT NULL DEFAULT 1,
      CONSTRAINT "PK_BggRawSnapshots" PRIMARY KEY ("BggId")
  );
  CREATE INDEX IF NOT EXISTS "IX_BggRawSnapshots_FetchedAt" ON public."BggRawSnapshots" ("FetchedAt");
  ```
- **SQLite (`LudekaDbContext`):** Mapeo de `JsonData` a columna de texto relacional compatible.

---

## 3. Conversión Fiel XML a JSON (`BggXmlToJsonConverter`)

Para dotar al sistema de máxima flexibilidad analítica sin perder fidelidad:
- Transforma nodos XML a estructuras JSON nativas (objetos, arreglos de elementos repetidos, preservación de atributos `@id`, `@type`, `@value`).
- Conserva descripciones multilínea, categorías, mecánicas, diseñadores, artistas y todos los enlaces relacionales (`boardgameexpansion`, `boardgameaccessory`, etc.).

---

## 4. Extracción y Vinculación de Expansiones

### 4.1 Detección de Tipo y Enlaces en `BggXmlParser`
- **Reconocimiento de Expansión:** Si el elemento `<item>` tiene `type="boardgameexpansion"`, el parser asigna `GameType.Expansion` en lugar de `GameType.BaseGame`.
- **Extracción de Juego Base (Inbound):** `ExtractInboundBaseGameBggId(xml)` extrae el `id` del juego base en `<link type="boardgameexpansion" id="..." inbound="true" />`.
- **Extracción de Expansiones (Outbound):** `ExtractOutboundExpansionBggIds(xml)` extrae la lista de identificadores BGG de expansiones hijas en `<link type="boardgameexpansion" id="..." />`.
- **Extracción de Metadatos de Enlace:** `ExtractExpansionLinks(xml)` devuelve objetos `BggExpansionLinkDto` con `BggId`, `Name` e `IsInbound`.

### 4.2 Auto-vinculación en Catálogo y Descubrimiento
El servicio `BggRawSnapshotSyncService` implementa:
1. **Auto-vinculación (`AutoLinkExistingExpansionsAsync`):** Recorre los snapshots crudos existentes. Si detecta una expansión que ya existe en el catálogo pero tiene `BaseGameId = null`, busca el juego base por su `BggId` y actualiza el juego asignándole `SetBaseGameId(baseGame.Id)`.
2. **Descubrimiento y Encolado (`DiscoverAndEnqueueMissingExpansionsAsync`):** Identifica expansiones referenciadas en los snapshots crudos que aún no existen en el catálogo de Ludeka ni en la cola pendiente, y las añade a `PendingBggImports` con origen `CatalogQueueOrigin.BggExpansionDiscovery` (5) para su importación controlada.

---

## 5. Sincronización Defensiva con Límite de Tasa (`BggRawSnapshotSyncService`)

Para respetar escrupulosamente los términos de servicio de BoardGameGeek y evitar respuestas `429 Too Many Requests`:
- **Limitación de Tasa:** Intervalo obligatorio de ~1.200 ms entre peticiones consecutivas a XMLAPI2 (`DelayBetweenRequestsMs = 1200`).
- **Sincronización por Lotes:** Método `SyncBatchAsync(batchSize: 20)` que prioriza juegos del catálogo que carecen de snapshot en `BggRawSnapshots`.
- **Telemetría e Interfaz Administrativa:** Nueva tarjeta de control en `/admin/cola-catalogacion` con:
  - Métrica de cobertura: Snapshots almacenados frente a juegos en catálogo con BGG ID.
  - Conteo de expansiones identificadas y vinculadas.
  - Acciones interactivas en un clic: Sincronizar Lote (20), Descubrir Expansiones Faltantes y Auto-Vincular Expansiones Existentes.

---

## 6. Refinamientos Editoriales y de Catálogo Integrados

Como parte del barrido de usabilidad del incremento INC-90:
1. **Ficha de Juego (`GameDetail.razor`):**
   - Unificación a un único enlace canónico a BoardGameGeek en la barra de acciones.
   - Restricción del botón «Cartel para Redes» exclusivamente a usuarios con permiso `CanEditGames`.
   - Reorganización de pestañas secundarias situando la pestaña Multimedia en primer término.
   - Disposición limpia del contenido con el semáforo y multimedia antes de la síntesis de IA.
2. **Editor Fotográfico Multirranura (`GameEditorModal.razor`):**
   - Incorporación de ranuras específicas e independientes para Carátula (`CoverImageUrl`), Trasera (`BackCoverImageUrl`) y Foto de Mesa (`TableImageUrl`), nutriendo el carrusel de tres fotos de la ficha.
3. **Preservación de Retorno en Autenticación Externa:**
   - Corrección en `/login/external` en `Program.cs` para respetar y sanitizar `returnUrl`, evitando que los inicios de sesión redirijan forzosamente a la portada.
4. **Catálogo Elevado y Filtros Avanzados (`Home.razor`):**
   - Retirada de la hilera redundante de filtros rápidos superiores y titulares estáticos para ganar altura útil en pantalla.
   - Selector configurable «Ordenar por» en el modal de filtros avanzados (`GameSortOrder`: Ranking, Mejor Valorados, Dureza, Duración, Año de Publicación).
   - Tarjetas `GameCard.razor` optimizadas: badge compacto de jugadores (`3-4J`), badges inferiores reducidos a solo iconos con tooltip y leyenda explicativa accesible.
   - Búsqueda SQL tolerante a mayúsculas y acentos (`ILike`).

---

## 7. Volcado Masivo Desatendido (`Ludeka.Jobs`) y Sincronización Continua Web (INC-91)

Para abordar el volcado de los ~13.853 títulos de catálogo respetando los ~1.200 ms por llamada sin depender de mantener abierto el navegador:

### 7.1 Métodos de Sistema sin Guarda de Sesión Interactiva
`Ludeka.Jobs` emplea `DenyAllSessionPermissionGuard`. Se incorporan en `IBggRawSnapshotSyncService` métodos desacoplados que ejecutan la lógica de negocio sin exigir sesión web de moderador:
- `RunScheduledSyncBatchAsync(batchSize, delayMs, ct)`.
- `RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue, ct)`.
- `RunScheduledAutoLinkExistingExpansionsAsync(ct)`.

### 7.2 Runner de Consola `bgg-raw-backfill` (`BggRawBackfillJobRunner`)
- Registrado en `JobNames.BggRawBackfill` (`bgg-raw-backfill`) y cableado en `Ludeka.Jobs`.
- Ejecutable mediante:
  ```bash
  dotnet run --project src/Ludeka.Jobs -- bgg-raw-backfill
  ```
- O como Cloud Run Job en infraestructura gestionada.
- Arrendamiento seguro vía `IJobExecutionCoordinator.ExecuteWithWindowLeaseAsync`.
- Itera en lotes de 50 títulos hasta que no queden pendientes (`ProcessedCount == 0`).
- Al finalizar el volcado, ejecuta automáticamente `RunScheduledAutoLinkExistingExpansionsAsync` y `RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue: 200)`.

### 7.3 Sincronización Continua en `CatalogQueueAdmin.razor`
- Botón interactivo de inicio y parada («Sincronización Total en Segundo Plano» / «Pausar Sincronización Continua»).
- Control reactivo con `CancellationTokenSource`, ejecución encadenada en bucle con telemetría en tiempo real (lotes procesados, éxitos, fallos y títulos restantes).
- Liberación garantizada de recursos con `IDisposable`.

### 7.4 Peticiones en Bloque Multi-ID y Snapshot de Control Defensivo (INC-97)
Para optimizar el rendimiento del backfill masivo y reducir el tiempo de volcado de 7 horas a ~18 minutos:
1. **Consultas Agrupadas a BGG XMLAPI2 (`/xmlapi2/thing?id={csv}&stats=1`):**
   - La interfaz `IBggClient` incorpora `FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct)`.
   - `BggXmlApiClient` empaqueta hasta 20 identificadores en una sola llamada HTTP con rate limiting (1.200 ms entre llamadas) y reintentos exponenciales con jitter.
   - `BggRawSnapshotSyncService` procesa los identificadores faltantes en fragmentos (`.Chunk(20)`), reduciendo el número de llamadas a BGG en un 95%.
2. **Snapshot de Control para IDs Inexistentes (`{"notFound":true}`):**
   - Si un identificador en catálogo ha sido retirado o marcado como privado en BGG (por lo que la API no devuelve elemento `<item>` para él), se persiste un snapshot de control con `{"notFound":true}` para evitar que entre en un bucle infinito de reintentos en sucesivos lotes.
3. **Ampliación de Timeout de Tarea en `Ludeka.Jobs`:**
   - La configuración por defecto `Workers:JobTimeoutMinutes` se eleva de 30 a 120 minutos en `src/Ludeka.Jobs/Program.cs`, evitando cancelaciones prematuras en ejecuciones masivas en Cloud Run.

### 7.5 Persistencia Automática a Nivel de Cliente y Telemetría de Staging (INC-98)
Para garantizar que **cualquier** consulta externa a BoardGameGeek nutra de forma autónoma la tabla satélite `BggRawSnapshots` sin depender de llamadas explícitas desde servicios superiores:
1. **Auto-Persistencia en Clientes BGG (`BggXmlApiClient` y `SimulatedBggClient`):**
   - Inyección opcional y desacoplada de `IBggRawSnapshotRepository` en `BggXmlApiClient` y `IServiceScopeFactory` en `SimulatedBggClient`.
   - En cada ejecución de `FetchRawThingsXmlAsync` y `FetchGameByBggIdAsync`, el cliente parsea los elementos `<item>`, los convierte a JSON canónico mediante `BggXmlToJsonConverter` y los upserta de inmediato en `BggRawSnapshots`.
   - Si se solicitaron identificadores que BGG no devolvió en el XML de respuesta, se registra automáticamente el placeholder defensivo `{"notFound":true}`.
   - Resiliencia no bloqueante: cualquier excepción en la base de datos se captura y registra en los logs de advertencia sin interrumpir el flujo principal de obtención de datos del juego.
2. **Telemetría y Cuadrícula de Métricas de Staging (`CatalogQueueAdmin.razor`):**
   - Ampliación de la cuadrícula de estado de staging a 7 columnas (`lg:grid-cols-7`).
   - Nueva tarjeta destacada de «Fallidos» (`_stagingMetrics.FailedCount`), que aclara la discrepancia entre el total de elementos en staging y los promovidos exitosamente al catálogo.

### 7.6 Reconstitución Determinista JSON a XML y Barrido Snapshot-First (INC-103)
Para aprovechar el 100% de cobertura de snapshots locales (~17.505 registros) y auditar o enriquecer el catálogo a velocidad de memoria y CPU sin realizar llamadas HTTP a la API de BGG:
1. **Conversión Determinista JSON a XML (`BggJsonToXmlConverter`):**
   - Implementado en `Ludeka.Infrastructure.Bgg.BggJsonToXmlConverter` como la inversa exacta de `BggXmlToJsonConverter`.
   - Reconstituye árboles `XElement` fieles desde el `RawJson` del snapshot satélite respetando atributos (`@id`, `@value`), texto (`#text`) y colecciones repetidas (`<link>`, `<name>`, `<poll>`).
2. **Método de Interfaz en `IBggClient`:**
   - Incorpora `Game? ParseGameFromRawJson(string rawJson)` implementado en `BggXmlApiClient` y `SimulatedBggClient`.
   - Reconstituye el `Game` mediante `BggJsonToXmlConverter` y `BggXmlParser.ParseGameElement`.
3. **Estrategia Snapshot-First en Enriquecimiento de Calidad:**
   - En `BggMassIngestionService.EnrichSingleGameQualityAsync`, se consulta `_snapshotRepo.GetByBggIdAsync(game.BggId, ct)` antes de cualquier llamada a red.
   - Si el snapshot local está presente, se reconstruye el `Game` y se actualizan escalabilidad, fundas, duraciones, huella y editoriales sin realizar tráfico de red externo.
