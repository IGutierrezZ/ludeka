# 📦 Incremento 94: Retención y Purga de Entidades Caducadas (Sorteos, Eventos y Novedades Editoriales) y Liberación de Almacenamiento

> **Estado:** ⏳ Verificado / Abriendo PR  
> **Rama:** `inc/retencion-caducados-novedades`  
> **Worktree:** `F:\repos\ludeka-wt\retencion-caducados-novedades`  
> **Pruebas Unitarias:** 2.238 pasando al 100% (2.238 superadas, 0 errores, 0 omitidos).

---

## 1. Contexto y Motivación

En Ludeka, las secciones de comunidad y actualidad editorial acumulan entidades efímeras dependientes del tiempo:
1. **Sorteos (`Giveaway`):** Cuentan con una fecha límite de participación (`DeadlineAt`). Una vez vencidos, carecen de vigencia para el usuario.
2. **Grandes Eventos (`BoardGameEvent`):** Tienen fechas de inicio y fin (`StartDate` y `EndDate`). Una vez concluido el último día del evento (`EndDate < today`), el evento ya no debe mostrarse en la agenda activa.
3. **Novedades Editoriales (`WeeklyRelease`):** Anuncian lanzamientos comerciales. Existían casos donde los anuncios no cuentan con fecha exacta (ej. "novedades del segundo trimestre" o "próximamente"), por lo que la fecha de lanzamiento no debía ser obligatoria. Además, las novedades deben ordenarse por orden de llegada a la plataforma (`CreatedAt` descendente) y permanecer un máximo de 60 días salvo que su lanzamiento aún esté pendiente en el futuro.
4. **Liberación de Almacenamiento:** Con el paso del tiempo, las imágenes asociadas a sorteos caducados, carteles de eventos antiguos y carátulas de novedades consumen espacio de almacenamiento en disco local y en el bucket de Cloudflare R2.

---

## 2. Decisiones Arquitectónicas y Reglas de Negocio

1. **Fecha de Lanzamiento Opcional en Novedades:**
   - `WeeklyRelease.ReleaseDate` pasa de `DateOnly` a `DateOnly?`.
   - Si `ReleaseDate == null`, la interfaz Blazor muestra la insignia "Próximamente" en `/novedades`, ficha de detalle, moderación de Instagram y carril de portada.
   - Migración EF Core generada (`MakeWeeklyReleaseDateNullable`) para SQLite y PostgreSQL.
2. **Ordenación Canónica por Entrada:**
   - Las novedades se ordenan por `CreatedAt` descendente tanto en `/novedades` (`News.razor` y `SqliteWeeklyReleaseRepository`) como en el dashboard de inicio (`HomeDashboardService`), garantizando que lo que entra va primero con independencia de su fecha prevista.
3. **Eventos Multidía Vigentes:**
   - Retirado el relleno artificial con eventos pasados en `SqliteBoardGameEventRepository.GetUpcomingEventsAsync`. Los eventos solo son visibles mientras `EndDate >= today`.
4. **Política de Retención y Purga (`DataRetentionService`):**
   - **Sorteos:** Se eliminan físicamente tras 7 días de gracia después de su vencimiento (`DeadlineAt <= nowUtc.AddDays(-7)`).
   - **Eventos:** Se eliminan físicamente tras 7 días de gracia después de su último día (`EndDate <= today.AddDays(-7)`).
   - **Novedades:** Se eliminan físicamente cuando llevan 60 días o más registradas en Ludeka (`CreatedAt <= nowUtc.AddDays(-60)`) **y** (`ReleaseDate == null || ReleaseDate.Value < today`). Si la fecha es futura, se mantienen con independencia de su antigüedad.
   - Parámetros configurables mediante `DataRetentionOptions` (`GiveawayGracePeriodDays: 7`, `EventGracePeriodDays: 7`, `ReleaseRetentionDays: 60`, `ExecutionHourUtc: 3`).
5. **Liberación de Medios (`ManagedImageKeyExtractor`):**
   - Extrae de forma segura las claves relativas de objetos gestionados (`events/...`, `community/...`, `games/...`) a partir de rutas locales `/images/...` o URLs del CDN propio / R2.
   - Ignora hosts externos ajenos (BGG/GeekDo, Instagram, Facebook, Unsplash, Imgur, etc.).
   - Invoca `IImageStorageService.DeleteImageAsync(key)` antes de eliminar el registro en base de datos.
   - Si el borrado de una imagen falla o la imagen ya no existía en disco, no interrumpe la purga de la entidad de la base de datos y se registra como advertencia en logs.
6. **Ejecución Desatendida con Idempotencia de Ventana:**
   - Runner `data-retention` en `Ludeka.Jobs` (`DataRetentionJobRunner`) con concesión de ventana diaria (`JobWindowKeyCalculator.DailyUtc(nowUtc)`).
   - Servicio hosted complementario `DataRetentionHostedService` en `Ludeka.Infrastructure.Background`.

---

## 3. Componentes Modificados y Creados

### Dominio y Capa de Aplicación
- `src/Ludeka.Core/Entities/WeeklyRelease.cs`: `ReleaseDate` nullable.
- `src/Ludeka.Application/DTOs/CommunityDtos.cs`: DTOs de novedades con `DateOnly? ReleaseDate` y exposición de `CreatedAt`.
- `src/Ludeka.Application/Options/DataRetentionOptions.cs`: Opciones de retención y purga.
- `src/Ludeka.Application/DTOs/DataRetentionResult.cs`: Resultado con conteo de entidades purgadas e imágenes liberadas.
- `src/Ludeka.Application/Contracts/IDataRetentionService.cs`: Contrato de purga de datos.
- `src/Ludeka.Application/Features/Maintenance/ManagedImageKeyExtractor.cs`: Extracción de claves de imágenes internas.
- `src/Ludeka.Application/Features/Maintenance/DataRetentionService.cs`: Lógica de retención y purga con `TimeProvider`.
- `src/Ludeka.Application/Features/Home/HomeDashboardService.cs`: Ordenación de novedades por `CreatedAt` descendente.

### Infraestructura y Persistencia
- `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs`: Configuración de columna `ReleaseDate` opcional.
- `src/Ludeka.Infrastructure/Migrations/20261001151945_MakeWeeklyReleaseDateNullable.cs`: Migración EF Core.
- `src/Ludeka.Infrastructure/Data/SqliteWeeklyReleaseRepository.cs`: Ordenación en memoria por `CreatedAt` desc y soporte de `ReleaseDate` null.
- `src/Ludeka.Infrastructure/Data/SqliteBoardGameEventRepository.cs`: Eliminación del bloque de relleno con eventos pasados.
- `src/Ludeka.Infrastructure/Background/DataRetentionHostedService.cs`: Servicio en segundo plano con temporización nocturna.
- `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs`: Registro de `DataRetentionOptions` y `IDataRetentionService`.

### Trabajos de Fondo (CLI y Cloud Run Jobs)
- `src/Ludeka.Jobs/JobNames.cs`: Constante `DataRetention = "data-retention"`.
- `src/Ludeka.Jobs/Runners/DataRetentionJobRunner.cs`: Implementación de `IJobRunner` con ventana diaria.
- `src/Ludeka.Jobs/JobRunnerServiceCollectionExtensions.cs`: Registro de `DataRetentionJobRunner`.

### Interfaz Blazor
- `src/Ludeka.Web/Components/Pages/News.razor`: Soporte de fecha opcional, insignia "Próximamente", ordenación por entrada.
- `src/Ludeka.Web/Components/Home/HomeReleaseCard.razor`: Insignia "Próximamente" para lanzamientos sin fecha.
- `src/Ludeka.Web/Components/Pages/ReleaseDetail.razor`: Soporte en formulario y vista de detalle.
- `src/Ludeka.Web/Components/Pages/InstagramModeration.razor`: Soporte de `DateOnly?` con fallback.
- `src/Ludeka.Application/Features/Instagram/InstagramComposerService.cs`: Fallback a "Próximamente".

### Pruebas Unitarias
- `tests/Ludeka.UnitTests/Domain/WeeklyReleaseDomainTests.cs`: Pruebas de constructor y actualización con fecha null.
- `tests/Ludeka.UnitTests/Application/HomeDashboardServiceTests.cs`: Verificación de ordenación por `CreatedAt` desc.
- `tests/Ludeka.UnitTests/Infrastructure/SqliteCommunityRepositoriesTests.cs`: Verificación de filtrado y ordenación.
- `tests/Ludeka.UnitTests/Application/DataRetentionServiceTests.cs`: Pruebas completas de purga de sorteos, eventos, novedades y extractor de imágenes.
- `tests/Ludeka.UnitTests/Jobs/DataRetentionJobRunnerTests.cs`: Verificación del runner e idempotencia.
- `tests/Ludeka.UnitTests/Jobs/LudekaJobsCompositionTests.cs`: Actualizado a 10 runners registrados.

---

## 4. Verificación y Resultados

- **Compilación:** `dotnet build` -> Correcta, 0 errores.
- **Suite de Pruebas Unitarias:** `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj`:
  - **Superadas:** 2.238
  - **Con error:** 0
  - **Omitidas:** 0
  - **Tasa de éxito:** 100%
