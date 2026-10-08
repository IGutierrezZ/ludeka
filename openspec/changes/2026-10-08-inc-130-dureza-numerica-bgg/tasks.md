# Lista de Tareas: INC-130 — Dureza Numérica BGG, Extrapolación de Complejidad y Ordenación Precisa

> **ID del Cambio:** `2026-10-08-inc-130-dureza-numerica-bgg`  
> **Incremento Asociado:** INC-130  
> **Estado:** ⏳ Planificado y en progreso  

---

## Tareas de Implementación por Fases

### Fase 1: Dominio y Helper Canónico (`Ludeka.Core`)
- [ ] 1.1 Añadir la propiedad `public double? BggWeight { get; private set; }` en [`Game.cs`](file:///f:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs), junto con método `UpdateBggWeight(double? weight)` y parámetros opcionales en constructores preservando compatibilidad.
- [ ] 1.2 Crear la clase canónica [`ComplexityCalculator`](file:///f:/repos/Ludeka/src/Ludeka.Core/Helpers/ComplexityCalculator.cs) con umbrales definidos (< 2.20 Ligero, 2.20 - 3.25 Medio, ≥ 3.25 Duro) y fallback a la heurística previa si `BggWeight` es nulo o 0.
- [ ] 1.3 Crear pruebas unitarias para `ComplexityCalculator` y validación de `BggWeight` en `GameTests.cs`.

### Fase 2: Ingesta, Persistencia y Migraciones (`Ludeka.Infrastructure`)
- [ ] 2.1 Actualizar [`BggXmlParser.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/BggXmlParser.cs) para extraer `<averageweight value="..." />` dentro de `<statistics><ratings>` y pasarlo a la instanciación de `Game`.
- [ ] 2.2 Configurar el mapeo de `BggWeight` en `LudekaDbContext.cs` y generar las migraciones EF Core para SQLite y PostgreSQL (`AddGameBggWeight`).
- [ ] 2.3 Implementar método de backfill `BackfillBggWeightsFromSnapshotsAsync` en `SqliteGameRepository` / `ICatalogQualityService` para poblar `BggWeight` a partir del JSON crudo de `BggRawSnapshots`.
- [ ] 2.4 Actualizar `seed-games.json` incorporando `BggWeight` en los títulos de semilla.

### Fase 3: Consultas, Filtrado y Ordenación en Repositorio
- [ ] 3.1 Actualizar `SqliteGameRepository.cs`:
  - En `ApplyQuerySorting`: ordenar `ComplexityAsc` y `ComplexityDesc` directamente por `g.BggWeight` en SQL (situando nulos al final).
  - En `GameFilterIndexItem`: incluir `double? BggWeight`.
  - En `ApplyIndexSorting`: ordenar por `BggWeight` numérico.
  - En filtrado por `criteria.Complexities`: evaluar mediante `ComplexityCalculator.Calculate`.
- [ ] 3.2 Actualizar pruebas unitarias de `SqliteGameRepositoryTests.cs` verificando la ordenación continua por peso y el filtrado.

### Fase 4: Presentación Editorial en Web (`Ludeka.Web`)
- [ ] 4.1 En [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor), actualizar el bloque de ADN lúdico y el eyebrow para mostrar el peso decimal exacto (ej: `2.5 / 5 · Medio`) usando `ComplexityCalculator`.
- [ ] 4.2 En [`Home.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor) y tarjetas, asegurar que el filtrado y ordenación por dureza se coordinan limpiamente con el backend.
- [ ] 4.3 Actualizar pruebas de contrato de interfaz en `tests/Ludeka.UnitTests/Web/`.

### Fase 5: Verificación Integral y Suite de Pruebas
- [ ] 5.1 Ejecutar suite completa `dotnet test` y comprobar 0 errores y 100% verde.
- [ ] 5.2 Abrir PR con `scripts/sdd-worktree.ps1 pr dureza-numerica-bgg` y monitorizar CI.
