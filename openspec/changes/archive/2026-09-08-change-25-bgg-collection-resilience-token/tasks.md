# Checklist de Tareas Técnicas: change-25-bgg-collection-resilience-token

- **Incremento:** INC-25
- **Estado:** ✅ Completado y Verificado (609 pruebas pasando al 100%)

---

## Tarea 1: Dominio y Contratos de Aplicación
- [x] 1.1 Crear `src/Ludeka.Core/Enums/BggImportPhase.cs` con las fases del ciclo de importación.
- [x] 1.2 Crear `src/Ludeka.Application/DTOs/BggImportProgressReport.cs` con el record inmutable de progreso.
- [x] 1.3 Actualizar el contrato `src/Ludeka.Application/Contracts/IBggClient.cs` añadiendo la sobrecarga o parámetro opcional `IProgress<BggImportProgressReport>? progress = null`.
- [x] 1.4 Actualizar el contrato `src/Ludeka.Application/Contracts/IBggImportService.cs` añadiendo el parámetro opcional `IProgress<BggImportProgressReport>? progress = null`.

## Tarea 2: Infraestructura y Red HTTP
- [x] 2.1 Actualizar `src/Ludeka.Infrastructure/Bgg/BggOptions.cs` con `ApiKey`, `BearerToken`, `MaxPollingRetries`, `PollingTimeoutSeconds`, `InitialPollingDelaySeconds`, `MaxRateLimitRetries` y sincronización bidireccional.
- [x] 2.2 Crear `src/Ludeka.Infrastructure/Bgg/BggResilienceAndAuthHandler.cs` (`DelegatingHandler`) para inyección de `User-Agent`, `Authorization: Bearer`, `X-BGG-API-KEY` y método auxiliar de extracción de `Retry-After`.
- [x] 2.3 Refactorizar `src/Ludeka.Infrastructure/Bgg/BggXmlApiClient.cs` implementando sondeo asíncrono con backoff progresivo (3s, 5s, 8s...), lectura de `Retry-After` ante 429, timeout global de 45s y notificación continua a través de `IProgress<BggImportProgressReport>`.
- [x] 2.4 Actualizar `src/Ludeka.Infrastructure/Bgg/SimulatedBggClient.cs` para implementar la firma con `IProgress<BggImportProgressReport>`.
- [x] 2.5 Registrar `BggResilienceAndAuthHandler` en `src/Ludeka.Web/Program.cs` y vincularlo al `HttpClient` de `BggXmlApiClient`.

## Tarea 3: Orquestación en la Capa de Aplicación
- [x] 3.1 Modificar `src/Ludeka.Application/Features/Bgg/BggImportService.cs` para emitir progresos en las fases `Initializing`, delegar a `_bggClient.FetchUserCollectionAsync`, reportar `ProcessingItems` durante el cruce de catálogo y `Completed` al finalizar.

## Tarea 4: Interfaz de Usuario y Experiencia en Vivo (Blazor)
- [x] 4.1 Actualizar `src/Ludeka.Web/Components/Shared/BggImportModal.razor` integrando `Progress<BggImportProgressReport>`.
- [x] 4.2 Renderizar paneles reactivos en el modal:
  - Estado de preparación en BGG (`PreparingInBgg`) con intento actual.
  - Alerta de calma por saturación (`RateLimitedWaiting`) con contador regresivo.
  - Contador dinámico de juegos encontrados (`ProcessingItems`).
  - Manejo amigable de timeout con botón de reintento.

## Tarea 5: Suite de Pruebas Automatizadas
- [x] 5.1 Crear `tests/Ludeka.UnitTests/Infrastructure/BggXmlApiClientResilienceTests.cs` con pruebas unitarias para 202 con reintentos y éxito 200, 429 con cabecera Retry-After, inyección de BearerToken y ApiKey, y timeout amigable de sondeo.
- [x] 5.2 Crear `tests/Ludeka.UnitTests/Application/BggImportServiceProgressTests.cs` para validar la propagación de eventos de progreso en la capa de aplicación.
- [x] 5.3 Ampliar `tests/Ludeka.UnitTests/Infrastructure/BggOptionsTests.cs` con las nuevas opciones de resiliencia y tokens.
- [x] 5.4 Ejecutar `dotnet test` y garantizar 0 errores y que todas las pruebas existentes y nuevas pasen al 100%.

## Tarea 6: Verificación y Cierre SDD (`sdd-verify` y `sdd-archive`)
- [x] 6.1 Generar informe de verificación `verify-report.md`.
- [x] 6.2 Actualizar la Especificación Viva del Sistema en `docs/specs/sistema/05-integracion-bgg.md` y `docs/specs/sistema/README.md`.
- [x] 6.3 Archivar el incremento trasladando documentos a `archive/` y actualizando los índices de Roadmap.
- [x] 6.4 Guardar resumen y decisiones en la memoria persistente Engram.
