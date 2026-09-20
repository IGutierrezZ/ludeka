# Informe de Verificación — INC-48: Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental

> **Fase:** `sdd-verify` · **Fecha:** 2026-09-20
> **Cambio:** `change-48-persistencia-produccion-postgres`
> **Estado verificado:** `main` en `57d93cd` (merge del PR #70, última rebanada de la cadena)
> **Worktree de verificación:** `inc/persistencia-produccion-postgres-6-verificacion`, base `57d93cd`
> **Base del incremento:** `62f5b78` (merge del PR #62, cierre de INC-47)

---

## 1. Resumen ejecutivo

**Veredicto global: CONFORME.**

Las **seis** especificaciones delta de INC-48 tienen correspondencia real y localizable en el código y en las pruebas: `media-storage-precedence`, `production-persistence-guard`, `postgres-schema-verification`, `health-checks`, `dockerfile-build` y `docker-compose-orchestration`. Las **44 tareas** de `tasks.md` tienen evidencia directa en ficheros reales; ninguna está marcada sin respaldo.

- **0 hallazgos CRITICAL.**
- **2 WARNING**, ambos huecos de evidencia sobre comportamiento en tiempo de ejecución que queda fuera del alcance de `dotnet test`. Ninguno niega un requisito verificado.
- **3 SUGGESTION**, de bajo riesgo y ya declaradas.

El incremento cerró los cuatro defectos de producción que lo motivaron. Conviene dejar constancia de que **solo uno de los cuatro figuraba en el documento de incremento de partida**; los otros tres los destapó la auditoría de `sdd-explore`.

---

## 2. Evidencia de pruebas

### 2.1. Medición del verificador

Ejecutada en el worktree de verificación, con redirección a fichero para leer el código de salida real (un `| tail` devuelve el código de `tail`, no el de `dotnet`, y en este mismo incremento ya nos ocultó una prueba en rojo):

```
dotnet test Ludeka.sln > "$TEMP/inc48-verify-tests.log" 2>&1; echo "EXIT REAL: $?"
```

```
EXIT REAL: 0
Correctas! - Con error: 0, Superado:    10, Omitido: 0, Total:    10 - Ludeka.IntegrationTests.dll (net10.0)
Correctas! - Con error: 0, Superado:  1565, Omitido: 0, Total:  1565 - Ludeka.UnitTests.dll (net10.0)
```

### 2.2. Medición independiente del orquestador (contraste)

Ejecutada **en paralelo y en un directorio distinto** (`C:\repos\Ludeka`, mismo commit `57d93cd`), precisamente para no firmar la cifra del subagente sin contraste propio:

```
EXIT REAL ORQUESTADOR: 0
Correctas! - Con error: 0, Superado:    10, Omitido: 0, Total:    10 - Ludeka.IntegrationTests.dll (net10.0)
Correctas! - Con error: 0, Superado:  1565, Omitido: 0, Total:  1565 - Ludeka.UnitTests.dll (net10.0)
```

**Las dos mediciones coinciden exactamente.** Docker estuvo disponible en ambas: Testcontainers levantó `postgres:17-alpine` y las 10 pruebas de integración —incluida `PostgresSchemaVerificationTests`, que migra desde una base vacía— se ejecutaron de verdad, no se omitieron.

### 2.3. Magnitud real del incremento

```
git diff --shortstat 62f5b78 57d93cd -- . ":!openspec"
 32 files changed, 2908 insertions(+), 548 deletions(-)
```

Coincide con la tabla «Cambios por fichero» de `design.md` §5, sin desbordamiento de alcance ni ficheros olvidados.

---

## 3. Conformidad requisito a requisito

### 3.1. `media-storage-precedence`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| R2 válido gana la precedencia | CONFORME | `LudekaServiceCollectionExtensions.cs:290-292`; `MediaStorageSelectionTests.cs:56-69` |
| Sin R2, la ruta en disco gana a memoria | CONFORME | `LudekaServiceCollectionExtensions.cs:295-298`; `MediaStorageSelectionTests.cs:71-80` |
| Sin R2 ni ruta local, memoria | CONFORME | `LudekaServiceCollectionExtensions.cs:301`; `MediaStorageSelectionTests.cs:82-88` |
| El fallback en disco se sirve por HTTP con 200 | CONFORME | `MediaStaticFilesExtensions.cs:24-56`, cableado en `Program.cs:213`; `MediaStaticFilesDeliveryTests.cs:42-74` (Kestrel real, bytes comparados uno a uno) |
| URL nunca guardada devuelve 404 | CONFORME | `MediaStaticFilesDeliveryTests.cs:76-93` |
| Aviso de degradación en Production sin R2 | CONFORME | `MediaStorageWarnings.cs:15-30`; `Program.cs:133-137`; `MediaStorageWarningsTests.cs:31-45` |
| Claves `Cloudflare__*` en el despliegue | CONFORME | `ci-cd.yml:116-118`, `:127-129`, `:150-151` |

**Regresión del defecto latente confirmada:** `_gamesDirectory = Path.Combine(customPath, "games");` (`PhysicalFileImageStorageService.cs:33`), con prueba dedicada que además comprueba que el fichero **no** queda en la raíz de `customPath` (`PhysicalFileImageStorageServiceTests.cs:37-63`). Sin esa línea, el servicio escribía en un directorio y publicaba la URL de otro: un 404 encadenado al de la entrega HTTP.

**Matriz de amenazas ejercitada de verdad:** `MediaStaticFilesDeliveryTests.cs:100-103` es un `[Theory]` con dos cargas de recorrido de directorio, más la resolución de ruta relativa en `:127-128`. Verdes.

### 3.2. `production-persistence-guard`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| Production con PostgreSQL resoluble arranca | CONFORME | `WebStartupGuards.cs:20-46`; `WebStartupGuardsTests.cs:30-39` |
| Production sin PostgreSQL falla explícitamente | CONFORME | `WebStartupGuardsTests.cs:41-51`; guarda en `Program.cs:116-123`, antes de tocar la base de datos |
| Entornos distintos de Production no la activan | CONFORME | `WebStartupGuardsTests.cs:53-75` (`Development`, `Staging`, `null`) |
| Comparación de entorno insensible a mayúsculas | CONFORME | `WebStartupGuardsTests.cs:77-87` |
| La imagen no impone cadena de conexión por defecto | CONFORME | `Dockerfile:65-67` |
| Sin variable inyectada, Production falla en vez de degradar | CONFORME CON RESERVAS | Composición verificada por lectura más pruebas unitarias; **no se construyó la imagen Docker real** (ver §6) |

### 3.3. `postgres-schema-verification`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| Migrar desde cero crea exactamente las tablas del modelo | CONFORME | `PostgresSchemaVerificationTests.cs:42-86`, ejecutada contra `postgres:17-alpine` real |
| El historial de migraciones iguala al número de ficheros | CONFORME | Mismo test, `:64-68` |
| Colección xUnit propia, sin colisión de esquema | CONFORME | `PostgresFixture.cs:142-145`, quinta y distinta de las cuatro existentes (`:80`, `:95`, `:111`, `:126`) |
| Canario literal de 34 tablas y 7 migraciones | CONFORME | `PostgresSchemaVerificationTests.cs:29`, `:32`, `:84-85` |
| El script derivado no diverge del modelo | CONFORME | 35 `CREATE TABLE` = 34 tablas del modelo + `__EFMigrationsHistory`; `SupabaseSchemaFreshnessTests.cs:22-45` |

El aislamiento en colección propia no es cosmético: `[Collection("postgres-real")]` comparte una sola base y un solo `__EFMigrationsHistory` sin garantía de orden en xUnit, así que una prueba que migra desde cero y asevera recuentos exactos no puede convivir con las que siembran bajo esquema ya migrado.

### 3.4. `health-checks`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| `database` reporta el proveedor real, no un literal | CONFORME | `DatabaseHealthCheck.cs:33`; `HealthChecksTests.cs:192-208` |
| `storage` comprueba el almacén realmente configurado | CONFORME | `StorageHealthCheck.cs:37-57`; `HealthChecksTests.cs:241-312` (R2, disco y memoria) |
| `/ready` con dependencias sanas | CONFORME | `Program.cs:243-266` |
| `/ready` ante degradación | CONFORME CON RESERVAS | Depende del valor por defecto de `HealthCheckOptions.ResultStatusCodes`; sin prueba HTTP de extremo a extremo (ver §6) |
| Sondas de despliegue en Cloud Run | CONFORME | `ci-cd.yml:107` con `--liveness-probe` y `--readiness-probe`; documentado en `google-cloud-run.md:166-186` |

### 3.5. `dockerfile-build`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| Sin cadena de conexión por defecto en `ENV` | CONFORME | `Dockerfile:65-67` |
| Resto de variables estándar sin cambios | CONFORME | `Dockerfile:73-90` |

Verificación textual; no se ejecutó `docker build` real.

### 3.6. `docker-compose-orchestration`

| Requisito / Escenario | Veredicto | Evidencia |
|---|---|---|
| `docker-compose.yml` no dispara la guarda | CONFORME | `docker-compose.yml:16`; prueba dedicada `WebStartupGuardsTests.cs:89-103` |
| `docker-compose.staging.yml` ya usa `Staging` | CONFORME | `docker-compose.staging.yml:15` |
| Comentario explícito de entorno local | CONFORME | Cabeceras de ambos ficheros |

Este requisito existe porque la guarda nueva habría roto `docker compose up` en local: el fichero defaulteaba a `Production`.

---

## 4. Cobertura de tareas (44/44)

Todas las líneas marcadas en `tasks.md` tienen correspondencia real comprobada contra el código, no copiada del propio documento:

- **PR1a (1.1–1.8):** `MediaOptions.cs`, `MediaStorageSelectionTests.cs`, `PhysicalFileImageStorageServiceTests.cs:37-63`, `MediaStorageWarningsTests.cs`, factoría en `LudekaServiceCollectionExtensions.cs:277-302`, corrección en `PhysicalFileImageStorageService.cs:33`, aviso en `Program.cs:133-137`.
- **PR1b (2.1–2.7):** `MediaStaticFilesDeliveryTests.cs` con sus cuatro escenarios, `MediaStaticFilesExtensions.cs`, cableado en `Program.cs:213`.
- **PR2 (3.1–3.9):** `WebStartupGuardsTests.cs` (6 pruebas), `WebStartupGuards.cs`, invocación en `Program.cs:116-123`, `appsettings.json:16`, `Dockerfile:65-67`, `docker-compose.yml:16`, `docker-compose.staging.yml`, y la unificación de resolución de rutas en `MediaOptions.ResolveLocalStoragePath` sin duplicados residuales.
- **PR3 (4.1–4.7):** `HealthChecksTests.cs:192-208`, `:241-312`; `DatabaseHealthCheck.cs`; `StorageHealthCheck.cs`; `Program.cs:106`.
- **PR4 (5.1–5.5):** `PostgresFixture.cs:142-145`, `PostgresSchemaVerificationTests.cs`, `SupabaseSchemaFreshnessTests.cs`, `supabase_schema.sql` regenerado.
- **PR5 (6.1–6.8):** `google-cloud-run.md:166-186`, recuento remedido, `ci-cd.yml:107-129`, `:150-154`, y las afirmaciones falsas de documentación corregidas.

---

## 5. Hallazgos

| # | Severidad | Hallazgo |
|---|---|---|
| 1 | WARNING | El comportamiento HTTP de `/ready` ante `Degraded` depende del valor por defecto de `HealthCheckOptions.ResultStatusCodes` del framework, y no hay prueba de extremo a extremo propia que lo fije. Hoy devuelve **200**, no 503. Es coherente con el diseño (D4), pero es una decisión que hoy sostiene un valor por defecto ajeno, no una prueba nuestra. |
| 2 | WARNING | Ningún artefacto Docker (`docker build`, `docker run`, `docker compose up`) se ejercitó en este ciclo. La conformidad de `dockerfile-build`, `docker-compose-orchestration` y un escenario de `production-persistence-guard` se apoya en inspección textual más pruebas unitarias que replican la configuración efectiva. |
| 3 | SUGGESTION | La nota de Engram de `apply-progress` (#493) declara pendiente la corrección de «32 tablas» en `09-arquitectura-y-despliegue.md:30`. Ya está hecha en `6f40b77`: la línea dice **34**. La nota es anterior al commit; no debe arrastrarse como riesgo abierto. |
| 4 | SUGGESTION | `6f40b77` corrigió además `DEPLOYMENT_GUIDE.md:227` y `google-cloud-run.md:116`, que la tarea 6.7 no anclaba literalmente. Trabajo correcto por encima del alcance textual. |
| 5 | SUGGESTION | `LudekaServiceCollectionExtensions.cs:283` usa `GetService<IHostEnvironment>()` (anulable) en vez de `GetRequiredService`. Desviación ya declarada en PR1a; riesgo bajo, todo host real lo registra. |

---

## 6. Huecos de evidencia y riesgos residuales

1. **No existe entorno de producción.** No hay proyecto de GCP ni base de Supabase. Los pasos de despliegue de `ci-cd.yml` están condicionados a `has_gcp == 'true'`, que exige `GCP_PROJECT_ID` y `GCP_SA_KEY`; sin ellos se omiten. **Ninguna spec de este incremento está acreditada contra producción real:** lo verificado es conformidad de código, pruebas y configuración.
2. **Las sondas de Cloud Run se acreditaron solo contra documentación.** Se confirmó que `gcloud run deploy` admite `--liveness-probe` y `--readiness-probe`, y que `--readiness-probe` no acepta `initialDelaySeconds` ni `--liveness-probe` acepta `successThreshold`. Nunca se ejecutó un despliegue real que lo confirme.
3. **Docker no se ejercitó** en este ciclo (hallazgo 2).
4. **`/ready` en degradación** no está fijado por prueba propia (hallazgo 1).
5. **Duplicidad de claves** `Database:RequirePostgreSqlInProduction` y `Workers:RequirePostgreSqlInProduction`: deuda declarada y aceptada en el diseño D3, no defecto. Ambos hosts comparten un único `appsettings.json` por enlace de proyecto pero cada uno lee su propia clave.
6. **Fallo silencioso documentado, no eliminado:** inyectar las tres credenciales de R2 sin fijar `Cloudflare__Simulate=false` deja el almacenamiento en memoria sin ningún síntoma. El despliegue parecería correcto y las imágenes se seguirían perdiendo. Está advertido en `DEPLOYMENT_GUIDE.md:227`, pero sigue siendo un pie de fábrica esperando a quien despliegue.

---

## 7. Auditoría del orquestador sobre este informe

El contrato del proyecto obliga a no firmar las afirmaciones de un subagente sin contrastarlas. Se re-ejecutaron de forma independiente:

| Afirmación comprobada | Resultado |
|---|---|
| Suite completa y código de salida | **Coincide**: 1565/1565 + 10/10, exit 0, medido en directorio distinto |
| `PhysicalFileImageStorageService.cs:33` | **Correcta** |
| `Program.cs:213` (`UseLudekaMediaFiles`) y `:116` (guarda) | **Correctas** |
| `ci-cd.yml:107` con ambas sondas | **Correcta** |
| Los cuatro escenarios de `MediaStaticFilesDeliveryTests.cs` | **Correctos**, incluido el `[Theory]` de recorrido de directorio en `:100-103` |
| `git diff --shortstat 62f5b78 57d93cd`: 32 ficheros, 2908/548 | **Correcta** |
| 35 `CREATE TABLE` en `supabase_schema.sql` | **Correcta** |
| El verificador no mutó el repositorio | **Confirmado**: `git status --porcelain` vacío |

Este fichero lo escribió el orquestador: el agente `sdd-verify` no dispone de herramienta de escritura.

---

## 8. Veredicto

**CONFORME.** INC-48 puede pasar a `sdd-archive`.

Los dos WARNING son huecos de evidencia en tiempo de ejecución, fuera del alcance de `dotnet test`, y no incumplimientos de requisitos. La suite está en verde con cifras medidas dos veces por vías independientes.

**Lo que este incremento acredita:** que el código, la configuración y las pruebas son coherentes con «producción es PostgreSQL y Cloudflare R2; SQLite es solo motor de pruebas y desarrollo local».
**Lo que no acredita, y conviene no confundir:** que eso funcione en un entorno de producción que todavía no existe.
