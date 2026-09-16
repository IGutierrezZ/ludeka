# Especificación Técnica: Resiliencia, Rate Limiting y Estrategia de Token en BGG

- **Identificador de Cambio:** `change-25-bgg-collection-resilience-token`
- **Módulo Afectado:** Integración BGG XMLAPI2 / Importador de Ludotecas Personales
- **Versión:** 1.0.0

---

## 1. Requerimientos Funcionales (RF)

### RF-01: Sondeo Asíncrono de Colecciones con Backoff Exponencial (HTTP 202 Accepted)
- El cliente `BggXmlApiClient` debe detectar el código de estado `HTTP 202 Accepted` devuelto por `/xmlapi2/collection?username={user}&stats=1`.
- Al recibir un 202, el cliente no debe fallar ni devolver una lista vacía de inmediato; debe iniciar un ciclo de sondeo controlado.
- El intervalo entre intentos debe aplicar un backoff exponencial progresivo:
  - Intento 1: 3 segundos
  - Intento 2: 5 segundos
  - Intento 3: 8 segundos
  - Intento 4: 12 segundos
  - Intento 5+: 15 segundos
- El tiempo total acumulado de sondeo no debe superar `PollingTimeoutSeconds` (por defecto 45 segundos) ni exceder `MaxPollingRetries` (por defecto 6 intentos).
- Si se alcanza el tiempo máximo sin obtener respuesta 200 OK, la operación debe finalizar de forma controlada indicando el tiempo de espera agotado.

### RF-02: Control y Respeto de Rate Limiting (HTTP 429 Too Many Requests y 503)
- El pipeline HTTP debe capturar respuestas `429 Too Many Requests` y `503 Service Unavailable`.
- El sistema debe inspeccionar el encabezado de respuesta `Retry-After`:
  - Si contiene un valor numérico (en segundos), la pausa respetará exactamente dicho número de segundos.
  - Si contiene una fecha HTTP (formato RFC 1123), se calculará la diferencia de segundos hasta dicha fecha (con un mínimo de 1 segundo).
  - Si no contiene encabezado `Retry-After`, se aplicará una pausa por defecto con backoff (5s, 10s, 15s).
- El reintento por rate limit debe limitarse a un máximo configurable (`MaxRateLimitRetries`, por defecto 3).

### RF-03: Inyección de Identificación y Cabeceras Oficiales
- Todas las peticiones salientes hacia los endpoints de BGG XMLAPI2 deben incorporar:
  - Cabecera obligatoria `User-Agent`: formateada según las directrices de BGG (por defecto `"LudekaApp/1.0 (https://ludeka.es; contacto@ludeka.es)"`).
  - Cabecera `Authorization: Bearer <token>`: si `BearerToken` o `ApiToken` está presente en la configuración.
  - Cabecera `X-BGG-API-KEY: <key>`: si `ApiKey` está presente en la configuración.
- La inyección debe realizarse de forma desacoplada y reutilizable mediante un `DelegatingHandler` (`BggResilienceAndAuthHandler`).

### RF-04: Reporte de Progreso Multietapa en Tiempo Real (`IProgress<T>`)
- Se define el modelo inmutable `BggImportProgressReport` con:
  - `Phase`: Fase actual del proceso (`BggImportPhase`).
  - `Message`: Mensaje contextual explicativo en español.
  - `CurrentAttempt`: Intento actual de sondeo o reintento.
  - `MaxAttempts`: Total de intentos máximos configurados.
  - `WaitSeconds`: Segundos de pausa programada antes del siguiente intento (si aplica).
  - `ItemsFound`: Número de juegos recuperados en el XML.
- La interfaz `IBggClient` y el servicio `IBggImportService` deben admitir un parámetro opcional `IProgress<BggImportProgressReport>? progress = null`.
- Los reportes se emitirán en cada transición clave:
  1. `Initializing`: Preparando la solicitud.
  2. `RequestingBgg`: Solicitando la colección a BGG.
  3. `PreparingInBgg`: BGG generando colección en sus servidores (intento X de Y).
  4. `RateLimitedWaiting`: BGG saturado temporalmente (pausa de Z segundos con mensaje de calma).
  5. `ProcessingItems`: Procesando N juegos encontrados en el catálogo local y cola.
  6. `Completed`: Importación finalizada con resumen.
  7. `Failed`: Error o timeout controlado.

### RF-05: Experiencia Visual Interactiva en el Modal de Importación (`BggImportModal.razor`)
- El modal no debe permanecer congelado con un texto estático mientras BGG genera la colección.
- Durante `PreparingInBgg`, la UI debe mostrar un badge informativo con el intento actual y un texto de tranquilidad.
- Durante `RateLimitedWaiting`, la UI debe mostrar un banner ámbar/amarillo explicativo con el contador de espera restante.
- Durante `ProcessingItems`, la UI debe reflejar el volumen de juegos detectados.
- Si se agota el tiempo límite, se mostrará un mensaje amigable con opción de reintentar.

### RF-06: Configuración Extensible y Retrocompatible (`BggOptions`)
- `BggOptions` debe soportar la configuración mediante `appsettings.json` o variables de entorno:
  - `Bgg:ApiKey`
  - `Bgg:BearerToken` (con sincronización bidireccional hacia `ApiToken` para preservar compatibilidad con código existente)
  - `Bgg:MaxPollingRetries`
  - `Bgg:PollingTimeoutSeconds`
  - `Bgg:InitialPollingDelaySeconds`
  - `Bgg:MaxRateLimitRetries`

---

## 2. Requerimientos No Funcionales (RNF)

- **RNF-01 (Compatibilidad hacia atrás):** Las llamadas existentes a `IBggClient.FetchUserCollectionAsync(username)` y `IBggImportService.ImportUserCollectionAsync(request)` sin el parámetro `progress` deben seguir funcionando exactamente igual gracias a parámetros opcionales.
- **RNF-02 (Cero fugas de recursos):** El `DelegatingHandler` y los clientes HTTP deben gestionar adecuadamente el ciclo de vida de `HttpResponseMessage` y `CancellationToken`.
- **RNF-03 (Determinismo en Pruebas):** Todas las pruebas unitarias deben ejecutarse en milisegundos mediante un `HttpMessageHandler` mockeado sin realizar llamadas reales a Internet.
- **RNF-04 (Accesibilidad WCAG 2.2 AA):** Los nuevos estados de carga y avisos de calma en el modal deben contener roles ARIA (`role="status"`, `aria-live="polite"`) y contraste cromático adecuado en modos claro y oscuro.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Resiliencia en la importación de colecciones de BoardGameGeek

  Escenario: Recuperación exitosa de colección tras respuestas 202 Accepted de BGG
    Dado que el usuario "jugador_sevilla" solicita importar su colección de BGG
    Cuando BGG XMLAPI2 responde inicialmente con código 202 Accepted en los dos primeros intentos
    Y BGG responde con código 200 OK y el XML de la colección en el tercer intento
    Entonces el cliente no cancela la solicitud prematuramente
    Y se emiten reportes de progreso con fase "PreparingInBgg" indicando los intentos 1 y 2
    Y finalmente se procesan los juegos recibidos completando la importación exitosamente

  Escenario: Manejo transparente de Rate Limiting 429 con cabecera Retry-After
    Dado que BGG responde con código 429 Too Many Requests y cabecera "Retry-After: 3"
    Cuando el cliente procesa la respuesta
    Entonces se emite un reporte de progreso con fase "RateLimitedWaiting" y WaitSeconds igual a 3
    Y el cliente espera 3 segundos antes de reintentar la solicitud
    Y al reintentar recibe respuesta 200 OK completando el flujo sin errores

  Escenario: Inyección automática de cabeceras de autenticación y User-Agent
    Dado que el sistema tiene configurado BearerToken "bgg-token-secret-123" y ApiKey "api-key-456"
    Cuando se despacha una petición HTTP hacia BGG
    Entonces la solicitud contiene la cabecera "User-Agent" oficial
    Y la solicitud contiene la cabecera "Authorization" con esquema "Bearer bgg-token-secret-123"
    Y la solicitud contiene la cabecera "X-BGG-API-KEY" con valor "api-key-456"

  Escenario: Agotamiento controlado del tiempo de sondeo (Timeout de 45 segundos)
    Dado que BGG responde indefinidamente con código 202 Accepted
    Cuando el tiempo acumulado de sondeo supera los 45 segundos o 6 intentos
    Entonces el cliente finaliza el sondeo de forma segura
    Y emite un reporte de progreso con fase "Failed" o finaliza devolviendo lista vacía
    Y no se lanzan excepciones no controladas que quiebren el servidor Blazor
```
