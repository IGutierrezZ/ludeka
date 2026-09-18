# Checklist de Tareas — INC-44: Worker de Recolección Multicanal Automática

- [x] **Fase 1: Dominio y Contratos (`Ludeka.Core` y `Ludeka.Application`)**
  - [x] Extender `SocialPlatform` con `Telegram = 10` y `RssFeed = 11`.
  - [x] Actualizar `SocialNetworkLink.cs` con iconos y nombres.
  - [x] Añadir `ResolvedFeedUrl` y `SetResolvedFeedUrl` en `MonitoredSocialAccount.cs`.
  - [x] Crear DTOs `DiscoveredSocialPostDto`, `SocialCollectorAccountSummaryDto` y `SocialCollectorRunResultDto`.
  - [x] Crear opciones `SocialCollectorOptions`.
  - [x] Definir contrato `ISocialChannelCollector`.
  - [x] Definir contrato `ISocialCollectorService`.
  - [x] Implementar `SocialCollectorService`.

- [x] **Fase 2: Infraestructura y Recolectores (`Ludeka.Infrastructure`)**
  - [x] Implementar `YouTubeFeedCollector` (Atom XML parser y resolución de `@handle`).
  - [x] Implementar `TelegramChannelCollector` (HTML parser de vista pública `t.me/s/{channel}`).
  - [x] Implementar `RssBlogFeedCollector` (soporte universal RSS 2.0 y Atom para blogs).
  - [x] Implementar `InstagramFeedCollector` (RSS-Bridge, crawler de cortesía y modo simulado).
  - [x] Implementar `SocialCollectorHostedService` (`BackgroundService`).
  - [x] Actualizar `LudekaDbContext.cs` y migrador SQLite `SqliteSchemaMigrator.cs`.
  - [x] Registrar dependencias en `Program.cs`.

- [x] **Fase 3: Interfaz de Usuario Blazor (`Ludeka.Web`)**
  - [x] Configurar sección `"SocialCollector"` en `appsettings.json`.
  - [x] Actualizar `MonitoredAccountsDirectory.razor` con botón de sondeo general, individual y opciones de Telegram/RSS.
  - [x] Actualizar `SocialInboxModeration.razor` con botón de sondeo y eliminación de emojis en marcado.

- [x] **Fase 4: Verificación y Pruebas Unitarias (`Ludeka.UnitTests`)**
  - [x] Crear `YouTubeFeedCollectorTests.cs`.
  - [x] Crear `TelegramChannelCollectorTests.cs`.
  - [x] Crear `RssBlogFeedCollectorTests.cs`.
  - [x] Crear `InstagramFeedCollectorTests.cs`.
  - [x] Crear `SocialCollectorServiceTests.cs`.
  - [x] Verificar `WebMarkupContractTests.cs`.
  - [x] Ejecutar suite completa con 988/988 tests superados al 100%.

- [x] **Fase 5: Documentación y Archivado SDD**
  - [x] Crear módulo de especificación del sistema `30-recolector-canales-sociales.md`.
  - [x] Actualizar índice `docs/specs/sistema/README.md`.
  - [x] Trasladar incremento a `docs/increments/archive/inc-44-social-collector-worker.md`.
  - [x] Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
