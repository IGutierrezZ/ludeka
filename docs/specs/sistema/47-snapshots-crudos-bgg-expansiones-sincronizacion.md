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

### 4.2 Auto-vinculación en Catálogo y Descubrimiento con Filtrado Inteligente (INC-107)
El servicio `BggRawSnapshotSyncService` implementa:
1. **Auto-vinculación (`AutoLinkExistingExpansionsAsync`):** Recorre los snapshots crudos existentes. Si detecta una expansión que ya existe en el catálogo pero tiene `BaseGameId = null`, busca el juego base por su `BggId` y actualiza el juego asignándole `SetBaseGameId(baseGame.Id)`.
2. **Descubrimiento y Encolado con Priorización por BggRank de Juegos Base y Cribado Anti-Promos (`DiscoverAndEnqueueMissingExpansionsAsync` - INC-107 e INC-109):**
   - **Priorización Top BGG Rank (INC-109):** En lugar de iterar snapshots sin orden (que arrojaba títulos antiguos por ID asc de 1995-2001), consulta los juegos base de Ludeka por BggRank ascendente mediante `IGameRepository.GetTopRankedBaseGameBggIdsAsync(1500)`. Carga sus snapshots prioritarios con `IBggRawSnapshotRepository.GetSnapshotsByBggIdsAsync` para procesar primero las expansiones de los juegos más emblemáticos y populares del catálogo (*Terraforming Mars*, *Wingspan*, *Dune: Imperium*, *Catan*, *Ark Nova*, etc.), con complementación defensiva desde el resto de snapshots hasta completar el cupo.
   - **Ampliación de Cupo (INC-109):** Permite encolar interactivamente hasta 1.200 títulos (`maxToEnqueue = 1200`), frente al cupo anterior de 50.
   - **Pre-filtro Léxico:** Evalúa los títulos enlazados en snapshots locales mediante `BggRawSnapshotParser.IsProbablePromoOrAccessory`, descartando promos de eventos, paquetes de cartas o accesorios físicos (promo, onus, pack, miniature, playmat, dice, etc.).
   - **Umbral Comunitario y Comercial:** Consulta BGG en bloques de 20 IDs con `stats=1` y versiones. Solo se encolan expansiones con tracción real (`usersrated >= 30` o `owned >= 100`) o que cuenten con edición comercial registrada en español (título o editorial en español).
   - Añade los candidatos admitidos a `PendingBggImports` con origen `CatalogQueueOrigin.BggExpansionDiscovery` (5) para su importación controlada.

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

### 7.7 Ingesta 2x1 de Versiones BGG (versions=1), Títulos en Español y Códigos EAN-13 (INC-105)
Para corregir sistemáticamente juegos registrados con nombres en inglés (ej. *Power Grid* en vez de *Alta Tensión*) y proveer de códigos de barras comerciales estándar (EAN-13 / GTIN-13 / UPC-A) para la integración con afiliados:
1. **Soporte de Versiones en Cliente BGG (`&versions=1`):**
   - Sobrecarga de métodos en `IBggClient`, `BggXmlApiClient` y `SimulatedBggClient` con parámetro booleano `includeVersions = false` (por defecto compatible hacia atrás).
   - Consulta el endpoint `/xmlapi2/thing?id={csv}&stats=1&versions=1` de BGG en bloques de hasta 20 IDs.
   - Los nodos `<versions>` recibidos se persisten automáticamente dentro del payload JSON en `BggRawSnapshots`.
2. **Parser Analítico Puro (`BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson`):**
   - Extrae deterministamente el nodo de versión española (`<link type="language" value="Spanish" />`).
   - Resuelve el título comercial oficial en español (`SpanishTitle`), editorial (`SpanishPublisher`) y código de barras (`Ean`).
   - Normalización y validación estricta de códigos de barras comerciales mediante `BarcodeValidator.TryNormalizeEan13` (módulo 10 con pesos alternos 1 y 3, soporte UPC-A de 12 dígitos normalizado a GTIN-13 con 0 inicial).
3. **Orquestador de Sincronización y Barrido Local en `BggRawSnapshotSyncService`:**
   - `SyncVersionsBatchAsync`: Obtiene juegos sin nodo de versiones en snapshot, invoca a BGG con `versions=1`, guarda el snapshot y actualiza la entidad `Game` en catálogo.
   - `SweepCatalogFromVersionsAsync`: Proceso 100% offline que itera sobre snapshots locales con versiones mediante paginación ascendente por cursor, actualizando en memoria/CPU `SpanishTitle`, `SpanishPublisher`, `Ean` y `LocalizedTitles`.
4. **Runner CLI Autónomo (`BggVersionsSweepJobRunner`) y Consola Web:**
   - Runner `bgg-versions-sweep` registrado en `JobNames.cs` y expuesto en `Ludeka.Jobs`.
   - Métricas y acciones en `CatalogQueueAdmin.razor`: tarjeta KPI de «Versiones BGG (EAN / ES)» y botones interactivos «Sincronizar Versiones (2x1)» y «Barrer Títulos ES y EAN».
5. **Compatibilidad Dual SQLite y PostgreSQL (`jsonb`):**
   - En PostgreSQL (Supabase), la columna `RawJson` (`jsonb`) se consulta mediante la función nativa `jsonb_exists("RawJson", 'versions')`, previniendo errores de operador `!~~` (`NOT LIKE`) inexistente sobre tipos JSONB y excluyendo snapshots marcados con `notFound`. En SQLite se consulta con `.Contains("\"versions\"")` y filtro defensivo anti-`notFound`.

### 7.8 Corrección de Detección de Idioma (ID 2195 BGG), Filtrado de Descriptores de Edición y Saneamiento Automático (INC-111)
Para corregir la corrupción de títulos en juegos de catálogo que adoptaban nombres como «Korean edition» o «Angry Lion Korean edition» y blindar el extractor de versiones frente a falsos positivos:
1. **Causa Raíz y Eliminación de ID 2195 BGG:**
   - En la API de BGG, el identificador `2195` corresponde a la etiqueta *Korean* (coreano) y no a *Spanish*. Su inclusión en la comprobación rápida de INC-105 provocaba que versiones coreanas fuesen catalogadas como españolas.
   - Se eliminó la comprobación por ID numérico en `BggRawSnapshotParser.IsSpanishLanguageLink`, delegando la detección exclusivamente a la validación semántica del nombre del idioma (`Spanish`, `Español`, `Castellano` y variantes flexivas).
2. **Filtrado Estricto de Descriptores Genéricos de Edición:**
   - En BoardGameGeek, el campo `name` de `boardgameversion` habitualmente describe la edición física de la caja (ej. «Spanish edition», «Angry Lion Korean edition», «Edición en español») en lugar de un nombre propio de juego localizado.
   - Implementación de `BggRawSnapshotParser.IsGenericEditionTitle` y `CleanVersionTitle` con expresiones regulares especializadas que detectan y descartan descriptores de edición de caja, evitando que sobreescriban `SpanishTitle` salvo que constituyan un título comercial propio y localizado (ej. «Alta Tensión»).
   - `BggSpanishVersionInfoDto.Title` pasa a ser nullable (`string?`), permitiendo extraer la editorial y el EAN-13 de una edición física sin imponer un título de caja como nombre del juego.
3. **Fusión Inteligente de Versiones Candidatas:**
   - Cuando un juego dispone de múltiples entradas de versión en español (por ejemplo, una entrada con el título localizado «Alta Tensión» y otra con el código EAN oficial), el parser consolida armónicamente los datos para no perder ni el título localizado ni el código de barras comercial.
4. **Saneador Automático de Base de Datos (`CatalogDataSanitizer`):**
   - Servicio determinista en `Ludeka.Infrastructure.Seeding.CatalogDataSanitizer` que analiza la base de datos y repara anomalías:
     - Detecta títulos contaminados con patrones de edición («korean», «angry lion», descriptores de edición).
     - Restaura `SpanishTitle` con el título original (`OriginalTitle`) o con el título oficial en español obtenido del snapshot de BGG si está disponible.
     - Limpia editoriales coreanas y códigos de barras con prefijo GS1 de Corea del Sur (`880...`) erróneamente atribuidos.
   - Ejecutado proactivamente durante el arranque de la aplicación web (`Program.cs`) y al inicio de los barridos de catálogo (`BggRawSnapshotSyncService.SweepCatalogFromVersionsCoreAsync` y `ProcessBatchAsync`).

### 7.9 Saneamiento de Descriptores de Edición con Acrónimos y Reparación de Catálogo (INC-135)
Para erradicar casos donde las versiones multilingües de BGG asignan rótulos técnicos de producción (ej. `ENG/GER/FRE/SPA edition` en *Queen Alice*, BggId 456236) y asegurar que nunca degraden el título canónico en catálogo ni en búsquedas:
1. **Reconocimiento Exhaustivo de Acrónimos y Descriptores (`BggRawSnapshotParser`):**
   - `IsGenericEditionTitle`: Ampliado con reconocimiento de códigos ISO/BGG de 2 y 3 letras (`ENG`, `SPA`, `ESP`, `GER`, `FRE`, `FRA`, `ITA`, `POR`, `DUT`, `POL`, `CZE`, `RUS`, `KOR`, `JPN`, `CHI`, `EN`, `ES`, `FR`, `DE`, `IT`, `PT`, etc.) en combinaciones con barras, guiones o conectores (`ENG/GER/FRE/SPA edition`), y términos de tirada/formato (`Retail edition`, `Deluxe edition`, `Kickstarter edition`, `Multilingual edition`, `Combo Games edition`).
   - `CleanVersionTitle`: Retira sufijos multilingües compuestos (ej. `"Queen Alice - ENG/GER/FRE/SPA edition"` -> `"Queen Alice"`) y devuelve `null` si la cadena íntegra es un descriptor genérico.
   - `ExtractSpanishVersionInfoFromJson`: Fija `Title = null` cuando la versión carece de un título propio localizado, preservando intacto el nombre canónico del juego.
2. **Blindaje de Nombres Alternativos (`BggXmlParser`):**
   - `ExtractSpanishTitle` descarta nombres alternativos del nodo XML raíz clasificados como descriptores genéricos por `IsGenericEditionTitle`.
3. **Saneador Autónomo y Reparación Prioritaria (`CatalogDataSanitizer`):**
   - Ampliación del predicado SQL en EF Core para evaluar títulos con `"edition"`, `"edicion"`, `"edición"`, `"version"`, `"versión"` y barras (`/`).
   - Saneamiento prioritario O(1) en `EnsureKnownPriorityGamesRepairedAsync` para *Queen Alice* (456236) restaurándolo a `OriginalTitle` y asignando su editorial local.

### 7.10 Refresco de Versiones BGG de Novedades, Soporte Editorial Lúdilo y Saneamiento de Catálogo (INC-136)
Para resolver el desfase en juegos recientes donde las ediciones en español se registran en BGG con posterioridad a la indexación del título original (ej. *Got Five!*, BggId 453526, de Yoann Levet, publicado en España por Lúdilo como *Código 5*):
1. **Soporte Editorial Lúdilo (`RegionalPublisherMatcher`):**
   - Incorporación de `"Lúdilo"`, `"Ludilo"`, `"Lúdilo Games"` y `"Ludilo Games"` como sello editorial español oficial (`CountryCode = "ES"`, `OfficialName = "Lúdilo"`, `Slug = "ludilo"`), corrigiendo la atribución errónea a distribuidores genéricos.
2. **Detección de Snapshots Candidatos a Refresco (`IBggRawSnapshotRepository` / `SqliteBggRawSnapshotRepository`):**
   - Implementación de `GetBggIdsNeedingVersionRefreshAsync(minYear, limit)`: detecta títulos recientes (`YearPublished >= minYear`) cuyo título en español coincide con el original y cuyos snapshots aún no registran versiones en español, habilitando su re-sincronización periódica en segundo plano.
3. **Integración en el Sincronizador de Versiones (`BggRawSnapshotSyncService`):**
   - `SyncVersionsBatchCoreAsync` consulta automáticamente los candidatos a refresco de novedades cuando no existen snapshots pendientes sin el nodo `versions`.
4. **Saneamiento Prioritario en `CatalogDataSanitizer`:**
   - `EnsureKnownPriorityGamesRepairedAsync` asegura de forma determinista O(1) que el juego BggId 453526 (*Got Five!*) actualice su título a `"Código 5"` y su editorial a `"Lúdilo"`, persistiendo además su snapshot de versiones asociado.

### 7.11 Sincronización Forzada de BGG desde la Ficha Editorial (INC-137)
Para permitir que moderadores y miembros de la Mesa Fundadora puedan corregir discrepancias o enriquecer de forma instantánea cualquier juego desde su propia ficha pública/editorial (`/juegos/{slug}`):
1. **Contrato de Sincronización en `IGameEditorService`:**
   - Método `ForceSyncFromBggAsync(Guid gameId, CancellationToken ct = default)` que valida roles/permisos (`IsFoundingTeam` o `CanEditGames`), comprueba `BggId > 0`, consulta en tiempo real a BGG XMLAPI2 con `&versions=1`, guarda o actualiza el snapshot crudo en `IBggRawSnapshotRepository` y actualiza la entidad `Game`:
     - Título en español: solo si la versión española dispone de un título limpio no genérico (descartando "Spanish edition", etc.).
     - Editorial española: extraída de la versión española en BGG.
     - Código de barras EAN-13: validado con algoritmo de dígito de control módulo 10.
     - Imágenes: portada y miniatura actualizadas si proceden de la edición local o si la ficha carecía de ellas.
   - Registro de auditoría atómico en `GameEditLog` y `IAuditService`, e invalidación de caché L1 de catálogo (`CachedCatalogService`).
2. **Desacoplamiento Limpio en `IBggClient`:**
   - Incorporación de `FetchRawThingJsonAsync(int bggId, bool includeVersions = true, CancellationToken ct = default)` en `IBggClient`, implementado en `BggXmlApiClient` y `SimulatedBggClient` mediante `BggXmlToJsonConverter`, evitando dependencias de XML en la capa de aplicación.
3. **Controles de Usuario en Blazor Web App:**
   - Botón reactivo en el panel flotante `GameStaffToolsPanel.razor` («Sincronizar BGG (id)»).
   - Botón en la botonera editorial de staff en la pestaña de veredicto de `GameDetail.razor`.
   - Botón contextual «Sincronizar» en el pie del marco polaroid junto a «Ver en BGG ↗».
   - Estados de carga `IsSyncingBgg` con spinner e información reactiva de cambios en `_actionFeedbackMessage`.

### 7.12 Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos (INC-145)
Para erradicar discrepancias donde las ediciones físicas de BGG usan nombres de tirada (ej. `Z-Man Spanish edition` o `Iberian edition`) en vez del título comercial localizado oficial (ej. `Pandemic Legacy: Segunda temporada`), preservando las portadas y editoriales reales (ej. Devir):
1. **Priorización de `canonicalname` sobre `name` (`BggRawSnapshotParser`):**
   - En la estructura de BGG, cada elemento `boardgameversion` contiene invariablemente `canonicalname` con el título del juego traducido legítimo, mientras que `name` es únicamente una etiqueta de inventario o descriptor físico de tirada.
   - `ExtractVersionTitle` consulta en primer término `canonicalname`, extrayendo el título comercial limpio traducido en BGG y recurriendo a `name` como fallback seguro únicamente si `canonicalname` está ausente.
2. **Robustecimiento de Detección de Descriptores Genéricos (`IsGenericEditionTitle` / `CleanVersionTitle`):**
   - Incorporación a `IsGenericEditionTitle` de marcas y sellos editoriales no registrados previamente (`z-man`, `zman`, `lúdilo`, `ludilo`, `salt & pepper`, `tranjis`, `loki`, `playte`, etc.).
   - Reconocimiento de descriptores regionales (`iberian`, `chilean`, `colombian`), acrónimos multilingües (`cat`, `sp`, `ge`, `ja`, `ko`) y formatos de producción (`print & play`, `pnp`, `cube box`).
   - Retorno inmediato de `null` en `CleanVersionTitle` ante descriptores genéricos sin degradar títulos legítimos que contienen guiones o separadores.
3. **Saneamiento Determinista y Reparación Prioritaria (`CatalogDataSanitizer`):**
   - Reparación prioritaria O(1) en `EnsureKnownPriorityGamesRepairedAsync` para BggId 221107 fijando `SpanishTitle = "Pandemic Legacy: Segunda temporada"` y `SpanishPublisher = "Devir"`.
   - Ampliación del predicado SQL de EF Core en `SanitizeCorruptedSpanishTitlesAsync` para incluir patrones de `iberian` y `z-man`, garantizando la limpieza del catálogo en arranques y barridos.


