# Lista de Tareas: INC-89 — Saneamiento Integral de Consultas SQL Restantes, Eliminación de Egress O(N) y Blindaje de Caché de Fichas Públicas

> **ID del Cambio:** `change-89-opt-consultas-egress-fase2`  
> **Incremento Asociado:** INC-89  
> **Estado:** ✅ Completado e implementado  

---

## Tareas de Implementación

### Fase 1: Optimización de Consultas SQL en `SqliteGameRepository`
- [x] 1.1 Refactorizar `SqliteGameRepository.GetByPublisherAsync` para filtrar con `EF.Functions.Like` sobre `Publisher` y `SpanishPublisher` directamente en SQL, eliminando el `ToListAsync()` incondicional sobre toda la tabla `Games`.
- [x] 1.2 Refactorizar `SqliteGameRepository.GetGamesPendingQualityBackfillAsync` y `GetGamesPendingQualityBackfillCountAsync` para aplicar proyecciones acotadas sin descargar la tabla completa.
- [x] 1.3 Optimizar `SqliteFoundingVerdictRepository.GetAllAsync` para que en PostgreSQL ordene directamente en SQL con `OrderByDescending(v => v.CreatedAt)`.

### Fase 2: Blindaje de Caché L1 en Fichas de Editoriales (`CachedPublisherService`)
- [x] 2.1 Extender `CachedPublisherService.cs` para almacenar en caché `GetBySlugAsync(slug)` y `GetByIdAsync(id)` con un TTL de 30 minutos.
- [x] 2.2 Blindar la invalidación reactiva de `CachedPublisherService` para que al invocar `CreateAsync`, `UpdateAsync` o `DeleteAsync` se purguen las claves de caché individuales de las editoriales afectadas.

### Fase 3: Pruebas Automatizadas y Verificación (TDD)
- [x] 3.1 Añadir pruebas unitarias en `SqliteGameRepositoryOptimizationTests.cs` comprobando que `GetByPublisherAsync` filtra correctamente en base de datos.
- [x] 3.2 Añadir pruebas unitarias en `CachedDirectoryServicesTests.cs` verificando la caché de `GetBySlugAsync` e `GetByIdAsync` y su invalidación.
- [x] 3.3 Ejecutar la suite completa de pruebas unitarias (`dotnet test tests/Ludeka.UnitTests`) y verificar 100% verde (2.155 pruebas superadas).
