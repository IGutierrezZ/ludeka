# Propuesta: INC-48 — Persistencia de Producción en PostgreSQL, Medios en R2 con Fallback Local y Verdad Documental

> Fase `sdd-propose` · Worktree `C:\repos\ludeka-wt\persistencia-produccion-postgres` · Rama `inc/persistencia-produccion-postgres` · Base `62f5b78`
> Alcance cerrado por el maintainer (Engram `sdd/change-48-persistencia-produccion-postgres/decisiones-producto`). Evidencia: `explore.md` §5 (prevalece sobre §1–§4).

## Intención

Producción es PostgreSQL y Cloudflare R2; SQLite es solo motor local y de pruebas. Hoy el código no lo cumple: sin credenciales R2 las imágenes van a memoria (`LudekaServiceCollectionExtensions.cs:278-286`), sin PostgreSQL el arranque cae en silencio a SQLite efímero (`Program.cs:146-157`), `/ready` miente sobre el proveedor (`SqliteDatabaseHealthCheck.cs:34`) y su sonda `storage` mide el directorio de la base de datos, no el almacén de medios (`StorageHealthCheck.cs:20-22`). **No existe entorno de producción**, así que la ventana para arreglarlo sin migrar datos es ahora.

## Alcance

### En alcance

1. **Medios**: precedencia R2 → disco → memoria; opción nueva `Media__LocalStoragePath`; aviso al arrancar en `Production` sin R2; `UseStaticFiles()` para servir el fallback; `Cloudflare__*` en `ci-cd.yml`.
2. **Fail-fast**: `Dockerfile:61-64` deja de fijar SQLite; en `Production` sin PostgreSQL resoluble el arranque falla; los dos `docker-compose` se rotulan como entornos locales.
3. **Health checks**: proveedor real en metadatos y mensajes; `storage` verifica el almacén de medios configurado; sondas en Cloud Run.
4. **Esquema**: prueba de integración con Testcontainers (34 tablas, 7 filas en `__EFMigrationsHistory`) y regeneración de `docs/database/supabase_schema.sql`.
5. **Documental**: `09-arquitectura-y-despliegue.md:61` (1.345) y `README.md:27` (1.534) → 1537 unitarias + 9 de integración; añadir las tablas de INC-47.

### Fuera de alcance

- *Staging* con PostgreSQL, perfil PostgreSQL en `docker-compose.yml`, `supabase-restore.ps1` y anonimización (ver «Trabajo diferido»).
- `OutboxOptions.HealthQueryTimeoutSeconds` y el resto de la deuda de INC-47.
- Verificación contra una Supabase real: no hay entorno.

## Capacidades

### Nuevas

- `media-storage-precedence`: selección de tres vías, ruta local configurable, entrega HTTP del fallback y aviso de degradación.
- `production-persistence-guard`: `Ludeka.Web` aborta en `Production` sin PostgreSQL; la imagen no impone cadena de conexión.
- `postgres-schema-verification`: migración desde cero contra PostgreSQL real y esquema SQL derivado de las migraciones.

### Modificadas

- `health-checks`: `/ready` con tres comprobaciones fieles al proveedor y al almacén de medios reales.
- `dockerfile-build`: la imagen deja de traer `ConnectionStrings__DefaultConnection`.
- `docker-compose-orchestration`: ambos ficheros declarados entornos locales.

## Enfoque

- **Selección de medios**: `MediaOptions` nueva + factoría de tres vías en `LudekaServiceCollectionExtensions.cs:278-286`. `PhysicalFileImageStorageService` ya admite `customPath` (`:24-40`), así que no hay servicio nuevo. Como la ruta puede caer fuera de `wwwroot`, la entrega se hace con `UseStaticFiles` y `PhysicalFileProvider`; `MapStaticAssets()` (`Program.cs:323`) **no** sirve ficheros escritos en caliente.
- **Fail-fast**: espejar `src/Ludeka.Jobs/StartupGuards.cs` **Guarda 1** (`:44-54`), que es la de coherencia de proveedor y ya nombra el secreto ausente; reutilizar `DatabaseOptions.IsPostgreSql`. La Guarda 2 (`:56-67`) **no** se porta: el host web sí migra (`Program.cs:148`). Extraer la lógica a una clase testeable, como en `Ludeka.Jobs`.
- **Health checks**: leer el proveedor efectivo del `DbContext`; sustituir el parseo de `Data Source=` por una sonda del almacén de medios seleccionado.
- **Esquema**: `CollectionDefinition` xUnit propia (4 precedentes en `PostgresFixture.cs:80,95,111,126`); regeneración con `Database__Provider=PostgreSql` (restricción heredada de INC-46).
- **Puesta en marcha**: no se altera el orden de `docs/deployment/google-cloud-run.md` §9.0 ni la regla de que `Ludeka.Jobs` nunca migra. Solo se amplía el **paso 4** con los secretos de Cloudflare y las sondas del despliegue.

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs:278-286` | Modificado | Selección de tres vías + `MediaOptions` |
| `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs:24-40` | Modificado | Ruta desde configuración |
| `src/Ludeka.Web/Program.cs:103-106,146-157,323` | Modificado | Guarda de arranque, health checks, `UseStaticFiles()` |
| `src/Ludeka.Web/Health/*.cs` | Modificado | Proveedor real y sonda de medios |
| `Dockerfile:61-64` | Modificado | Retirada del default SQLite |
| `.github/workflows/ci-cd.yml:102-118` | Modificado | `Cloudflare__*` y sondas |
| `docker-compose.yml`, `docker-compose.staging.yml` | Modificado | Rótulo de entorno local |
| `docs/database/supabase_schema.sql` | Regenerado | Derivado de las 7 migraciones |
| `tests/Ludeka.IntegrationTests/`, `tests/Ludeka.UnitTests/` | Nuevo | Colección propia + pruebas de los 5 ejes |
| `docs/specs/sistema/09-arquitectura-y-despliegue.md`, `docs/specs/sistema/README.md` | Modificado | Cifras y tablas |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| **Sondas en `deploy-cloudrun@v2` sin verificar** (hueco de evidencia declarado y aceptado) | Alta | Verificar la capacidad **antes** de `sdd-apply`; si no existe, degradar a provisión manual documentada, como hizo INC-47 con `gcloud run jobs deploy` |
| El fallback local devuelve 404 si solo se recablea la inyección | Alta | `UseStaticFiles` con `PhysicalFileProvider` y prueba de descarga HTTP, no solo de escritura |
| Subestimar el volumen (las 11 rebanadas de INC-47 se quedaron cortas **sin excepción**, peor caso 350 → 1132) | Alta | No se dan cifras aquí: `sdd-tasks` mide sobre ficheros reales. Lo más frágil son las pruebas —no hay precedente de «la imagen sobrevive a la recreación» ni de verificación de esquema— y el refactor del arranque de `Program.cs` |
| Colisión de colecciones xUnit al migrar desde cero | Media | Colección nueva, nunca reusar `postgres-real` |
| Retirar el default del `Dockerfile` rompe `docker run` sin variables | Baja | Documentar el arranque local vía `docker-compose.yml`, que sí aporta configuración |
| Nada se puede probar de extremo a extremo | Alta | Toda la verificación es automática y local; no se declara verificado nada que exija GCP o Supabase |

## Plan de reversión

Los cinco ejes son independientes y viajan en PRs encadenados: revertir el PR de la rebanada basta. Los dos cambios con efecto sobre un despliegue real —retirada del `ENV` del `Dockerfile` y guarda de arranque— se revierten restaurando esas líneas. `supabase_schema.sql` es derivado: se regenera con un comando. Sin entorno de producción, ninguna reversión implica migrar datos.

## Dependencias

- INC-40 (R2 + SkiaSharp), INC-46 (restricción `Database__Provider=PostgreSql` al generar scripts), INC-47 (Cloud Run Jobs, `StartupGuards`, `PostgresFixture`).
- Externa **no resuelta**: soporte de sondas en `google-github-actions/deploy-cloudrun@v2`.
- Operativa: cuenta y bucket de Cloudflare R2 para que el camino de producción deje de ser teórico.

## Criterios de éxito

- [ ] Migrando desde cero contra PostgreSQL 17 real: **34** tablas y **7** filas en `__EFMigrationsHistory`.
- [ ] `supabase_schema.sql` regenerado desde las migraciones, con el banner conservado.
- [ ] Con R2 → R2; sin R2 y con `Media__LocalStoragePath` → disco; sin ninguna de las dos → memoria.
- [ ] Una imagen escrita por el fallback local se descarga por HTTP con **200**, no 404.
- [ ] En `Production` sin PostgreSQL resoluble, el arranque falla con mensaje explícito.
- [ ] `/ready` reporta el proveedor real y el estado del almacén de medios configurado, en sus tres comprobaciones.
- [ ] `09-arquitectura-y-despliegue.md` y `README.md` dicen 1537 unitarias + 9 de integración.
- [ ] `dotnet test Ludeka.sln` en verde.

## Trabajo diferido (incremento futuro, a abrir al cerrar INC-48)

Objetivo declarado del maintainer: sacar copias de producción y montar entornos de desarrollo con ellas.

- *Staging* con servicio PostgreSQL y perfil PostgreSQL opcional en `docker-compose.yml`.
- `supabase-restore.ps1`: **no existe ningún script de restauración para PostgreSQL** (`scripts/` y `deploy/` solo tienen `restore-sqlite.sh`), y un `pg_dump` no se restaura en SQLite.
- Anonimización de datos personales: la copia arrastra `AppUsers`, correos y `ExternalLogins` reales. Es requisito de RGPD, no opcional.
