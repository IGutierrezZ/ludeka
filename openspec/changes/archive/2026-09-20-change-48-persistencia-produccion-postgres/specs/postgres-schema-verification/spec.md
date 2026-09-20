# Especificación: `postgres-schema-verification` (Verificación de Esquema contra PostgreSQL Real)

> **Cambio:** `change-48-persistencia-produccion-postgres` (INC-48) · **Fase:** `sdd-spec` · **Fecha:** 2026-09-19
> **Worktree:** `C:\repos\ludeka-wt\persistencia-produccion-postgres` (rama `inc/persistencia-produccion-postgres`, base `62f5b78`)
> **Entradas:** [`../../proposal.md`](../../proposal.md) (alcance aprobado, Engram `sdd/change-48-persistencia-produccion-postgres/aprobacion-alcance`) · [`../../explore.md`](../../explore.md) §5
> Los marcadores `## Requirements`, `### Requirement:`, `#### Scenario:`, `GIVEN`/`WHEN`/`THEN`/`AND` se mantienen literales en inglés; el resto del contenido está en español.

## Propósito

Verifica automáticamente, contra un PostgreSQL 17 real (Testcontainers), que las 7 migraciones de Entity Framework Core existentes producen el esquema completo declarado por el modelo (34 `DbSet` = 34 tablas); y mantiene `docs/database/supabase_schema.sql` como derivado fiel de esas migraciones, nunca como fuente de verdad editable a mano. No existe una Supabase real de *staging*: ningún escenario de este documento depende de ella.

## Requirements

### Requirement: La migración desde cero crea el esquema completo en PostgreSQL real

Al aplicar, desde una base de datos vacía, la secuencia completa de migraciones de Entity Framework Core contra una instancia PostgreSQL 17 real, el sistema DEBE crear exactamente las tablas declaradas por el modelo (`LudekaDbContext`; 34 en el momento de este cambio) y DEBE registrar en `__EFMigrationsHistory` exactamente una fila por cada fichero de migración existente en el proyecto (7 en el momento de este cambio). Esta prueba DEBE ejecutarse en una colección xUnit propia (`CollectionDefinition`), distinta de las 4 ya existentes en `PostgresFixture.cs`, para no compartir instancia de base de datos con pruebas que asumen un recuento de filas o un estado preexistente.

*Verificación: prueba de integración con Testcontainers (`postgres:17-alpine`). No requiere ninguna Supabase real.*

#### Scenario: Las tablas del modelo se crean desde cero

- GIVEN una instancia PostgreSQL 17 real y vacía, provista por Testcontainers
- WHEN se aplican todas las migraciones de Entity Framework Core desde cero
- THEN el esquema resultante contiene exactamente las tablas declaradas por el modelo, ni una menos ni una de más

#### Scenario: El historial de migraciones registra una fila por migración

- GIVEN la misma instancia PostgreSQL recién migrada desde cero
- WHEN se consulta la tabla `__EFMigrationsHistory`
- THEN el número de filas coincide exactamente con el número de ficheros de migración del proyecto

#### Scenario: La colección xUnit propia evita colisión con otras pruebas de PostgreSQL

- GIVEN las 4 colecciones xUnit ya existentes sobre PostgreSQL real
- WHEN se ejecuta la prueba de migración desde cero
- THEN se ejecuta en su propia colección, sin compartir instancia de base de datos con pruebas que asertan un recuento de filas o un estado preexistente

### Requirement: El esquema SQL documental es un derivado fiel de las migraciones

`docs/database/supabase_schema.sql` DEBE regenerarse a partir de las migraciones de Entity Framework Core —no escribirse ni editarse a mano— y DEBE conservar el banner que advierte que no es la fuente de verdad ejecutable. El script DEBE generarse forzando el proveedor PostgreSQL (`Database__Provider=PostgreSql`, restricción heredada de INC-46), para no producir sintaxis del proveedor equivocado.

*Verificación: regeneración con `dotnet ef migrations script` y comparación textual del banner. Es una operación offline sobre las migraciones compiladas; no requiere PostgreSQL real.*

#### Scenario: El script regenerado conserva el banner de advertencia

- GIVEN las migraciones actuales del proyecto
- WHEN se regenera `docs/database/supabase_schema.sql` con `Database__Provider=PostgreSql`
- THEN el fichero resultante conserva, en sus primeras líneas, el banner que advierte que no es la fuente de verdad

#### Scenario: El script regenerado no diverge del número real de tablas

- GIVEN el script SQL recién regenerado
- WHEN se cuentan sus sentencias `CREATE TABLE`
- THEN el recuento coincide con el número de tablas declaradas por el modelo
