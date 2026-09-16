# Apply Progress: change-46-autenticacion-real (INC-46) — Fases F0, F1, F2, F2-bis, F3 y F4

> Fase SDD `sdd-apply`. Store: openspec (este archivo + `tasks.md`).
> Worktree: `C:\repos\ludeka-wt\autenticacion-real`.
> · Slice **PR-1 / F0**: rama `inc/autenticacion-real`, mergeado a `main` en `3ec23d5`.
> · Slice **PR-2 / F1**: rama `inc/autenticacion-real-f1`, base `3ec23d5`, mergeado a `main` en `06e53cc`.
> · Slice **PR-3 / F2**: rama `inc/autenticacion-real-f2`, base `06e53cc`, mergeado a `main` en `83063bd` (PR #14).
> · Slice **PR-5/PR-6 / F3**: rama `inc/autenticacion-real-f3`, base `83063bd`, mergeado a `main` en `51e16f3` (PR #15).
> · Slice **PR-7 / F4**: rama `inc/autenticacion-real-f4`, base `51e16f3`, mergeado a `main` en `fc9e50b` (PR #16).
> · Slice **F2-bis** (este): rama `inc/autenticacion-real-permisos`, base `fc9e50b` (`origin/main`); cierra
>   el punto a confirmar de 2.10/2.11 (permiso granular propio para `/admin/eventos` y `/admin/notificaciones`).
> Modo: **TDD estricto**. Runner contractual: `dotnet test Ludeka.sln`. Cadena `stacked-to-main`.
> Alcance de este slice: dos banderas nuevas de dominio, dos políticas y la migración de las dos páginas.
> **Sin** `sdd-archive` (F5).

## Estado por fase

| Fase | Tareas | Estado |
|---|---|---|
| F0 — Artefactos y dominio de permisos | 6/6 | ✅ mergeada en `main` (`3ec23d5`) |
| F1 — Persistencia de identidad externa | 8/8 | ✅ mergeada en `main` (`06e53cc`) |
| F2 — Autenticación social y autorización por política | 13/13 | ✅ mergeada en `main` (`83063bd`) |
| F2-bis — Permiso granular de eventos y notificaciones | 6/6 | ✅ completada (este slice) |
| F3 — Retirada de la identidad simulada | 8/8 | ✅ mergeada en `main` (`51e16f3`) |
| F4 — Revalidación en escritura y anonimia | 6/6 | ✅ mergeada en `main` (`fc9e50b`, PR #16) |
| F5 — Verificación, documentación y archive | 0/6 | ⬜ sin tocar |

## Estado F2-bis: COMPLETADO ✅

> Slice `inc/autenticacion-real-permisos`, base `fc9e50b` (`origin/main`). Cierra el punto a confirmar
> de las tareas 2.10/2.11 y la decisión abierta de `design.md` §9.3: `/admin/eventos` y
> `/admin/notificaciones` dejan la política de rol `RolModerador` y pasan a dos permisos granulares
> propios. Sin migraciones, sin cambios de esquema, sin tocar F5.

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 2b.1 + 2b.2 `CanManageEvents` y `CanManageNotifications`; `All` = 4095 | ✅ | ROJO por aserción (1023 vs 4095) → ROJO por compilación (CS0117) → VERDE 26/26 focal | `2a5b5dc` |
| 2b.3 + 2b.4 Políticas `PermisoGestionarEventos`/`PermisoGestionarNotificaciones` y migración de las dos páginas | ✅ | ROJO ejecutable en `PolicyAuthorizationTests` (12/58) + ROJO por compilación (CS0117) del contrato de páginas → VERDE 79/79 focal (59 + 20) | `2aac16e` |
| 2b.5 `dotnet test Ludeka.sln --configuration Release` verde | ✅ | VERDE **1247/1247** en Release (0 errores, 0 omitidas; base 1231, +16) | (docs) |
| 2b.6 Humo real (criterio de F2 repetido) | ✅ | `/admin/eventos` y `/admin/notificaciones` **302** a `/login`; públicas **200**; 0 excepciones no controladas | (docs) |

### Commits del slice F2-bis (rama `inc/autenticacion-real-permisos`, base `fc9e50b`)

| Sha | Mensaje | Cambios |
|---|---|---|
| `2a5b5dc` | `feat(core): añadir los permisos granulares de eventos y notificaciones` | 2 archivos · +37/−3 |
| `2aac16e` | `feat(web): dar permiso granular propio a eventos y notificaciones` | 5 archivos · +35/−14 |
| (docs) | `docs(sdd): registrar el progreso de apply del slice de permisos granulares` | `tasks.md` + este archivo |

### TDD Cycle Evidence (F2-bis)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2b.1 + 2b.2 | `Application/GranularPermissionsTests.cs` | Unit (dominio) | ✅ 1231/1231 base | ✅ ROJO por aserción `All_WithTwelveGranularFlags_EqualsExpectedMask` (esperado 4095, actual 1023; 1/21 en rojo) y ROJO por compilación CS0117 de los dos miembros | ✅ 26/26 focal | ✅ Teoría de las 12 banderas dentro de `All`, posiciones de bit 1024/2048 (contrato de persistencia) e invariante `All_ShouldEqualTheOrOfEveryDeclaredFlag` sobre `Enum.GetValues` | ➖ Ninguno necesario (enum declarativo) |
| 2b.3 + 2b.4 | `Web/PolicyAuthorizationTests.cs` | Integration (SQLite `:memory:` + `IAuthorizationService` real) | ✅ 1231/1231 base | ✅ 12/58 en rojo: política inexistente en anónimo, comunidad, fundador, suspendida, mapeo bandera→política y conteo de las 11 | ✅ 79/79 focal (59 + 20 del contrato) | ✅ Once políticas × cinco escenarios + denegación cruzada entre las dos banderas nuevas + revocación en caliente | ✅ Lista congelada actualizada y doc de clase alineada con las 11 políticas |
| 2b.3 + 2b.4 | `Web/AuthorizationPipelineContractTests.cs` | Contract (fuente) | ✅ 1231/1231 base | ✅ ROJO por compilación CS0117 (`PermisoGestionarEventos`/`PermisoGestionarNotificaciones`) al apuntar la tabla de páginas | ✅ 20/20 del archivo | ✅ Las dos páginas se contrastan contra su `@page` y su `@attribute [Authorize(Policy = …)]` reales | ➖ Ninguno necesario |

### Work Unit Evidence (F2-bis)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U2b-A Banderas nuevas / `2a5b5dc` | `--filter FullyQualifiedName~GranularPermissionsTests` → **26/26** | N/A: solo dominio y contrato de bits; el efecto se comprueba en U2b-B | Revertir el commit: desaparecen las dos banderas y `All` vuelve a 1023; ninguna política ni página las usa en este punto |
| U2b-B Políticas y páginas / `2aac16e` | `--filter FullyQualifiedName~PolicyAuthorizationTests\|FullyQualifiedName~AuthorizationPipelineContractTests` → **79/79** | Arranque real (`Production`, SQLite temporal, sin credenciales OAuth): `/admin/eventos` y `/admin/notificaciones` **302** a `/login?ReturnUrl=…`; `/`, `/catalogo`, `/eventos`, `/radar` y `/healthz` **200**; 0 excepciones no controladas | Revertir el commit: las dos páginas vuelven a `RolModerador` y desaparecen las dos políticas |

### Verificación observada (registro) — F2-bis

| Comando / comprobación | Resultado observado |
|---|---|
| Línea base antes de tocar código: `dotnet test Ludeka.sln --configuration Release` en `fc9e50b` | **1231 correctas, 0 con error, 0 omitidas** |
| `dotnet test Ludeka.sln --configuration Release` (final) | **1247 correctas, 0 con error, 0 omitidas** (+16: 5 del enum, 11 de las políticas) |
| Arranque real (`ASPNETCORE_ENVIRONMENT=Production`, SQLite temporal en `%TEMP%`, `Database__SeedDemoData=false`, sin credenciales OAuth, `PORT=5187`, `dotnet bin\Release\net10.0\Ludeka.Web.dll`) | Matriz solicitada sin ningún **500**: `/` **200**, `/catalogo` **200**, `/eventos` **200**, `/radar` **200**, `/healthz` **200**, `/admin/eventos` **302** → `/login?ReturnUrl=/admin/eventos`, `/admin/notificaciones` **302** → `/login?ReturnUrl=/admin/notificaciones`; 0 excepciones no controladas en el log |
| Comprobación aritmética del enum | 12 banderas declaradas, **12 bits únicos** y OR de las 12 = **4095**; el OR de `All` enumera 12 términos y su comentario dice 4095 |
| Búsqueda de `RolModerador` en `src/` y `tests/` (`*.cs`, `*.razor`) | **3 resultados**: la constante y su registro en `AuthorizationPolicies.cs`, y la aserción de `PolicyAuthorizationTests` que fija que sigue declarada. **Ninguna página la declara** |
| Búsqueda del literal `1023` en el repositorio | **1 resultado restante**: la nota histórica R4 de este `tasks.md`. El único test que dependía del valor (`GranularPermissionsTests`) ya exige 4095; ningún dato persistido depende del literal (bits 10 y 11 son nuevos, sin desplazamiento) |

### Desviaciones y hallazgos (F2-bis)

1. **`design.md` §9.3 queda superado por decisión del maintainer**: el diseño proponía `PermisoGestionarEditores` (`CanManagePublishers`) para `/admin/eventos` y `/admin/notificaciones`; el maintainer pidió permiso granular propio, así que se añadieron dos banderas nuevas. Queda registrado en R3 de `tasks.md`.
2. **`RolModerador` se conserva**: ya no lo declara ninguna página, pero la constante, su registro en `Configure` y su prueba siguen en pie por indicación explícita (otros incrementos pueden depender de él). No se eliminó ni se tocaron sus pruebas.
3. **Público efectivo preservado**: quien puede pasar las políticas nuevas es la Mesa Fundadora (por rol) y un Moderador con la bandera exacta; el marcado de las dos páginas sigue condicionado a `IsFoundingTeam || IsInRole("Moderator")`, que es un subconjunto de ese público, de modo que nadie autorizado ve el aviso de acceso restringido.
4. **Hueco preexistente (fuera del alcance exclusivo, se reporta)**: `UserPermissionsModal.razor` solo ofrece 8 casillas, no incluye `CanManageUsers`, `CanViewAuditLog`, `CanPublishInstagram` ni las dos banderas nuevas; además reconstruye la máscara desde esas 8 casillas, así que guardar desde el modal **borra** los permisos que no muestra. Hoy no hay forma de conceder `CanManageEvents`/`CanManageNotifications` desde la interfaz. `UserManagementService.GetPermissionNames` también enumera 7 banderas para las insignias. Es un defecto anterior a este slice (F2 lo dejó así al añadir dos banderas), no una regresión.
5. **Sin migraciones ni cambios de esquema**: `ModeratorPermission` se persiste como entero; las banderas nuevas ocupan bits vírgenes y no desplazan ningún valor guardado. La frontera de reversión es exacta (`fc9e50b`).

## Estado F4: COMPLETADO ✅

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 4.1 RED de `AnonymityPolicyTests` (denegación sin sesión en los servicios de identidad) | ✅ | ROJO por aserción: 24/34 en rojo sobre los servicios sin guarda → VERDE 34/34 focal | `3d23381` |
| 4.1 RED de `SessionIdentity` | ✅ | ROJO por compilación (CS0103: `SessionIdentity` no existe) → VERDE 7/7 focal | `3d23381` |
| 4.1 Invariante de las 9 entidades | ✅ | 9 teorías × 3 variantes (vacío, espacios, nulo) en verde con los constructores existentes (aprobación del invariante, no RED) | `3d23381` |
| 4.2 Guarda de sesión en los 15 servicios | ✅ | `SessionIdentity.Require` en partidas, colección, préstamos, reseñas, reportes, preguntas, auditoría, preferencias, BGG y directorios | `3d23381` |
| 4.2 Verificación de `UserLocationService` y `UserLibraryStatsService` | ✅ | Auditados: solo lecturas, ya toleran `UserId` vacío; ninguna entidad de identidad se instancia | `3d23381` |
| 4.3 Redirección a `/login` y `/u/` sin identidad | ✅ | ROJO por compilación de `LoginRedirect` (CS1061/CS0103) y ROJO por contrato de marcado (11/17) → VERDE 17/17 focal | `0ccf1b8` |
| 4.4 Auditoría solo con identidades reales | ✅ | `AuditService.RecordChangeAsync` exige identidad y nombre no vacíos; `UserManagementService` exige sesión antes del privilegio | `3d23381` |
| 4.4 Retirada del revisor centinela `admin-system` | ✅ | ROJO por contrato de marcado del guardián de bandeja social → VERDE | `0ce34f6` |
| 4.5 `dotnet test Ludeka.sln` verde | ✅ | VERDE **1231/1231** en Release (0 errores, 0 omitidas) | (docs) |
| 4.6 Smoke de navegador (criterio 11) | ✅ | Clic real anónimo «En mi ludoteca» → `/login?ReturnUrl=%2Fjuegos%2Fbrass-birmingham`; voto en Q&A → mismo destino; estado sin cambios | (docs) |
| Handoff F3: render de las tres páginas protegidas | ✅ | ROJO por aserción → VERDE 4/4 focal: 77 usos de `<Icon>` rendidos con sus atributos reales | `3bc5b8b` |

### Commits del slice F4 (rama `inc/autenticacion-real-f4`, base `51e16f3`)

| Sha | Mensaje | Cambios |
|---|---|---|
| `3d23381` | `feat(application): exigir sesion real en las escrituras con identidad` | 22 archivos · +1149/−35 |
| `0ccf1b8` | `feat(web): llevar al acceso las acciones con identidad sin sesion` | 13 archivos · +574/−67 |
| `3bc5b8b` | `test(web): renderizar los iconos de las paginas protegidas con sus atributos reales` | 1 archivo · +210 |
| `0ce34f6` | `fix(web): retirar el revisor centinela de la bandeja social` | 1 archivo · +11/−2 |
| (docs) | `docs(sdd): registrar el progreso de apply de la fase F4 de INC-46` | `tasks.md` + este archivo |

### Servicios a los que se añadió guarda de sesión (F4)

| # | Servicio | Guarda |
|---|---|---|
| 1 | `GamePlayLogService` (`RecordPlayAsync`, `DeletePlayAsync`) | `SessionIdentity.Require(_currentUserService)` |
| 2 | `UserLibraryService` (colección, jugado, préstamos, reseñas) | `SessionIdentity.Require` en las 6 escrituras |
| 3 | `GameIssueReportService.CreateReportAsync` | guarda + identidad forzada a la sesión |
| 4 | `RuleQAService` (pregunta, respuesta, votos, respuesta aceptada) | `SessionIdentity.Require(userId)` |
| 5 | `AuditService.RecordChangeAsync` | identidad y nombre no vacíos |
| 6 | `SqliteUserPreferenceService` (tema, país) | `SessionIdentity.Require(userId)` |
| 7 | `BggImportService.ImportUserCollectionAsync` | guarda antes de llamar a BGG |
| 8 | `BggSearchAssistedService.AddGameToCollectionAsync` | guarda antes de llamar a BGG |
| 9 | `MediaService.EnsurePermission` | guarda + permiso |
| 10 | `CreatorService.EnsurePermission` | guarda + permiso |
| 11 | `PublisherService.EnsurePermission` | guarda + permiso |
| 12 | `StoreService.EnsurePermission` | guarda + permiso |
| 13 | `GameEditorService.UpdateGameAsync` | guarda antes del permiso y de la bitácora |
| 14 | `UserManagementService.EnsureFoundingTeam` | guarda antes del privilegio |
| 15 | `FoundingVerdictService` (guardar, borrar, síntesis IA) | guarda antes del rol |
| — | `SocialInboxModeration` (UI) | revisor centinela `admin-system` retirado |

## TDD Cycle Evidence (F4)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 4.1 (entidades) | `Application/AnonymityPolicyTests.cs` | Unit (dominio) | ✅ 1142/1142 base | ➖ Invariante preexistente (9 entidades × 3 variantes) | ✅ 27/27 | ✅ Vacío, espacios y nulo por entidad | ➖ Ninguno necesario |
| 4.1 + 4.2 | `Application/AnonymityPolicyTests.cs` | Integration (SQLite `:memory:` + repositorios reales) | ✅ 1142/1142 base | ✅ 24/34 en rojo por aserción (denegación ausente; hoy `ArgumentException` o escritura) | ✅ 34/34 focal | ✅ Cada escritura denegada + camino con sesión que sí persiste con el `UserId` de la sesión | ✅ `SessionIdentity` como punto único de decisión |
| 4.1 + 4.2 | `Application/SessionIdentityTests.cs` | Unit (contrato) | ✅ 1142/1142 base | ✅ CS0103 `SessionIdentity` no existe | ✅ 7/7 focal | ✅ Servicio nulo, identidad vacía, espacios, nulo, normalización con recorte | ✅ Dos sobrecargas (`ICurrentUserService` y `string`) sobre una única regla |
| 4.2 | `Application/AnonymityPolicyPrivilegedServicesTests.cs` | Integration (SQLite `:memory:`) | ✅ 1142/1142 base | ✅ 5/9 en rojo: con privilegios declarados y sin sesión se escribía y se rompía al auditar | ✅ 9/9 focal | ✅ Cada servicio deniega sin sesión pero escribe con sesión y permiso | ✅ Reutilización del `TextWriter` de auditoría y de los dobles de catálogo/verificador |
| 4.3 | `Web/LoginRedirectTests.cs` | Unit (NavigationManager instrumentado) | N/A (API nueva) | ✅ CS1061/CS0103 `TryRedirectToLogin` y `LoginRedirect` | ✅ 6/6 focal | ✅ Denegación, fallo ajeno, excepción nula, ya en `/login`, `ReturnUrl` escapado, `forceLoad` | ✅ Resolución de `ReturnUrl` en una única función privada |
| 4.3 | `Web/SessionDenialUiContractTests.cs` | Contract (fuente) | N/A (contrato nuevo) | ✅ 11/17 en rojo (las 8 superficies sin traducción y MyLibrary sin invitación) | ✅ 17/17 focal | ✅ Ocho componentes + invitación al acceso + ausencia de `/u/` vacío | ✅ `LoginRedirect` en `_Imports.razor` como extensión compartida |
| Handoff F3 | `Web/ProtectedPagesIconRenderTests.cs` | Component (HtmlRenderer real) | N/A (prueba nueva) | ✅ ROJO por aserción (nombre de icono fuera del catálogo y expectativas de precedencia) | ✅ 4/4 focal | ✅ 77 combinaciones reales de atributos + nombre desconocido + precedencia de `aria-hidden` | ✅ Renderizador instanciado una vez por prueba y extracción de atributos en un único helper |

## Work Unit Evidence (F4)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U4-A Guardas en aplicación / `3d23381` | `--filter FullyQualifiedName~Anonymity\|FullyQualifiedName~SessionIdentity` → **42/42** | Arranque real (SQLite temporal, `Production`): matriz anónima completa sin 500 y `/mi-ludoteca` 200 | Revertir el commit: `SessionIdentity` desaparece y los servicios vuelven a escribir con `UserId` vacío |
| U4-B Interfaz hacia el acceso / `0ccf1b8` | `--filter FullyQualifiedName~LoginRedirect\|FullyQualifiedName~SessionDenialUi` → **17/17** | Navegador real (Chrome DevTools, SQLite con datos demo, sin sesión): «En mi ludoteca» → **`/login?ReturnUrl=%2Fjuegos%2Fbrass-birmingham`**; voto en Q&A → mismo destino; la ficha sigue sin ítem de colección y con 2 votos | Revertir el commit: `LoginRedirect` desaparece y `MyLibrary` vuelve a construir `/u/` vacío |
| U4-C Handoff F3 / `3bc5b8b` | `--filter FullyQualifiedName~ProtectedPagesIconRender` → **4/4** | Render real de `Icon` con los 77 conjuntos de atributos de las tres páginas protegidas, que además se comprueban protegidas con `[Authorize]` | Revertir el commit: sin cambios de producción, solo desaparece la verificación |
| U4-D Anonimia de la bandeja social / `0ce34f6` | `dotnet test Ludeka.sln --configuration Release` → **1231/1231** | Arranque real: sin sesión las rutas de moderación siguen respondiendo **302** a `/login` | Revertir el commit: vuelve el revisor centinela `admin-system` |

## Verificación observada (registro) — F4

| Comando / comprobación | Resultado observado |
|---|---|
| Línea base antes de tocar código: `dotnet test Ludeka.sln --configuration Release` en `51e16f3` | **1142 correctas, 0 con error, 0 omitidas** |
| `dotnet test Ludeka.sln --configuration Release` (final) | **1231 correctas, 0 con error, 0 omitidas** (+89 casos: 27 del invariante de las 9 entidades, 34 de la política de anonimia, 7 del contrato de `SessionIdentity`, 6 de `LoginRedirect`, 11 de contrato de la interfaz, 4 del render de las páginas protegidas) |
| `dotnet build Ludeka.sln --configuration Release --no-incremental` | **0 errores**; solo los avisos preexistentes (`CS8629` `BggImportService.cs:150` —línea desplazada por la guarda—, `CS8604` `UserCollectionItemTests.cs:35` y `GameLoanTests.cs:38`, `CS8625` `SocialIngestionServiceTests.cs:108` y `GranularPermissionsTests.cs:278`, `xUnit2013` `WebMarkupContractTests.cs:739` y `SocialIngestionServiceTests.cs:257`); ningún aviso nuevo |
| Búsqueda de `usuario-fundador-ludeka|SwitchRole|SwitchUser|DefaultCurrentUserService` en `src/` | **1 resultado**: `UserManagementSeeder.cs:39`, fila de datos demostrativos (`Database:SeedDemoData`) sin ninguna ruta de escritura que la use como identidad |
| Búsqueda de `admin-system` en `src/` | **0 resultados** (centinela retirado de `SocialInboxModeration.razor`) |
| Búsqueda de migraciones nuevas (`git diff --name-only 51e16f3 HEAD -- src/Ludeka.Infrastructure/Migrations`) | **vacío**: sin cambios de esquema |
| Arranque real 1 (`ASPNETCORE_ENVIRONMENT=Production`, SQLite temporal, `Database__SeedDemoData=false`, sin credenciales OAuth, `PORT=5187`, `dotnet bin\Release\net10.0\Ludeka.Web.dll`) | Arranca y sirve `/healthz` **200** y `/ready` **200**; 0 excepciones no controladas en el log |
| Arranque real 2 (criterio 11, `Development` + datos demo, `PORT=5190`) | Chrome DevTools sin sesión sobre `/juegos/brass-birmingham`: clic en «En mi ludoteca» → **`/login?ReturnUrl=%2Fjuegos%2Fbrass-birmingham`**; clic en «Votar esta duda» → **mismo destino**; tras volver a la ficha, la colección sigue vacía y la duda mantiene **2 votos** (ninguna fila escrita) |

### Matriz anónima de códigos de estado (prueba de humo real, `Production` + SQLite)

| Ruta | Sin sesión | Nota |
|---|---|---|
| `/` | **200** | |
| `/catalogo` | **200** | |
| `/radar` | **200** | |
| `/eventos` | **200** | |
| `/novedades` | **200** | |
| `/sorteos` | **200** | |
| `/creadores` | **200** | |
| `/editoriales` | **200** | |
| `/transparencia` | **200** | |
| `/login` | **200** | Sin proveedores configurados: muestra el aviso de configuración |
| `/healthz` | **200** | Sonda anónima preservada |
| `/ready` | **200** | Sonda anónima preservada |
| `/mi-ludoteca` | **200** | Invitación al acceso en el HTML y **ningún** `href="/u/"` |
| `/admin/auditoria` | **302** | → `/login?ReturnUrl=/admin/auditoria` |
| `/admin/usuarios` | **302** | → `/login?ReturnUrl=/admin/usuarios` |
| `/moderacion/reportes` | **302** | → `/login?ReturnUrl=/moderacion/reportes` |
| `/admin/cola-catalogacion` | **302** | → `/login?ReturnUrl=/admin/cola-catalogacion` |
| `/admin/ingesta-social` | **302** | → `/login?ReturnUrl=/admin/ingesta-social` |
| `/admin/canales-monitorizados` | **302** | → `/login?ReturnUrl=/admin/canales-monitorizados` |
| **Total de 500 en la matriz** | **0** | |


## Estado F3: COMPLETADO ✅

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 3.1 RED de `AuthenticatedCurrentUserServiceTests` (sin sesión vacío/denegado, roles y permisos desde `AppUser`, suspendida) | ✅ | ROJO por aserción del contrato (3/3) y por compilación del servicio (CS0234/CS0246) → VERDE 8/8 focal | `e747551`, `be84f03` |
| 3.2 Contrato sin `SwitchRole`/`SwitchUser` y sin cuerpo por defecto en `HasPermission` | ✅ | ROJO por aserción (conmutadores presentes, `HasPermission` no abstracto) → VERDE 3/3 | `e747551` |
| 3.3 `AuthenticatedCurrentUserService` + `UserIdentitySnapshot` + `UserCircuitHandler` (Scoped; claims de `IHttpContextAccessor` en SSR) | ✅ | ROJO por compilación → VERDE 8/8 focal (identidad) + 5/5 focal (circuito) | `be84f03` |
| 3.4 `AddScoped<ICurrentUserService, AuthenticatedCurrentUserService>` y borrado de `DefaultCurrentUserService` | ✅ | VERDE de la suite completa | `be84f03` |
| 3.5 Migración de los 13 dobles y de `FoundingVerdictServiceTests` | ✅ | ROJO por compilación en los 14 ficheros → VERDE | `e747551` |
| 3.6 Retirada de los 5 puntos de UI de simulación | ✅ | Contratos de markup actualizados (`drama`, `IsFoundingTeam`) → VERDE | `e747551` |
| 3.7 `IUserSessionInvalidator`, invalidación desde `UserManagementService` y `SessionGuard` con `forceLoad` | ✅ | ROJO por compilación (CS0246) → VERDE 3/3 focal + 3/3 focal + contrato del guardián | `856e3c4` |
| 3.8 `dotnet test Ludeka.sln` verde | ✅ | VERDE **1142/1142** en Release (0 errores, 0 omitidas) | (docs) |

### Commits del slice F3 (rama `inc/autenticacion-real-f3`, base `83063bd`)

| Sha | Mensaje | Cambios |
|---|---|---|
| `e747551` | `refactor(identity): retirar los conmutadores de rol y usuario del contrato` | 23 archivos · +152/−205 |
| `be84f03` | `feat(web): resolver la identidad desde la sesion y eliminar la simulacion` | 11 archivos · +715/−167 |
| `3d57915` | `fix(web): aceptar atributos adicionales en el componente Icon` | 2 archivos · +14/−5 |
| `856e3c4` | `feat(web): invalidar el circuito cuando la sesion se suspende o cambian sus permisos` | 10 archivos · +299/−1 |
| (docs) | `docs(sdd): registrar el progreso de apply de la fase F3 de INC-46` | `tasks.md` + este archivo |

### Commits de los slices previos

| Slice | Shas |
|---|---|
| F0 (`inc/autenticacion-real`, → `3ec23d5`) | `ff6eaee` specs canónicas y Markdown LF · `c18f652` `CanManageUsers`/`CanViewAuditLog` · `ee582cb` `AuditAction.LinkedFounderIdentity` |
| F1 (`inc/autenticacion-real-f1`, → `06e53cc`) | `b4ab43a` entidad `ExternalLogin` · `21eb78a` persistencia e índice único · `36cb30f` migración PostgreSQL `AddExternalLogins` · `959f222` docs de apply |
| F2 (`inc/autenticacion-real-f2`, → `83063bd`) | `333e37a` cookie y esquemas sociales · `4078b13` vinculación y sesión · `0373876` nueve políticas de permiso · `28b6464` protección de rutas · `70ac33e` antiforgery 400 |
| F3 (`inc/autenticacion-real-f3`, → `51e16f3`) | `e747551` contrato sin conmutadores · `be84f03` identidad de sesión · `3d57915` `Icon` con atributos adicionales · `856e3c4` invalidación de circuito |
| F4 (`inc/autenticacion-real-f4`, este slice) | `3d23381` guardas de sesión en aplicación · `0ccf1b8` interfaz hacia el acceso · `3bc5b8b` render de las páginas protegidas · `0ce34f6` centinela retirado |

## TDD Cycle Evidence (F3)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 3.2 | `Application/CurrentUserContractTests.cs` | Unit (reflexión) | ✅ 1120/1120 base | ✅ 3/3 por aserción: conmutadores presentes y `HasPermission` no abstracto | ✅ 3/3 focal | ✅ Superficie de propiedades y métodos congelada + `HasPermission` abstracto + ausencia de ambos conmutadores | ✅ El test descarta los accesores (`IsSpecialName`) para afirmar solo la superficie real |
| 3.1 + 3.3 | `Web/AuthenticatedCurrentUserServiceTests.cs` | Unit + proyección de claims | ✅ 1122/1122 acumulada | ✅ CS0234/CS0246 (`Ludeka.Web.Services`, `AuthenticatedCurrentUserService`) | ✅ 8/8 focal | ✅ Sin sesión, fundador, moderadora con bandera exacta, comunitaria, suspendida, cookie autenticada, cookie anónima y nombres de rol vacíos | ✅ La regla de permisos se extrae a `ModeratorPermissionRules` y la proyección de claims queda como struct privado |
| 3.3 | `Web/UserCircuitHandlerTests.cs` | Integration (SQLite `:memory:`) | ✅ 1130/1130 acumulada | ✅ CS0246 (`UserCircuitHandler`) | ✅ 5/5 focal | ✅ Sesión conocida, suspendida, anónima, cookie sin fila en BD y fallo del repositorio | ✅ Cookie de cuenta inexistente degrada a anónimo determinista (no cae al `HttpContext` de la conexión) |
| 3.7 | `Web/UserSessionInvalidatorTests.cs` | Unit | ✅ 1135/1135 acumulada | ✅ CS0246 (`InMemoryUserSessionInvalidator`) | ✅ 3/3 focal | ✅ Versión monótona, usuario normalizado, usuario inexistente y cadenas vacías | ✅ Normalización en un único punto (`Normalize`) |
| 3.7 | `Application/UserManagementAndAuditServiceTests.cs` | Unit (dobles) | ✅ 1138/1138 acumulada | ✅ CS0246 (`IUserSessionInvalidator`) | ✅ 3/3 nuevos + 6/6 del archivo | ✅ Cambio de estado invalida, cambio de rol/permisos invalida, y cambio de estado sin efecto **no** invalida | ✅ `InvalidateSession` centraliza el aviso |
| 3.7 | `Web/AuthorizationPipelineContractTests.cs` | Contract (fuente) | ✅ 1141/1141 acumulada | ✅ `SessionGuard.razor` y su montaje aún no existían | ✅ 21/21 del archivo | ✅ Suscripción, `forceLoad: true`, guarda de interactividad, `Dispose` y montaje `<SessionGuard />` en el layout | ➖ Ninguno necesario |

## Work Unit Evidence (F3)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U3-A Contrato y UI sin conmutadores / `e747551` | `dotnet test Ludeka.sln --configuration Release` → **1122/1122** (base 1120 + 3 del contrato − 1 conmutador retirado) | Arranque real (SQLite temporal, `Production`): navegación anónima aún servida por la identidad simulada, sin cambios de comportamiento | Revertir el commit: vuelven los conmutadores al contrato y los 5 puntos de UI; nada más depende de él |
| U3-B Identidad de sesión / `be84f03` | `--filter FullyQualifiedName~AuthenticatedCurrentUserServiceTests` → **8/8** · `--filter FullyQualifiedName~UserCircuitHandlerTests` → **5/5** | Arranque real tras borrar la simulación: 41/41 fichas del catálogo y 11 rutas públicas **200**; 11 rutas protegidas **302** a `/login`; 0 excepciones no controladas en el log | Revertir el commit: vuelve `DefaultCurrentUserService` como Singleton y `Program.cs` recupera su registro |
| U3-B-bis Componente `Icon` / `3d57915` | `--filter FullyQualifiedName~WebMarkupContractTests` → **106/106** (contrato de `Icon` ampliado) | Arranque real: `/radar` pasa de **500** (`Icon ... does not have a property matching the name 'class'`) a **200** con el chip «Solo Mínimos Históricos» renderizado | Revertir el commit: el paso de atributos adicionales desaparece y `/radar` vuelve a fallar; ninguna otra conducta cambia |
| U3-C Invalidación de circuito / `856e3c4` | `--filter FullyQualifiedName~UserSessionInvalidatorTests` → **3/3** · `--filter FullyQualifiedName~UserManagementAndAuditServiceTests` → **8/8** (5 previos + 3 nuevos) | Arranque real con `SessionGuard` montado en el layout: misma matriz de códigos (públicas 200, protegidas 302) y 0 excepciones; el guardián no renderiza nada | Revertir el commit: desaparecen el invalidator, sus llamadas y el guardián; `UserManagementService` recupera su constructor de tres parámetros |

## Verificación observada (registro) — F3

| Comando / comprobación | Resultado observado |
|---|---|
| Línea base antes de tocar código: `dotnet test Ludeka.sln --configuration Release` en `83063bd` | **1120 correctas, 0 con error, 0 omitidas** |
| `dotnet test Ludeka.sln --configuration Release` (final) | **1142 correctas, 0 con error, 0 omitidas** (+22 casos: 3 del contrato, 8 de la identidad, 5 del circuito, 3 del invalidator, 3 de `UserManagementService`, 1 del guardián; −1 del conmutador retirado) |
| Búsqueda de `SwitchRole`/`SwitchUser` en `src/` | **0 resultados** (`.cs` y `.razor`) |
| Búsqueda de `DefaultCurrentUserService` en `src/` y `tests/` | **0 resultados**; el fichero `src/Ludeka.Infrastructure/Services/DefaultCurrentUserService.cs` está eliminado en el árbol |
| Avisos preexistentes de compilación | Sin cambios y sin avisos nuevos: `CS8629` `BggImportService.cs:149`, `CS8604` `UserCollectionItemTests.cs:35` y `GameLoanTests.cs:38`, `CS8625` `SocialIngestionServiceTests.cs:108` y `GranularPermissionsTests.cs` (línea desplazada por la migración del doble), `xUnit2013` `WebMarkupContractTests.cs` y `SocialIngestionServiceTests.cs` |
| Arranque real (`ASPNETCORE_ENVIRONMENT=Production`, SQLite temporal `smoke.db` con 41 fichas y directorios sembrados, `Database__SeedDemoData=false`, sin credenciales OAuth, `dotnet bin\Release\net10.0\Ludeka.Web.dll`) | La aplicación arranca y sirve `/healthz` **200** y `/ready` **200**; el log no contiene ninguna excepción no controlada tras toda la matriz de peticiones |

### Matriz anónima de códigos de estado (prueba de humo real)

| Ruta | Sin sesión | Nota |
|---|---|---|
| `/` | **200** | |
| `/catalogo` | **200** | |
| `/juegos/brass-birmingham` | **200** | Ficha concreta con datos reales |
| Las 41 fichas del catálogo (`/juegos/*` enlazadas desde `/catalogo`) | **200** en 41/41 | Ejercita ficha, galería, hub multimedia, Q&A y veredicto |
| `/radar` | **200** | Antes del arreglo de `Icon`: **500** (pestaña de ofertas) |
| `/sorteos` | **200** | Alias del radar, pestaña de sorteos |
| `/eventos` | **200** | |
| `/novedades` | **200** | |
| `/transparencia` | **200** | |
| `/mi-ludoteca` | **200** | Página pública enlazada desde la cabecera |
| `/creadores` | **200** | Ruta real del directorio de creadores |
| `/editoriales` | **200** | Ruta real del directorio de editoriales |
| `/directorio/creadores` | **404** | **La ruta no existe en la aplicación** (no es un 500): el directorio vive en `/creadores` |
| `/directorio/editoriales` | **404** | **La ruta no existe en la aplicación** (no es un 500): el directorio vive en `/editoriales` |
| `/login` | **200** | |
| `/healthz` · `/ready` | **200** · **200** | Sondas anónimas preservadas |

### Rutas protegidas sin sesión (repetición del criterio de F2)

| Ruta | Sin sesión | Destino |
|---|---|---|
| `/admin/auditoria` | **302** | `/login?ReturnUrl=%2Fadmin%2Fauditoria` |
| `/admin/usuarios` | **302** | `/login?ReturnUrl=%2Fadmin%2Fusuarios` |
| `/moderacion/reportes` | **302** | `/login?ReturnUrl=%2Fmoderacion%2Freportes` |
| `/admin/reportes` | **302** | `/login?ReturnUrl=%2Fadmin%2Freportes` |
| `/admin/multimedia` · `/moderacion-media` · `/moderacion/multimedia` · `/admin/moderacion-medios` | **302** | alias de la misma política |
| `/admin/eventos` · `/admin/notificaciones` | **302** | |
| `/admin/instagram` · `/admin/cola-catalogacion` · `/admin/ingesta-social` · `/admin/canales-monitorizados` | **302** | |

## Verificación observada (registro) — F2 (slice previo)

| Comando / comprobación | Resultado observado |
|---|---|
| `dotnet test Ludeka.sln --configuration Release` | **1120 correctas, 0 con error, 0 omitidas** (línea base `06e53cc`: 1041 → +79 casos nuevos). Avisos preexistentes sin cambios (CS8629 `BggImportService.cs:149`, CS8604 `UserCollectionItemTests.cs:35` y `GameLoanTests.cs:38`, CS8625 `SocialIngestionServiceTests.cs:108`, xUnit2013 `WebMarkupContractTests.cs:736` y `SocialIngestionServiceTests.cs:257`); no se añadió ningún aviso nuevo |
| Arranque real con SQLite y **sin credenciales OAuth** (`ASPNETCORE_ENVIRONMENT=Production`, `Database__SeedDemoData=false`, `ConnectionStrings__DefaultConnection=Data Source=<temp>\ludeka-f2-final.db`, `dotnet bin\Release\net10.0\Ludeka.Web.dll`) | Arranca y sirve `/healthz` **200**; sin avisos de proveedor con la configuración por defecto (los tres `Enabled=false`) |
| Arranque real con `Authentication__Providers__Google__Enabled=true` y **sin** `ClientId`/`ClientSecret` | Arranca igual; aviso `El proveedor de autenticación 'Google' está habilitado pero no tiene ClientId/ClientSecret configurados; su esquema no se registrará…`; `/healthz` **200** |
| `curl /healthz` · `curl /ready` | **200** · **200** (ambas `.AllowAnonymous()`, sin política de fallback) |
| `curl` sin sesión a `/admin/auditoria`, `/admin/usuarios`, `/moderacion/reportes` | **302** → `/login?ReturnUrl=…` en los tres (también el resto de rutas protegidas) |
| `curl /login` | **200** (con `Enabled=false` muestra «El acceso social aún no está configurado» y ningún botón de proveedor) |
| Navegador real (Chrome DevTools) con Google configurado con credenciales de prueba | `/login` renderiza el formulario con `__RequestVerificationToken` y el botón «Continuar con Google»; el desafío redirige a `accounts.google.com` con el `client_id` y el `redirect_uri` del esquema |
| POST `curl` sin token a `/login/external` | **400** (`Token antiforgery ausente o inválido`) y **0** excepciones en el log de la aplicación |
| Proveedores efectivamente registrados con la configuración por defecto | **Ninguno**: `Authentication:Providers:{Google,Discord,Facebook}.Enabled=false` y sin credenciales; Facebook desactivado por defecto |

## Work Unit Evidence (F2 — slice previo)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U2-A Registro / `333e37a` | `dotnet test Ludeka.sln --filter FullyQualifiedName~WebAuthenticationRegistrationTests` → **5/5** | Arranque real con SQLite y `Authentication__Providers__Google__Enabled=true` sin credenciales: la app arranca, registra el aviso y sirve `/healthz` **200** | Revertir el commit: desaparecen opciones, registro y sección de configuración; `appsettings.json` vuelve a su estado previo |
| U2-B Identidad externa / `4078b13` | `dotnet test Ludeka.sln --filter FullyQualifiedName~ExternalLoginServiceTests` → **6/6** | Navegador real: `/login` interactivo con botones y token; clic en Google → 302 a `accounts.google.com` con el `client_id` configurado | Revertir el commit: se eliminan servicio, eventos, pantalla y endpoints; `Program.cs` conserva solo el registro de esquemas |
| U2-C Políticas / `0373876` | `dotnet test Ludeka.sln --filter FullyQualifiedName~PolicyAuthorizationTests` → **48/48** | SQLite `:memory:` con usuarios reales; la revocación fuera de banda deja de autorizar sin reiniciar | Revertir el commit: las páginas quedan sin políticas que las respalden |
| U2-D Protección / `28b6464` + `70ac33e` | `dotnet test Ludeka.sln --filter FullyQualifiedName~AuthorizationPipelineContractTests` → **20/20** | Arranque real: `/healthz` 200, `/ready` 200, `/login` 200 y **302 a `/login`** en las 12 rutas administrativas y de moderación; POST sin token → 400 | Revertir los commits: pipeline, atributos y rutas vuelven atrás; la simulación nunca se tocó |

## TDD Cycle Evidence (F2 — slice previo)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 + 2.2 + 2.3 | `Web/WebAuthenticationRegistrationTests.cs` | Unit + DI real | ✅ 1041/1041 base | ✅ CS0234 `Ludeka.Application.Features.Identity` y CS0246 `ExternalProviderOptions` | ✅ 5/5 focal | ✅ Deshabilitado con credenciales, habilitado sin credenciales, habilitado con credenciales, cookie, `appsettings.json` versionado | ✅ Alias de tipo para evitar la colisión con `Microsoft.AspNetCore.Authentication.AuthenticationOptions`; scopes con guarda anti-duplicado |
| 2.4 + 2.5 | `Application/ExternalLoginServiceTests.cs` | Integration (SQLite `:memory:`) | ✅ 1046/1046 acumulada | ✅ CS0246 `IExternalLoginService` | ✅ 6/6 focal | ✅ Par reincidente, correo verificado con caja distinta, alta comunitaria, correo sin verificar, sin correo, claves vacías | ✅ El correo sin verificar deja de persistirse en `AppUser` (índice único de `Email`) y queda solo en `ExternalLogin.ProviderEmail` |
| 2.7 + 2.8 | `Web/PolicyAuthorizationTests.cs` | Integration (SQLite `:memory:`) | ✅ 1052/1052 acumulada | ✅ CS0246/CS0311 `IAuthorizationHandler`, `IAuthorizationService` | ✅ 48/48 focal | ✅ 9 políticas × anónimo, comunitario, fundador y suspendida + moderador con flag exacto + mapeo bandera→política + revocación en caliente | ✅ `PermissionRequirement` y `LudekaRoleNames` viven junto a las políticas; `Assert.NotEqual` sustituido por `IsNullOrWhiteSpace` (xUnit2000 evitado) |
| 2.9 + 2.10 + 2.11 + 2.12 | `Web/AuthorizationPipelineContractTests.cs` | Contract (fuente) + arranque real | ✅ 1100/1100 acumulada | ✅ 5/20 casos en rojo: páginas sin `[Authorize]`, `Routes.razor` con `RouteView`, sondas sin `AllowAnonymous` | ✅ 20/20 focal (79/79 con el resto de F2) | ✅ 10 páginas + 4 alias + 4 endpoints anónimos + orden del pipeline + `RedirectToLogin` guardado | ✅ Ruta de login tomada del identificador `ExternalAuthenticationSchemes.LoginPath` en el contrato |
| 2.6 (antiforgery) | `Web/AuthorizationPipelineContractTests.cs` + arranque real | Runtime | ✅ 1120/1120 acumulada | ✅ POST sin token → 500 observado con `[FromForm]` | ✅ POST sin token → **400** y 0 excepciones en el log | ✅ POST con token desde el formulario interactivo completa el desafío del proveedor (navegador real) | ✅ Validación explícita de antiforgery antes de leer el formulario |

## Guardas mínimas y arreglos de componente añadidos en F3 (handoff a F4/verify)

> **Handoff cerrado en F4**: las tres páginas protegidas se verifican con `Web/ProtectedPagesIconRenderTests` (render real del componente `Icon` con los 77 conjuntos de atributos extraídos de su marcado) y `/radar` se revalidó en el humo anónimo con **200**; las cinco guardas de anonimia prometidas en el punto 5 se implementaron en F4.

1. **`Icon.razor` — paso directo de atributos adicionales (`CaptureUnmatchedValues`)**: doce puntos de la interfaz pasaban `class`, `Class` o `aria-hidden` a `<Icon>`, que no los declara, y el render lanzaba `InvalidOperationException` (`Icon does not have a property matching the name 'class'`). Afectaba a rutas **públicas** (`/radar` con la pestaña de ofertas, la galería de la ficha y el hub multimedia de la ficha) y a tres páginas protegidas. El arreglo es aditivo y no cambia el contrato propio del icono (`Name`, `Size`, `StrokeWidth`, `Title`) ni su contrato de markup. **F4/verify deben comprobar las tres páginas protegidas** (`/admin/cola-catalogacion`, `/admin/ingesta-social`, `/admin/canales-monitorizados`), que quedaron con `class`/`Class` sobre `<Icon>` y ahora se renderizan con el nuevo paso directo.
2. **`SqliteUserRepository` — `AsNoTracking()` en las lecturas de identidad** (`GetByIdAsync`, `GetByEmailAsync`, `GetAllAsync`): sin él, el `DbContext` de larga vida del circuito devolvería la entidad rastreada y la suspensión o revocación en caliente no surtiría efecto (riesgo R5 del `tasks.md`).
3. **Alineación del marcado con el permiso de la política** (solo experiencia de uso, nunca control de acceso): `MainLayout` muestra el bloque de Gobernanza con `CanManageUsers` o `CanViewAuditLog`, y las páginas `UserManagement`, `AuditLogViewer`, `MediaModeration` y `GameReportsModeration` condicionan su bloque de aviso al permiso de su propia política (`CanManageUsers`, `CanViewAuditLog`, `CanApproveMedia`, `CanResolveReports`). Antes dependían de `IsFoundingTeam`, que con la identidad simulada siempre era verdadero.
4. **`App.razor`**: el respaldo de tema en `localStorage` ya no cae al identificador simulado `usuario-fundador-ludeka`; sin identidad se usa el tema global. Es la última traza de la simulación fuera del ámbito estrictamente UI.
5. **Guardas de anonimia en servicios: NO se añadieron en este slice** (es el objeto de F4). La navegación pública anónima no necesitó ninguna: todas las lecturas toleran `UserId` vacío y devuelven vacío, y las escrituras públicas siguen esperando a la revalidación de F4.

## Desviaciones y hallazgos (F3)

1. **Mapeo de 2.10/2.11 fijado por el maintainer (no por `design.md`)**: `/admin/notificaciones` y `/admin/eventos` usan la política de rol `RolModerador` en lugar de `PermisoGestionarEditores`, y `/admin/ingesta-social` y `/admin/canales-monitorizados` usan `PermisoAprobarMedios`. **Punto a confirmar por el maintainer** (R3/§9.3).
2. **Correo sin verificar**: no se persiste en `AppUser.Email`; queda solo en `ExternalLogin.ProviderEmail`; las cuentas sin correo del proveedor reciben un correo sintético no enrutable (`<clave>@<proveedor>.ludeka.invalid`).
3. **Trazas de correo verificado**: se mapean claims propios (`ludeka:email_verified`) desde `email_verified` (Google) y `verified` (Discord/Facebook).
4. **Antiforgery explícito**: el endpoint `/login/external` valida el token antes de leer el formulario y responde 400.
5. **Identidad en dos tiempos (por diseño)**: en SSR la identidad se proyecta desde los claims de la cookie (`IHttpContextAccessor`) y en el circuito manda el `AppUser` releído por `UserCircuitHandler`. La proyección de claims es un snapshot de interfaz y **nunca** decide autorización: las políticas y (en F4) los servicios de escritura releen la fila. Los claims no transportan el estado de suspensión, de modo que un circuito abierto de una cuenta recién suspendida se corrige por el guardián de invalidación, no por la cookie.
6. **Degradaciones deliberadas y deterministas del circuito**: una cookie válida sin fila en `AppUsers` o un fallo de lectura del repositorio resuelven a sesión anónima (principal vacío), nunca a una identidad inventada, y jamás lanzan dentro del circuito.
7. **`ModeratorPermissionRules`**: la regla de concesión de permisos se extrajo de `AppUser` a `Ludeka.Core/Enums` para que la proyección de la cookie y el agregado no discrepen; `AppUser.HasPermission` delega en ella sin cambio de semántica (los tests de dominio y de políticas siguen verdes).
8. **`SessionInvalidator` en memoria (Singleton)**: el aviso de invalidación no cruza instancias en un despliegue multirréplica; la autorización sigue siendo correcta porque el handler de permisos relee el `AppUser` en cada comprobación. La versión monótona por usuario permite distinguir invalidaciones nuevas sin recargar en bucle.
9. **Retirada de un test de simulación**: se eliminó `FoundingVerdictServiceTests.CurrentUserService_SwitchRole_ShouldTogglePermissions`, que afirmaba el requerimiento retirado (REMOVED) del conmutador; su cobertura pasa a `CurrentUserContractTests` y `AuthenticatedCurrentUserServiceTests`.
10. **Rutas del prompt que no existen**: `/directorio/creadores` y `/directorio/editoriales` devuelven **404** porque la aplicación sirve esos directorios en `/creadores` y `/editoriales` (ambas **200** anónimas). No es una regresión de este slice ni un 500.
11. **Fuera de alcance por acuerdo**: F4 (revalidación de permiso en los 15 servicios de escritura y guardas de anonimia) y F5 (documentación, roadmap y `sdd-archive`) no se tocaron. **Gap conocido de F3**: sin sesión, una escritura desde el circuito (por ejemplo «Tengo» en una ficha) sigue alcanzando entidades que exigen identidad y fallará hasta que F4 añada la guarda y la redirección a login. Las rutas públicas de lectura ya no lo hacen.
12. **Documentación viva pendiente de F5**: `.openspec/specs/editorial-role-management/spec.md`, `.openspec/specs/user-management-permissions-audit/spec.md` y `docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md` aún describen la simulación y los conmutadores; el delta del cambio ya los retira y `sdd-archive-compose` los sincroniza.
13. **Histórico de F2 (sin cambios)**: `IExternalLoginRepository` y `ExternalLoginRepository` quedaron registrados como `Scoped` junto a `IExternalLoginService`, y F2 cerró con F3/F4/F5 sin tocar, la simulación intacta y sin cambios de esquema ni efectos de despliegue.

## Presupuesto y frontera de PR (F3)

- **Líneas cambiadas del slice F3**: `git diff --shortstat 83063bd` → **1180 inserciones y 378 supresiones en 42 archivos** de `src/` y `tests/`, con un único borrado (`DefaultCurrentUserService.cs`). Artefactos SDD (`tasks.md` y este `apply-progress.md`) aparte. Dentro del presupuesto de 3000 líneas del slice.
- **Modo**: chained/stacked PR slice (`stacked-to-main`). Los PR 5 y 6 del desglose se entregan como una unidad cohesionada (el contrato sin conmutadores es la precondición de compilación de la identidad real y de la invalidación).
- **Frontera**: de `83063bd` a la identidad de sesión real con invalidación de circuito; sin revalidación de escritura (F4) y sin migraciones ni cambios de esquema.
- **Reversión**: revertir los commits devuelve el árbol a `83063bd`.
- **Slice F2 (histórico)**: `git diff --shortstat 06e53cc` → **1567 inserciones y 73 supresiones en 30 archivos**, de las que 1472/4 (28 archivos) eran código y pruebas; modo chained/stacked PR slice, frontera de `06e53cc` a la autorización efectiva por política con la identidad simulada todavía viva.

## Desviaciones y hallazgos (F4)

1. **`UserLocationService` y `UserLibraryStatsService` no necesitaron guarda**: son rutas de lectura y ya toleraban `UserId` vacío. Se auditaron uno a uno; el comentario de `GetEffectiveCountryAsync` documenta por qué la ausencia de sesión no debe denegarse allí (la navegación pública debe seguir funcionando). Ningún servicio de los 15 queda sin decisión explícita.
2. **Servicios con dependencia opcional (`ICurrentUserService?`)**: conservan la convención del repositorio (nulo = sin contexto de identidad, usado por pruebas y ejecuciones sin web) y aplican la guarda cuando la dependencia existe. En producción la dependencia siempre está registrada (`Scoped`), así que la guarda es efectiva en todo el ciclo web.
3. **La identidad del reporte la manda la sesión**: `GameIssueReportService.CreateReportAsync` ignora el `UserId` del comando cuando hay sesión y escribe el de la sesión, de modo que un visitante no puede atribuir un reporte a otra persona ni escribir una identidad vacía.
4. **`AuditService` valida la identidad del comando, no la compara con la sesión**: `RecordChangeAsync` exige identidad y nombre no vacíos. No se añadió la comparación estricta con la sesión porque los servicios que auditan fuera del ciclo web (importaciones y procesos de datos) pasan identidades funcionales propias y hay cobertura existente que lo fija; la comparación estricta queda como endurecimiento futuro si el maintainer lo pide.
5. **Preferencias con denegación explícita**: `SqliteUserPreferenceService` ya no ignora silenciosamente una identidad vacía; ahora lanza la denegación controlada. La interfaz (`MainLayout`, `MyLibrary`, `LocationSelectorModal`) la traduce en redirección al acceso; un tema elegido sin sesión se aplica localmente por JS antes de la redirección y sigue vigente al volver.
6. **`SocialInboxModeration`: se retiró el revisor centinela `admin-system`**. Era código muerto (el operador `?.` sobre un `string` no nulo nunca lo alcanzaba) pero contradecía la invariante «sin usuario centinela» y podía propagar una identidad vacía.
7. **`InstagramModeration` no se tocó**: sus superficies están protegidas por la política `PermisoPublicarInstagram` y el servicio editor recibe la identidad como parámetro desde una página `[Authorize]`; no existe camino anónimo. Queda anotado para F5/futuro si se quiere homogeneizar la firma con `SessionIdentity`.
8. **`UserManagementSeeder` conserva la fila `usuario-fundador-ludeka`**: es dato demostrativo (`Database:SeedDemoData`, solo en `Development`) y ninguna ruta de escritura la usa como identidad. No es un centinela funcional.
9. **Handoff de F3 cerrado con render real**: las tres páginas protegidas no se pueden abrir sin sesión (302), así que su render se verifica con 77 combinaciones reales de atributos sobre el componente `Icon` extraídas de su propio marcado; `/radar` (pública, mismo defecto) se revalidó en el humo con **200**.
10. **Sin migraciones ni cambios de esquema**: la frontera de reversión es exacta (`51e16f3`).
11. **PostgreSQL no disponible en este entorno**: no hay servidor ni Docker, así que el humo de F4 se ejecutó sobre SQLite. F4 no toca persistencia (solo guardas en servicios), por lo que el arranque con PostgreSQL permanece idéntico al verificado en F3; el cambio de proveedor sigue cubierto por `DatabaseProviderTests` en la suite.

## Presupuesto y frontera de PR (F4)

- **Líneas cambiadas del slice F4**: `git diff --shortstat 51e16f3 HEAD` → **1944 inserciones y 104 supresiones en 37 archivos** de `src/` y `tests/`; artefactos SDD (`tasks.md` y este `apply-progress.md`) aparte. Dentro del presupuesto de 3000 líneas del slice.
- **Modo**: chained/stacked PR slice (`stacked-to-main`). Los PR 5 y 6 del desglose ya se entregaron juntos en F3; este slice cubre el PR 7 completo.
- **Frontera**: de `51e16f3` a la anonimia efectiva en escritura, con redirección al acceso en la interfaz; sin `sdd-archive` (F5) y sin migraciones.
- **Reversión**: revertir los 4 commits devuelve el árbol a `51e16f3`.

## Presupuesto y frontera de PR (F2-bis)

- **Líneas cambiadas del slice F2-bis**: `git diff --shortstat fc9e50b` → **72 inserciones y 17 supresiones en 7 archivos** de `src/` y `tests/` (commits `2a5b5dc` y `2aac16e`); artefactos SDD (`tasks.md` y este `apply-progress.md`) aparte. Muy por debajo del presupuesto de 1500 líneas del slice.
- **Modo**: chained/stacked PR slice (`stacked-to-main`); es un refuerzo de F2 nacido de la confirmación del maintainer, no un PR nuevo del desglose original.
- **Frontera**: de `fc9e50b` a los dos permisos granulares propios con las dos páginas migradas y `RolModerador` conservado sin páginas; sin migraciones, sin cambios de esquema y sin `sdd-archive` (F5).
- **Reversión**: revertir los 2 commits devuelve el árbol a `fc9e50b`.

## Estado F2-ter: COMPLETADO ✅ — Corrección de la regresión de pérdida de permisos

> Slice acotado de `sdd-apply` en la misma rama `inc/autenticacion-real-permisos`, base `723c668`.
> Cierra el hallazgo 4 de F2-bis (`UserPermissionsModal` desactualizado): el modal solo ofrecía 8
> casillas y reconstruía la máscara desde esa lista parcial, de modo que guardar los permisos de un
> moderador borraba en silencio `CanManageUsers`, `CanViewAuditLog`, `CanManageEvents` y
> `CanManageNotifications`, y no existía vía en la interfaz para concederlos. Sin migraciones ni
> cambios de esquema; sin tocar la especificación viva, el ROADMAP ni F5.

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 2t.1 RED del comportamiento del modal (`UserPermissionsModalTests`) y de su contrato de fuente (`UserPermissionsModalContractTests`) | ✅ | ROJO 8/11: máscara `CanEditGames\|…\|CanPublishInstagram` (255) en vez de `All`; render de 8 casillas en vez de 12; cuatro banderas sin referencia | `fc1bf4d` |
| 2t.2 GREEN: las 12 casillas del modal con el mismo patrón y textos en español, y guardado que conserva los bits fuera de `ModeratorPermission.All` | ✅ | VERDE 7/7 focal del modal | `fc1bf4d` |
| 2t.3 RED de `GetPermissionNames` (`PermissionNamesTests`, teoría sobre el enum) | ✅ | ROJO 2/4: `All` devolvía 7 nombres en vez de 12; las 5 banderas sin nombre devolvían lista vacía | `56a6d6c` |
| 2t.4 GREEN: `GetPermissionNames` nombra las 12 banderas en el orden del enum | ✅ | VERDE 4/4 focal | `56a6d6c` |
| 2t.5 `dotnet test Ludeka.sln --configuration Release` verde | ✅ | VERDE **1258/1258** (base 1247, +11) | (docs) |
| 2t.6 Humo real (`Production` + SQLite, sin credenciales OAuth) y demostración de detección de la pérdida | ✅ | `/` **200**, `/healthz` **200**, `/admin/usuarios` **302** a `/login`; 0 excepciones no controladas; retirar una casilla o su línea de guardado vuelve a rojo | (docs) |

### Commits del slice F2-ter (rama `inc/autenticacion-real-permisos`, base `723c668`)

| Sha | Mensaje | Cambios |
|---|---|---|
| `fc1bf4d` | `fix(web): representar las doce banderas en el modal de permisos` | 3 archivos · +347 |
| `56a6d6c` | `fix(application): nombrar las doce banderas en las insignias de permisos` | 2 archivos · +65 |

### TDD Cycle Evidence (F2-ter)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2t.1 + 2t.2 | `Web/UserPermissionsModalTests.cs` | Unit + render real (`HtmlRenderer`) | ✅ 1247/1247 base | ✅ ROJO 5/7 del archivo: la máscara guardada era `CanEditGames \| ... \| CanPublishInstagram` (255) en vez de `All` (4095); el render ofrecía 8 casillas en vez de 12; las banderas nuevas no se cargaban ni se enviaban | ✅ 11/11 focal (5 + 2 + 4) | ✅ Usuario con `All`, con solo las 4 banderas nuevas, con bit fuera de la máscara declarada, rol no moderador y render con 12 casillas marcadas | ➖ Ninguno necesario (se conserva el patrón de casillas existente) |
| 2t.1 + 2t.2 | `Web/UserPermissionsModalContractTests.cs` | Contract (fuente) | ✅ 1247/1247 base | ✅ ROJO 2/2: `CanManageUsers`, `CanViewAuditLog`, `CanManageEvents` y `CanManageNotifications` sin referencia en el modal; el bloque `SavePermissions` no reconstruía la máscara desde las 12 | ✅ 2/2 focal | ✅ Teoría de reflexión sobre `Enum.GetValues<ModeratorPermission>()` (12 banderas) y bloque de guardado aislado por marcadores | ➖ Ninguno necesario |
| 2t.3 + 2t.4 | `Application/PermissionNamesTests.cs` | Unit (reflexión sobre el enum) | ✅ 1247/1247 base | ✅ ROJO 2/4: `GetPermissionNames(All, Moderator)` devolvía 7 nombres; `CanPublishInstagram`, `CanManageUsers`, `CanViewAuditLog`, `CanManageEvents` y `CanManageNotifications` devolvían lista vacía | ✅ 4/4 focal | ✅ Cada bandera por separado (exactamente un nombre), la máscara completa (12 nombres distintos), Mesa Fundadora y comunidad intactas, y máscara vacía | ➖ Ninguno necesario |

### Demostración de que la prueba de regresión detecta la pérdida (F2-ter)

| Mutación temporal (tras el commit) | Resultado observado |
|---|---|
| Retirar del guardado la línea `if (_canManageEvents) permissions \|= ModeratorPermission.CanManageEvents;` | **ROJO 3/7** en `UserPermissionsModal`: `SaveBlock_ShouldRebuildTheMaskFromEveryDeclaredGranularFlag`, `SavePermissions_WithEveryFlagInTheUserMask_ShouldSendTheWholeTwelveFlagMask` (`Expected: All` · `Actual: CanEditGames \| … \| CanViewAuditLog \| CanManageNotifications`) y `SavePermissions_WithOnlyNewGranularFlags_ShouldLoadAndSendExactlyThoseFlags` (`Expected: CanManageUsers \| CanViewAuditLog \| CanManageEvents \| CanManageNotifications` · `Actual` sin `CanManageEvents`). Árbol restaurado con `git checkout --` |
| Retirar una casilla (bloque `9. CanManageUsers`) del marcado | **ROJO 1/7**: `RenderedModal_ForAModeratorWithEveryFlag_ShouldOfferOneCheckboxPerDeclaredFlag` (`Expected: 12` · `Actual: 11`). Árbol restaurado con `git checkout --` |

### Work Unit Evidence (F2-ter)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U2t-A Modal de permisos / `fc1bf4d` | `--filter FullyQualifiedName~UserPermissionsModal` → **7/7** | Arranque real (`Production`, SQLite temporal, sin credenciales OAuth): `/` **200**, `/healthz` **200**, `/admin/usuarios` **302** → `/login?ReturnUrl=%2Fadmin%2Fusuarios`; 0 excepciones no controladas | Revertir el commit: el modal vuelve a 8 casillas y a reconstruir la máscara desde la lista parcial; nada más depende de él |
| U2t-B Insignias de permisos / `56a6d6c` | `--filter FullyQualifiedName~PermissionNamesTests` → **4/4** | El mismo arranque real; las insignias se pintan en `/admin/usuarios` (ruta protegida, sin sesión no se puede abrir) y la teoría de reflexión cubre cada bandera | Revertir el commit: vuelven los 7 nombres y las 5 banderas sin insignia |

### Verificación observada (registro) — F2-ter

| Comando / comprobación | Resultado observado |
|---|---|
| Línea base antes de tocar código: `dotnet test Ludeka.sln --configuration Release` en `723c668` | **1247 correctas, 0 con error, 0 omitidas** |
| `dotnet test Ludeka.sln --configuration Release` (final) | **1258 correctas, 0 con error, 0 omitidas** (+11: 5 de comportamiento del modal, 2 de contrato de fuente, 4 de `GetPermissionNames`) |
| Suites vecinas (`WebMarkupContractTests`, `GranularPermissionsTests`, `UserManagementAndAuditServiceTests`, `PolicyAuthorizationTests`, `ProtectedPagesIconRenderTests`, `AuthorizationPipelineContractTests`, `IconCatalogTests`) | **242/242** |
| Comprobación del modal | 12 casillas: las 8 previas + `CanManageUsers` (users), `CanViewAuditLog` (scroll), `CanManageEvents` (calendar) y `CanManageNotifications` (bell); ninguna bandera declarada queda fuera (teoría de reflexión sobre el enum) |
| Auditoría de la cadena de permisos | Único punto que reconstruía la máscara: el modal (corregido). `GetPermissionNames` era el único que enumeraba banderas para mostrarlas (corregido). `UserManagement.razor` solo consulta `HasPermission`; `AppUser.UpdateRoleAndPermissions` aplica la regla de rol (Fundadora → `All`, resto → `None`); la cookie firma la máscara como entero y `Enum.TryParse` la restaura íntegra; sembradores y `AuthorizationPolicies` no reconstruyen máscaras |
| Arranque real (`ASPNETCORE_ENVIRONMENT=Production`, SQLite temporal `%TEMP%\ludeka-smoke-permisos.db`, `Database__SeedDemoData=false`, sin credenciales OAuth, `PORT=5187`) | `/` **200**, `/healthz` **200**, `/admin/usuarios` **302** → `/login?ReturnUrl=%2Fadmin%2Fusuarios`; **0** excepciones no controladas en el log |

### Desviaciones y hallazgos (F2-ter)

1. **Ubicación real del modal**: el prompt lo situaba en `src/Ludeka.Web/Components/Pages/UserPermissionsModal.razor`, pero vive en `src/Ludeka.Web/Components/Shared/UserPermissionsModal.razor`. No se movió: así lo referencia `UserManagement.razor` y así lo fijan los contratos de markup existentes.
2. **Decisión sobre los bits no representados**: se representan las 12 banderas (no se oculta ninguna) y, además, el guardado conserva `User.Permissions & ~ModeratorPermission.All`, de modo que un bit sin casilla (bandera futura o dato heredado) nunca se revoca desde el modal. La máscara conocida es el propio `All`, cuyo invariante `All_ShouldEqualTheOrOfEveryDeclaredFlag` ya está probado: no se añadió ninguna lista paralela que mantener y el contrato de fuente impide que una bandera declarada se quede sin representación.
3. **`AuthorizationPolicies.PermissionPolicies` no es una reconstrucción de máscara**: es el mapa política → bandera (11 entradas; `CanUploadImages` no tiene política propia porque se exige en `GameEditorService.UpdateGameAsync`). No se tocó: cambiar la superficie de autorización queda fuera del alcance de esta corrección.
4. **`WebMarkupContractTests` conserva sus contratos del modal**: los textos existentes («Gestionar Creadores de Contenido») y los iconos Lucide no se movieron ni reescribieron; las casillas nuevas siguen el mismo patrón de icono + título + descripción, y los cuatro iconos (`users`, `scroll`, `calendar`, `bell`) existen en `IconCatalog`.
5. **Sin migraciones ni cambios de esquema**: `AppUsers.Permissions` sigue siendo el mismo entero; las banderas nuevas ya ocupaban bits vírgenes desde F2-bis.

### Presupuesto y frontera de PR (F2-ter)

- **Líneas cambiadas del slice**: `git diff --shortstat 723c668` → **412 inserciones y 0 supresiones en 5 archivos** de `src/` y `tests/`; artefactos SDD (`tasks.md` y este `apply-progress.md`) aparte. Dentro del presupuesto de 1500 líneas indicado para el slice; por encima del presupuesto de revisión de 400 del incremento, que este slice acotado no cubría (el defecto es una regresión de F0/F2-bis, no un PR nuevo del desglose original).
- **Modo**: corrección acotada sobre `inc/autenticacion-real-permisos` (giro de `stacked-to-main` dentro del mismo slice de F2-bis); sin migraciones y sin `sdd-archive` (F5).
- **Frontera**: de `723c668` a las 12 casillas del modal con guardado sin pérdida y las 12 insignias.
- **Reversión**: revertir los 2 commits de código devuelve el árbol a `723c668`.

## Estado acumulado

- **F0: 6/6** (mergeada en `3ec23d5`). **F1: 8/8** (mergeada en `06e53cc`). **F2: 13/13** (mergeada en `83063bd`). **F2-bis: 6/6**. **F2-ter: 6/6** (esta corrección). **F3: 8/8** (mergeada en `51e16f3`). **F4: 6/6** (mergeada en `fc9e50b`). **F5: 0/6**.
- Suite completa: **1258/1258** en Release, 0 con error, 0 omitidas (línea base del slice corregido `723c668`: 1247).
- Listo para la verificación independiente de `sdd-verify` sobre F2-bis + F2-ter (y, si el maintainer lo pide, sobre el conjunto F0–F4 antes de F5). F5 queda sin tocar.
