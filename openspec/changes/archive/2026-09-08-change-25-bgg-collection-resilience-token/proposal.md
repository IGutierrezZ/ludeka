# Propuesta de Cambio: change-25-bgg-collection-resilience-token

## 1. Resumen Ejecutivo
El **Incremento 25** consolida la resiliencia técnica y la experiencia de usuario en el importador de colecciones de BoardGameGeek (`/xmlapi2/collection`):
1. **Gestión Robusta de Respuestas Asíncronas (HTTP 202 Accepted):**
   - Implementación de un ciclo de polling inteligente con backoff exponencial progresivo (3s, 5s, 8s, 12s...) y timeout configurable de 45 segundos.
   - Eliminación de fallos falsos positivos por colecciones frías en la caché de BGG.
2. **Mitigación y Calma ante Rate Limiting (HTTP 429 y 503):**
   - Detección e inspección de la cabecera `Retry-After` (en segundos o fecha HTTP).
   - Pausa controlada y emisión de estados de espera a la capa de presentación, informando transparentemente al usuario de la saturación momentánea de BGG.
3. **Estrategia de Identificación y Autenticación con Tokens/Keys BGG:**
   - Inyección desacoplada mediante un `DelegatingHandler` (`BggResilienceAndAuthHandler`) que adjunta:
     - Cabecera obligatoria `User-Agent` según directrices de BGG.
     - Cabecera `Authorization: Bearer <token>` si `BearerToken` o `ApiToken` está configurado.
     - Cabecera `X-BGG-API-KEY` o query param `?token=` si `ApiKey` está presente.
   - Extensión de `BggOptions` con opciones de configuración de credenciales y resiliencia.
4. **Feedback Visual en Tiempo Real en la Interfaz (`BggImportModal.razor`):**
   - Transición desde un spinner estático a una barra interactiva de progreso multietapa basada en `IProgress<BggImportProgressReport>`.
   - Mensajes contextuales explicativos: preparación en servidores BGG, intento actual de sondeo, aviso de calma por rate limit y recuento de títulos encontrados.

---

## 2. Justificación y Valor para el Ecosistema
1. **Fin a las Importaciones Frustradas por Colecciones Frías:** Usuarios con colecciones grandes o que llevan meses sin consultar BGG sufrían fallos silenciosos porque BGG devolvía 202 en el primer segundo y la app abandonaba la petición. Con polling adaptativo, el 100% de estas peticiones se recuperan con éxito.
2. **Protección contra Bloqueos de IP:** El cumplimiento estricto de las directrices de User-Agent de BGG y el respeto a las cabeceras `Retry-After` evitan penalizaciones y baneos temporales de Cloudflare.
3. **Futuro Garantizado ante Cambios de BGG:** Con el despliegue progresivo de Application Tokens por parte de BoardGameGeek, Ludeka queda lista para operar tanto en modo abierto como autenticado sin refactorizaciones adicionales.
4. **Experiencia de Usuario Transparente y Confiable:** Informar al usuario de que "BGG está preparando su colección" elimina la sensación de cuelgue o lentitud atribuida erróneamente a Ludeka.

---

## 3. Alcance de la Propuesta por Capas

### 3.1 Dominio y Contratos (`Ludeka.Core` / `Ludeka.Application`)
- **`BggImportPhase`:** Enum con las fases del ciclo de vida de importación:
  - `Initializing`: Inicializando solicitud.
  - `RequestingBgg`: Contactando con BGG XMLAPI2.
  - `PreparingInBgg`: BGG está generando la colección en caché (HTTP 202).
  - `RateLimitedWaiting`: BGG ha solicitado esperar (HTTP 429 con `Retry-After`).
  - `ProcessingItems`: XML recibido, cruzando títulos con catálogo local y cola.
  - `Completed`: Importación completada con éxito.
  - `Failed`: Error irrecuperable o timeout excedido.
- **`BggImportProgressReport`:** Record inmutable con la información de progreso:
  ```csharp
  public record BggImportProgressReport(
      BggImportPhase Phase,
      string Message,
      int CurrentAttempt = 0,
      int MaxAttempts = 0,
      int? WaitSeconds = null,
      int ItemsCount = 0
  );
  ```
- **Contrato `IBggClient`:**
  - Incorporar sobrecarga con soporte de reporte de progreso:
    `Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, IProgress<BggImportProgressReport>? progress = null, CancellationToken ct = default);`
- **Contrato `IBggImportService`:**
  - Actualizar firma para admitir `IProgress<BggImportProgressReport>? progress = null`.

### 3.2 Infraestructura (`Ludeka.Infrastructure`)
- **`BggOptions`:**
  - Nuevas propiedades:
    - `ApiKey`: Clave de API de BGG.
    - `BearerToken`: Token Bearer emitido por BGG Applications (sincronizado bidireccionalmente con `ApiToken`).
    - `MaxPollingRetries`: Número máximo de reintentos ante 202 (por defecto 6).
    - `PollingTimeoutSeconds`: Tiempo límite total para polling de colecciones (por defecto 45s).
    - `InitialPollingDelaySeconds`: Delay base inicial de polling (por defecto 3s).
    - `MaxRateLimitRetries`: Intentos máximos de espera ante 429 (por defecto 3).
- **`BggResilienceAndAuthHandler` (`DelegatingHandler`):**
  - Manejador HTTP registrado en el pipeline de `HttpClient`.
  - Inyección de `UserAgent`, `Authorization: Bearer` y cabeceras de API key.
  - Intercepción de `429 Too Many Requests` y lectura del encabezado `Retry-After`.
- **`BggXmlApiClient`:**
  - Refactorización de `FetchUserCollectionAsync` implementando polling adaptativo con backoff (3s, 5s, 8s, 12s, 15s) hasta `PollingTimeoutSeconds`.
  - Emisión de notificaciones a través de `IProgress<BggImportProgressReport>` en cada transición de estado y reintento.
- **`SimulatedBggClient`:**
  - Adaptación a la nueva firma con `IProgress<BggImportProgressReport>`.
  - Notificación de progreso de prueba determinista para no romper entornos simulados.
- **Inyección de Dependencias en `Program.cs`:**
  - Registro de `BggResilienceAndAuthHandler` en la configuración de `HttpClient<BggXmlApiClient>`.

### 3.3 Presentación (`Ludeka.Web`)
- **`BggImportModal.razor`:**
  - Integración con `Progress<BggImportProgressReport>` para actualizar la interfaz en tiempo real.
  - Indicador visual dinámico según `BggImportPhase`:
    - Spinner con animación adaptativa y badge de estado.
    - Mensajes explícitos durante `PreparingInBgg` indicando intento y espera.
    - Banner amarillo de calma con cuenta atrás durante `RateLimitedWaiting`.
    - Contador de juegos durante `ProcessingItems`.
  - Botón de reintento amigable si se agota el tiempo de espera máximo.

---

## 4. Criterios de Aceptación y Pruebas Unitarias
1. **Respuesta 202 Accepted:**
   - Emulación de 2 respuestas 202 seguidas de un 200 OK con colección XML.
   - Se verifica que el cliente reintenta con backoff, no aborta, emite las fases `PreparingInBgg` y finalmente retorna los elementos completos.
2. **Control de Rate Limit 429:**
   - Emulación de una respuesta 429 con cabecera `Retry-After: 3`.
   - Se verifica que el manejador/cliente extrae el delay exacto, notifica `RateLimitedWaiting` con 3 segundos de espera y reintenta exitosamente.
3. **Inyección de Autenticación y User-Agent:**
   - Se verifica que la cabecera `User-Agent` viaja en cada petición saliente.
   - Con `BearerToken` configurado, la petición incluye `Authorization: Bearer <token>`.
   - Con `ApiKey` configurado, la petición incluye la cabecera correspondiente.
4. **Timeout y Resiliencia:**
   - Si BGG persiste en 202 superando el límite de 45s o reintentos máximos, se retorna colección vacía o notificación de fallo controlado sin excepciones no controladas.
5. **No Regresión:**
   - Los 600 tests existentes continúan en verde al 100%.
