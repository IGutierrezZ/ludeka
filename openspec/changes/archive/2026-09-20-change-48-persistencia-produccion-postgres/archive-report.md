```yaml
schema: gentle-ai.archive-report/v1
evidence_revision: sha256:1d97fdba0294a547cb9d44bfefaada40536c572298ec28870f13f01cb7f847ea
verdict: archived
status: pass_with_warnings
test_count: 1575
test_passed: 1575
test_failed: 0
test_skipped: 0
build_exit_code: 0
verification_date: 2026-09-20
prs_merged: 8
```

# Informe de Archivo — INC-48: Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental

> **Cambio:** `change-48-persistencia-produccion-postgres` (INC-48)  
> **Fase:** `sdd-archive`  
> **Fecha:** 2026-09-20  
> **Estado:** Archivado  
> **Veredicto de verificación:** `pass_with_warnings`  
> **Entregables:** 8 Pull Requests fusionadas a `main` en `0a320e6`

---

## 1. Resumen de Entrega

El incremento INC-48 se entregó en **8 Pull Requests** fusionadas secuencialmente a `main`:

1. **PR #63** — Estabilización de un test `flaky` preexistente (preparación)
2. **PR #64** — Artefactos de planificación (`proposal.md`, `spec.md`, `design.md`, `tasks.md`)
3. **PR #65** (PR1a) — Precedencia de almacenamiento de tres vías, corrección de la subcarpeta `games` y aviso de degradación
4. **PR #66** (PR1b) — Entrega HTTP del fallback en disco, extensiones de `Program.cs`
5. **PR #67** (PR2) — Guarda de arranque en Production, configuración de Docker y `docker-compose`
6. **PR #68** (PR3) — Health checks fieles al proveedor y almacén real
7. **PR #69** (PR4) — Verificación de esquema PostgreSQL real, regeneración de `supabase_schema.sql`
8. **PR #70** (PR5) — Volcado de verdad documental, sondas de Cloud Run, ajuste de documentación
9. **PR #71** — Informe de verificación (cierre de verificación)

**Magnitud:** 32 ficheros modificados, 2.908 inserciones, 548 borrados (excluyendo `openspec/`).

---

## 2. Veredicto de Verificación

**Estatus global: CONFORME** (`pass_with_warnings`)

### 2.1 Evidencia de pruebas

| Métrica | Resultado |
|---------|-----------|
| Pruebas unitarias | **1.565/1.565** (100%, verde) |
| Pruebas de integración | **10/10** (100%, verde) |
| Total suite | **1.575/1.575** |
| Errores | **0** |
| Omitidas | **0** |
| Código de salida | **0** |
| Compilación | **exit 0, 0 errores, 13 advertencias** |

**Verificación independiente:** La suite fue medida tres veces por vías distintas (verificador en worktree, orquestador en repositorio raíz, y vuelto a medir en worktree de archivado). Las tres mediciones coinciden exactamente.

### 2.2 Conformidad de requisitos

**11 de 11 requisitos verificados.**  
**25 de 25 escenarios verificados.**

- `media-storage-precedence` — 3 requisitos, 7 escenarios, **CONFORME**
- `production-persistence-guard` — 2 requisitos, 4 escenarios, **CONFORME**
- `postgres-schema-verification` — 2 requisitos, 5 escenarios, **CONFORME**
- `health-checks` — 2 requisitos (MODIFIED + ADDED), 6 escenarios, **CONFORME**
- `dockerfile-build` — Modificado (Escenario 3), **CONFORME**
- `docker-compose-orchestration` — ADDED requisito, **CONFORME**

### 2.3 Hallazgos

- **0 CRITICAL**
- **2 WARNING** — Huecos de evidencia en tiempo de ejecución (Docker, sondas HTTP), no incumplimientos
- **3 SUGGESTION** — Riesgos bajos, ya declarados

Ver `verify-report.md` §5 y §6 para el detalle completo de hallazgos.

---

## 3. Defectos Cerrados

INC-48 cerró **cuatro defectos de producción**. Solo el primero figuraba en el documento de incremento de partida; los otros tres fueron destapados por la auditoría de `sdd-explore`.

### 3.1 Defecto 1: Imágenes se guardaban en memoria

**Problema:** El selector de `IImageStorageService` era binario (R2 o memoria). Existía `PhysicalFileImageStorageService` pero nunca se seleccionaba, por lo que toda imagen caía a memoria aunque hubiera disco disponible.

**Solución:** Precedencia de tres vías en `LudekaServiceCollectionExtensions.cs:277-302`:  
1. R2 válido → `CloudflareR2StorageService`  
2. Disco local configurado → `PhysicalFileImageStorageService`  
3. Fallback → `SimulatedImageStorageService`

**Verificación:** `MediaStorageSelectionTests.cs:56-88` (3 escenarios, verdes).

---

### 3.2 Defecto 2: Fallback en disco devolvía 404

**Problema:** Dos causas encadenadas:  
a) `MapStaticAssets()` solo sirve el manifiesto de compilación, no ficheros escritos en tiempo de ejecución.  
b) `PhysicalFileImageStorageService` escribía en la raíz de la ruta pero publicaba la URL con subcarpeta `games`.

**Solución:**  
a) Nueva extensión `MediaStaticFilesExtensions.cs` con `UseLudekaMediaFiles()` (PhysicalFileProvider, `/images`, `ServeUnknownFileTypes=false`), cableada en `Program.cs:213`.  
b) Corrección en `PhysicalFileImageStorageService.cs:33`: unificación de ruta de escritura y lectura.

**Verificación:** `MediaStaticFilesDeliveryTests.cs:42-128` (HTTP 200, 404 correcto, recorrido de directorios), verdes.

---

### 3.3 Defecto 3: Arranque silencioso a SQLite efímera en Production

**Problema:** Sin variable de conexión inyectada, `Program.cs:146-157` caía silenciosamente a `"Data Source=ludeka.db"` (SQLite) incluso con `ASPNETCORE_ENVIRONMENT=Production`, perdiendo datos al reiniciar.

**Solución:** Guarda de arranque en `WebStartupGuards.cs:20-46` (espejo de `src/Ludeka.Jobs/StartupGuards.cs:44-54`), invocada en `Program.cs:116-123` antes de tocar la base de datos:
- Si `Environment == "Production"` → exige `Database.ProviderName.Contains("Npgsql")`
- Si no → lanza excepción explícita, sin crear SQLite

**Verificación:** `WebStartupGuardsTests.cs:30-87` (6 escenarios, verdes).

---

### 3.4 Defecto 4: Sondas de salud mienten

**Problema:** `DatabaseHealthCheck` reportaba `"provider": "Microsoft.EntityFrameworkCore.Sqlite"` como literal, incluso con PostgreSQL. `StorageHealthCheck` extraía un directorio de `Data Source=` de la cadena de conexión, sin relación con `IImageStorageService`.

**Solución:**  
a) `DatabaseHealthCheck.cs:33` → lee `_dbContext.Database.ProviderName` del contexto real  
b) `StorageHealthCheck.cs:37-57` → verifica el almacén realmente configurado (R2, disco local, memoria)

**Verificación:** `HealthChecksTests.cs:192-312` (todos los backends, verdes).

---

## 4. Especificaciones Integradas

Las seis especificaciones delta de INC-48 fueron fusionadas en el almacén canónico (`openspec/specs/`):

- **NUEVAS:** `media-storage-precedence/spec.md`, `production-persistence-guard/spec.md`, `postgres-schema-verification/spec.md`
- **EXTENDIDAS:** `health-checks/spec.md` (MODIFIED + ADDED), `dockerfile-build/spec.md`, `docker-compose-orchestration/spec.md`

Todas conservan los marcadores `## Requirements`, `### Requirement:`, `#### Scenario:`, `GIVEN`/`WHEN`/`THEN` literales en inglés.

---

## 5. Entregables Secundarios

### 5.1 Verificación de esquema PostgreSQL

- Colección xUnit propia (quinta colección, `PostgresFixture.cs:142-145`) sin colisión de base de datos
- Prueba `PostgresSchemaVerificationTests.cs:42-86` contra `postgres:17-alpine` real (Testcontainers)
- Asevera exactamente **34 tablas** y **7 migraciones** (un CREATE TABLE en `__EFMigrationsHistory` por cada fichero de migración)

### 5.2 Esquema documental (`docs/database/supabase_schema.sql`)

- Regenerado con `dotnet ef migrations script` (forzando `Database__Provider=PostgreSql`)
- **35 sentencias `CREATE TABLE`** (34 tablas del modelo + `__EFMigrationsHistory`)
- Conserva el banner que advierte que no es la fuente de verdad
- Guardado por `SupabaseSchemaFreshnessTests.cs:22-45` (impide divergencia futura)

### 5.3 Sondas de Cloud Run

- `ci-cd.yml:107`: `--liveness-probe /healthz` y `--readiness-probe /ready`
- Documentadas en `google-cloud-run.md:166-186`
- **Verificadas contra documentación**, no contra despliegue real (ver §6)

### 5.4 Unificación de rutas locales

- `MediaOptions.ResolveLocalStoragePath` (en `src/Ludeka.Application/Options/MediaOptions.cs`) es la única resolución compartida
- Ambos hosts (`Ludeka.Web` y `Ludeka.Jobs`) acceden a la misma lógica

---

## 6. Huecos de Evidencia Abiertos

Conforme al `verify-report.md` §6, los siguientes huecos **no bloquean el archivado** pero quedan abiertos para post-verificación o post-despliegue:

### 6.1 No existe entorno de producción

No hay proyecto de GCP ni base de Supabase reales. El pipeline de CI/CD está condicionado a `has_gcp == 'true'` (exige `GCP_PROJECT_ID` y `GCP_SA_KEY` en secretos). **Ninguna especificación de INC-48 está acreditada contra producción real.**

### 6.2 Sondas de Cloud Run verificadas solo por documentación

Se confirmó que `gcloud run deploy` admite `--liveness-probe` y `--readiness-probe`, y sus limitaciones (sin `initialDelaySeconds`, etc.). Nunca se ejecutó un despliegue real que lo confirme.

### 6.3 Docker no fue ejercitado

No se ejecutó `docker build`, `docker run` ni `docker compose up` en este ciclo. La conformidad de `dockerfile-build`, `docker-compose-orchestration` y un escenario de `production-persistence-guard` se apoya en inspección textual más pruebas unitarias.

### 6.4 Comportamiento HTTP de `/ready` ante degradación

El código HTTP que `/ready` devuelve ante `Degraded` depende del valor por defecto de `HealthCheckOptions.ResultStatusCodes` del framework (.NET devuelve `200`, no `503`). No hay prueba HTTP de extremo a extremo propia que lo fije. El diseño es coherente, pero sostenido por un valor por defecto ajeno.

### 6.5 Fallo silencioso con R2

Si se inyectan las tres credenciales de Cloudflare R2 pero se omite `Cloudflare__Simulate=false`, el almacenamiento seguirá siendo memoria sin síntoma alguno. El despliegue parecería correcto. Está advertido en `DEPLOYMENT_GUIDE.md:227`, pero sigue siendo un pie de fábrica esperando a quien despliegue.

---

## 7. Magnitud Verificada

```
git diff --shortstat 62f5b78 57d93cd -- . ":!openspec"
 32 files changed, 2908 insertions(+), 548 deletions(-)
```

Coincide exactamente con la tabla de cambios de `design.md` §5, sin desbordamiento de alcance ni ficheros olvidados.

---

## 8. Trabajo Diferido

No hay trabajo diferido de este incremento. Los huecos abiertos (§6) se resuelven con post-verificación operativa, no con más código.

La duplicidad de claves `Database:RequirePostgreSqlInProduction` y `Workers:RequirePostgreSqlInProduction` fue declarada como deuda aceptada en el diseño (D3) y no es defecto: ambos hosts comparten un único `appsettings.json` por enlace de proyecto pero cada uno lee su propia clave.

---

## 9. Próximos Pasos

INC-48 está listo para:
1. ✅ **Volcado a la especificación viva** (`docs/specs/sistema/35-persistencia-produccion-y-medios-con-fallback.md`) — Tarea D
2. ✅ **Actualización del índice maestro** (`docs/specs/sistema/README.md`) — Tarea E
3. ✅ **Actualización del roadmap** (`docs/increments/ROADMAP.md`) — Tarea F
4. ✅ **Traslado del documento de incremento al archivo** (`docs/increments/archive/`) — Tarea G

Tras completarse, todas las especificaciones y documentación del cambio habrán sido integradas en el almacén permanente del sistema, y el cambio quedará cerrado archivado.

---

**Generado por:** `sdd-archive`  
**Fecha de archivo:** 2026-09-20  
**Observación Engram:** topic_key `sdd/change-48-persistencia-produccion-postgres/archive-report`
