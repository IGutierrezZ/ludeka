# Tareas — INC-46: Autenticación Real, Autorización por Permisos y Retirada de la Identidad Simulada

> Runner contractual (Strict TDD): `dotnet test Ludeka.sln`. Worktree único: `C:\repos\ludeka-wt\autenticacion-real`.
> Migración EF innegociable: `$env:Database__Provider="PostgreSql"`; con el default de `appsettings.json` (Sqlite), EF escribiría tipos SQLite y corrompería `LudekaDbContextModelSnapshot.cs`.

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas cambiadas (autoradas) | ~2 500–3 500 totales; migración EF y snapshot generados quedan fuera del conteo de riesgo |
| Estimación por fase | F0 ~150–200 · F1 ~350–450 · F2 ~800–1 100 · F3 ~550–750 · F4 ~500–700 · F5 ~200–300 |
| Riesgo de presupuesto de 400 líneas | High |
| PRs encadenados recomendados | Yes |
| Estrategia de entrega | ask-on-risk (no se inyectó `delivery_strategy`; default del contrato) |
| Estrategia de cadena | stacked-to-main (convención del repo: ramas apiladas desde el mismo worktree) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Decisión de planificación: sí se añade `.gitattributes` (`*.md text eol=lf`). El índice ya almacena LF (`i/lf`) y el working tree CRLF (`w/crlf`), de modo que no hay diff de contenido y el compositor deja de depender del checkout.

### Suggested Work Units

| # | Meta (inicio → fin) | PR | Prueba focalizada | Arnés de ejecución | Frontera de reversión |
|---|---|---|---|---|---|
| 1 | F0: encabezados canónicos + `.gitattributes` → banderas nuevas y `All` recalculado | PR 1 | `dotnet test Ludeka.sln --filter FullyQualifiedName~GranularPermissionsTests` | N/A: solo specs, atributos y enum | Revertir commit; sin código de aplicación afectado |
| 2 | F1: entidad `ExternalLogin` → migración PostgreSQL y tabla SQLite | PR 2 | `--filter FullyQualifiedName~ExternalLogin` | Arranque local: `SqliteSchemaMigrator` crea `ExternalLogins` | Revertir PR; tabla nueva, no altera datos existentes |
| 3 | F2: options y esquemas externos → vinculación, `/login` y `/logout` | PR 3 | `--filter FullyQualifiedName~ExternalLoginServiceTests` | Login real con Google/Discord en `localhost` | Revertir PR; cookie y pantalla aisladas |
| 4 | F2: handler de permiso → `[Authorize]`, `AuthorizeRouteView` y Paso 1 verde | PR 4 | `--filter FullyQualifiedName~PolicyAuthorizationTests` | Anónimo a `/admin/auditoria` con la simulación aún viva | Revertir PR; simulación intacta |
| 5 | F3: contrato sin conmutadores → `AuthenticatedCurrentUserService` Scoped y 13 dobles | PR 5 | `--filter FullyQualifiedName~AuthenticatedCurrentUserServiceTests` | Navegación anónima pública sin error 500 | Revertir PR; conservar PR 4 |
| 6 | F3: retirada de los 5 puntos de UI → invalidación de circuito | PR 6 | `dotnet test Ludeka.sln` | Suspender un usuario con circuito abierto | Revertir PR; PR 5 sigue verde |
| 7 | F4: guardas de anonimia → revalidación en 15 servicios y smoke | PR 7 | `--filter FullyQualifiedName~AnonymityPolicyTests` | Smoke de navegador: escritura sin sesión redirige a login | Revertir PR; sin migraciones |
| 8 | F5: suite completa y docs → `sdd-archive-compose` y PR/cleanup | PR 8 | `dotnet test Ludeka.sln` | `gentle-ai sdd-archive-compose` sobre specs normalizadas | Revertir PR; el archive previo queda intacto |

### Riesgos y huecos de diseño detectados

- R1: F2 y F3 exceden 400 líneas por PR; los PR 3–7 pueden requerir un corte adicional (p. ej. PR 4 en handler/políticas y páginas/rutas; PR 7 por grupos de servicios).
- R2: `design.md` no fija el mapeo página→política de `/admin/ingesta-social` y `/admin/canales-monitorizados` (hoy `CanApproveMedia` OR `Moderator`); la tarea 2.11 queda bloqueada hasta confirmación.
- R3: `design.md` §9 deja abiertas la pantalla de vinculación del fundador (§9.2) y la reasignación de `/admin/eventos` y `/admin/notificaciones` a `PermisoGestionarEditores` (§9.3); el desglose no las implementa. La parte de §9.3 queda **resuelta en la Fase 2-bis** con dos banderas propias (`CanManageEvents`/`CanManageNotifications`) en lugar de `PermisoGestionarEditores`; §9.2 sigue abierta. También queda abierto si `Enabled=true` sin credenciales debe registrar el esquema.
- R4: `ModeratorPermission.All` pasa de 255 a 1023; comprobar que ningún test ni dato persistido dependa del valor numérico.
- R5: sin `AsNoTracking()` en la relectura del `AppUser` del circuito, la revocación en caliente no surte efecto.

## Fase 0 — Prerrequisitos de artefactos y dominio de permisos (PR 1)

- [x] 0.1 Normalizar `openspec/specs/editorial-role-management/spec.md`: `### Requerimiento:`→`### Requirement:` (3) y `#### Escenario:`→`#### Scenario:` (5), solo encabezados.
- [x] 0.2 Normalizar `openspec/specs/media-moderation-panel/spec.md`: 4 y 6 sustituciones equivalentes, preservando el texto de los requisitos.
- [x] 0.3 Crear `.gitattributes` con `*.md text eol=lf`; re-materializar con `git checkout-index -f -a` y verificar con `git check-attr text eol -- openspec/specs/editorial-role-management/spec.md` (→ `lf`) y `git ls-files --eol -- "*.md"` (sin `w/crlf`).
- [x] 0.4 RED: ampliar `tests/Ludeka.UnitTests/Application/GranularPermissionsTests.cs` para exigir ambas banderas nuevas en `All` y el comportamiento granular intacto.
- [x] 0.5 GREEN: añadir `CanManageUsers = 1 << 8` y `CanViewAuditLog = 1 << 9`, y recalcular `All` en `src/Ludeka.Core/Enums/ModeratorPermission.cs`.
- [x] 0.6 Añadir `AuditAction.LinkedFounderIdentity` en `src/Ludeka.Core/Enums/AuditAction.cs`; `dotnet test Ludeka.sln` verde.

## Fase 1 — Persistencia de identidad externa (PR 2)

- [x] 1.1 RED: crear `tests/Ludeka.UnitTests/Domain/ExternalLoginTests.cs` (constructor rechaza `UserId`/`Provider`/`ProviderKey` vacíos; expone `LinkedAt`).
- [x] 1.2 GREEN: crear `src/Ludeka.Core/Entities/ExternalLogin.cs` con `Id`, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail?`, `LinkedAt`.
- [x] 1.3 RED: crear `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginPersistenceTests.cs` (índice único `(Provider, ProviderKey)` y FK `Cascade` a `AppUsers`).
- [x] 1.4 GREEN: registrar `DbSet<ExternalLogin>` y el índice único más la FK en `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs`.
- [x] 1.5 GREEN: añadir la tabla `ExternalLogins` y su índice único al reconciliador `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` (arranque local usa SQLite).
- [x] 1.6 GREEN: crear `IExternalLoginRepository.cs` en `src/Ludeka.Application/Contracts/` y `ExternalLoginRepository` en `src/Ludeka.Infrastructure/`.
- [x] 1.7 Migración innegociable: `$env:Database__Provider="PostgreSql"; dotnet ef migrations add AddExternalLogins -p src/Ludeka.Infrastructure -s src/Ludeka.Web`; comprobar tipos Npgsql en `LudekaDbContextModelSnapshot.cs`.
- [x] 1.8 `dotnet test Ludeka.sln` verde.

## Fase 2 — Autenticación social y autorización por política (Paso 1, simulación viva; PRs 3–4)

> Ejecutada como slice PR-3 (`inc/autenticacion-real-f2`, base `06e53cc`). El maintainer fijó en el prompt
> de apply el mapeo de las tareas 2.10/2.11: `SocialInboxModeration` y `MonitoredAccountsDirectory`
> quedan bajo `PermisoAprobarMedios` (ya no bloqueadas por R2) y `EventsManagement` y `AdminNotifications`
> bajo la política de rol `RolModerador` —no `PermisoGestionarEditores`— para preservar exactamente el
> público actual (`IsFoundingTeam || IsInRole("Moderator")`). **Punto cerrado en la Fase 2-bis**: el
> maintainer decidió permiso granular propio para ambas páginas.

- [x] 2.1 RED: crear `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` (proveedor `Enabled=false` sin esquema; cookie `HttpOnly`/`Secure`/`SameSite=Lax` y caducidad; `/healthz` y `/ready` anónimos).
- [x] 2.2 GREEN: crear `src/Ludeka.Application/Features/Identity/AuthenticationOptions.cs` y la sección `Authentication` en `src/Ludeka.Web/appsettings.json` (`Enabled=false` hasta que el maintainer configure credenciales; R3).
- [x] 2.3 GREEN: crear `src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs` (Google, Discord, Facebook dirigidos por configuración) y añadir los paquetes `Microsoft.AspNetCore.Authentication.Google`, `Microsoft.AspNetCore.Authentication.Facebook` y `AspNet.Security.OAuth.Discord` a `src/Ludeka.Web/Ludeka.Web.csproj`; callbacks `/signin-{google|discord|facebook}`.
- [x] 2.4 RED: crear `tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs` con los 5 escenarios de la spec (par reincidente, correo verificado, alta `CommunityUser`/`None`, correo sin verificar, nunca `FoundingTeam`).
- [x] 2.5 GREEN: crear `IExternalLoginService.cs` y `ExternalLoginService.ResolveAsync` en `src/Ludeka.Application/Features/Identity/`; los eventos del proveedor solo extraen claims y firman la cookie.
- [x] 2.6 GREEN: crear `src/Ludeka.Web/Components/Pages/Login.razor` (`/login`) y los endpoints `POST /login/external` y `GET /logout` en `src/Ludeka.Web/Program.cs`, con la cookie de sesión propia.
- [x] 2.7 RED: crear `tests/Ludeka.UnitTests/Web/PolicyAuthorizationTests.cs` (anónimo, `CommunityUser`, `Moderator` con y sin el flag, `FoundingTeam`, `Suspended`) sobre SQLite `:memory:`.
- [x] 2.8 GREEN: crear `src/Ludeka.Web/Authentication/PermissionAuthorizationHandler.cs` (Singleton con `IServiceScopeFactory` y `AsNoTracking`) y `AuthorizationPolicies.cs` con las 9 políticas del diseño.
- [x] 2.9 GREEN: registrar en `Program.cs` la cookie, `AddAuthorization`, `AddCascadingAuthenticationState` y `UseAuthentication`/`UseAuthorization` antes de `UseAntiforgery`; `.AllowAnonymous()` en `/healthz` y `/ready`.
- [x] 2.10 GREEN: aplicar `[Authorize(Policy = …)]` en `src/Ludeka.Web/Components/Pages/` a UserManagement→`PermisoGestionarUsuarios`, AuditLogViewer→`PermisoVerAuditoria`, MediaModeration→`PermisoAprobarMedios`, GameReportsModeration→`PermisoResolverReportes`, InstagramModeration→`PermisoPublicarInstagram`, CatalogQueueAdmin→`PermisoEditarFichas`; EventsManagement y AdminNotifications→`RolModerador` (superado por 2b.4: pasan a `PermisoGestionarEventos` y `PermisoGestionarNotificaciones`); ajustar `src/Ludeka.Web/Components/_Imports.razor`; el marcado queda solo como ocultación.
- [x] 2.11 GREEN: `[Authorize]` de SocialInboxModeration (`/admin/ingesta-social`) y MonitoredAccountsDirectory (`/admin/canales-monitorizados`) bajo `PermisoAprobarMedios` por decisión del maintainer en el prompt de apply (resuelve R2).
- [x] 2.12 GREEN: pasar `src/Ludeka.Web/Components/Routes.razor` a `AuthorizeRouteView` y crear `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor`.
- [x] 2.13 `dotnet test Ludeka.sln` verde: fin del Paso 1, con la autorización efectiva y la simulación todavía viva (1120 correctas, 0 con error, 0 omitidas).

## Fase 2-bis — Confirmación del maintainer: permiso granular para eventos y notificaciones

> Ejecutada como slice `inc/autenticacion-real-permisos`, base `fc9e50b` (`origin/main`). Cierra el punto
> a confirmar de 2.10 y sustituye la decisión abierta de `design.md` §9.3 (`PermisoGestionarEditores`)
> por dos banderas propias: `CanManageEvents` (1<<10) y `CanManageNotifications` (1<<11). Sin migraciones
> ni cambios de esquema: `AppUsers.Permissions` conserva el entero y los bits nuevos no desplazan ningún
> valor almacenado.

- [x] 2b.1 RED: ampliar `tests/Ludeka.UnitTests/Application/GranularPermissionsTests.cs` con `All = 4095`, las posiciones de bit 1024/2048 y el invariante OR de las 12 banderas.
- [x] 2b.2 GREEN: añadir `CanManageEvents` y `CanManageNotifications` a `src/Ludeka.Core/Enums/ModeratorPermission.cs` y recalcular `All` a 4095.
- [x] 2b.3 RED: ampliar `tests/Ludeka.UnitTests/Web/PolicyAuthorizationTests.cs` a las 11 políticas (congeladas, mapeo bandera→política, denegación cruzada) y apuntar `AuthorizationPipelineContractTests` a las políticas nuevas.
- [x] 2b.4 GREEN: añadir `PermisoGestionarEventos` y `PermisoGestionarNotificaciones` a `src/Ludeka.Web/Authentication/AuthorizationPolicies.cs` y migrar `EventsManagement.razor` y `AdminNotifications.razor`; `RolModerador` se conserva registrado, ya sin páginas que lo declaren.
- [x] 2b.5 `dotnet test Ludeka.sln --configuration Release` verde: 1247 correctas, 0 con error, 0 omitidas (base 1231).
- [x] 2b.6 Humo real (`Production` + SQLite, sin credenciales OAuth): `/admin/eventos` y `/admin/notificaciones` **302** a `/login`; públicas **200**; 0 excepciones no controladas.

## Fase 3 — Retirada de la identidad simulada (Paso 2; PRs 5–6)

> Ejecutada como slice F3 (`inc/autenticacion-real-f3`, base `83063bd`). Los PR 5 y 6 del desglose
> se entregaron como una sola unidad de trabajo verificable: sin el contrato sin conmutadores no
> compila ninguno de los dos, y la invalidación de circuito es la mitad natural del mismo retirado.

- [x] 3.1 RED: crear `tests/Ludeka.UnitTests/Web/AuthenticatedCurrentUserServiceTests.cs` (sin sesión `UserId=""` y todo `false`; roles y permisos desde `AppUser`; `Suspended` denegado).
- [x] 3.2 GREEN: quitar `SwitchRole`/`SwitchUser` y el cuerpo por defecto de `HasPermission` de `src/Ludeka.Application/Contracts/ICurrentUserService.cs`.
- [x] 3.3 GREEN: crear `src/Ludeka.Web/Services/AuthenticatedCurrentUserService.cs`, el snapshot de identidad y `UserCircuitHandler` (Scoped; `IHttpContextAccessor` en SSR).
- [x] 3.4 GREEN: registrar `AddScoped<ICurrentUserService, AuthenticatedCurrentUserService>` en `Program.cs` y eliminar `src/Ludeka.Infrastructure/Services/DefaultCurrentUserService.cs`.
- [x] 3.5 GREEN: migrar los 13 dobles de `tests/Ludeka.UnitTests/` al contrato sin conmutadores (7 ficheros usan `SwitchRole`/`SwitchUser`) y los usos directos de `DefaultCurrentUserService` en `tests/Ludeka.UnitTests/Application/FoundingVerdictServiceTests.cs`.
- [x] 3.6 GREEN: eliminar los 5 puntos de UI del conmutador en `src/Ludeka.Web/Components/Layout/MainLayout.razor`, `src/Ludeka.Web/Components/Pages/UserManagement.razor`, `AuditLogViewer.razor`, `MediaModeration.razor` y `GameReportsModeration.razor`.
- [x] 3.7 GREEN: implementar `IUserSessionInvalidator`, la invalidación al cambiar `Status`/permisos desde `UserManagementService` y `src/Ludeka.Web/Components/Shared/SessionGuard.razor` con `forceLoad: true`.
- [x] 3.8 `dotnet test Ludeka.sln` verde: fin del Paso 2, sin identidad simulada.

## Fase 4 — Revalidación en escritura, anonimia y smoke (PR 7)

> Ejecutada como slice F4 (`inc/autenticacion-real-f4`, base `51e16f3`). Las guardas se concentran en
> `SessionIdentity` (`src/Ludeka.Application/Contracts/SessionIdentity.cs`), la denegación es
> `UnauthorizedAccessException` y la interfaz la traduce en `/login` con `LoginRedirect`
> (`src/Ludeka.Web/Services/LoginRedirect.cs`). Sin migraciones ni cambios de esquema: F5 queda sin tocar
> y el arranque con PostgreSQL permanece idéntico al verificado en F3 (este entorno no dispone de
> servidor PostgreSQL ni de Docker, así que el humo de F4 se ejecutó sobre SQLite).

- [x] 4.1 RED: crear `tests/Ludeka.UnitTests/Application/AnonymityPolicyTests.cs` (sin sesión, los servicios lanzan `UnauthorizedAccessException` sin persistir ni auditar; las 9 entidades rechazan `UserId` vacío).
- [x] 4.2 GREEN: revalidar permiso e identidad en los 15 servicios que consumen `ICurrentUserService` (`src/Ludeka.Application/Features/*` y `src/Ludeka.Infrastructure/Services/UserLocationService.cs`).
- [x] 4.3 GREEN: guardar las acciones con identidad (partidas, colección, préstamos, reseñas, preguntas, reportes y preferencias) tras comprobar sesión; redirigir a login sin escritura y corregir `/u/` vacío en `src/Ludeka.Web/Components/Pages/MyLibrary.razor`.
- [x] 4.4 GREEN: verificar que `AuditService` y `UserManagementService` solo auditan identidades reales de sesión, sin `AuditLogEntry` de identidad simulada.
- [x] 4.5 `dotnet test Ludeka.sln` verde.
- [x] 4.6 Smoke de navegador (criterio 11): `/admin/auditoria` sin sesión redirige a login; SQLite verificado con la matriz anónima de rutas y la demostración real en navegador del clic «En mi ludoteca» → `/login`. El arranque con PostgreSQL no se pudo reejecutar aquí (sin servidor ni Docker); F4 no toca persistencia.

## Fase 5 — Verificación, documentación y archive (PR 8)

- [ ] 5.1 `dotnet test Ludeka.sln` completo en verde; registrar el conteo de pruebas superadas.
- [ ] 5.2 Volcar el incremento en `docs/specs/sistema/` (módulo de autenticación nuevo, actualización de `docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md`) y en `docs/specs/sistema/README.md`.
- [ ] 5.3 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
- [ ] 5.4 Mover `docs/increments/inc-46-autenticacion-real.md` a `docs/increments/archive/inc-46-autenticacion-real.md`.
- [ ] 5.5 Ejecutar `gentle-ai sdd-archive-compose` sobre los deltas contra las specs canónicas ya normalizadas (depende de 0.1–0.3) y mover el cambio a `openspec/changes/archive/`.
- [ ] 5.6 Publicar los PRs encadenados y limpiar el worktree con `scripts/sdd-worktree.ps1` (read-only).
