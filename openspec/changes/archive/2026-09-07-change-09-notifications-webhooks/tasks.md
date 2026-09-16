# Checklist de Tareas: change-09-notifications-webhooks (Incremento 9)

## Fase 1: Dominio (`Ludeka.Core`)
- [x] 1.1 Crear enums de notificación:
  - [x] `src/Ludeka.Core/Enums/NotificationChannel.cs`
  - [x] `src/Ludeka.Core/Enums/NotificationEventType.cs`
  - [x] `src/Ludeka.Core/Enums/NotificationStatus.cs`
- [x] 1.2 Crear entidad de auditoría e historial:
  - [x] `src/Ludeka.Core/Entities/CommunityNotificationLog.cs`
- [x] 1.3 Crear objeto de mensaje inmutable:
  - [x] `src/Ludeka.Core/ValueObjects/CommunityNotificationMessage.cs`
- [x] 1.4 Pruebas unitarias de Dominio:
  - [x] `tests/Ludeka.UnitTests/Domain/CommunityNotificationLogTests.cs`

---

## Fase 2: Aplicación y Casos de Uso (`Ludeka.Application`)
- [x] 2.1 Crear opciones y DTOs de notificación:
  - [x] `src/Ludeka.Application/Options/CommunityNotificationOptions.cs`
  - [x] `src/Ludeka.Application/DTOs/CommunityNotificationDtos.cs`
- [x] 2.2 Crear interfaces de cola y persistencia:
  - [x] `src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs`
  - [x] `src/Ludeka.Application/Contracts/ICommunityNotificationRepository.cs`
  - [x] `src/Ludeka.Application/Contracts/ICommunityNotificationService.cs`
- [x] 2.3 Implementar servicio de negocio:
  - [x] `src/Ludeka.Application/Features/Community/CommunityNotificationService.cs`
- [x] 2.4 Pruebas unitarias de Aplicación:
  - [x] `tests/Ludeka.UnitTests/Application/CommunityNotificationServiceTests.cs`

---

## Fase 3: Infraestructura y Segundo Plano (`Ludeka.Infrastructure`)
- [x] 3.1 Clientes HTTP de Discord y Telegram:
  - [x] `src/Ludeka.Infrastructure/Notifications/IDiscordWebhookClient.cs`
  - [x] `src/Ludeka.Infrastructure/Notifications/DiscordWebhookClient.cs`
  - [x] `src/Ludeka.Infrastructure/Notifications/ITelegramBotClient.cs`
  - [x] `src/Ludeka.Infrastructure/Notifications/TelegramBotClient.cs`
- [x] 3.2 Cola de canal en memoria:
  - [x] `src/Ludeka.Infrastructure/Notifications/InMemoryCommunityNotificationQueue.cs`
- [x] 3.3 Servicio Hosted en segundo plano:
  - [x] `src/Ludeka.Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs`
- [x] 3.4 Persistencia EF Core SQLite:
  - [x] Actualizar `LudekaDbContext.cs` con `DbSet<CommunityNotificationLog> NotificationLogs`
  - [x] `src/Ludeka.Infrastructure/Repositories/SqliteCommunityNotificationRepository.cs`
- [x] 3.5 Registro de dependencias en `Program.cs`:
  - [x] Registrar opciones, `HttpClient`, `ICommunityNotificationQueue`, repositorios, servicios y el hosted service.
- [x] 3.6 Pruebas unitarias de Infraestructura y Cola:
  - [x] `tests/Ludeka.UnitTests/Infrastructure/CommunityNotificationQueueTests.cs`
  - [x] `tests/Ludeka.UnitTests/Infrastructure/SqliteCommunityNotificationRepositoryTests.cs`

---

## Fase 4: Interfaz de Usuario Blazor (`Ludeka.Web`)
- [x] 4.1 Panel de control de notificaciones:
  - [x] `src/Ludeka.Web/Components/Pages/AdminNotifications.razor`
- [x] 4.2 Integrar navegación en cabecera:
  - [x] Añadir enlace `🔔 Webhooks` en `MainLayout.razor` para fundadores/moderadores.

---

## Fase 5: Verificación Integral y Cierre
- [x] 5.1 Ejecución de pruebas unitarias (.NET 10 xUnit -> 189 tests superados).
- [x] 5.2 Generar reporte de verificación (`verify-report.md`).
- [x] 5.3 Actualizar hoja de ruta (`ROADMAP_MVP_SLICES.md`).
- [x] 5.4 Archivar incremento en `.openspec/changes/archive/`.
- [x] 5.5 Commit convencional en Git.
