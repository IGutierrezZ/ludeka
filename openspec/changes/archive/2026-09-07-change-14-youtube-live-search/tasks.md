# Tareas de Implementación: change-14-youtube-live-search (Incremento 14)

## Tareas

- [x] **1. Dominio (`Ludeka.Core`)**
  - [x] 1.1 Añadir `QuickOverview` al enum `MediaType` en `src/Ludeka.Core/Enums/MediaType.cs`.
  - [x] 1.2 Implementar `PlayerCountExtractor` en `src/Ludeka.Core/Helpers/PlayerCountExtractor.cs` con analizador regex en español y fallback a la entidad `Game`.

- [x] **2. Contratos y DTOs (`Ludeka.Application`)**
  - [x] 2.1 Crear DTOs `YouTubeSearchResultDto` y `YouTubeIngestRequestDto` en `src/Ludeka.Application/DTOs/YouTubeDtos.cs`.
  - [x] 2.2 Crear abstracción y modelos `IChannelFocusProvider` en `src/Ludeka.Application/Contracts/IChannelFocusProvider.cs`.
  - [x] 2.3 Crear contrato `IYouTubeSearchService` en `src/Ludeka.Application/Contracts/IYouTubeSearchService.cs`.
  - [x] 2.4 Agregar método `ExistsByUrlAsync(string url, CancellationToken ct = default)` a `IMediaRepository` en `src/Ludeka.Application/Contracts/IMediaRepository.cs`.
  - [x] 2.5 Actualizar DTO `GameMediaHubDto` en `src/Ludeka.Application/DTOs/MediaDtos.cs` para incluir lista de `QuickOverviews`.
  - [x] 2.6 Actualizar `MediaService.GetGameMediaAsync` para poblar la lista de `QuickOverviews`.

- [x] **3. Infraestructura y Servicios (`Ludeka.Infrastructure`)**
  - [x] 3.1 Crear `YouTubeOptions` en `src/Ludeka.Infrastructure/YouTube/YouTubeOptions.cs`.
  - [x] 3.2 Implementar `ChannelFocusProvider` en `src/Ludeka.Infrastructure/YouTube/ChannelFocusProvider.cs` con el padrón de canales de Editoriales, Creadores y Tiendas.
  - [x] 3.3 Crear dataset curado y generador heurístico `YouTubeSimulationDataset` en `src/Ludeka.Infrastructure/YouTube/YouTubeSimulationDataset.cs` (salvaguardas para tests sin internet).
  - [x] 3.4 Implementar `YouTubeSearchService` en `src/Ludeka.Infrastructure/YouTube/YouTubeSearchService.cs` con cliente HTTP real a YouTube Data API v3, cálculo de relevancia según canales foco, filtros de duración (QuickOverview ≤ 3 min, Tutoriales 8-25 min), extracción de badges y prevención de duplicados.
  - [x] 3.5 Implementar `ExistsByUrlAsync` en `src/Ludeka.Infrastructure/Data/SqliteMediaRepository.cs`.

- [x] **4. Presentación Blazor y Configuración (`Ludeka.Web`)**
  - [x] 4.1 Añadir sección `"YouTube"` en `src/Ludeka.Web/appsettings.json`.
  - [x] 4.2 Registrar servicios en `Program.cs` (`YouTubeOptions`, `IChannelFocusProvider`, `IYouTubeSearchService` con `AddHttpClient`).
  - [x] 4.3 Actualizar `MultimediaHub.razor` para soportar la pestaña `⚡ Cómo funciona (~2 min)`.
  - [x] 4.4 Añadir ruta `@page "/admin/moderacion-medios"` a `MediaModeration.razor`.
  - [x] 4.5 Implementar componente modal / sección `YouTubeSearchModal.razor` e integrarlo en `MediaModeration.razor` con previsualización embebida e ingesta en 1 clic (`📥 Guardar en Pendientes` / `✅ Aprobar Directo`).

- [x] **5. Pruebas Automatizadas y Verificación (`Ludeka.UnitTests`)**
  - [x] 5.1 Crear `PlayerCountExtractorTests.cs` (validación de expresiones regulares de comensales y fallbacks de escalabilidad).
  - [x] 5.2 Crear `ChannelFocusProviderTests.cs` (clasificación y bonificación de canales de editoriales, creadores y tiendas).
  - [x] 5.3 Crear `YouTubeSearchServiceTests.cs` (pruebas de búsqueda real/mock con `HttpMessageHandler`, filtros de duración, badges de partidas y prevención de duplicados).
  - [x] 5.4 Ejecutar suite completa `dotnet test` asegurando cero regresiones y 100% de tests aprobados.
  - [x] 5.5 Generar informe de verificación `verify-report.md`.
