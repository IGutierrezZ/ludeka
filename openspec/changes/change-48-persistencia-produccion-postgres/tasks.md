# Tareas: INC-48 — Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental

> Fase `sdd-tasks` · Worktree `C:\repos\ludeka-wt\persistencia-produccion-postgres` (rama `inc/persistencia-produccion-postgres`, base `62f5b78`)
> Estimaciones medidas abriendo cada fichero real de este worktree (no copiadas de `design.md` §9). Lección de INC-47 aplicada: sus 11 rebanadas se quedaron cortas **sin excepción** (peor caso 350→1132); aquí se parte la rebanada 1 del diseño en dos PR desde el principio, en vez de descubrirlo a mitad de implementación.

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas estimadas | 860–1280 totales repartidas en 6 PR (ninguna individual debería superar 400 si se respeta el corte 1a/1b) |
| Riesgo de presupuesto de 400 líneas | **High** — PR3 mide ya 220–280 con incertidumbre real sobre `HealthChecksTests.cs`; PR1b depende de una contingencia de arnés HTTP no resuelta. INC-47 dobló casi todas sus estimaciones: se declara High en vez de Medium a propósito |
| PR encadenados recomendados | Sí |
| Orden sugerido | PR1a → PR1b → PR2 → PR3 → PR4 → PR5 |
| Estrategia de entrega | auto-chain |
| Estrategia de cadena | stacked-to-main |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

**Restricciones de orden**: PR1a → PR3 (obligatorio, `StorageHealthCheck` usa `MediaOptions`). PR2 y PR4 son independientes entre sí y del resto. PR5 va última: el criterio «1537+9 pruebas» de la propuesta nace caducado y se mide en 6.2, tras fusionar 1a-1b-2-3-4. El hueco de evidencia de sondas `deploy-cloudrun@v2` solo bloquea PR5 (tarea 6.1), no las demás.

### Suggested Work Units

| Unit | Objetivo | PR | Prueba enfocada | Arnés en tiempo de ejecución | Límite de reversión |
|---|---|---|---|---|---|
| 1a | Factoría de 3 vías + fix `:28` + aviso degradación | PR1a | `dotnet test tests/Ludeka.UnitTests --filter FullyQualifiedName~MediaStorageSelectionTests` | N/A — resolución de DI pura, sin servidor HTTP | Revertir retira `MediaOptions`, la factoría de 3 vías (vuelve a R2/memoria) y el fix de `:28` |
| 1b | Entrega HTTP del fallback (`UseLudekaMediaFiles`) | PR1b | `dotnet test tests/Ludeka.UnitTests --filter FullyQualifiedName~MediaStaticFilesDeliveryTests` | Host ASP.NET Core mínimo en proceso sirviendo `UseLudekaMediaFiles`; primer uso de un servidor HTTP real en `Ludeka.UnitTests` — ver contingencia 2.1 | Revertir retira la extensión y la línea de `Program.cs`; PR1a sigue intacta |
| 2 | Guarda fail-fast + Dockerfile + composes | PR2 | `dotnet test tests/Ludeka.UnitTests --filter FullyQualifiedName~WebStartupGuardsTests` | N/A — guarda pura, sin Kestrel; `docker compose up` es verificación manual fuera de `dotnet test` | Revertir restaura el `ENV` del Dockerfile y retira la guarda; los compose vuelven a su rótulo previo |
| 3 | Health checks fieles (depende de 1a) | PR3 | `dotnet test tests/Ludeka.UnitTests --filter FullyQualifiedName~HealthChecksTests` | N/A — instanciación directa de `IHealthCheck`, igual que hoy | Revertir restaura `SqliteDatabaseHealthCheck`/`sqlite_db` y el parseo antiguo |
| 4 | Verificación de esquema PostgreSQL | PR4 | `dotnet test tests/Ludeka.IntegrationTests --filter FullyQualifiedName~PostgresSchemaVerificationTests` | Testcontainers `postgres:17-alpine` real (requiere Docker en el runner) | Revertir retira colección y pruebas; el script se regenera con un comando, sin datos reales que migrar |
| 5 | Verdad documental y despliegue (última) | PR5 | N/A salvo 6.2 (`dotnet test Ludeka.sln` completo) | N/A — sin cambio de comportamiento; `ci-cd.yml` solo se ejercita en push/PR real | Revertir retira cifras/nombres y claves de Cloudflare de `ci-cd.yml`; no toca ningún eje de código |

**Contingencia de corte si una unidad se acerca a 400 al medir de nuevo**: PR3 → partir en 3a (renombrado `DatabaseHealthCheck` + `Program.cs`, bajo riesgo) y 3b (`StorageHealthCheck` + sus 3 escenarios). PR1b → si el arnés HTTP crece, partir en 1b-i (extensión + caso feliz 200, prueba que el arnés arranca) y 1b-ii (404 + recorrido de directorio + ruta relativa, reusando el arnés ya probado).

## PR1a — Selección de almacén de medios (factoría de 3 vías)

- [x] 1.1 Crear `src/Ludeka.Application/Options/MediaOptions.cs`: `LocalStoragePath`, `HasLocalStoragePath`, `SectionName="Media"` (patrón `src/Ludeka.Application/Options/CloudflareR2Options.cs` (read-only), 50 líneas).
- [x] 1.2 RED en `tests/Ludeka.UnitTests/Infrastructure/DependencyInjection/MediaStorageSelectionTests.cs`: R2 válido gana sobre disco/memoria; disco configurado gana sobre memoria; sin ninguno → memoria (`ServiceCollection` + `ValidateOnBuild`, técnica de `tests/Ludeka.UnitTests/Web/WebHostHostedServiceCompositionTests.cs` (read-only) escenario 1).
- [x] 1.3 RED: prueba que confirma el defecto de `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs:28` (portada escrita en `{ruta}` pero pedida en `/images/games/...`).
- [x] 1.4 RED: aviso — `Production` sin R2 registra advertencia de almacén degradado; `Production` con R2 no la registra (spec `media-storage-precedence`, requisito 3).
- [x] 1.5 GREEN: `Configure<MediaOptions>` + factoría de 3 vías en `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs:273-286`, sustituyendo `AddScoped<PhysicalFileImageStorageService>()` por fábrica con ruta resuelta contra `ContentRootPath`.
- [x] 1.6 GREEN: corregir `PhysicalFileImageStorageService.cs:28` → `_gamesDirectory = Path.Combine(customPath, "games")`.
- [x] 1.7 GREEN: emitir el aviso de degradación en `src/Ludeka.Web/Program.cs` (mismo patrón que el bucle de `Program.cs:111-114`).
- [x] 1.8 Ejecutar `dotnet test Ludeka.sln` completo (nunca con tubería) y registrar el resultado en la descripción del PR.

## PR1b — Entrega HTTP del fallback en disco (depende de PR1a fusionada)

- [ ] 2.1 ⚠️ Contingencia primero: probar host mínimo con `WebApplication.CreateBuilder` dentro de `Ludeka.UnitTests`. Confirmado por lectura directa: `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` no tiene `FrameworkReference` a `Microsoft.AspNetCore.App` ni paquete `Microsoft.AspNetCore.TestHost`; los `using Microsoft.AspNetCore.*` existentes en `tests/Ludeka.UnitTests/Web/` son namespaces ligeros (`Http`, `Authentication`, `Components`), no arranque de host real. Si no compila/arranca, añadir el paquete o `FrameworkReference` al csproj.
- [ ] 2.2 RED en `tests/Ludeka.UnitTests/Web/MediaStaticFilesDeliveryTests.cs`: imagen escrita por `PhysicalFileImageStorageService` se descarga con 200 vía host mínimo que compone solo `UseLudekaMediaFiles`.
- [ ] 2.3 RED: URL de imagen nunca guardada devuelve 404.
- [ ] 2.4 RED (matriz de amenazas, fila «Exposición de rutas»): petición con recorrido de directorio (`..%2f`/`%2e%2e/`) no sirve fichero fuera de la raíz de medios; ruta local relativa resuelve dentro de `ContentRootPath`.
- [ ] 2.5 GREEN: crear `src/Ludeka.Web/Extensions/MediaStaticFilesExtensions.cs` → `UseLudekaMediaFiles` con `PhysicalFileProvider` bajo `/images`, sin activar `ServeUnknownFileTypes`.
- [ ] 2.6 GREEN: insertar `app.UseLudekaMediaFiles(...)` en `src/Ludeka.Web/Program.cs` entre `:180` (`UseHttpsRedirection`) y `:183` (`UseAuthentication`).
- [ ] 2.7 Ejecutar `dotnet test Ludeka.sln` completo y registrar el resultado.

## PR2 — Fail-fast de PostgreSQL en Production

- [ ] 3.1 RED en `tests/Ludeka.UnitTests/Web/WebStartupGuardsTests.cs` (patrón `tests/Ludeka.UnitTests/Jobs/StartupGuardsTests.cs` (read-only), sin `ServiceProvider`/SQLite porque la firma recibe `DatabaseOptions` directo): Production+PostgreSQL resoluble pasa; Production+SQLite/ausente falla nombrando la conexión; entorno no-Production no activa; entorno `null` no activa; entorno `"production"` en minúsculas SÍ activa (matriz de amenazas, fila «Arranque del proceso»).
- [ ] 3.2 GREEN: crear `src/Ludeka.Web/WebStartupGuards.cs` con `Evaluate(IConfiguration, DatabaseOptions, string?)`, espejando la Guarda 1 de `src/Ludeka.Jobs/StartupGuards.cs:44-54` (read-only).
- [ ] 3.3 GREEN: invocar la guarda en `src/Ludeka.Web/Program.cs` tras `:108` y antes de `:117`; lanzar `InvalidOperationException` si devuelve mensaje.
- [ ] 3.4 Añadir `Database:RequirePostgreSqlInProduction=true` a `src/Ludeka.Web/appsettings.json` (sección `Database`, junto a `:13-16`).
- [ ] 3.5 Retirar `ConnectionStrings__DefaultConnection` del bloque `ENV` de `Dockerfile:61-64` (ajustar el `\` de continuación de la línea anterior).
- [ ] 3.6 RED+GREEN: `docker-compose.yml:11` → valor por defecto `Staging` (no `Development`) + comentario de entorno local; añadir a `WebStartupGuardsTests.cs` una prueba que replica esa configuración efectiva contra `WebStartupGuards.Evaluate` y confirma que no activa la guarda.
- [ ] 3.7 Añadir comentario de entorno local a `docker-compose.staging.yml` (sin cambio funcional; ya usa `Staging`).
- [ ] 3.8 Ejecutar `dotnet test Ludeka.sln` completo y registrar el resultado.

## PR3 — Health checks fieles (depende de PR1a fusionada)

- [ ] 4.1 RED: adaptar en `tests/Ludeka.UnitTests/Health/HealthChecksTests.cs:148,161,174,186` los 2 tests de `SqliteDatabaseHealthCheck` para esperar el proveedor real vía `_dbContext.Database.ProviderName`, no el literal `"Microsoft.EntityFrameworkCore.Sqlite"`.
- [ ] 4.2 RED: sustituir el test único `StorageHealthCheck_ConDirectorioAccesible...` (`HealthChecksTests.cs:197-227`) por 3 escenarios — R2 con credenciales válidas → `Healthy`/`mode=r2`; disco vía `MediaOptions.LocalStoragePath` → `Healthy` tras escritura real; ninguno → `Degraded`/`mode=memory` (spec `health-checks`, tabla D4 del diseño).
- [ ] 4.3 GREEN: renombrar `src/Ludeka.Web/Health/SqliteDatabaseHealthCheck.cs` → `DatabaseHealthCheck.cs`; leer `_dbContext.Database.ProviderName` en vez del literal.
- [ ] 4.4 GREEN: reescribir `src/Ludeka.Web/Health/StorageHealthCheck.cs` para sondear la vía de medios seleccionada (misma precedencia que PR1a) en vez de parsear `Data Source=`.
- [ ] 4.5 GREEN: `src/Ludeka.Web/Program.cs:104` → `.AddCheck<DatabaseHealthCheck>("database", ...)` (antes `sqlite_db`).
- [ ] 4.6 Actualizar `.AddCheck<SqliteDatabaseHealthCheck>("sqlite_db",...)` en `HealthChecksTests.cs:262` al nuevo tipo/nombre.
- [ ] 4.7 Ejecutar `dotnet test Ludeka.sln` completo y registrar el resultado.

## PR4 — Verificación de esquema PostgreSQL (independiente; puede ir en paralelo a PR2)

- [ ] 5.1 Añadir `[CollectionDefinition("postgres-real-schema")]` a `tests/Ludeka.IntegrationTests/PostgresFixture.cs` (patrón de las 4 existentes, `:80-129`).
- [ ] 5.2 RED: crear `tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs` — desde vacío, migrar contra `postgres:17-alpine` y assert 34 tablas, 7 filas en `__EFMigrationsHistory`, `GetPendingMigrationsAsync()` vacío.
- [ ] 5.3 RED: crear `tests/Ludeka.UnitTests/Infrastructure/SupabaseSchemaFreshnessTests.cs` — cada tabla del modelo tiene un `CREATE TABLE` en `docs/database/supabase_schema.sql` (técnica de `WebHostHostedServiceCompositionTests.cs:80-101` (read-only), offline, sin PostgreSQL real). Verificado por lectura directa: el fichero actual tiene 27 `CREATE TABLE` y banner en `:1-14`.
- [ ] 5.4 GREEN: ejecutar `dotnet ef migrations script --idempotent` con `Database__Provider=PostgreSql` (proyecto `src/Ludeka.Infrastructure`, arranque `src/Ludeka.Web`) y regenerar `docs/database/supabase_schema.sql`, actualizando el banner de advertencia.
- [ ] 5.5 Ejecutar `dotnet test Ludeka.sln` completo; anotar en la descripción del PR que el script regenerado infla el diff (generado, excluido del presupuesto de 400 de riesgo, no de la identidad del PR).

## PR5 — Verdad documental y despliegue (ÚLTIMA; depende de PR1a+1b+2+3+4 fusionadas)

- [ ] 6.1 Verificar (solo documentación, sin GCP) si `google-github-actions/deploy-cloudrun@v2` admite sondas de liveness/readiness nativas; si no, redactar la provisión manual documentada. Bloquea solo este PR.
- [ ] 6.2 Ejecutar `dotnet test Ludeka.sln` completo tras fusionar PR1a-1b-2-3-4 y anotar el recuento real de pruebas unitarias/integración; el criterio «1537+9» de la propuesta nace caducado y NO se copia sin medir.
- [ ] 6.3 Añadir `Cloudflare__AccountId/AccessKeyId/SecretAccessKey` a `secrets:` (`.github/workflows/ci-cd.yml:111-118`) y `Cloudflare__BucketName/PublicCdnBaseUrl/Simulate=false` a `env_vars:` (`:103-110`) del despliegue web.
- [ ] 6.4 Repetir el reparto de claves de Cloudflare en el paso de los 4 Cloud Run Jobs (`ci-cd.yml:139-140`, `--set-env-vars`/`--set-secrets`); NO fijar `Media__LocalStoragePath` ahí (Jobs no comparte disco con el web).
- [ ] 6.5 Si 6.1 confirma el mecanismo nativo, añadir las sondas a `ci-cd.yml` `flags:` (`:102`).
- [ ] 6.6 Actualizar `docs/deployment/google-cloud-run.md:163` (ya no hay carencia de Cloudflare) y añadir junto a `:164` la subsección de sondas de 6.1.
- [ ] 6.7 Actualizar `docs/specs/sistema/09-arquitectura-y-despliegue.md:61` (recuento de 6.2), `:68` (`DatabaseHealthCheck`/`StorageHealthCheck`), `:71` (el proveedor ya no miente).
- [ ] 6.8 Actualizar `docs/specs/sistema/README.md:27-28` con el recuento medido en 6.2.

## Riesgos

- **PR3 es el más cerca del techo** (220–280 medido): si `HealthChecksTests.cs` crece más de lo previsto al escribir los 3 escenarios de `StorageHealthCheck`, aplicar el corte 3a/3b declarado arriba.
- **PR1b tiene una contingencia real, no hipotética**: confirmado por lectura directa que `Ludeka.UnitTests.csproj` carece de `FrameworkReference`/`TestHost`; si añadirlo no basta, aplicar el corte 1b-i/1b-ii.
- **`appsettings.json` y `Program.cs` son puntos de contacto compartidos** entre PR1a/PR1b/PR2/PR3 (secciones y rangos de línea distintos); seguir el orden sugerido minimiza rebases.
- **34 tablas / 7 migraciones proviene de proposal/design/spec, no de un recuento propio**: el banner actual del script (27 `CREATE TABLE`) solo lista 4 tablas ausentes como ejemplo, no exhaustivo; PR4 confirma la cifra real al migrar.
- Sin entorno de producción: ninguna tarea depende de GCP/Supabase reales; PR5 tarea 6.1 es lectura de documentación, no acceso a GCP.
