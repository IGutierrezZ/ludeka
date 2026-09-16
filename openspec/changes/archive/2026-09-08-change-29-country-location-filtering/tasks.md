# Checklist de Tareas Técnicas: change-29-country-location-filtering

## Fase 1: Dominio y Entidades (`Ludeka.Core`)
- [ ] **1.1** Crear `src/Ludeka.Core/ValueObjects/CountryInfo.cs` y `CountryCatalog.cs` con el catálogo oficial de países lúdicos y métodos de normalización y obtención de banderas.
- [ ] **1.2** Actualizar `src/Ludeka.Core/Entities/Store.cs` incorporando `Country` y `ShippingCountries` con sus constructores y métodos de actualización.
- [ ] **1.3** Actualizar `src/Ludeka.Core/Entities/Giveaway.cs` incorporando `Country` y propiedad helper `IsInternational`.
- [ ] **1.4** Actualizar `src/Ludeka.Core/Entities/BoardGameEvent.cs` incorporando `Country`.
- [ ] **1.5** Actualizar `src/Ludeka.Core/ValueObjects/GamePurchaseLink.cs` incorporando `Country`, `ShippingCountries` y método `ShipsTo(string targetCountry)`.
- [ ] **1.6** Actualizar `src/Ludeka.Core/Entities/AppUser.cs` y `UserPreference.cs` incorporando `Country` nullable.

## Fase 2: Capa de Aplicación (`Ludeka.Application`)
- [ ] **2.1** Actualizar DTOs en `DirectoryDtos.cs`, `GiveawayDto.cs`, `BoardGameEventDto.cs`, `UserPreferenceDto.cs`.
- [ ] **2.2** Actualizar interfaces de servicio (`IStoreService`, `IGiveawayService`, `IBoardGameEventService`, `IUserPreferenceService`).
- [ ] **2.3** Crear `src/Ludeka.Application/Contracts/IUserLocationService.cs` para soporte de ubicación y ordenación prioritaria.
- [ ] **2.4** Implementar métodos de filtrado y actualización en los servicios de aplicación (`StoreService`, `GiveawayService`, `BoardGameEventService`).

## Fase 3: Infraestructura y Persistencia SQLite (`Ludeka.Infrastructure`)
- [ ] **3.1** Actualizar `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` con las sentencias `ALTER TABLE` e índices para `Stores`, `Giveaways`, `BoardGameEvents`, `AppUsers`, `UserPreferences`.
- [ ] **3.2** Actualizar `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` con los mapeos de EF Core.
- [ ] **3.3** Actualizar `SqliteUserPreferenceService.cs` para persistir y leer el país preferido.
- [ ] **3.4** Crear `src/Ludeka.Infrastructure/Services/UserLocationService.cs` implementando `IUserLocationService`.
- [ ] **3.5** Actualizar `CatalogSeeder.cs` con datos de prueba realistas para tiendas, sorteos y eventos en España, México, Argentina e Internacional.

## Fase 4: Componentes Web Blazor (`Ludeka.Web`)
- [ ] **4.1** Crear `src/Ludeka.Web/wwwroot/js/locationInterop.js` con detección de zona horaria y geolocalización de navegador.
- [ ] **4.2** Actualizar `src/Ludeka.Web/Components/Shared/StoreOffersCard.razor` con filtrado territorial estricto, badges de país y estado vacío de ausencia de tiendas en el territorio.
- [ ] **4.3** Actualizar `src/Ludeka.Web/Components/Shared/GiveawayCard.razor` y `Radar.razor` con badge de país y filtrado automático (país local + internacional).
- [ ] **4.4** Actualizar `src/Ludeka.Web/Components/Pages/Events.razor` y `StoresDirectory.razor` con selectores de país y badges.
- [ ] **4.5** Actualizar el perfil de usuario / preferencias (`MyLibrary.razor` o modal) con selector de país y advertencia obligatoria destacada sobre el filtrado.

## Fase 5: Pruebas Unitarias y Verificación (`Ludeka.UnitTests`)
- [ ] **5.1** Crear pruebas unitarias para `CountryCatalog` y `GamePurchaseLink.ShipsTo`.
- [ ] **5.2** Crear pruebas unitarias para las entidades de dominio con país (`Store`, `Giveaway`, `BoardGameEvent`, `UserPreference`).
- [ ] **5.3** Crear pruebas de infraestructura para migración SQLite y persistencia de país.
- [ ] **5.4** Crear pruebas de servicio para `UserLocationService` y filtrado por país en sorteos/eventos/tiendas.
- [ ] **5.5** Ejecutar suite completa `dotnet test` y verificar que el 100% de los tests estén en verde.
