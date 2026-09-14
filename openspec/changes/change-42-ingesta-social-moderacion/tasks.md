# Tareas de Implementación: change-42-ingesta-social-moderacion (Incremento 42)

## Fase 1: Dominio (`Ludeka.Core`)
- [ ] 1.1 Crear enums de estado y tipología: `SocialInboxStatus`, `SocialSubmissionType`, `SocialPlatform`, `MonitoredAccountType` en `Ludeka.Core.Enums`.
- [ ] 1.2 Crear entidad de dominio `SocialInboxItem` en `Ludeka.Core.Entities` con métodos de mutación, validación defensiva y ciclo de vida (`UpdateDetails`, `Approve`, `Reject`).
- [ ] 1.3 Crear entidad de dominio `MonitoredSocialAccount` en `Ludeka.Core.Entities` con constructor, validaciones y método de conmutación de estado.
- [ ] 1.4 Pruebas unitarias de dominio para `SocialInboxItem` y `MonitoredSocialAccount` (creación válida, transiciones de estado y excepciones ante datos inválidos).

## Fase 2: Aplicación (`Ludeka.Application`)
- [ ] 2.1 Definir DTOs: `SocialMetadataResultDto`, `SocialAiAnalysisResultDto`, `SocialInboxItemDto`, `SocialInboxManualInputDto`, `SocialInboxUpdateDto`, `MonitoredAccountDto`.
- [ ] 2.2 Definir interfaces y contratos en `Ludeka.Application.Contracts`:
  - `ISocialInboxRepository`
  - `IMonitoredAccountRepository`
  - `ISocialMetadataExtractor`
  - `ISocialAiAnalysisService`
  - `ISocialIngestionService`
  - `IMonitoredAccountService`
- [ ] 2.3 Implementar `SocialIngestionService` en `Ludeka.Application.Features.Community`:
  - Flujo de Alta Exprés: extracción de metadatos -> análisis IA/heurístico -> optimización de imagen con R2 -> persistencia en bandeja.
  - Flujo Modo Manual Avanzado: extracción de carátula/miniatura a R2 -> asignación de juego y categoría manual -> persistencia en bandeja.
  - Flujo de Edición: actualización de campos por el moderador.
  - Flujo de Aprobación y Publicación Atómica: creación de `Giveaway`, `WeeklyRelease`, `BoardGameEvent` o `MediaItem` según tipo, y transición a `Approved`.
  - Flujo de Descarte: transición a `Rejected` con motivo.
- [ ] 2.4 Implementar `MonitoredAccountService` en `Ludeka.Application.Features.Community`:
  - Listado, alta y actualización de canales.
  - Método `SyncFromDirectoryAsync`: rastrea editoriales, creadores y tiendas en la base de datos e inserta perfiles en el directorio.
- [ ] 2.5 Pruebas unitarias para `SocialIngestionService` y `MonitoredAccountService` con Fakes puros (sin Moq).

## Fase 3: Infraestructura (`Ludeka.Infrastructure`)
- [ ] 3.1 Implementar `OpenGraphSocialMetadataExtractor`:
  - Extracción de títulos y miniaturas nativas de YouTube (`hqdefault.jpg`, oEmbed).
  - Parseo de metaetiquetas OpenGraph (`og:title`, `og:image`, `og:description`).
- [ ] 3.2 Implementar `GeminiSocialAnalysisService`:
  - Llamada REST JSON estructurada a Google Gemini API.
  - Generador heurístico completo en español como fallback robusto para entornos sin clave o modo simulado.
- [ ] 3.3 Implementar repositorios `SqliteSocialInboxRepository` y `SqliteMonitoredAccountRepository`.
- [ ] 3.4 Actualizar `LudekaDbContext` con `DbSet<SocialInboxItem>` y `DbSet<MonitoredSocialAccount>`, y actualizar `SqliteSchemaMigrator` para soporte transparente en desarrollo y pruebas.
- [ ] 3.5 Registrar servicios y repositorios en `ServiceCollectionExtensions` / `Program.cs`.
- [ ] 3.6 Pruebas unitarias para el extractor de metadatos, análisis IA heurístico y repositorios.

## Fase 4: Interfaz de Usuario Blazor (`Ludeka.Web`)
- [ ] 4.1 Crear componente modal `SocialExpressIngestModal.razor`:
  - Pestaña "Pegar URL y Listo" (campo URL + texto opcional + botón de procesar con spinner).
  - Pestaña "Modo Manual Avanzado" (campo URL + selector de tipo + buscador de juego + categoría/badge).
- [ ] 4.2 Crear componente modal `SocialInboxEditModal.razor`:
  - Edición interactiva de todos los campos extraídos antes de publicar.
  - Buscador reactivo de juegos del catálogo para cambiar el juego asignado.
- [ ] 4.3 Crear página de bandeja de moderación `SocialInboxModeration.razor` en `/admin/ingesta-social`:
  - Métricas de pendientes por categoría.
  - Pestañas de filtrado (`Todos`, `Sorteos`, `Novedades`, `Eventos`, `Vídeos`).
  - Botones de acción "Aprobar y Publicar", "Editar" y "Descartar".
- [ ] 4.4 Crear página de directorio de cuentas `MonitoredAccountsDirectory.razor` en `/admin/canales-monitorizados`:
  - Filtros por plataforma y tipo.
  - Acciones "Nueva Cuenta", "Sincronizar desde Directorio" y botón rápido "⚡ Añadir Publicación".
- [ ] 4.5 Añadir accesos de navegación en la barra de administración y botón "⚡ Alta Exprés" en las cabeceras de Sorteos, Eventos y Novedades.

## Fase 5: Verificación y Cierre
- [ ] 5.1 Ejecución completa de la suite de pruebas unitarias (`dotnet test`) asegurando 0 regresiones.
- [ ] 5.2 Redacción del informe de verificación `verify-report.md`.
- [ ] 5.3 Actualización de la especificación viva del sistema en `docs/specs/sistema/`.
