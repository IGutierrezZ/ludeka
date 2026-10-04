# 25. Motor de Afiliados, Atribución BGG, Comunidad y Modo Producción de APIs

> **Estado del Módulo:** ✅ Implementado y Verificado  
> **Incremento Asociado:** INC-37 (`apis-produccion-afiliados`), INC-56 (`comunidad-mecenazgo`) e INC-86 (`feeds-catalogo-afiliados-ean`)  
> **Pruebas Unitarias Asociadas:** `AffiliateUrlResolverTests.cs`, `CommunityAndSupportLinksContractTests.cs`, `GeminiGameSummaryServiceTests.cs`, `BggOptionsTests.cs`, `CommunityNotificationServiceTests.cs`, `GoogleShoppingFeedParserTests.cs`, `CatalogFeedSyncServiceTests.cs`, `CatalogFeedSyncJobRunnerTests.cs`, `AffiliatesAdminWebTests.cs` (2.390 pruebas unitarias + 10 de integración en verde en la suite global).

---

## 1. Propósito y Filosofía del Módulo

El objetivo primordial de este módulo es preparar a Ludeka para su salida a producción en entorno real (Docker sobre Google Cloud con base de datos en Supabase), garantizando:
1. **Veracidad y Cero Datos Inventados:** En modo producción (`Simulate = false`), los servicios externos (BoardGameGeek, Google Gemini y YouTube) nunca deben generar texto simulado ni datasets falsos si la API falla o la clave no está configurada; deben fallar de forma controlada y registrable en la cola de incidencias/moderación.
2. **Atribución Legal y Comunitaria:** Cumplimiento de los términos de servicio de BGG mediante el sello oficial *"Powered by BoardGameGeek"* en el pie de página global, junto con canales directos a las comunidades oficiales de Discord y Telegram, y vías de mecenazgo voluntario en Ko-fi.
3. **Monetización Ética y Privada de Afiliados:** Un motor centralizado que inyecta los tags de afiliación de tiendas colaboradoras y Amazon de forma totalmente transparente para el usuario pero desacoplada de la base de datos y de los formularios de edición.

---

## 2. Motor Centralizado de Afiliados (`IAffiliateUrlResolver`)

### 2.1 Principio de Privacidad y Desacople
Los enlaces de tiendas en Ludeka (tanto para compra de juegos como para fundas/sleeves) se almacenan o resuelven como URLs limpias del producto o dominio (ej. `https://zacatrus.es/juegos-de-mesa/catan.html`).
Los códigos de afiliado, identificadores de campaña o parámetros (`id_affiliate`, `ref`, `tag`, etc.) **nunca** se introducen manualmente en cada juego ni se exponen en formularios de administración. El backend inyecta los parámetros de afiliación de forma centralizada en el momento de renderizar o resolver el enlace.

### 2.2 Contrato e Implementación
- **Interfaz:** `Ludeka.Application.Contracts.IAffiliateUrlResolver`
  ```csharp
  public interface IAffiliateUrlResolver
  {
      string ResolveAffiliateUrl(string? rawUrl);
  }
  ```
- **Implementación:** `Ludeka.Application.Features.Affiliates.AffiliateUrlResolver`
  - Utiliza `IOptions<AffiliateOptions>`.
  - Normaliza la URL entrante, analiza el host (omitiendo prefijos como `www.`) y busca reglas coincidentes configuradas en `AffiliateOptions.Rules`.
  - Inyecta los parámetros query respetando cadenas de consulta existentes (`?` vs `&`), codificando adecuadamente las claves y valores vía `Uri.EscapeDataString`, y preservando fragmentos hash `#`.
  - Si una URL ya contiene el parámetro de afiliado configurado, reemplaza el valor con el tag oficial actualizado.
  - Si la URL no coincide con ningún socio afiliado registrado o es nula/vacía, se devuelve la URL original sin alteraciones.
- **Configuración (`AffiliateOptions`):**
  - Mapeo configurable en `appsettings.json` o variables de entorno:
    - **Zacatrus:** Host `zacatrus.es`, Parámetros: `id_affiliate`
    - **Mathom:** Host `mathom.es`, Parámetros: `ref`
    - **Dungeon Marvels:** Host `dungeonmarvels.com`, Parámetros: `affiliate`
    - **Cuarto de Juegos:** Host `cuartodejuegos.es`, Parámetros: `ref`
    - **Tablerum:** Host `tablerum.es`, Parámetros: `partner`
    - **Amazon:** Host `amazon.es`, Parámetro: `tag` (tag oficial `ludeka-21`, INC-56)
- **Etiquetado HTML Seguro:**
  - Los componentes visuales (`StoreOffersCard.razor`, `SleeveStoreUrlResolver`) añaden siempre el atributo obligatorio:
    `rel="noopener noreferrer sponsored"`

---

## 3. Desacople de Mocks y Modo Producción

### 3.1 BoardGameGeek XMLAPI2
- En `BggOptions`, la propiedad `ShouldSimulate` ahora depende exclusivamente de `SimulateApi` (no de si `BearerToken` está vacío, ya que el token BGG XMLAPI2 es opcional o suplementario).
- En caso de caída de la API de BGG o error HTTP 5xx/429 en producción, se informa al usuario mediante mensajes limpios de indisponibilidad temporal en lugar de inyectar juegos simulados inventados.

### 3.2 Google Gemini API
- En `GeminiGameSummaryService`, cuando `Simulate = false`:
  - Si no existe `ApiKey`, se lanza `InvalidOperationException` y no se genera texto simulado.
  - Si la API de Gemini responde con error de cuota o servicio no disponible, no se recurre a la heurística editorial; se lanza `InvalidOperationException` para que el procesamiento por lotes (`ProcessPendingSummariesBatchAsync`) incremente `FailedCount` y el juego permanezca en la lista de pendientes de síntesis.
  - Si `Simulate = true` (entorno local de desarrollo), sí se utiliza el generador heurístico editorial.

### 3.3 YouTube Data API
- En `YouTubeSearchService`, cuando `ShouldSimulate = false`:
  - Si la cuota de YouTube se agota o la API falla, el servicio registra la advertencia y devuelve una lista vacía `[]`, en lugar de poblar la ficha con vídeos de prueba simulados.

---

## 4. Footer Global y Comunidad

### 4.1 Atribución BGG
- En cumplimiento de las directrices de BGG, se incorpora una sección destacada en el pie de página de `MainLayout.razor`:
  - Insignia con icono Lucide `database` y texto:  
    *"Datos lúdicos y referencias cruzadas suministradas por BoardGameGeek bajo sus términos de uso de API."* con enlace canónico externo `rel="noopener noreferrer"`.

### 4.2 Enlaces Comunitarios y Vía de Mecenazgo (INC-56)
- Propiedades en `CommunityNotificationOptions`:
  - `DiscordInviteUrl`: Enlace de invitación al servidor oficial de Discord.
  - `TelegramChannelUrl`: Enlace al canal o grupo oficial de Telegram.
  - `KofiUrl`: Enlace directo de apoyo voluntario en Ko-fi (`https://ko-fi.com/ludeka`).
- Renderizados en el pie de página de `MainLayout.razor` con iconos Lucide (`coffee` en ámbar para Ko-fi, `message-circle` para Discord, `send` para Telegram), estilos accesibles con microinteracciones de marca, anillos de foco visibles (`focus-visible:ring-2`) y apertura en pestaña segura (`target="_blank" rel="noopener noreferrer"`).
- Consumo centralizado en [`Transparency.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Transparency.razor), eliminando enlaces estáticos hardcodeados en favor de las opciones tipadas del sistema.

### 4.3 Transparencia de Afiliados y Mecenazgo
- Microtexto informativo en el footer:
  *"Participamos en programas de afiliación de tiendas especializadas sin coste adicional para ti."*
- Página pública de manifiesto ético en `/transparencia`, detallando los 3 pilares de reinversión (infraestructura, copias de mesa real y sorteos) y ofreciendo botones accesibles de mecenazgo y canales comunitarios.

---

## 5. Corrección de Calendario en Notificaciones Comunitarias

- En `CommunityNotificationService.cs`, se corrigió el cálculo de `startOfWeek` para considerar el domingo (`DayOfWeek.Sunday = 0`) en el sistema europeo comenzando en lunes:
  ```csharp
  int diff = (7 + ((int)today.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
  var startOfWeek = today.AddDays(-diff);
  ```
  Esto garantiza que los envíos dominicales computen con exactitud el rango de la semana en curso sin arrojar discrepancias de fechas.

---

## 6. Ingesta de Feeds Comerciales, Mapeo EAN y Panel de Discrepancias (INC-86)

### 6.1 Identificadores Comerciales en la Entidad `Game`
- **Código de Barras Principal (`Ean`):** Normalizado a EAN-13 (con conversión de UPC-A de 12 dígitos anteponiendo `'0'`) y validado matemáticamente con cálculo del dígito de control módulo 10 (`BarcodeValidator.TryNormalizeEan13`).
- **Códigos de Barras Secundarios (`AdditionalBarcodes`):** Colección persistida como JSON de códigos EAN alternativos correspondientes a reimpresiones o ediciones internacionales. Permite cruzar unívocamente productos comerciales sin sobreescribir el EAN principal de referencia en castellano.

### 6.2 Entidades de Dominio y Persistencia Dual
- **`AffiliateFeedSource`:**
  - Modela los orígenes de datos de comercios: `StoreName`, `FeedUrl`, `Format` (`GoogleShoppingXml`, `GenericCsv`), `AffiliateTag`, `Country`, `IsEnabled`, `SyncIntervalHours`, `LastSyncUtc`, `LastSyncStatus`, `MatchedProductsCount`.
  - Mapeado en EF Core con tabla `affiliate_feed_sources` y persistencia dual en `SqliteAffiliateFeedSourceRepository` y PostgreSQL Supabase.
- **`AffiliateEanDiscrepancyLog`:**
  - Registra colisiones cuando un feed comercial trae un EAN para un juego que difiere del registrado en Ludeka: `GameId`, `GameTitle`, `GameSlug`, `CurrentEan`, `FeedEan`, `StoreName`, `DetectedAtUtc`, `IsResolved`, `ResolutionNote`.
  - Mapeado con tabla `affiliate_ean_discrepancy_logs` y repositorio `IAffiliateEanDiscrepancyRepository`.

### 6.3 Parsers de Feeds en Streaming (`GoogleShoppingFeedParser`)
- **Consumo de Memoria Constante ($O(1)$):** Procesa archivos XML pesados (Google Merchant / Shopping XML) mediante `XmlReader` con avance por subárboles `using var subtree = reader.ReadSubtree(); await XElement.LoadAsync(subtree, ...)` sin cargar el árbol completo en memoria.
- **Extracción Estructurada:** Mapea identificadores comerciales `<g:gtin>`, `<g:id>`, título `<title>`, enlace `<link>`, disponibilidad `<g:availability>` (normalizada a `InStock` / `OutOfStock`), y precio `<g:price>`.

### 6.4 Servicio de Sincronización y Cruce (`CatalogFeedSyncService`)
- **Cruce Determinista:** Compara el GTIN/EAN del feed contra el índice de catálogo (`Ean` y `AdditionalBarcodes`).
- **Auto-Asignación Segura:** Para juegos en catálogo que no poseen EAN asignado, si el título comercial coincide exactamente con `SpanishTitle` o `OriginalTitle`, auto-asigna el EAN tras validar su dígito de control.
- **Detección y Manejo de Discrepancias:** Cuando el título coincide pero el EAN del feed difiere del `Ean` actual de Ludeka (frecuentemente proveniente de BGG), almacena de inmediato el código nuevo en `AdditionalBarcodes` para que la oferta comercial no se pierda, y levanta un registro de discrepancia pendiente para moderación editorial.
- **Actualización Idempotente:** Sincroniza `Game.PurchaseLinks` actualizando precio, divisa y stock en tiempo real enriqueciendo el enlace con el tag de afiliación de la tienda.

### 6.5 Runner en `Ludeka.Jobs` (`CatalogFeedSyncJobRunner`)
- Runner de consola para Cloud Run Jobs registrado como `feed-sync` en `JobNames.All`.
- Ejecuta secuencialmente la sincronización de todas las fuentes de catálogo habilitadas, coordinado con `IJobExecutionCoordinator` para idempotencia y registro estructurado en logs.

### 6.6 Panel de Administración Web (`/admin/afiliados`)
- Vista interactiva en Blazor Web App protegida con la política `AuthorizationPolicies.PermisoGestionarTiendas` (`ModeratorPermission.CanManageStoreLinks`).
- **Pestaña 1 (Fuentes de Catálogo):** Listado de feeds, formulario de alta/edición, conmutador de estado (activar/pausar), métricas de última sincronización y botón para forzar sincronización manual individual o global.
- **Pestaña 2 (Discrepancias EAN):** Cola de discrepancias pendientes con comparativa visual del código actual vs código del comercio, y botón de acción atómica **"Promover a EAN principal"** (que promueve el código del comercio a principal y traslada el anterior a `AdditionalBarcodes`) o **"Descartar"**.
