# Tareas de Implementación: INC-45 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'

- [x] **T1: Modelos de Dominio y Pruebas Unitarias (`Ludeka.Core`)**
  - [x] Crear entidad inmutable `GamePriceSnapshot.cs` con validaciones de invariantes.
  - [x] Crear Value Object `GamePriceMetrics.cs` con fórmula determinista de cálculo de mínimos, medias y porcentajes.
  - [x] Crear Value Object `PriceDropAlert.cs`.
  - [x] Escribir suite de pruebas unitarias `GamePriceMetricsTests.cs` (cálculo de mínimos, descuentos, empates de precio, colecciones vacías).

- [x] **T2: Persistencia e Infraestructura (`Ludeka.Infrastructure` & `Ludeka.Application`)**
  - [x] Definir contrato `IGamePriceRepository.cs` en `Ludeka.Application.Contracts`.
  - [x] Mapear entidad `GamePriceSnapshot` en `LudekaDbContext.cs` (EF Core / PostgreSQL).
  - [x] Añadir migración de tabla `GamePriceSnapshots` e índices en `SqliteSchemaMigrator.cs`.
  - [x] Implementar `SqliteGamePriceRepository.cs` con prevención de duplicados en ventana de tiempo.
  - [x] Escribir pruebas unitarias/de integración `SqliteGamePriceRepositoryTests.cs`.

- [x] **T3: Casos de Uso y Servicio de Radar (`Ludeka.Application`)**
  - [x] Definir `PriceRadarOptions.cs` y DTOs auxiliares.
  - [x] Definir contrato `IPriceRadarService.cs`.
  - [x] Implementar `PriceRadarService.cs` (detección de chollos, alertas de usuarios con `WantToBuy`, muestreo y métricas).
  - [x] Escribir pruebas unitarias exhaustivas `PriceRadarServiceTests.cs`.

- [x] **T4: Background Worker y Registro de Inyección de Dependencias**
  - [x] Implementar `PriceRadarHostedService.cs` en segundo plano con control de errores y ciclo configurable.
  - [x] Registrar `IGamePriceRepository`, `IPriceRadarService` y `PriceRadarHostedService` en `Program.cs`.

- [x] **T5: Componentes e Interfaces Blazor (`Ludeka.Web`)**
  - [x] Actualizar `StoreOffersCard.razor` para exhibir el Mínimo Histórico y badges de récord en ofertas.
  - [x] Actualizar `MyLibrary.razor` (pestaña `Quiero comprar`) mostrando precios en vivo, badges de bajada y resumen de oportunidades.
  - [x] Actualizar `Radar.razor` incorporando pestaña de "Ofertas & Chollos" con filtro territorial y lista de descuentos.
  - [x] Validar compatibilidad y contratos de marcado en `WebMarkupContractTests.cs`.

- [x] **T6: Verificación Global, Documentación y Archivo SDD**
  - [x] Ejecutar `dotnet test` y verificar 100% en verde con más de 1.000 tests.
  - [x] Generar especificación viva del sistema: `docs/specs/sistema/31-radar-precios-minimos-historicos.md`.
  - [x] Actualizar índice maestro `docs/specs/sistema/README.md`.
  - [x] Trasladar documento de incremento a `docs/increments/archive/inc-45-price-radar-discounts.md`.
  - [x] Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` a `✅ Archivado`.
  - [x] Crear Pull Request a `main` mediante `scripts/sdd-worktree.ps1 pr price-radar-discounts`.
