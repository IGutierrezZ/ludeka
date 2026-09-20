# Diseño: INC-48 — Persistencia de producción en PostgreSQL, medios en R2 con fallback local y verdad documental

> Fase `sdd-design` · Worktree `C:\repos\ludeka-wt\persistencia-produccion-postgres` · Rama `inc/persistencia-produccion-postgres` · Base `62f5b78`
> Entradas: `proposal.md` (alcance cerrado), `explore.md` §5 (prevalece sobre §1–§4), Engram #482 / #479 / #474, `docs/deployment/google-cloud-run.md` §9.0.
> Todas las citas `fichero:línea` de este documento se han verificado abriendo el fichero en este worktree. Lo no verificado se marca literalmente **no verificado**.

## 1. Enfoque técnico

Cinco ejes independientes bajo un mismo patrón: **la configuración decide, el arranque lo declara y una sonda lo confirma**. No hay servicios de dominio nuevos. El trabajo es de composición (`Options` + factorías), tubería HTTP, guarda de arranque y verificación. `Ludeka.Jobs` no migra nunca y el orden de los siete pasos del §9.0 no cambia.

## 2. Decisiones de arquitectura

### D1 — `MediaOptions` y precedencia de tres vías

| | |
|---|---|
| **Elección** | `MediaOptions` nueva en `src/Ludeka.Application/Options/MediaOptions.cs` (junto a `CloudflareR2Options.cs`), `SectionName = "Media"`, propiedad única `LocalStoragePath` (vacía por defecto) y `HasLocalStoragePath => !string.IsNullOrWhiteSpace(LocalStoragePath)`. Clave de entorno: `Media__LocalStoragePath`. La factoría de `LudekaServiceCollectionExtensions.cs:278-286` pasa a tres ramas: `CloudflareR2Options.HasValidCredentials` → R2; en su defecto `HasLocalStoragePath` → `PhysicalFileImageStorageService`; en su defecto `SimulatedImageStorageService`. |
| **Descartadas** | (a) añadir `LocalStoragePath` a `CloudflareR2Options`: mezcla la configuración del proveedor remoto con la del respaldo; (b) enum `Media:Provider`: duplica una verdad ya derivable y admite estados incoherentes (`Provider=R2` sin credenciales). |
| **Razón** | «Ruta local configurada» = cadena no vacía tras `Trim()`. Si es relativa se resuelve contra `IHostEnvironment.ContentRootPath`. No se valida existencia al seleccionar: `PhysicalFileImageStorageService` ya hace `Directory.CreateDirectory` al escribir (`:65-68`, `:140-143`, `:201-204`); la escribibilidad la comprueba la sonda de D4. |

**Registro.** `services.AddScoped<PhysicalFileImageStorageService>()` (`:277`) se sustituye por una factoría `sp => new PhysicalFileImageStorageService(env, rutaResuelta)`; el constructor ya admite `customPath` (`:24`), así que **no hay servicio nuevo**.

🚨 **Defecto verificado que hay que corregir en el mismo cambio.** Con `customPath`, `PhysicalFileImageStorageService.cs:28` asigna `_gamesDirectory = customPath` (sin subcarpeta `games`), pero `SaveGameCoverAsync` devuelve la URL `/images/games/{fichero}` (`:85`). La portada se escribiría en `{ruta}/fichero.jpg` y se pediría en `/images/games/fichero.jpg` → **404**, justo el criterio que el incremento promete. Corrección: `_gamesDirectory = Path.Combine(customPath, "games")`. Una línea. `SaveToFolderAsync` (`:139`) y `UploadOptimizedImageAsync` (`:199`) ya son coherentes.

### D2 — Entrega HTTP del fallback

| | |
|---|---|
| **Elección** | Se admite **cualquier ruta**, servida siempre bajo el prefijo `/images` con `UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(raízDeMedios), RequestPath = "/images" })`, registrado **solo cuando la vía de disco es la seleccionada**, y **además** de `MapStaticAssets()` (`Program.cs:323`), que no se toca. Se extrae a `src/Ludeka.Web/Extensions/MediaStaticFilesExtensions.cs` → `UseLudekaMediaFiles(this IApplicationBuilder, MediaOptions, IHostEnvironment)`, que devuelve la tubería intacta si no hay ruta. |
| **Descartadas** | (a) restringir la ruta a `wwwroot`: el único volumen escribible del despliegue local es `ludeka_data:/app/data` (`docker-compose.yml:17`), **fuera** de `wwwroot`; restringir haría el respaldo no duradero justo donde tiene sentido; (b) `UseStaticFiles()` a secas sobre `wwwroot`: no sirve rutas externas y expone todo el *web root* por una vía extra sin necesidad; (c) cambiar el prefijo público: `GetPublicUrl` fija `/images/{clave}` (`:264-269`) y las URLs ya persistidas en base de datos usan ese prefijo. |
| **Razón** | El prefijo `/images` está fijado en el código, no es negociable sin romper datos existentes. Un `PhysicalFileProvider` propio es la única forma de mapearlo a una ruta arbitraria. |

**Orden en la tubería.** `UseLudekaMediaFiles` se inserta tras `app.UseHttpsRedirection()` (`Program.cs:180`) y **antes** de `UseAuthentication()` (`:183`): son medios públicos y no deben pagar autenticación ni antiforgery. `UseStaticFiles` es *middleware* que cortocircuita; `MapStaticAssets()` registra *endpoints*, que se ejecutan al final de la tubería. Por tanto el proveedor de medios se evalúa **antes** que el manifiesto. Como la raíz de medios es un directorio distinto de `wwwroot/images` y los nombres que escribe el servicio llevan sufijo de marca temporal (`{slug}-cover-{timestamp}{ext}`, `:72`), no hay solape con los ~90 activos de compilación de `wwwroot/images`. Si el operador apunta la ruta **a** `wwwroot/images`, se sirven los mismos bytes perdiendo solo huella y compresión para esas peticiones: degradación aceptable y documentada.
**No verificado**: el orden efectivo entre este *middleware* y los *endpoints* de `MapStaticAssets` no se ha comprobado ejecutando la aplicación. La prueba HTTP de la rebanada 1 existe exactamente para verificarlo, y es el punto que debe fallar en rojo primero.

### D3 — Fail-fast de PostgreSQL en `Production`

| | |
|---|---|
| **Elección** | Clase nueva `src/Ludeka.Web/WebStartupGuards.cs`: `public static string? Evaluate(IConfiguration, DatabaseOptions, string? environmentName)`, espejo de la **Guarda 1** de `src/Ludeka.Jobs/StartupGuards.cs:44-54`. Clave nueva `Database:RequirePostgreSqlInProduction` (`true` por defecto), añadida a la sección `Database` ya existente de `appsettings.json:13-16`. Se invoca en `Program.cs` tras `builder.Build()` (`:108`) y antes del ámbito de inicialización (`:117`); si devuelve mensaje, se lanza `InvalidOperationException`. |
| **Descartadas** | (a) reutilizar `Workers:RequirePostgreSqlInProduction` (`appsettings.json:75-81`): el host web no es un *worker*, y quien más adelante lo pusiera a `false` para desbloquear un trabajo desactivaría en silencio la guarda del web; (b) `Persistence:*`: tercer espacio de nombres para una política que ya tiene sección propia; (c) portar la Guarda 2 (migraciones pendientes): el host web **sí** migra (`Program.cs:148`); (d) `Environment.Exit(3)`: el código de salida es el contrato de un *job* (`src/Ludeka.Jobs/Program.cs:67,73`), no el de un servicio HTTP de larga vida. |
| **Razón** | `src/Ludeka.Jobs/Ludeka.Jobs.csproj:26` **enlaza** `..\Ludeka.Web\appsettings.json`: hay un único fichero para los dos hosts, y cualquier clave nueva es visible para ambos. `Database:` es el espacio correcto precisamente porque la política es de base de datos, no de host; `Ludeka.Jobs` simplemente no la lee. **Deuda declarada y aceptada**: quedan dos claves para una misma política. Unificarlas cambiaría el comportamiento de INC-47 sin requisito que lo pida y queda fuera de INC-48. |

**Alcance del refactor**: se extrae **solo** la guarda, no el bloque `Program.cs:117-170`. Precedente de estilo y tamaño: `tests/Ludeka.UnitTests/Jobs/StartupGuardsTests.cs` (89 líneas, 3 pruebas).

🚨 **Colisión verificada con `docker-compose.yml`.** Ese fichero fija `ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}` (`:11`) junto a una cadena SQLite (`:12`): con la guarda activa, `docker compose up` **aborta**. El rótulo que pide la propuesta no basta; hay que cambiar el valor por defecto a `Staging`. No `Development`, porque `Program.cs:163` sembraría datos demostrativos y `:173` retiraría `UseExceptionHandler`/`UseHsts`. `docker-compose.staging.yml:11` ya usa `Staging` y no se ve afectado. Retirar `ConnectionStrings__DefaultConnection` del `ENV` del `Dockerfile:61-64` no afecta a ninguno de los dos compose, que la fijan explícitamente; sí hace que un `docker run` pelado caiga en la guarda, que es el comportamiento buscado.

### D4 — Health checks fieles

| | |
|---|---|
| **Elección** | `SqliteDatabaseHealthCheck` → `DatabaseHealthCheck`; nombre registrado `sqlite_db` → `database`. El proveedor se lee de `_dbContext.Database.ProviderName`, no de la configuración. `StorageHealthCheck` deja de parsear `Data Source=` (`:20-22`) y sondea la vía de medios **seleccionada**, con la misma precedencia de D1. |
| **Descartadas** | (a) mantener el nombre `sqlite_db`: es la mentira que el incremento existe para borrar; (b) leer el proveedor de `DatabaseOptions.Provider`: la configuración puede mentir, `ProviderName` es lo que el contexto usa de verdad; (c) sondear R2 con una llamada de red: encarece cada ciclo de sonda de Cloud Run y hace que la disponibilidad dependa de un tercero. |

| Vía | Sonda | Resultado |
|---|---|---|
| R2 (`HasValidCredentials`) | sin red: presencia de los cuatro campos y del bucket; metadatos `mode=r2`, `bucket` | `Healthy` |
| Disco (`HasLocalStoragePath`) | crear directorio si falta + escribir/leer/borrar fichero de prueba en la raíz de medios (misma técnica de `:36-39`) | `Healthy` / `Unhealthy` |
| Memoria | ninguna | `Degraded`, `mode=memory`, «el almacén de medios es volátil» |

⚠ **Consecuencia que hay que tener presente**: `Degraded` **no** devuelve 503 en `/ready` con la configuración actual (`Program.cs:210-212` no define `ResultStatusCodes`). En `Production` sin R2 la sonda seguirá respondiendo 200 con `status: Degraded` en el cuerpo. Es deliberado: la aplicación funciona, los medios no sobreviven al reinicio. Convertirlo en 503 tumbaría el servicio entero por una degradación de medios.

**Impacto verificado del renombrado**: `src/Ludeka.Web/Program.cs:104`; `tests/Ludeka.UnitTests/Health/HealthChecksTests.cs:148,161,174,186,262`; `docs/specs/sistema/09-arquitectura-y-despliegue.md:68,71`. Ningún fichero de despliegue ni script referencia `sqlite_db`; los demás resultados viven en `openspec/changes/archive/`, que es histórico y no se toca.

### D5 — Verificación de esquema y `supabase_schema.sql`

| | |
|---|---|
| **Elección** | `tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs` en colección propia `postgres-real-schema` (quinta `CollectionDefinition` en `PostgresFixture.cs`, tras las cuatro de `:80,95,111,126`). Contra un contenedor limpio: `MigrateAsync()` y luego **dos niveles de aserción**: (1) invariante — `GetPendingMigrationsAsync()` vacío, filas de `__EFMigrationsHistory` == `Database.GetMigrations().Count()`, y tablas base de `information_schema.tables` en `public` (excluyendo `__EFMigrationsHistory`) == nombres distintos de `Model.GetEntityTypes().GetTableName()`; (2) canario literal — **34** tablas y **7** filas. |
| **Descartadas** | (a) solo literales: caducan con la siguiente migración, que es exactamente el fallo que INC-48 existe para corregir; (b) solo invariante: no detecta un cambio de modelo accidental; (c) reusar `postgres-real`: xUnit no garantiza el orden entre clases y esa colección ya aloja pruebas que migran a un punto histórico (`PostgresFixture.cs:85-98`). |

**Regeneración.** `dotnet ef migrations script --idempotent` sobre `src/Ludeka.Infrastructure` con `src/Ludeka.Web` como *startup project* y `Database__Provider=PostgreSql` en el entorno (restricción heredada de INC-46). `--idempotent` es lo que hace el fichero seguro contra una base ya existente. El *banner* de `docs/database/supabase_schema.sql:1-14` se **reescribe**, no se conserva tal cual: su texto actual enumera las tablas que faltan y quedaría falso tras regenerar. El nuevo dice qué comando lo genera y que no se edita a mano.

**Cómo se evita que vuelva a mentir.** Prueba estructural nueva sobre el fichero real (misma técnica que `WebHostHostedServiceCompositionTests.cs:80-101`): para cada nombre de tabla del modelo debe existir un `CREATE TABLE "<nombre>"` en el script. Descartado un paso de CI que regenere y compare: exige herramienta EF y compilación en tiempo de diseño dentro del *pipeline*, y el fichero es derivado, no fuente.

### D6 — Despliegue: dónde van los secretos de R2

**Corrección a la propuesta.** El **paso 4** del §9.0 (`google-cloud-run.md:192`) es de secretos de **GitHub** (`GCP_PROJECT_ID`, `GCP_SA_KEY`) y no cambia. Los valores de R2 son secretos de **Google Secret Manager**, que se provisiona en el **paso 2** (`:190`); allí se documentan, junto a la nota de `roles/secretmanager.secretAccessor` del §8 (`:162`). **El orden de los siete pasos no se altera.**

Reparto en `.github/workflows/ci-cd.yml` (hoy: 7 `env_vars` en `:103-110` y 7 `secrets` en `:111-118`, cero de Cloudflare):

| Destino | Claves |
|---|---|
| `secrets:` (Secret Manager) | `Cloudflare__AccountId`, `Cloudflare__AccessKeyId`, `Cloudflare__SecretAccessKey` |
| `env_vars:` | `Cloudflare__BucketName`, `Cloudflare__PublicCdnBaseUrl`, **`Cloudflare__Simulate=false`** |

🚨 **La línea más importante del eje es `Cloudflare__Simulate=false`.** `CloudflareR2Options.HasValidCredentials` exige `!Simulate` (`:44-49`) y `appsettings.json:136` trae `"Simulate": true`. Sin esa variable, inyectar los tres secretos deja el almacenamiento **en memoria** igual que hoy, y el despliegue parecería correcto.

**También en el paso de Jobs** (`ci-cd.yml:127-141`). Verificado: `Ludeka.Jobs/Program.cs:30` llama a `AddLudekaApplicationCore`, que incluye `AddLudekaExternalIntegrations` (`LudekaServiceCollectionExtensions.cs:352-356`); es decir, los cuatro Cloud Run Jobs resuelven `IImageStorageService` por la misma factoría. Si no reciben las credenciales, la catalogación nocturna escribiría imágenes en memoria y las perdería al terminar el proceso. `Media__LocalStoragePath` **no** debe fijarse para los Jobs: no comparten sistema de ficheros con el servicio web.

## 3. Hueco de evidencia externo — sondas de Cloud Run

**No está verificado que `google-github-actions/deploy-cloudrun@v2` permita configurar sondas de *liveness* y *readiness*.** El §2.4 del documento de partida lo da por hecho; INC-47 se comió un hueco idéntico con las banderas de `gcloud run jobs deploy` (`google-cloud-run.md:193` y el aviso de `:210-211`).

**Camino de degradación**, en este orden: (1) verificar las entradas de la acción **antes** de `sdd-apply` —es una lectura de documentación, no exige GCP—; (2) si no existen, intentarlo por la cadena `flags:` (`ci-cd.yml:102`), lo cual es **igualmente no verificado**; (3) si ninguna de las dos se confirma, **provisión manual documentada**: subsección nueva de sondas en el §8 de `google-cloud-run.md`, junto a la nota de *readiness* que ya está en `:164`, con la misma forma de aviso que usó INC-47.

**Ninguna otra decisión depende de esta capacidad.** D4 se verifica íntegramente con pruebas unitarias locales; el cableado de sondas puede entregarse como documentación sin bloquear nada.

## 4. Flujo de datos (selección de medios)

```
appsettings / env  ──►  CloudflareR2Options.HasValidCredentials ──sí──► CloudflareR2StorageService ──► R2/CDN
                                      │no
                                      ▼
                        MediaOptions.HasLocalStoragePath ──sí──► PhysicalFileImageStorageService
                                      │no                              │ escribe {raíz}/games/...
                                      ▼                                ▼ devuelve /images/games/...
                        SimulatedImageStorageService            UseLudekaMediaFiles
                        (ConcurrentDictionary en memoria)       PhysicalFileProvider @ /images  ──► 200
                                      │                                │
                                      └──────────► StorageHealthCheck ─┘   (misma precedencia, /ready)
```

## 5. Cambios por fichero

| Fichero | Acción | Descripción |
|---|---|---|
| `src/Ludeka.Application/Options/MediaOptions.cs` | Crear | `LocalStoragePath`, `HasLocalStoragePath`, `SectionName = "Media"` |
| `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs` | Modificar | `:28` → `Path.Combine(customPath, "games")` |
| `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs` | Modificar | `:273` `Configure<MediaOptions>`; `:277` factoría con ruta; `:278-286` tres vías |
| `src/Ludeka.Web/Extensions/MediaStaticFilesExtensions.cs` | Crear | `UseLudekaMediaFiles` |
| `src/Ludeka.Web/WebStartupGuards.cs` | Crear | Guarda 1 espejada, función pura |
| `src/Ludeka.Web/Health/DatabaseHealthCheck.cs` | Renombrar | Desde `SqliteDatabaseHealthCheck.cs`; proveedor real |
| `src/Ludeka.Web/Health/StorageHealthCheck.cs` | Modificar | Sonda de la vía de medios seleccionada |
| `src/Ludeka.Web/Program.cs` | Modificar | `:104-105` nombres; `:108-117` guarda; `:180-183` medios |
| `src/Ludeka.Web/appsettings.json` | Modificar | `Database:RequirePostgreSqlInProduction`, sección `Media` |
| `Dockerfile` | Modificar | `:64` fuera `ConnectionStrings__DefaultConnection` |
| `docker-compose.yml` | Modificar | `:11` `Production` → `Staging` + rótulo de entorno local |
| `docker-compose.staging.yml` | Modificar | Rótulo de entorno local (solo comentario) |
| `.github/workflows/ci-cd.yml` | Modificar | `Cloudflare__*` en `:103-118` y en `:139-140`; sondas según §3 |
| `docs/database/supabase_schema.sql` | Regenerar | Script idempotente + banner nuevo |
| `tests/Ludeka.IntegrationTests/PostgresFixture.cs` | Modificar | Quinta `CollectionDefinition` |
| `tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs` | Crear | 34 tablas / 7 migraciones + invariante |
| `tests/Ludeka.UnitTests/…` | Crear/Modificar | Medios, guarda, sondas, frescura del script; `Health/HealthChecksTests.cs` renombrados |
| `docs/specs/sistema/09-arquitectura-y-despliegue.md`, `README.md` | Modificar | Cifras, nombres de sondas, tablas de INC-47 |
| `docs/deployment/google-cloud-run.md` | Modificar | §8 sondas + secretos de R2 en el paso 2 |

## 6. Contratos

```csharp
public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public string LocalStoragePath { get; set; } = string.Empty;
    public bool HasLocalStoragePath => !string.IsNullOrWhiteSpace(LocalStoragePath);
}

public static class WebStartupGuards
{
    public static string? Evaluate(IConfiguration configuration, DatabaseOptions databaseOptions, string? environmentName);
}

public static class MediaStaticFilesExtensions
{
    public static IApplicationBuilder UseLudekaMediaFiles(this IApplicationBuilder app, MediaOptions options, IHostEnvironment environment);
}
```

## 7. Estrategia de pruebas

| Capa | Qué se prueba | Cómo |
|---|---|---|
| Unitaria | Las tres vías de selección, incluidas las combinaciones (R2 gana sobre disco; disco gana sobre memoria) | `ServiceCollection` real + `ValidateOnBuild`, como `WebHostHostedServiceCompositionTests.cs:53-68` |
| Unitaria | Ruta/URL de portada coherentes con `customPath` (defecto de D1) | Escritura en directorio temporal y comparación con la URL devuelta |
| Unitaria | Guarda: `Production`+SQLite falla nombrando `SUPABASE_DB_CONNECTION`; fuera de `Production` pasa; clave a `false` pasa | Espejo de `StartupGuardsTests.cs` (89 líneas, 3 pruebas) |
| Unitaria | `DatabaseHealthCheck` reporta el proveedor efectivo; `StorageHealthCheck` da `Healthy`/`Healthy`/`Degraded` en las tres vías | Instanciación directa, como `HealthChecksTests.cs:161,211` |
| Unitaria | El script SQL contiene un `CREATE TABLE` por tabla del modelo | Búsqueda de texto sobre el fichero real |
| HTTP | Una imagen escrita por el fallback se descarga con **200** | Host mínimo en proceso que compone **solo** `UseLudekaMediaFiles` |
| Integración | 34 tablas y 7 migraciones sobre `postgres:17-alpine` limpio | `PostgresFixture` en colección `postgres-real-schema` |

**Decisión sobre el arnés HTTP.** Se descarta `WebApplicationFactory<Program>`: el repositorio ya lo rechazó con motivos nombrados (manifiesto de activos, resolución de *content root* entre proyectos, claves de `DataProtection` en disco — `WebHostHostedServiceCompositionTests.cs:23-31`), y `Ludeka.IntegrationTests.csproj:24` solo referencia `Ludeka.Infrastructure`. La prueba vive en `Ludeka.UnitTests`, que sí referencia `Ludeka.Web` (`:25`) y por tanto arrastra el *framework* de ASP.NET Core, como demuestran los `using Microsoft.AspNetCore.*` ya presentes en `tests/Ludeka.UnitTests/Web/`.
**No verificado**: que ese arrastre transitivo baste para usar `WebApplication.CreateBuilder` sin añadir `Microsoft.AspNetCore.TestHost` o un `<FrameworkReference Include="Microsoft.AspNetCore.App" />` al `csproj`. Degradación: añadir esa línea al `csproj` de pruebas. Es el punto más frágil del incremento.

## 8. Matriz de amenazas

Aplicable: el diseño introduce **enrutamiento** nuevo (un `PhysicalFileProvider` que expone por HTTP un directorio configurable) y toca el **arranque del proceso** (guarda que aborta).

| Frontera | Casos adversarios mínimos | Aplicabilidad | Respuesta de diseño | Pruebas RED previstas |
|---|---|---|---|---|
| Rutas de tipo documentación | `requirements.txt`, Markdown ejecutable, `README.sh` | **N/A**: el proveedor de ficheros no clasifica ni ejecuta nada; `StaticFileOptions` sirve bytes con el tipo MIME conocido y devuelve 404 para extensiones desconocidas | — | — |
| Selección de repositorio git | `git -C`, rutas relativas y absolutas | **N/A**: no hay automatización de git en el código de aplicación | — | — |
| Estado del índice | preparado, `commit -a`, índice vacío | **N/A**: sin automatización de commits | — | — |
| Estado de push | rama de seguimiento, primer push, refspec | **N/A**: sin automatización de push | — | — |
| Órdenes de PR | `--head` explícito, prefijo de entorno, órdenes compuestas | **N/A**: el PR lo abre `scripts/sdd-worktree.ps1`, fuera del alcance del código | — | — |
| **Exposición de rutas (fila añadida, aplicable)** | `..%2f` y `%2e%2e/` en la URL; ruta configurada = `/` o la raíz del repositorio; enlace simbólico dentro de la raíz de medios; ruta relativa | **Aplicable** | El `PhysicalFileProvider` acota la raíz y normaliza; la ruta relativa se resuelve contra `ContentRootPath`; no se activa `ServeUnknownFileTypes`; el prefijo `/images` limita la superficie | Petición con recorrido de directorio devuelve 404, no el fichero de fuera de la raíz; ruta relativa resuelve dentro del *content root* |
| **Arranque del proceso (fila añadida, aplicable)** | `ASPNETCORE_ENVIRONMENT` ausente, en minúsculas, o con espacios | **Aplicable** | Comparación `OrdinalIgnoreCase` idéntica a `StartupGuards.cs:47`; ausente ⇒ no es `Production` ⇒ no aborta | Entorno `null`, `"production"` y `"Development"` sobre la misma configuración |

## 9. Entrega y reversión

Cinco rebanadas encadenadas (`auto-chain`, techo duro de 400 líneas). Estimación **medida sobre los ficheros reales**, con el aviso de INC-47 delante: sus 11 rebanadas se estimaron cortas **sin excepción** (peor caso 350 → 1132).

| # | Rebanada | Líneas estimadas | Dónde es más frágil |
|---|---|---|---|
| 1 | Medios: `MediaOptions`, factoría, defecto de `:28`, `UseLudekaMediaFiles`, pruebas | 250–300 | **Muy frágil.** El arnés HTTP no tiene precedente. Si no arranca sin paquete nuevo, partir en 1a (selección + unitarias) y 1b (*middleware* + HTTP) |
| 2 | Fail-fast: guarda, `appsettings`, `Dockerfile`, los dos compose, pruebas | 160–200 | Baja: hay precedente exacto de 89 líneas |
| 3 | Health checks (**depende de la 1**: `StorageHealthCheck` usa `MediaOptions`) | 230–280 | Media: el tamaño real de la actualización de `HealthChecksTests.cs` está estimado, no medido línea a línea |
| 4 | Esquema: prueba de integración, colección, prueba de frescura, script regenerado | ~180 de autoría + **varios cientos generados** | El script generado infla el diff aunque §E lo excluya del recuento de riesgo. Avisar al revisor en la descripción del PR |
| 5 | Documental y despliegue: cifras, nombres de sondas, `ci-cd.yml`, §8/§9.0 | 80–120 | Baja |

**Orden obligatorio**: 1 → 3. Las rebanadas 2 y 4 son independientes. La 5 va **la última**, y con un motivo concreto: ⚠ el criterio de la propuesta «1537 unitarias + 9 de integración» **nace caducado**, porque las rebanadas 1–4 añaden pruebas. La rebanada 5 debe escribir la cifra **medida tras fusionar 1–4**, no la de la propuesta.

`Decision needed before apply: Yes` (verificar las sondas de la acción, §3) · `Chained PRs recommended: Yes` · `400-line budget risk: Medium`.

**Reversión**: revertir el PR de la rebanada basta; los cinco ejes son independientes salvo 1→3. Los dos cambios con efecto sobre un despliegue real —`ENV` del `Dockerfile` y guarda— se revierten restaurando esas líneas. `supabase_schema.sql` es derivado: se regenera con un comando. No existe entorno de producción, así que ninguna reversión implica migrar datos.

## 10. Preguntas abiertas

- [ ] **Bloqueante para la rebanada 5**: ¿`deploy-cloudrun@v2` admite sondas? Resolver antes de `sdd-apply` (§3). No bloquea las rebanadas 1–4.
- [ ] **No bloqueante**: ¿se acepta la duplicidad `Database:RequirePostgreSqlInProduction` / `Workers:RequirePostgreSqlInProduction`, o el maintainer prefiere unificar en un incremento posterior? El diseño asume que se acepta y se declara como deuda.
- [ ] **No bloqueante**: `StorageHealthCheck` en `Degraded` devuelve 200 en `/ready` (D4). Se asume deliberado; si el maintainer quiere 503, exige `ResultStatusCodes` explícito en `Program.cs:210`.
