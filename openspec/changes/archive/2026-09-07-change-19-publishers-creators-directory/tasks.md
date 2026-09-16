# Tareas de Implementación: change-19-publishers-creators-directory

## Fase 1: Dominio (`Ludeka.Core`)
- [ ] 1.1 Crear enumerados `SocialPlatform.cs` y `StoreType.cs` en `Ludeka.Core/Enums`.
- [ ] 1.2 Crear Value Object `SocialNetworkLink.cs` en `Ludeka.Core/ValueObjects`.
- [ ] 1.3 Crear entidades `Publisher.cs`, `Creator.cs` y `Store.cs` en `Ludeka.Core/Entities`.
- [ ] 1.4 Agregar pruebas unitarias de dominio para las entidades y sus métodos de negocio en `Ludeka.UnitTests/Domain`.

## Fase 2: Aplicación y Contratos (`Ludeka.Application`)
- [ ] 2.1 Crear interfaces de repositorio `IPublisherRepository.cs`, `ICreatorRepository.cs` e `IStoreRepository.cs`.
- [ ] 2.2 Crear DTOs (`PublisherDto`, `CreatorDto`, `StoreDto`, creación y actualización) en `Ludeka.Application/DTOs`.
- [ ] 2.3 Crear contratos e implementaciones de servicios: `IPublisherService`, `ICreatorService`, `IStoreService`.
- [ ] 2.4 Definir contrato `IChannelDirectoryProvider` y su implementación para alimentar el foco de YouTube.
- [ ] 2.5 Crear pruebas unitarias para los servicios de aplicación en `Ludeka.UnitTests/Services`.

## Fase 3: Infraestructura y Persistencia (`Ludeka.Infrastructure`)
- [ ] 3.1 Registrar `DbSet<Publisher>`, `DbSet<Creator>` y `DbSet<Store>` en `LudekaDbContext.cs` con configuración y mapeo JSON de `SocialLinks`.
- [ ] 3.2 Implementar repositorios SQLite: `SqlitePublisherRepository.cs`, `SqliteCreatorRepository.cs`, `SqliteStoreRepository.cs`.
- [ ] 3.3 Crear `DirectorySeeder.cs` para sembrar editoriales, autores y tiendas canónicas.
- [ ] 3.4 Conectar `IChannelDirectoryProvider` con `ChannelFocusProvider` para que los canales de la base de datos se agreguen dinámicamente al padrón de YouTube.
- [ ] 3.5 Registrar servicios y repositorios en la inyección de dependencias en `Ludeka.Web/Program.cs`.

## Fase 4: Componentes Web y Vistas Blazor (`Ludeka.Web`)
- [ ] 4.1 Crear componente `SocialLinksList.razor` para renderizar badges de redes sociales de forma limpia y accesible.
- [ ] 4.2 Crear páginas de directorio:
  - `PublishersDirectory.razor` (`/editoriales`)
  - `CreatorsDirectory.razor` (`/creadores` y `/autores`)
  - `StoresDirectory.razor` (`/tiendas`)
- [ ] 4.3 Crear páginas de detalle:
  - `PublisherDetail.razor` (`/editoriales/{slug}`)
  - `CreatorDetail.razor` (`/creadores/{slug}` y `/autores/{slug}`)
  - `StoreDetail.razor` (`/tiendas/{slug}`)
- [ ] 4.4 Crear modales de moderación reactivos:
  - `PublisherEditModal.razor`
  - `CreatorEditModal.razor`
  - `StoreEditModal.razor`
- [ ] 4.5 Conectar navegación cruzada:
  - Enlaces a editorial y autor en `GameDetail.razor`.
  - Enlaces a ficha de tienda en `StoreOffersCard.razor`.
  - Enlaces en cabecera y pie de página en `MainLayout.razor`.

## Fase 5: Verificación y Cierre
- [ ] 5.1 Ejecutar suite completa de pruebas (`dotnet test`) y verificar cero regresiones.
- [ ] 5.2 Generar reporte de verificación `verify-report.md`.
- [ ] 5.3 Archivar el incremento en `.openspec/changes/archive/` y actualizar `docs/increments/`.
- [ ] 5.4 Registrar observaciones y resumen de sesión en Engram (`mem_save` y `mem_session_summary`).
