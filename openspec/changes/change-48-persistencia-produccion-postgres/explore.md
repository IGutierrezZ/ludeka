# Exploración — INC-48: Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental

> Cambio `change-48-persistencia-produccion-postgres` · Fase `sdd-explore` · 2026-09-19
> Worktree `C:\repos\ludeka-wt\persistencia-produccion-postgres` · Rama `inc/persistencia-produccion-postgres` · Base `62f5b78`
> Almacén: **hybrid** — este fichero más la observación Engram `sdd/change-48-persistencia-produccion-postgres/explore` (#473).
> Documento de partida: `docs/increments/inc-48-persistencia-produccion-postgres.md` (193 líneas, escrito el 2026-09-15 15:04:49).

---

## 0. Cómo leer este documento

El encargo de esta fase no era proponer alcance, sino **auditar el §1 del documento de incremento contra el código real**. Ese §1 se escribió el 2026-09-15; desde entonces se han mergeado INC-46, INC-49 e INC-47 (26 PRs). El resultado de la auditoría es que **el documento de partida ya no describe el repositorio**: sus cifras están desfasadas, casi todas sus citas `fichero:línea` son incorrectas, y una parte sustancial del trabajo que declara pendiente ya está hecha.

El §5 recoge las correcciones que el orquestador introdujo sobre el informe del subagente tras re-verificarlo. **Prevalecen sobre cualquier afirmación contraria en los §1–§4.**

---

## 1. Hallazgo transversal: la corrección documental ya se hizo, y antes que el propio documento

La corrección documental que el §1.6 y el §2.5 del documento describen como pendiente **ya se aplicó casi por completo**, y se aplicó **antes de que el documento INC-48 existiera**.

| Hora del 2026-09-15 | Suceso |
|---|---|
| 12:49:21 | Se reescriben `DEPLOYMENT_GUIDE.md`, `09-arquitectura-y-despliegue.md`, `README.md` (sistema) y `ROADMAP_MVP_SLICES.md`, y se añade el banner de advertencia a `supabase_schema.sql` (Engram #273). |
| 15:04:49 | Se escribe el documento INC-48, cuyo §1.6 sigue describiendo el estado **previo** a esa corrección (Engram #275). |

Verificado abriendo cada fichero: **ningún documento del repositorio afirma hoy que producción use SQLite.** El criterio de aceptación §4.7 ya se cumple.

---

## 2. Veredictos sobre el §1, con evidencia medida hoy

| Afirmación del documento | Veredicto | Evidencia actual |
|---|---|---|
| §1.1 «`LudekaDbContext` declara **31 `DbSet`** (`:8-38`)» | **REFUTADA** | Hoy **34 `DbSet`**. Triple confirmación: 34 `DbSet` declarados, **34 nombres de tabla distintos** en `LudekaDbContextModelSnapshot.cs`, y 27+4+1+0+0+1+1 = **34** `CreateTable` sumando las 7 migraciones. |
| §1.1 «la **única** migración existente creaba 27 tablas» | **VIGENTE como dato histórico** | `20260914001323_InitialSupabasePostgres.cs` tiene exactamente 27 `CreateTable`. Pero hoy hay **7 migraciones**, no una. |
| §1.1 faltaban 4 tablas + 2 columnas de `Games` + `BggDiscoveryCount` | **YA RESUELTA** | `20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages.cs` añade exactamente esas 4 tablas. `Game.cs:24-25` tiene `BackCoverImageUrl`/`TableImageUrl`. |
| §1.1 «`Program.cs:329-332` ejecuta `MigrateAsync()`» | **VIGENTE / CITA DESFASADA** | `Program.cs` tiene **327 líneas**: la cita es imposible. `MigrateAsync()` está en **`Program.cs:148`**, hoy condicionado por `if (db.Database.IsNpgsql())`. |
| **§1.1 titular: «producción arrancaba con un esquema incompleto»** | **YA RESUELTA** | Modelo y migraciones están sincronizados: 34 = 34. Lo que nunca se ha hecho es la verificación contra una Supabase real, porque **no existe entorno**. |
| §1.2 `supabase_schema.sql` contradice al modelo | **VIGENTE, Y PEOR DE LO DESCRITO** | Todas las discrepancias listadas siguen ahí, desplazadas +15 líneas por el banner. La divergencia real es mayor: el fichero tiene **27 `CREATE TABLE`** frente a 34 tablas reales. Faltan **7** (`ExternalLogins`, `NotificationOutboxMessages`, `JobExecutionLeases`, `BggCatalogStaging`, `SocialInboxItems`, `MonitoredSocialAccounts`, `GamePriceSnapshots`) y una octava, `NotificationLogs`, solo existe bajo el nombre equivocado `CommunityNotificationLogs`. Además `Games.CoverUrl` (singular, NOT NULL) no corresponde a ninguna de las 4 columnas reales de imagen; `ReleaseYear`≠`YearPublished`; `LanguageDependence`≠`Language`; `TableFootprint`≠`Footprint`; `ExpansionRecipes.ExpansionIds text[]`≠`IncludedExpansionIds uuid[]`. |
| §1.2 marcarlo como no ejecutable a mano | **YA RESUELTA** | El banner ya existe en `docs/database/supabase_schema.sql:1-14`: *«ESTE ARCHIVO NO ES LA FUENTE DE VERDAD»*, y remite a INC-48 por nombre. |
| §1.3 precedencia R2 → memoria, sin paso por disco | **VIGENTE / CITA DESFASADA** | La lógica es idéntica pero se movió de `Program.cs:158-166` a **`LudekaServiceCollectionExtensions.cs:278-286`** (extracción de INC-47). `PhysicalFileImageStorageService` está registrado en la línea 277 y **nunca se selecciona**. |
| §1.3 el workflow no inyecta `Cloudflare__*` | **VIGENTE** | `grep -c "Cloudflare__" .github/workflows/ci-cd.yml` → **0**. El paso de despliegue declara 7 `env_vars` y 7 `secrets`, ninguno de Cloudflare. |
| §1.4 el `Dockerfile` fija SQLite por defecto | **VIGENTE / CITA DESFASADA** | Está en el bloque `ENV` de las líneas **61-64**, no en la 52. El contenido no ha cambiado. |
| §1.4 el arranque no falla, cae a SQLite efímero | **VIGENTE** | Sin `IsNpgsql()`, `Program.cs:152` cae a `EnsureCreatedAsync()` sin mirar `ASPNETCORE_ENVIRONMENT`. |
| §1.5 `SqliteDatabaseHealthCheck.cs:23,34,37,41` | **VIGENTE, CITA EXACTA** | El fichero no ha cambiado; las líneas citadas son correctas. |
| §1.5 `StorageHealthCheck.cs:20-22` | **VIGENTE, CITA EXACTA** | El fichero no ha cambiado; las líneas citadas son correctas. |
| §1.5 Cloud Run no configura `/ready` como *readiness* | **VIGENTE** | El paso de despliegue solo pasa `flags` generales, sin sondas. |
| §1.6 seis citas «afirman SQLite en producción» | **YA RESUELTA en 5 de 6** | `09-arq:4` y `:14`, `README.md:16`, `DEPLOYMENT_GUIDE.md` completo y `ROADMAP_MVP_SLICES.md:152` están corregidos y verificados uno a uno. El único residuo vivo es de **configuración, no de prosa**: `Dockerfile`, `docker-compose.yml` y `docker-compose.staging.yml` siguen fijando SQLite. Eso es trabajo del §2.3, no corrección documental. |
| §1.6 «887 frente a 1.005» pruebas | **REFUTADA: ninguna de las dos cifras sigue viva** | Hoy `09-arquitectura-y-despliegue.md:61` dice **1.345** y `README.md:27` dice **1.534**. La cifra real es **1537 unitarias + 9 de integración**. El desfase real es de 192 pruebas en un fichero y de 3 en el otro. |

### Cronología reconstruida

| Fecha y hora | Suceso | Tablas |
|---|---|---|
| 2026-09-15 10:01 | Migración `AddBggStagingSocialInboxPriceRadarAndGameImages` | 27 → 31 |
| 2026-09-15 12:49 | Corrección documental íntegra (Engram #273) | — |
| 2026-09-15 15:04 | **Se escribe el documento INC-48, ya desfasado el mismo día** | — |
| 2026-09-15 16:49 | Migración `AddExternalLogins` (INC-46) | 31 → 32 |
| 2026-09-17 11:21 | Migración `AddProviderEmailVerifiedAtToExternalLogins` (INC-49) | 32 |
| 2026-09-19 00:06–01:27 | Tres migraciones de INC-47 | 32 → **34** |

---

## 3. Estado real de cada eje del §2

| Eje | Estado | Qué falta de verdad |
|---|---|---|
| **§2.1** Esquema canónico | Modelo y migraciones **ya sincronizados** (34 = 34) | Decidir el destino de `supabase_schema.sql` (§3.2) y aceptar que la verificación contra Supabase real **no es ejecutable**: no existe entorno. |
| **§2.2** Precedencia de medios | Binaria (R2 → memoria) | `PhysicalFileImageStorageService` ya existe y ya escribe en disco. Falta: la opción de configuración (`Media__LocalStoragePath` **no existe hoy en ningún sitio del repositorio**), la selección de 3 vías, el aviso explícito en `Production` sin R2, **el middleware que sirva esos ficheros por HTTP** (ver §5.1) y las pruebas. |
| **§2.3** Fallback silencioso a SQLite | Sin resolver | Existe un patrón probado y con pruebas en `Ludeka.Jobs`. Portarlo a `Ludeka.Web` implica refactor: la lógica de arranque del host web está inline en `Program.cs`, no extraída a una clase testeable. |
| **§2.4** Health checks fieles | Sin resolver en código | Ya documentado como pendiente en dos ficheros. Atención: `/ready` expone hoy **3** comprobaciones (`sqlite_db`, `storage`, `notification_queue`), no 2 — la tercera es de INC-47 y el documento no la menciona. |
| **§2.5** Corrección documental | **Mayormente hecha** | Solo queda sincronizar las cifras de pruebas (1.345 y 1.534 → 1537+9) y ampliar en `09-arq` la lista de tablas con las que añadió INC-47. |
| **§2.6** Pasos operativos de R2 | Sin cambios de código | Los pasos ya constan casi palabra por palabra en `DEPLOYMENT_GUIDE.md §10.1` y `google-cloud-run.md §8`. |
| **§2.7** Pruebas | Ninguna de las 5 existe | Precedente de estilo y tamaño: `StartupGuardsTests.cs` (89 líneas, 3 pruebas). |

---

## 4. Evidencia para las cuatro decisiones del §3

No se decide nada aquí: la decisión es del maintainer. Esto es lo que dice hoy el repositorio.

1. **SQLite en desarrollo local.** `docker-compose.yml` **no tiene ningún servicio PostgreSQL**. La alternativa estricta no es un cambio de configuración: es infraestructura nueva.
2. **`supabase_schema.sql`.** `MigrateAsync()` ya crea el esquema en producción (`Program.cs:148`), así que la opción «eliminarlo» tiene base técnica real. «Regenerarlo» es mecánico vía `dotnet ef migrations script`. Reescribirlo a mano son 478 líneas.
3. **Backups.** Los tres ficheros candidatos a retirar existen exactamente como los nombra el documento: `deploy/backup-sqlite.ps1`, `deploy/backup-sqlite.sh`, `deploy/restore-sqlite.sh`. `scripts/supabase-backup.ps1` también existe y ya está documentado como el procedimiento real.
4. **`docker-compose.staging.yml`.** Hoy es SQLite puro (`ludeka_staging.db`, sin servicio PostgreSQL), igual que `docker-compose.yml`.

---

## 5. Correcciones del orquestador sobre el informe del subagente

Re-verificadas contra el repositorio el 2026-09-19. **Estas correcciones prevalecen.**

### 5.1. El fallback local en disco **no se sirve por HTTP** — hallazgo nuevo, no presente en el documento

El informe afirmaba que `PhysicalFileImageStorageService` «ya se sirve por `wwwroot/images` vía `app.MapStaticAssets()` (`Program.cs:323`)». **Es falso.**

`src/Ludeka.Web/Program.cs:323` llama a `app.MapStaticAssets()` y **`UseStaticFiles()` no aparece en ningún punto de `src/`**. La documentación oficial de ASP.NET Core es explícita:

> *«Because `MapStaticAssets` only serves assets listed in the manifest, it doesn't serve files that aren't part of the manifest. Files aren't part of the manifest when they're located outside the build-time web root, such as files served from disk... To serve files that aren't in the manifest, call `UseStaticFiles`, which serves files directly from the web root at runtime.»*

`PhysicalFileImageStorageService` escribe en `wwwroot/images/games/` **en tiempo de ejecución** (`:33-34`), así que esos ficheros no están en el manifiesto de compilación y **devolverían 404**.

**Consecuencia de alcance:** el criterio de aceptación §4.4 del documento («en desarrollo, el fallback local escribe realmente en disco **y la imagen se sirve por HTTP**») **no se cumple hoy y no se cumpliría** con solo recablear la selección de dependencias. Añadir `UseStaticFiles()` —o un `StaticFileOptions` apuntando a la ruta configurada— es una tarea adicional del §2.2 que el documento no contempla, y necesita su propia prueba.

### 5.2. La guarda de migraciones de `Ludeka.Jobs` es la Guarda **2**, y el código de salida no vive donde se decía

El informe situaba la guarda en «`StartupGuards.cs`, Guarda 1, exit code 3». Dos imprecisiones:

- La guarda de migraciones pendientes es la **Guarda 2** («Esquema al día», `StartupGuards.cs:56-67`). La Guarda 1 es la de coherencia de proveedor.
- `StartupGuards.EvaluateAsync` **no devuelve un código de salida**: devuelve un `string` con el motivo, o `null` si todo está bien. El **código 3** se emite en `src/Ludeka.Jobs/Program.cs:67` y `:73`, que es quien llama a la guarda.

La afirmación de fondo —que `Ludeka.Jobs` nunca migra y aborta con código 3 si hay migraciones pendientes— **es correcta y queda verificada**, y el patrón sigue siendo el modelo a espejar en `Ludeka.Web`.

### 5.3. Confirmaciones tras re-verificación

Comprobadas de primera mano y **correctas** en el informe: el barrido documental completo del §1.6; el banner de `supabase_schema.sql`; las cifras 1.345 y 1.534; las 4 colecciones de `PostgresFixture.cs`; la inexistencia de `LocalStoragePath` en todo el repositorio; la ausencia de servicio PostgreSQL en `docker-compose.yml` y `docker-compose.staging.yml`; los cero `Cloudflare__` en `ci-cd.yml`; las 89 líneas de `StartupGuardsTests.cs`; y el recuento 34 = 34 = 34.

---

## 6. Riesgos

| Riesgo | Detalle |
|---|---|
| **Partir de cifras muertas** | Si `sdd-propose` o `sdd-tasks` arrancan de «31 tablas» o de «a `supabase_schema.sql` le faltan 4 tablas», subestimarán la auditoría de columnas. Son 34 tablas y faltan 7, más una mal nombrada. |
| **Colisión de colecciones xUnit** | `PostgresFixture.cs` ya usa 4 `CollectionDefinition` separadas precisamente para aislar pruebas que migran o que asertan filas exactas. Cualquier prueba nueva que verifique `__EFMigrationsHistory` o el recuento de tablas sobre una base recién creada **necesita su propia colección**, no reusar `postgres-real`. |
| **Verificación imposible contra Supabase** | El criterio §4.1 exige verificar contra una Supabase de *staging*. **No existe entorno.** Hay que reformular ese criterio o aceptar que queda diferido. |
| **Estimaciones cortas** | Las 11 rebanadas de INC-47 se estimaron cortas sin excepción, cinco por más del doble (peor caso: 350 → 1132). El eje más frágil aquí son las pruebas: no hay precedente en el repositorio para «la imagen sobrevive a la recreación del servicio» ni para verificar esquema contra PostgreSQL real. |

---

## 7. Deuda de INC-47: fuera de radio

Ninguno de los cuatro huecos del `docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md` §8 cae dentro del alcance de INC-48: los cuatro son de programación de trabajos y del outbox, no de persistencia ni de medios. El único roce es `OutboxOptions.HealthQueryTimeoutSeconds`, que pertenece al health check del outbox — INC-48 toca los otros dos health checks, así que habrá tentación de arreglarlo de paso. **Queda fuera salvo decisión explícita del maintainer.**
