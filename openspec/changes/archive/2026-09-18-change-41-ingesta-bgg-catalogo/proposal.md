# Propuesta: change-41-ingesta-bgg-catalogo (Incremento 41: Ingesta Masiva de Catálogo BGG, Fotos GeekDo y Síntesis IA en Lotes)

## 1. Resumen Ejecutivo y Motivación

Actualmente, Ludeka cuenta con un catálogo inicial y un mecanismo de ingesta bajo demanda o nocturno limitado a 20-50 títulos desde la lista de tendencias ("Hot") de BGG y las peticiones de usuarios. Sin embargo, para convertirse en "El Letterboxd de los juegos de mesa en español", Ludeka requiere un catálogo base exhaustivo con los títulos de mayor relevancia histórica y tracción comunitaria real, enriquecidos con imágenes de calidad y síntesis editorial en español.

El **Incremento 41** implementa una arquitectura integral de ingesta masiva y enriquecimiento automatizado:
1. **Filtro de Catálogo BGG Relevante (~8.000 títulos):** Descarga y procesamiento del volcado de ranks de BGG filtrando por `usersrated >= 30` (configurable), descartando entradas residuales sin tracción comunitaria.
2. **Tabla Intermedia de Aislamiento y Staging (`BggCatalogStaging` / `bgg_staging`):** Mecanismo de persistencia intermedia que aísla la carga bruta, desacoplando los estados de descarga de metadatos (Thing XML), obtención de imágenes y enriquecimiento con IA, garantizando idempotencia, reanudabilidad y protección de la tabla principal `Games`.
3. **Galería Comunitaria de GeekDo Images & R2:** Integración con la API interna de imágenes de GeekDo para seleccionar las 3 fotos comunitarias más votadas (`boxartfront` / portada, `boxartback` / contraportada y `gameplay`/`creative` / componentes en mesa), optimizándolas y almacenándolas en WebP determinista en Cloudflare R2 vía `IImageStorageService` (INC-40).
4. **Síntesis IA por Lotes con Gemini Flash (Batching de 5 a 10 juegos):** Envío agrupado de 5 a 10 juegos por prompt estructurado JSON para optimizar radicalmente el cupo gratuito diario de 1.500 llamadas de Google Gemini Flash, junto con detección de 429/Resource Exhausted para pausar limpiamente y reanudar al día siguiente sin pérdida de datos.
5. **Reingeniería del Servicio Nocturno (`NightlyCatalogingHostedService`):** Orquestación completa que equilibra la detección de novedades editoriales, las solicitudes prioritarias de usuarios y el drenaje progresivo del staging masivo.

---

## 2. Arquitectura y Alcance por Capas

### 2.1 Dominio (`Ludeka.Core`)
- **`BggCatalogStagingItem`:** Entidad de staging con:
  - `BggId` (identificador BGG único).
  - `OriginalTitle`, `SpanishTitle`, `YearPublished`, `BggRank`, `UsersRated`.
  - `ThingXml` (caché de datos en bruto).
  - Estados: `FetchStatus`, `ImagesStatus`, `AiStatus`, `PromotionStatus`.
  - URLs de imágenes: `CoverImageUrl`, `ThumbnailUrl`, `BackCoverImageUrl`, `TableImageUrl`.
  - `AiSummaryJson` (resumen estructurado de IA).
  - Contadores y auditoría: `RetryCount`, `ErrorMessage`, `LastAttemptAt`, `CreatedAt`, `ProcessedAt`.
- **Extensiones en `Game`:**
  - Soporte para `BackCoverImageUrl` y `TableImageUrl` además de las portadas existentes.
  - Método `UpdateMediaUrls(coverUrl, thumbUrl, backUrl, tableUrl)`.

### 2.2 Aplicación (`Ludeka.Application`)
- **`IBggCatalogStagingRepository`:** Contrato para persistencia de staging (lotes, consultas por estado, contadores de progreso, marcado de cuota).
- **`IBggMassIngestionService`:** Orquestador de la ingesta masiva:
  - `IngestDumpAsync(Stream csvOrGzStream, int minUsersRated = 30, CancellationToken ct = default);`
  - `ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default);`
  - `ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default);`
  - `ProcessPendingAiBatchAsync(int gamesPerBatch = 5, int maxBatches = 10, CancellationToken ct = default);`
  - `PromoteCompletedStagingBatchAsync(int batchSize = 50, CancellationToken ct = default);`
  - `GetIngestionProgressAsync(CancellationToken ct = default);`
- **`IGeekDoImagesClient`:** Cliente HTTP para la API de imágenes de GeekDo:
  - `GetTopVotedImagesAsync(int bggId, CancellationToken ct = default);`
- **Ampliación de `IAiGameSummaryService`:**
  - `GenerateBatchSummariesAsync(IReadOnlyList<GameBatchInputDto> games, CancellationToken ct = default);`
  - Soporte de retorno con reporte de cuota agotada (`AiBatchGenerationResultDto`).

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`GeekDoImagesClient`:** Implementación HTTP que consulta `https://api.geekdo.com/api/images?ajax=1&gallery=all&objectid={bggId}&objecttype=thing`, parsea el JSON, selecciona las mejores fotos por `numpositive` y tipo, y devuelve URLs candidatas.
- **Pipeline de Imágenes con `IImageStorageService`:** Descarga los streams de las 3 imágenes más votadas y las procesa con `UploadGameImageVariantsAsync` generando:
  - `games/{bggId}/cover.webp` (+ `cover_thumb.webp`)
  - `games/{bggId}/back.webp`
  - `games/{bggId}/table.webp`
- **`GeminiGameSummaryService` (Batching):** Prompt optimizado para procesar entre 5 y 10 juegos en una única llamada JSON. Manejo de excepciones HTTP 429 y código de cuota excedida para pausar sin abortar con fallo fatal.
- **`SqliteBggCatalogStagingRepository` y mapeo EF Core (`LudekaDbContext`):** Soporte dual completo para SQLite y PostgreSQL (Npgsql) en `LudekaDbContext`.
- **`NightlyCatalogingHostedService` / `NightlyCatalogingService`:** Integración del drenaje de staging en el ciclo diario nocturno.

### 2.4 Interfaz de Usuario y Administración (`Ludeka.Web`)
- Panel de métricas y monitorización de ingesta en la vista de administración (`/admin/catalog-queue` o sección dedicada de ingesta) con contadores de:
  - Total títulos en staging.
  - Pendientes de detalle BGG XML.
  - Pendientes de imágenes GeekDo/R2.
  - Pendientes de síntesis IA.
  - Juegos promovidos al catálogo principal.
  - Botón de reanudación manual o disparo de lote.

### 2.5 Pruebas Automatizadas (`Ludeka.UnitTests`)
- Pruebas unitarias para el parser de dump de BGG y filtro `usersrated >= 30`.
- Pruebas unitarias para `GeekDoImagesClient` con respuestas JSON simuladas.
- Pruebas para `GeminiGameSummaryService` en modo batching (5-10 juegos) y manejo de cuota (HTTP 429).
- Pruebas de integración para el pipeline de staging completo (ingesta -> detalles -> fotos -> IA -> promoción a `Game`).

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Ingesta del volcado BGG con filtro de relevancia
  Dado un archivo de volcado de BGG con títulos que tienen entre 0 y 50.000 valoraciones
  Cuando se ejecuta la ingesta con filtro usersrated >= 30
  Entonces sólo los juegos con 30 o más valoraciones se insertan en BggCatalogStaging
  Y su estado inicial queda en FetchStatus = Pending

Escenario: Extracción de 3 fotos comunitarias de GeekDo
  Dado un juego en staging con BggId válido
  Cuando se consultan las imágenes comunitarias en GeekDo
  Entonces se seleccionan las imágenes con mayor número de votos positivos para portada, contraportada y mesa
  Y se procesan a WebP en Cloudflare R2 bajo las rutas deterministas games/{bggId}/cover.webp, back.webp y table.webp

Escenario: Síntesis con IA en lotes de 5 a 10 juegos
  Dados 8 juegos en staging pendientes de síntesis de IA
  Cuando se procesa el lote con Gemini Flash
  Entonces se realiza una única llamada a la API de Gemini enviando los 8 juegos
  Y se reciben y asignan las 4 propiedades editoriales para cada uno de los 8 juegos

Escenario: Agotamiento de cuota diaria de Gemini Flash (HTTP 429)
  Dado un lote de juegos enviado a Gemini Flash cuando la cuota diaria gratuita se ha agotado (HTTP 429)
  Cuando el servicio detecta el código 429
  Entonces los juegos permanecen en estado Pending para el siguiente ciclo
  Y el servicio no revienta la ejecución nocturna sino que registra una pausa limpia por cuota
```
