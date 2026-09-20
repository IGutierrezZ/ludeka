# 35. Persistencia de Producción en PostgreSQL, Medios con Fallback en Disco y Verdad Documental del Esquema

> **Estado:** Implementado y verificado (1.565 pruebas unitarias + 10 de integración en verde, 0 errores, 0 omitidas)
> **Incremento:** [INC-48](file:///c:/repos/Ludeka/docs/increments/archive/inc-48-persistencia-produccion-postgres.md) — 9 PRs (#63–#71), `main` en `0a320e6`
> **Módulos relacionados:** [09. Arquitectura, Persistencia y Despliegue](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md) · [26. Pipeline de Almacenamiento y Optimización de Medios](file:///c:/repos/Ludeka/docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md) · [34. Trabajos en Segundo Plano en Cloud Run](file:///c:/repos/Ludeka/docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md)

---

## 1. La regla que este módulo hace cumplir

**Producción es PostgreSQL y Cloudflare R2. SQLite existe únicamente como motor de pruebas y de desarrollo local.**

Antes de INC-48 el código, la configuración y la documentación afirmaban simultáneamente lo contrario en cuatro puntos distintos. Los cuatro estaban en la trayectoria del primer despliegue real y ninguno habría dado síntoma visible: la aplicación habría arrancado, respondido y perdido datos en silencio.

| # | Defecto | Consecuencia en producción |
|---|---|---|
| 1 | El selector de `IImageStorageService` era binario: Cloudflare R2 o memoria. `PhysicalFileImageStorageService` existía pero **nunca se seleccionaba**. | Toda imagen de catálogo se perdía al reiniciar el proceso. |
| 2 | `UseStaticFiles()` no existía en `src/`. `MapStaticAssets()` solo sirve el manifiesto de activos generado en compilación, no ficheros escritos en tiempo de ejecución. | El fallback en disco, aun seleccionándose, habría devuelto **404**. |
| 3 | En `Production` sin cadena de conexión inyectada, la configuración resolvía a `Data Source=ludeka.db`. | Base SQLite efímera dentro del contenedor, borrada en cada escalado a cero de Cloud Run. |
| 4 | El health check de base de datos reportaba un literal fijo y el de almacenamiento inspeccionaba la cadena de conexión de la base de datos, sin relación alguna con los medios. | `/ready` habría informado en verde de un estado que no era el real. |

**Solo el primero figuraba en el documento de incremento de partida.** Los otros tres los destapó la auditoría previa; el §0 del documento archivado recoge la rectificación completa.

---

## 2. Precedencia de almacenamiento de medios (tres vías)

### 2.1. La fábrica

[`LudekaServiceCollectionExtensions.cs:277-302`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs) registra `IImageStorageService` evaluando, en orden estricto:

1. **`CloudflareR2StorageService`** si `CloudflareR2Options.HasValidCredentials`.
2. **`PhysicalFileImageStorageService`** si no hay R2 pero sí `MediaOptions.HasLocalStoragePath` (`Media__LocalStoragePath`), apuntando a la ruta resuelta.
3. **`SimulatedImageStorageService`** (en memoria) si no se cumple ninguna de las dos.

La decisión se toma al construir el contenedor y permanece fija durante toda la vida del host.

`HasValidCredentials` ([`CloudflareR2Options.cs:44-49`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Options/CloudflareR2Options.cs)) exige **cinco** condiciones: `!Simulate`, `AccountId`, `AccessKeyId`, `SecretAccessKey` y `BucketName`.

> ⚠️ **`!Simulate` es la primera condición, y es un pie de fábrica silencioso.** Inyectar las tres credenciales reales de R2 sin fijar `Cloudflare__Simulate=false` deja el almacenamiento **en memoria** sin ningún síntoma: el despliegue parecería correcto y las imágenes se seguirían perdiendo.

**Verificación:** `MediaStorageSelectionTests.cs:56-88`, un escenario por vía.

### 2.2. Resolución única de la ruta local

`MediaOptions.ResolveLocalStoragePath(contentRootPath)` ([`src/Ludeka.Application/Options/MediaOptions.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Options/MediaOptions.cs)) devuelve la ruta configurada si es absoluta y, si es relativa, la combina con el `ContentRootPath` del host. Devuelve `null` cuando no hay ruta configurada.

Vive en `Ludeka.Application` a propósito: es el **único** algoritmo de resolución del repositorio y lo consumen tanto `Ludeka.Infrastructure` (para escribir) como `Ludeka.Web` (para servir). Antes de INC-48 cada ensamblado tenía su propia copia, sin ninguna prueba que las cruzara.

### 2.3. El defecto de ruta corregido

[`PhysicalFileImageStorageService.cs:33`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs):

```csharp
_gamesDirectory = Path.Combine(customPath, "games");
```

El servicio escribía en la raíz de la ruta configurada pero publicaba la URL con el segmento `games`. Era un 404 encadenado al del punto 2 de la tabla anterior: aunque se hubiera arreglado la entrega HTTP, el fichero no habría estado donde la URL decía.

**Verificación:** `PhysicalFileImageStorageServiceTests.cs:37-63`, que además comprueba que el fichero **no** queda en la raíz.

---

## 3. Entrega HTTP del fallback en disco

### 3.1. El middleware

[`MediaStaticFilesExtensions.cs`](file:///c:/repos/Ludeka/src/Ludeka.Web/Extensions/MediaStaticFilesExtensions.cs) expone `UseLudekaMediaFiles(MediaOptions, IHostEnvironment)`, que registra `UseStaticFiles` con:

- **`FileProvider`:** un `PhysicalFileProvider` cuya raíz es la ruta local resuelta.
- **`RequestPath`:** el prefijo público fijo `/images`.
- **`ServeUnknownFileTypes`:** se deja en su valor por defecto (`false`), deliberadamente. Activarlo serviría cualquier extensión como binario genérico bajo `/images`, ampliando la superficie expuesta sin necesidad: las portadas usan extensiones conocidas.

El prefijo `/images` es de **URL**, no de disco: la raíz del proveedor es la ruta configurada tal cual, sin segmento añadido.

Si no hay ruta local configurada, devuelve la aplicación intacta: el almacén activo es entonces R2 o memoria, y ninguno de los dos escribe ficheros en este proceso. El middleware crea el directorio al arrancar, porque un despliegue en frío puede tener la ruta configurada y aún vacía.

### 3.2. Dónde se cablea y por qué ahí

[`Program.cs:213`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs):

```csharp
var mediaOptionsValue = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
app.UseLudekaMediaFiles(mediaOptionsValue, app.Environment);
```

Va **antes** de la autenticación: los medios son públicos y no deben pagar autenticación ni antiforgery.

### 3.3. Pruebas de entrega y matriz de amenazas

`MediaStaticFilesDeliveryTests.cs` levanta un **host ASP.NET Core mínimo real con Kestrel**. Descarta `WebApplicationFactory<Program>` de forma explícita y documentada (`:33`), siguiendo el precedente del repositorio.

| Escenario | Línea | Qué acredita |
|---|---|---|
| Imagen guardada por el almacén en disco se descarga con 200 | `:42-74` | Bytes comparados uno a uno contra el original |
| URL de imagen nunca guardada devuelve 404 | `:76-93` | No hay respuesta fantasma |
| Recorrido de directorio no sirve ficheros de fuera de la raíz | `:100-103` | `[Theory]` con `/images/..%2f..%2fSECRETO.txt` y `/images/%2e%2e/%2e%2e/SECRETO.txt` |
| Ruta local relativa resuelve dentro de `ContentRootPath` | `:127-128` | Cruza el resolvedor común de §2.2 |

`PhysicalFileProvider` decodifica, normaliza y descarta cualquier ruta que resuelva fuera de su raíz antes de servir nada. Las dos cargas codificadas lo ejercitan de verdad, no por inspección.

---

## 4. Guarda de arranque contra persistencia efímera

### 4.1. `WebStartupGuards`

[`src/Ludeka.Web/WebStartupGuards.cs:20-46`](file:///c:/repos/Ludeka/src/Ludeka.Web/WebStartupGuards.cs) es el espejo de la **Guarda 1** de [`src/Ludeka.Jobs/StartupGuards.cs`](file:///c:/repos/Ludeka/src/Ludeka.Jobs/StartupGuards.cs) (coherencia de proveedor):

```csharp
public static string? Evaluate(IConfiguration configuration, DatabaseOptions databaseOptions, string? environmentName)
```

Es una **función pura**: no compone servicios ni lee variables de entorno globales, y recibe el nombre del entorno explícitamente para ser comprobable sin mutar estado del proceso. Devuelve el mensaje de fallo, o `null` si pasa.

Resuelve la cadena en este orden —`DefaultConnection` → `PostgreSqlConnection` → `Data Source=ludeka.db`— y exige PostgreSQL solo cuando se dan a la vez `Database:RequirePostgreSqlInProduction` (por defecto `true`) y entorno `Production`, con comparación insensible a mayúsculas.

**La Guarda 2 de `Ludeka.Jobs` (migraciones pendientes) NO se porta a este host**, y la diferencia es deliberada: `Ludeka.Jobs` nunca migra y sale con código 3 si hay migraciones pendientes, mientras que el host web sí ejecuta `MigrateAsync()` al arrancar.

### 4.2. Cómo falla

[`Program.cs:116-123`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs) invoca la guarda tras `builder.Build()` y **antes** de cualquier acceso a la base de datos; si devuelve mensaje, lanza `InvalidOperationException` con él. El mensaje nombra el secreto ausente (`SUPABASE_DB_CONNECTION`, variable `ConnectionStrings__DefaultConnection`), para que el fallo diga qué falta en vez de solo que algo falta.

**Verificación:** `WebStartupGuardsTests.cs:30-87` — arranque normal, fallo explícito, tres entornos no productivos (`Development`, `Staging`, `null`) e insensibilidad a mayúsculas.

### 4.3. Que la guarda no rompa lo que ya funcionaba

Una guarda que exige PostgreSQL en `Production` rompe cualquier entorno local que defaultee a `Production`. Por eso INC-48 tocó también la orquestación:

| Fichero | Valor | Motivo |
|---|---|---|
| `Dockerfile:65-67` | **Sin** `ConnectionStrings__DefaultConnection` en `ENV` | La conexión procede exclusivamente de variables inyectadas en tiempo de ejecución |
| `docker-compose.yml:16` | `ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Staging}` | Antes defaulteaba a `Production`; con la guarda nueva, `docker compose up` habría dejado de arrancar en local |
| `docker-compose.staging.yml:15` | `Staging` | Ya era correcto |

**Verificación:** `WebStartupGuardsTests.cs:89-103` replica ambas configuraciones de compose contra la guarda, de modo que el día que alguien cambie el entorno por defecto la prueba lo detecte.

---

## 5. Sondas de salud fieles

### 5.1. `DatabaseHealthCheck`

Renombrado desde `SqliteDatabaseHealthCheck`; el nombre anterior era en sí mismo una afirmación falsa sobre producción.

[`DatabaseHealthCheck.cs:33`](file:///c:/repos/Ludeka/src/Ludeka.Web/Health/DatabaseHealthCheck.cs) lee el proveedor efectivo de `_dbContext.Database.ProviderName` y lo publica como metadato `provider` en **todas** las ramas, incluidas las de fallo. Nunca un literal fijo.

**Verificación:** `HealthChecksTests.cs:192-208` compone el contexto sobre Npgsql y exige que el metadato contenga `Npgsql` y **no contenga** `Sqlite`. La prueba se validó inyectando el literal fijo y comprobando que solo ella caía en rojo.

### 5.2. `StorageHealthCheck`

[`StorageHealthCheck.cs:37-57`](file:///c:/repos/Ludeka/src/Ludeka.Web/Health/StorageHealthCheck.cs) sonda la vía de medios **realmente seleccionada**, con la misma precedencia de tres vías que §2.1, en vez de parsear `Data Source=` de la cadena de conexión de la base de datos, que no tiene ninguna relación con los medios:

| Vía | Comprobación | Estado |
|---|---|---|
| R2 | Credenciales válidas; publica `mode: r2` y el bucket | `Healthy` |
| Disco local | Escritura, lectura y borrado reales de un `.healthcheck_probe_*.tmp` en la ruta resuelta, para certificar permisos de volumen Docker | `Healthy` / `Unhealthy` |
| Memoria | Sin R2 ni ruta local; publica `mode: memory` | `Degraded` |

**Verificación:** `HealthChecksTests.cs:241-312`, las tres vías.

### 5.3. `/ready`

[`Program.cs:243-266`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs) agrega `database`, `storage` y `notification_queue`.

> **Hueco de evidencia declarado:** el código HTTP ante `Degraded` depende del valor por defecto de `HealthCheckOptions.ResultStatusCodes` del framework, que devuelve **200**, no 503. Es coherente con el diseño —un almacén volátil no impide servir tráfico—, pero hoy esa decisión la sostiene un valor por defecto ajeno, no una prueba nuestra.

---

## 6. Verificación del esquema contra PostgreSQL real

### 6.1. La prueba

[`tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs:42-86`](file:///c:/repos/Ludeka/tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs) levanta un `postgres:17-alpine` real con Testcontainers, migra desde una base **vacía** y asevera en dos niveles:

- **Invariante:** compara el modelo real contra el esquema real y detecta cualquier divergencia accidental entre ambos.
- **Canario literal:** 34 tablas y 7 filas en `__EFMigrationsHistory` (`:29`, `:32`). Caduca deliberadamente con la siguiente migración que añada o quite una tabla, para que ese cambio no pase inadvertido.

Las 7 migraciones reales son `20260914001323_InitialSupabasePostgres`, `20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages`, `20260915164921_AddExternalLogins`, `20260917112154_AddProviderEmailVerifiedAtToExternalLogins`, `20260919000613_AddNotificationLogDeliveryColumns`, `20260919010426_AddNotificationOutboxMessages` y `20260919012754_AddJobExecutionLeases`.

### 6.2. Por qué necesita colección propia

`[Collection("postgres-real")]` comparte **una sola** base PostgreSQL y **un solo** `__EFMigrationsHistory`, y xUnit no garantiza el orden de ejecución. Una prueba que migra desde cero y asevera recuentos exactos no puede convivir con las que siembran bajo un esquema ya migrado.

Por eso `PostgresSchemaVerificationTests` declara `[Collection("postgres-real-schema")]`, la quinta de [`PostgresFixture.cs`](file:///c:/repos/Ludeka/tests/Ludeka.IntegrationTests/PostgresFixture.cs):

| Colección | Línea |
|---|---|
| `postgres-real` | `:80` |
| `postgres-real-job-leases` | `:95` |
| `postgres-real-outbox-claim` | `:111` |
| `postgres-real-job-coordinator` | `:126` |
| **`postgres-real-schema`** | **`:142`** |

---

## 7. El script SQL como derivado, no como fuente de verdad

### 7.1. Qué es y cómo se regenera

[`docs/database/supabase_schema.sql`](file:///c:/repos/Ludeka/docs/database/supabase_schema.sql) se autodeclaraba «esquema maestro» y contradecía al modelo en ocho tablas. Hoy es un **derivado regenerado** de las migraciones, con un banner que lo dice y el comando exacto en su cabecera:

```bash
Database__Provider=PostgreSql dotnet ef migrations script --idempotent \
  --project src/Ludeka.Infrastructure --startup-project src/Ludeka.Web \
  --context LudekaDbContext --output docs/database/supabase_schema.sql
```

Forzar `Database__Provider=PostgreSql` es imprescindible —restricción heredada de INC-46—: sin ella EF Core generaría sintaxis SQLite.

Contiene **35** sentencias `CREATE TABLE`: las 34 tablas del modelo más `__EFMigrationsHistory`, que la propia migración crea.

### 7.2. Las dos pruebas que impiden que vuelva a desfasarse

| Prueba | Dónde | Qué garantiza |
|---|---|---|
| `SupabaseSchemaFreshnessTests` | `tests/Ludeka.UnitTests/Infrastructure/` | **Sin PostgreSQL real**, que cada tabla del modelo tiene su `CREATE TABLE` en el script. Barata, corre en cada CI. |
| `PostgresSchemaVerificationTests` | `tests/Ludeka.IntegrationTests/` | Con PostgreSQL 17 real, el esquema completo desde cero (§6). |

La fuente de verdad del esquema son, y siguen siendo, **las migraciones de EF Core**, aplicadas con `MigrateAsync()` al arrancar contra PostgreSQL.

---

## 8. Cableado de despliegue

### 8.1. Sondas de Cloud Run

[`.github/workflows/ci-cd.yml:107`](file:///c:/repos/Ludeka/.github/workflows/ci-cd.yml) pasa las sondas por `flags:`, que `google-github-actions/deploy-cloudrun@v2` traslada a `gcloud run deploy`:

```
--liveness-probe=httpGet.path=/healthz,httpGet.port=8080
--readiness-probe=httpGet.path=/ready,httpGet.port=8080
```

Restricciones de la herramienta, verificadas contra la referencia de `gcloud`: `--readiness-probe` **no** acepta `initialDelaySeconds` y `--liveness-probe` **no** acepta `successThreshold`.

### 8.2. Credenciales de Cloudflare R2

Se inyectan tanto en el servicio web como en los cuatro Cloud Run Jobs, repartidas según su sensibilidad:

- **`env_vars`** (valores no secretos): `Cloudflare__BucketName`, `Cloudflare__PublicCdnBaseUrl` y `Cloudflare__Simulate=false`.
- **`secrets`** (referencias a Secret Manager): `Cloudflare__AccountId`, `Cloudflare__AccessKeyId` y `Cloudflare__SecretAccessKey`.

`Cloudflare__Simulate=false` va explícito por el motivo de §2.1: sin él, las tres credenciales no bastan.

---

## 9. Deuda y huecos abiertos

1. **No existe entorno de producción.** No hay proyecto de GCP ni base de Supabase. Los pasos de despliegue están condicionados a `has_gcp == 'true'`, que exige `GCP_PROJECT_ID` y `GCP_SA_KEY`; sin ellos se omiten. **Nada de este módulo está acreditado contra un despliegue real**: lo verificado es coherencia de código, configuración y pruebas.
2. **Las sondas de Cloud Run se acreditaron solo contra documentación** (§8.1), nunca contra un despliegue.
3. **Docker no se ejercitó** en el ciclo de verificación: ni `docker build`, ni `docker run`, ni `docker compose up`. La conformidad de §4.3 se apoya en inspección más pruebas que replican la configuración.
4. **`/ready` ante `Degraded`** no está fijado por prueba propia (§5.3).
5. **Duplicidad de claves:** `Database:RequirePostgreSqlInProduction` (web) y `Workers:RequirePostgreSqlInProduction` (jobs) conviven sobre un mismo `appsettings.json` enlazado, y cada host lee la suya. Deuda declarada y aceptada en el diseño de INC-48.
6. **El fallo silencioso de `Cloudflare__Simulate` sigue vivo** (§2.1). Está advertido en `DEPLOYMENT_GUIDE.md:227`, pero es documentación, no una guarda.
7. **No existe ciclo de copia y restauración de producción.** `scripts/` y `deploy/` solo contienen `restore-sqlite.sh`; no hay restauración para PostgreSQL, y un volcado arrastraría `AppUsers`, correos y `ExternalLogins`, por lo que la anonimización es requisito RGPD antes de montar entornos de desarrollo con copia de producción.

---

## 10. Estado de entrega

- **9 PRs mergeados** a `main` en `0a320e6`: #63 (estabilización de un test flaky preexistente), #64 (planificación), #65 a #70 (las seis rebanadas) y #71 (informe de verificación).
- **Suite:** 1.565 pruebas unitarias + 10 de integración, 0 errores, 0 omitidas, exit real 0. Medida **tres veces por vías independientes** con resultado idéntico. Compilación: exit 0, 0 errores, 13 advertencias.
- **Magnitud:** 32 ficheros, 2.908 inserciones y 548 borrados, excluyendo `openspec/`.
- **Veredicto de verificación:** `pass_with_warnings` — 11 requisitos y 25 escenarios verificados, 0 CRITICAL, 2 WARNING (los huecos 3 y 4 de §9), 3 SUGGESTION.
