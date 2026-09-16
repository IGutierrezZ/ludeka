# Checklist de Tareas Técnicas: change-30-played-independent-status

## Fase 1: Dominio (`Ludeka.Core`)
- [x] 1.1 Actualizar entidad `UserCollectionItem` incorporando `bool IsPlayed`, `CollectionStatus? Status` nullable, constructores y métodos `ChangeStatus`, `TogglePlayed`, `SetPlayed`.
- [x] 1.2 Crear entidad de dominio `GamePlayLog` con validaciones de comensales (>=1), fecha y comentarios.
- [x] 1.3 Crear pruebas unitarias de dominio para `UserCollectionItem` y `GamePlayLog` en `Ludeka.UnitTests/Domain/`.

## Fase 2: Infraestructura y Persistencia (`Ludeka.Infrastructure`)
- [x] 2.1 Configurar `GamePlayLog` y adaptar `UserCollectionItem` en `LudekaDbContext`.
- [x] 2.2 Implementar migraciones defensivas y retrocompatibles en `SqliteSchemaMigrator`:
  - Añadir columna `IsPlayed` a `UserCollectionItems`.
  - Migrar `Status = 2` (`Played`) a `IsPlayed = 1, Status = NULL`.
  - Migrar `Status = 3` (`Wishlist`) a `Status = 4` (`WantToBuy`).
  - Crear tabla e índices para `GamePlayLogs`.
- [x] 2.3 Implementar `SqliteGamePlayLogRepository` bajo el contrato `IGamePlayLogRepository`.
- [x] 2.4 Actualizar `SqliteUserCollectionRepository` para soportar conteo y filtrado con `IsPlayed`.
- [x] 2.5 Pruebas de integración/persistencia SQLite para colecciones y partidas en `Ludeka.UnitTests/Infrastructure/`.

## Fase 3: Capa de Aplicación (`Ludeka.Application`)
- [x] 3.1 Definir DTOs: `GamePlayLogDto`, `RecordPlayRequest`, `UserPlaysStatsDto` y actualizar `UserCollectionItemDto` y `UserLibrarySummaryDto`.
- [x] 3.2 Crear servicio `GamePlayLogService` (`IGamePlayLogService`) para registrar partidas, asegurar `IsPlayed = true` y calcular estadísticas de juego.
- [x] 3.3 Adaptar `UserLibraryService` para 3 estados (`InCollection`, `WantToBuy`, `null`), desmarcado por toggle y regla de negocio de valoración condicionada a `IsPlayed`.
- [x] 3.4 Actualizar `UserLibraryStatsService` y `BggImportService` para reflejar `IsPlayed` y mapear listas BGG a `WantToBuy`.
- [x] 3.5 Pruebas unitarias de casos de uso en `Ludeka.UnitTests/Application/`.

## Fase 4: Componentes Web Blazor (`Ludeka.Web`)
- [x] 4.1 Rediseñar `CollectionActionBar.razor`:
  - 3 botones ergonómicos: `En mi ludoteca`, `Jugado`, `Comprar`.
  - Soporte de estado simultáneo `Jugado + Comprar` y `Jugado + En mi ludoteca`.
  - Píldora de aviso de radar de sorteos/descuentos cuando está en `Comprar`.
  - Botón contextual para registrar partida `[ ➕ Registrar Partida ]`.
- [x] 4.2 Crear componente modal `RecordPlayModal.razor` para registro táctil rápido de sesiones.
- [x] 4.3 Actualizar `GameDetail.razor`:
  - Integrar `RecordPlayModal` e indicador de partidas registradas del usuario.
  - Bloqueo de valoración con mensaje explicativo si `!IsPlayed`.
- [x] 4.4 Actualizar `MyLibrary.razor`:
  - Reestructurar pestañas eliminando "Deseados" y añadiendo "📝 Diario de Partidas".
  - Añadir panel de analíticas de partidas del usuario.
  - Pestaña "Comprar" con banner de radar de ofertas/sorteos.

## Fase 5: Verificación y Cierre (`sdd-verify` y `sdd-archive`)
- [x] 5.1 Ejecutar suite completa `dotnet test` y asegurar que todos los tests pasen al 100% (705/705 correctos).
- [x] 5.2 Generar reporte de verificación independiente.
- [x] 5.3 Volcar módulo a la especificación viva del sistema en `docs/specs/sistema/` y archivar el incremento.
