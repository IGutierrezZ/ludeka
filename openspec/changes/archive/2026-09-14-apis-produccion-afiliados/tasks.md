# Tareas: INC-37 — Modo Producción: APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados

- [x] **1. Corrección de Calendario en CommunityNotificationService (Strict TDD)**
  - [x] 1.1 Modificar la fórmula de `startOfWeek` en `CommunityNotificationService.cs` para manejar correctamente el domingo (`(7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7`).
  - [x] 1.2 Ejecutar `dotnet test --filter CommunityNotificationServiceTests` y verificar que los tests pasen en verde al 100%.

- [x] **2. Motor Centralizado y Privado de Afiliación de Tiendas**
  - [x] 2.1 Crear `IAffiliateUrlResolver.cs` en `Ludeka.Application.Contracts`.
  - [x] 2.2 Crear `AffiliateOptions.cs` en `Ludeka.Application.Options` con reglas por defecto para Zacatrus, Mathom, Dungeon Marvels, Cuarto de Juegos y Tablerum.
  - [x] 2.3 Implementar `AffiliateUrlResolver.cs` en `Ludeka.Application.Features.Affiliates` con soporte de inyección de parámetros query (`?` y `&`), normalización de dominios y preservación de URLs desconocidas.
  - [x] 2.4 Registrar `AffiliateOptions` y `IAffiliateUrlResolver` en el contenedor DI de `Program.cs`.
  - [x] 2.5 Crear pruebas unitarias completas en `Ludeka.UnitTests/Application/AffiliateUrlResolverTests.cs`.
  - [x] 2.6 Integrar `IAffiliateUrlResolver` en `StoreOffersCard.razor` y en `SleeveStoreUrlResolver.cs`.

- [x] **3. Desacople de Mocks en BGG, Gemini y YouTube**
  - [x] 3.1 Ajustar `BggOptions.cs` para que `ShouldSimulate` dependa únicamente de `SimulateApi`.
  - [x] 3.2 Actualizar `BggImportService.cs` y `BggXmlApiClient.cs` para devolver mensajes amigables y tipados cuando BGG falle, sin recurrir a datos mock cuando `SimulateApi=false`.
  - [x] 3.3 Modificar `GeminiGameSummaryService.cs` para que cuando `ShouldSimulate` sea `false`, ante un error o falta de `ApiKey` no genere resúmenes heurísticos inventados, sino un fallo controlado con registro de incidencia.
  - [x] 3.4 Modificar `YouTubeSearchService.cs` para que cuando `ShouldSimulate` sea `false`, ante error de cuota o fallo de API no devuelva el dataset mock.
  - [x] 3.5 Añadir/actualizar pruebas unitarias en `Ludeka.UnitTests` para verificar los comportamientos de error sin mock.

- [x] **4. Atribución BGG y Botones de Comunidad en Footer Global**
  - [x] 4.1 Añadir `DiscordInviteUrl` y `TelegramChannelUrl` a `CommunityNotificationOptions.cs`.
  - [x] 4.2 Actualizar el footer en `MainLayout.razor` para incluir:
    - Botón oficial con enlace a Discord (si está configurado o enlace por defecto).
    - Botón oficial con enlace a Telegram (si está configurado o enlace por defecto).
    - Insignia y atribución legal *"Powered by BoardGameGeek"* con enlace externo a BGG.
    - Microtexto de transparencia en afiliación.
  - [x] 4.3 Actualizar `appsettings.json` y `.env.example` con las nuevas opciones.

- [x] **5. Verificación Completa y Cierre**
  - [x] 5.1 Ejecutar `dotnet build Ludeka.sln` y verificar cero errores de compilación.
  - [x] 5.2 Ejecutar `dotnet test Ludeka.sln` y verificar que los 855+ tests pasen al 100% (866 superados).
  - [x] 5.3 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
