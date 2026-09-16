# Fase de Exploración: change-25-bgg-collection-resilience-token

## 1. Contexto y Objetivos del Incremento
El **Incremento 25** tiene como objetivo auditar y blindar el cliente de importación de colecciones de usuarios de BoardGameGeek (`/xmlapi2/collection`), abordando los tres grandes desafíos de resiliencia del protocolo BGG:
1. **Comportamiento Asíncrono de BGG (HTTP 202 Accepted):**
   - Cuando un usuario solicita su colección a BGG y ésta no reside en la caché en caliente de sus servidores, BGG responde con `202 Accepted` ("Your request for this collection has been accepted and will be processed. Please try again later for access.").
   - Si la aplicación no maneja este código como un estado transitorio normal, la importación falla prematuramente y el usuario ve un error o una colección vacía.
   - Es necesario un mecanismo de polling con backoff exponencial progresivo (3s, 5s, 8s...) con un límite de tiempo máximo (timeout amigable de hasta 45 segundos) informando continuamente del estado de preparación.
2. **Control de Límite de Tasa (Rate Limiting 429 / 503):**
   - Cuando los servidores de BGG o Cloudflare detectan ráfagas o sobrecarga, devuelven códigos `429 Too Many Requests` o `503 Service Unavailable`, a menudo acompañados de la cabecera `Retry-After`.
   - La aplicación debe capturar esta cabecera, respetar el tiempo indicado, pausar ordenadamente y transmitir a la interfaz un aviso de calma para el usuario.
3. **Estrategia de Identificación, Cabeceras y Tokens API:**
   - Cumplimiento de las directrices oficiales de BGG sobre cabecera `User-Agent` descriptiva con contacto.
   - Preparación para la nueva estrategia de autenticación de BGG (Application Tokens / Bearer y API Keys) mediante inyección limpia con `DelegatingHandler` en el pipeline de `HttpClient`.
4. **Feedback Visual en Tiempo Real (`BggImportModal.razor`):**
   - Sustituir el spinner congelado genérico por una experiencia interactiva y tranquilizadora con estados explícitos:
     - *"Solicitando colección a BoardGameGeek..."*
     - *"BGG está preparando tu colección en sus servidores (intento X de Y)..."*
     - *"BGG está saturado temporalmente. Reintentando en Xs..."*
     - *"Procesando N juegos encontrados en tu ludoteca..."*

---

## 2. Diagnóstico del Código Fuente Existente

### 2.1 Cliente HTTP y Resiliencia (`Ludeka.Infrastructure/Bgg`)
- **`BggXmlApiClient`:**
  - Actualmente implementa un bucle simple con `TokenBucketRateLimiter` (2 req/s) y reintentos genéricos con delays lineales (`delayMs * attempt`).
  - *Gaps detectados:*
    1. En `FetchUserCollectionAsync`, sólo reintenta 4 veces con delay fijo (`2000 * attempt`). Si la colección tarda 20 segundos en generarse, agota los intentos y devuelve una lista vacía `[]`.
    2. No soporta ni notifica progreso (`IProgress<T>`), impidiendo que la UI sepa si BGG devolvió 202 o en qué reintento va.
    3. Si recibe `429 Too Many Requests`, no inspecciona la cabecera `response.Headers.RetryAfter`.
    4. El `User-Agent` y la cabecera `Authorization: Bearer` se configuran estáticamente en el constructor del cliente sobre `DefaultRequestHeaders`, en lugar de utilizar un `DelegatingHandler` desacoplado y reutilizable en el pipeline de `HttpClient`.
- **`BggOptions`:**
  - Define `ApiToken`, `BaseUrl`, `UserAgent`, `SimulateApi` y `ShouldSimulate`.
  - *Gaps detectados:* No expone `ApiKey`, ni parámetros configurables de resiliencia (`MaxPollingRetries`, `PollingTimeoutSeconds`, `InitialPollingDelaySeconds`, `MaxRateLimitRetries`).
- **`SimulatedBggClient`:**
  - Devuelve inmediatamente datos del dataset simulado.
  - *Gaps detectados:* No permite simular respuestas 202 consecutivas ni 429 con `Retry-After` para comprobar los flujos de resiliencia y el comportamiento del modal en pruebas de integración y componentes.

### 2.2 Capa de Aplicación (`Ludeka.Application`)
- **`IBggClient`:**
  - La firma actual `Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)` no recibe un canal o interfaz de reporte de progreso.
  - Se requiere una sobrecarga o extensión con `IProgress<BggFetchProgressReport>` para comunicar los estados del polling y rate limiting.
- **`IBggImportService` y `BggImportService`:**
  - El método `ImportUserCollectionAsync(BggImportRequest request, CancellationToken ct = default)` ejecuta la llamada de forma síncrona sin reportar progreso a la UI.
  - Se necesita incorporar `IProgress<BggImportProgressReport>` para que el modal de importación reciba las fases (`Initializing`, `RequestingBgg`, `PreparingInBgg`, `RateLimitedWaiting`, `ProcessingItems`, `Completed`, `Failed`).

### 2.3 Capa de Presentación (`Ludeka.Web`)
- **`BggImportModal.razor`:**
  - Posee un estado booleano simple `_isProcessing`. Cuando está activo, muestra un icono rebotando y un texto fijo *"Sincronizando con BoardGameGeek..."*.
  - Si BGG tarda 25 segundos por respuestas 202, el usuario no sabe si la app se ha colgado o sigue trabajando.
  - No muestra mensajes de calma ante saturación de BGG (429).
  - Al recibir reportes de progreso, puede renderizar dinámicamente el mensaje exacto, el número de intento actual, el contador regresivo de reintento y el progreso en tiempo real.

---

## 3. Estrategia de Solución Propuesta
1. **Modelos de Reporte de Progreso:**
   - Crear `BggImportProgressReport` y `BggImportPhase` en `Ludeka.Application.DTOs`.
2. **Pipeline HTTP con DelegatingHandler:**
   - Crear `BggResilienceAndAuthHandler : DelegatingHandler` en `Ludeka.Infrastructure.Bgg` para inyectar cabeceras (`User-Agent`, `Authorization: Bearer`, `X-BGG-API-KEY` o query param `?token=`) y gestionar cabeceras de respuesta como `Retry-After`.
3. **Mecanismo de Polling Inteligente con Backoff:**
   - Enriquecer `BggXmlApiClient.FetchUserCollectionAsync` para emitir eventos de progreso, soportar backoff exponencial (ej. 3s, 5s, 8s, 12s, 15s) hasta un límite de tiempo total de 45s, y procesar cabeceras `Retry-After`.
4. **Actualización de Servicios de Aplicación:**
   - Actualizar `IBggImportService` y `BggImportService` para admitir `IProgress<BggImportProgressReport>? progress = null`.
5. **Mejora del Componente UI `BggImportModal.razor`:**
   - Diseñar una interfaz interactiva y reactiva con barra de progreso, badge de calma en caso de espera/reintento y mensajes pedagógicos claros.
6. **Batería de Pruebas Unitarias Robustas:**
   - Pruebas con `MockHttpMessageHandler` emulando: 202 inicial y 200 subsiguiente; 429 con `Retry-After`; autenticación con Bearer Token y ApiKey; y expiración amigable por timeout.
