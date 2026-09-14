# Tareas: INC-38 — Persistencia PostgreSQL en Supabase, Estrategia Dual y Usuario Admin Inicial

- [x] **1. Dependencias NuGet y Opciones de Configuración**
  - [x] 1.1 Añadir `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3) a `src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj`.
  - [x] 1.2 Crear `DatabaseOptions.cs` y `AdminUserOptions.cs` en `src/Ludeka.Infrastructure/Options/`.
  - [x] 1.3 Actualizar `appsettings.json` y `.env.example` con las secciones `Database` y `AdminUser`.

- [x] **2. Conmutación de Proveedor en Inyección de Dependencias y Startup**
  - [x] 2.1 Modificar `Program.cs` para registrar `LudekaDbContext` con detección dual (`UseNpgsql` vs `UseSqlite`) y `EnableRetryOnFailure`.
  - [x] 2.2 Condicionar la ejecución de `SqliteSchemaMigrator` únicamente cuando el proveedor sea SQLite (`db.Database.IsSqlite()`).
  - [x] 2.3 Implementar `AdminUserSeeder.cs` en `src/Ludeka.Infrastructure/Seeding/` para garantizar la existencia de un administrador `FoundingTeam`.
  - [x] 2.4 Condicionar la ejecución de los seeders demostrativos (`CatalogSeeder`, `DirectorySeeder`, etc.) al flag `Database:SeedDemoData`.

- [x] **3. Script SQL Canónico para Supabase**
  - [x] 3.1 Crear `docs/database/supabase_schema.sql` con la definición completa e idempotente de tablas, tipos `jsonb`, índices y claves foráneas para PostgreSQL en Supabase.

- [x] **4. Script Automatizado de Copias de Seguridad**
  - [x] 4.1 Crear `scripts/supabase-backup.ps1` con soporte de exportación comprimida (`.sql.gz`) y rotación de copias antiguas.

- [x] **5. Pruebas Automatizadas y Verificación**
  - [x] 5.1 Crear pruebas unitarias para `DatabaseOptions`, `AdminUserOptions` y la lógica de detección de proveedor en `tests/Ludeka.UnitTests/Infrastructure/DatabaseProviderTests.cs`.
  - [x] 5.2 Crear pruebas unitarias para `AdminUserSeeder` verificando que crea el admin cuando no existe y no duplica si ya existe.
  - [x] 5.3 Ejecutar `dotnet build Ludeka.sln` y verificar cero errores de compilación.
  - [x] 5.4 Ejecutar `dotnet test Ludeka.sln` y asegurar que el 100% de los tests (887) pasen en verde.

- [x] **6. Cierre, Documentación Viva y Pull Request**
  - [x] 6.1 Actualizar la Especificación Viva del Sistema (`docs/specs/sistema/`).
  - [x] 6.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 6.3 Realizar commit convencional y abrir Pull Request con `scripts/sdd-worktree.ps1 pr postgresql-supabase`.
