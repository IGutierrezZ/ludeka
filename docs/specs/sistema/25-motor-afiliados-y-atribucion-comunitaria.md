# 25. Motor de Afiliados, Atribución BGG, Comunidad y Modo Producción de APIs

> **Estado del Módulo:** ✅ Implementado y Verificado  
> **Incremento Asociado:** INC-37 (`apis-produccion-afiliados`)  
> **Pruebas Unitarias Asociadas:** `AffiliateUrlResolverTests.cs`, `GeminiGameSummaryServiceTests.cs`, `BggOptionsTests.cs`, `CommunityNotificationServiceTests.cs` (866 pruebas en verde en la suite global).

---

## 1. Propósito y Filosofía del Módulo

El objetivo primordial de este módulo es preparar a Ludeka para su salida a producción en entorno real (Docker sobre Google Cloud con base de datos en Supabase), garantizando:
1. **Veracidad y Cero Datos Inventados:** En modo producción (`Simulate = false`), los servicios externos (BoardGameGeek, Google Gemini y YouTube) nunca deben generar texto simulado ni datasets falsos si la API falla o la clave no está configurada; deben fallar de forma controlada y registrable en la cola de incidencias/moderación.
2. **Atribución Legal y Comunitaria:** Cumplimiento de los términos de servicio de BGG mediante el sello oficial *"Powered by BoardGameGeek"* en el pie de página global, junto con canales directos a las comunidades oficiales de Discord y Telegram.
3. **Monetización Ética y Privada de Afiliados:** Un motor centralizado que inyecta los tags de afiliación de tiendas colaboradoras de forma totalmente transparente para el usuario pero desacoplada de la base de datos y de los formularios de edición.

---

## 2. Motor Centralizado de Afiliados (`IAffiliateUrlResolver`)

### 2.1 Principio de Privacidad y Desacople
Los enlaces de tiendas en Ludeka (tanto para compra de juegos como para fundas/sleeves) se almacenan o resuelven como URLs limpias del producto o dominio (ej. `https://zacatrus.es/juegos-de-mesa/catan.html`).
Los códigos de afiliado, identificadores de campaña o parámetros (`id_affiliate`, `ref`, etc.) **nunca** se introducen manualmente en cada juego ni se exponen en formularios de administración. El backend inyecta los parámetros de afiliación de forma centralizada en el momento de renderizar o resolver el enlace.

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

### 4.2 Enlaces Comunitarios
- Propiedades en `CommunityNotificationOptions`:
  - `DiscordInviteUrl`: Enlace de invitación al servidor oficial de Discord.
  - `TelegramChannelUrl`: Enlace al canal o grupo oficial de Telegram.
- Renderizados con iconos Lucide (`gamepad-2` para Discord, `send` para Telegram), estilos accesibles con microinteracciones de marca y apertura en pestaña segura (`target="_blank" rel="noopener noreferrer"`).

### 4.3 Transparencia de Afiliados
- Microtexto informativo en el footer:
  *"Algunos enlaces a tiendas lúdicas contienen códigos de afiliación que ayudan a mantener los servidores de Ludeka sin coste adicional para ti."*

---

## 5. Corrección de Calendario en Notificaciones Comunitarias

- En `CommunityNotificationService.cs`, se corrigió el cálculo de `startOfWeek` para considerar el domingo (`DayOfWeek.Sunday = 0`) en el sistema europeo comenzando en lunes:
  ```csharp
  int diff = (7 + ((int)today.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
  var startOfWeek = today.AddDays(-diff);
  ```
  Esto garantiza que los envíos dominicales computen con exactitud el rango de la semana en curso sin arrojar discrepancias de fechas.
