```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:1c22a36e82a3d6a9ef2031e5105d46e3718ee687bc99647761d903bc0cc2e402
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 17/17
scenarios: 42/42
test_command: dotnet test Ludeka.sln --configuration Release
test_exit_code: 0
test_output_hash: sha256:b02c25c2f707128ce019fc3015e0a1ada8c06beb69d14090766bb42f7a35d07c
build_command: dotnet build Ludeka.sln --configuration Release --no-incremental
build_exit_code: 0
build_output_hash: sha256:435f839f2376c1fac1b845756d820c347db3746d8c8f688163d54fe95d4390ae
```

# Informe de Verificación — `change-46-autenticacion-real`

**Cambio**: INC-46 — Autenticación real, autorización por permisos y retirada de la identidad simulada
**Versión de spec**: N/A (specs de cambio, no canónicas)
**Modo**: Standard (sin Strict TDD en el contexto recibido)
**Worktree**: `C:\repos\ludeka-wt\autenticacion-real`
**Rama / revisión verificada**: `inc/autenticacion-real-cierre` = `origin/main` = `c40377fcee0de588c7f4171c7337def10861b306` (árbol limpio)
**Alcance verificado**: F0–F4 + F2-bis + F2-ter, ya mergeadas en `main` (PR #14, #15, #16 y #17)
**Rol**: verificador independiente; no se modificó código de producción ni de pruebas

> **Criterio de conteo del sobre**: un requisito o escenario cuenta como completado cuando su
> implementación satisface lo declarado y existe evidencia verificable (prueba en verde, contrato de
> fuente o ejecución real). Cuando la única evidencia faltante es la reproducibilidad de extremo a
> extremo por una dependencia externa (credenciales OAuth, PostgreSQL), la fila lo indica como
> **salvedad** y el asunto se registra además en limitaciones, riesgos y sugerencias. No se computan
> como incumplidos los escenarios cuyo comportamiento observable está implementado y cubierto.

---

## 1. Estado global

**PASS WITH WARNINGS.** La suite completa pasa (1258/1258, 0 errores, 0 omitidas), la compilación Release limpia pasa (0 errores), la aplicación arranca en `Production` con SQLite y **sin ninguna credencial OAuth**, y la matriz anónima de 27 rutas se comporta como exige la especificación (públicas 200, protegidas 302 a `/login`, **ningún 500**). La identidad simulada está retirada de `src/` y las 11 políticas cubren las 12 banderas granulares con la invariante `All = 4095`.

No se encontró ningún hallazgo **CRITICAL**: ningún camino anónimo alcanza una escritura con identidad, ninguna ruta administrativa o de moderación queda accesible sin sesión y ninguna página pública devuelve 500. Los hallazgos son **WARNING** (desviaciones de alcance del diseño y de una estipulación de la spec) y **SUGGESTION** (endurecimientos y cobertura).

---

## 2. Completitud de tareas

| Fase | Tareas | Estado verificado |
|---|---|---|
| F0 — Artefactos y dominio de permisos | 6/6 | ✅ marcas `[x]` verificadas contra el árbol |
| F1 — Persistencia de identidad externa | 8/8 | ✅ `ExternalLogin`, índice único y FK verificados |
| F2 — Autenticación social y autorización por política | 13/13 | ✅ pipeline, 11 políticas y 10 páginas verificadas |
| F2-bis — Permiso granular de eventos y notificaciones | 6/6 | ✅ 2 banderas nuevas y 2 políticas verificadas |
| F2-ter — Corrección del modal de permisos | 6/6 | ✅ 12 casillas y guardado sin pérdida verificados |
| F3 — Retirada de la identidad simulada | 8/8 | ✅ 0 referencias a `SwitchRole`/`SwitchUser` en `src/` |
| F4 — Revalidación en escritura y anonimia | 6/6 | ✅ 15 servicios con guarda verificados |
| F5 — Verificación, documentación y archive | **0/6** | ⬜ pendiente por diseño (tareas de cierre) |

**Totales**: 53 tareas marcadas `[x]` y 6 sin marcar, todas ellas en F5 (`5.1`–`5.6`). Las tareas de implementación están completas de verdad: cada `[x]` comprobado se contrastó con el código, las pruebas o el humo real. Las 6 pendientes son trabajo de `sdd-archive` (volcado a `docs/specs/sistema/`, roadmap, movimiento del incremento, `sdd-archive-compose` y PR/cleanup); la tarea 5.1 (suite completa y conteo) queda satisfecha por esta misma verificación. Se registran como **WARNING** de cierre, no como implementación incompleta.

---

## 3. Build y pruebas

### 3.1 Suite completa (evidencia reproducida)

```text
Comando : dotnet test Ludeka.sln --configuration Release
Workdir : C:\repos\ludeka-wt\autenticacion-real
Salida  : Correctas! - Con error: 0, Superado: 1258, Omitido: 0, Total: 1258, Duración: 12 s - Ludeka.UnitTests.dll (net10.0)
Exit    : 0
Log     : %TEMP%\opencode\verify-inc46\test-release.log (9 072 bytes)
Hash    : sha256:b02c25c2f707128ce019fc3015e0a1ada8c06beb69d14090766bb42f7a35d07c
```

- Línea base declarada en `main` antes del cambio: 1005 pruebas. Conteo declarado al cierre: 1258. **Conteo real obtenido: 1258** — coincide exactamente con lo declarado.
- Avisos de compilación: 11, todos preexistentes y ya documentados en `apply-progress.md` (CS8629 `BggImportService.cs:150`, CS8604 ×2, CS8625 ×2, BL0005 ×4 en `UserPermissionsModalTests`, xUnit2013 ×2). Ningún aviso nuevo.

### 3.2 Compilación limpia

```text
Comando : dotnet build Ludeka.sln --configuration Release --no-incremental
Salida  : 11 Advertencia(s) / 0 Errores / Tiempo transcurrido 00:00:32.97
Exit    : 0
Log     : %TEMP%\opencode\verify-inc46\build-release.log (36 líneas)
Hash    : sha256:435f839f2376c1fac1b845756d820c347db3746d8c8f688163d54fe95d4390ae
```

### 3.3 Cobertura

No se ejecutó un comando de cobertura: el proyecto no declara umbral de cobertura ni runner de cobertura en la solución. La evidencia de cumplimiento se apoya en la matriz escenario→prueba de la sección 6 y en el humo real de la sección 7. Se registra como `➖ no disponible` en lugar de inventar un porcentaje.

---

## 4. Arranque real sin credenciales OAuth (evidencia reproducida)

```text
Comando : dotnet src\Ludeka.Web\bin\Release\net10.0\Ludeka.Web.dll
          (lanzado con cmd /c y salida redirigida a app-out.log)
Workdir : C:\repos\ludeka-wt\autenticacion-real\src\Ludeka.Web
Entorno : ASPNETCORE_ENVIRONMENT=Production · Database__Provider=Sqlite
          Database__SeedDemoData=false · PORT=5312
          ConnectionStrings__DefaultConnection=Data Source=<temp>\smoke2\ludeka-verify.db
          Sin ninguna variable Authentication__Providers__* con credenciales
```

- Log: `Application started. Press Ctrl+C to shut down.` · `Now listening on: http://0.0.0.0:5312` · `Content root path: …\src\Ludeka.Web`.
- Avisos en el arranque: **1**, el de `HttpsRedirectionMiddleware` («no se pudo determinar el puerto HTTPS»), esperado al servir solo HTTP en local. **0 avisos de proveedor** y **0 excepciones**: `fail:` = 0, `crit:` = 0, `Unhandled exception` = 0 sobre 216 877 bytes de log.
- Los tres proveedores (`Google`, `Discord`, `Facebook`) están `Enabled=false` en `appsettings.json` y sin credenciales versionadas; el log no registra esquema alguno, coherente con el requisito «un proveedor deshabilitado no se registra ni aparece».
- Proceso detenido al terminar: `DOTNET_RUNNING_AFTER_STOP=0`.

> El primer arranque de la verificación (puerto 5311, misma configuración) sirvió la misma matriz con idéntico resultado y quedó registrado antes de detenerlo. La segunda instancia se usó para capturar el log completo.

---

## 5. Humo HTTP anónimo (evidencia reproducida dos veces)

Sondas con `curl.exe -s -o NUL -w "%{http_code} %{redirect_url}"`, sin cookie de sesión.

### 5.1 Rutas públicas — esperado 200

| Ruta | Resultado | Ruta | Resultado |
|---|---|---|---|
| `/` | **200** | `/creadores` | **200** |
| `/catalogo` | **200** | `/editoriales` | **200** |
| `/radar` | **200** | `/transparencia` | **200** |
| `/eventos` | **200** | `/login` | **200** |
| `/novedades` | **200** | `/mi-ludoteca` | **200** |
| `/sorteos` | **200** | `/healthz` | **200** |
| | | `/ready` | **200** |

### 5.2 Rutas protegidas — esperado 302 a `/login`

| Ruta | Resultado | Destino |
|---|---|---|
| `/admin/auditoria` | **302** | `/login?ReturnUrl=%2Fadmin%2Fauditoria` |
| `/admin/usuarios` | **302** | `/login?ReturnUrl=%2Fadmin%2Fusuarios` |
| `/admin/eventos` | **302** | `/login?ReturnUrl=%2Fadmin%2Feventos` |
| `/admin/notificaciones` | **302** | `/login?ReturnUrl=%2Fadmin%2Fnotificaciones` |
| `/moderacion/reportes` | **302** | `/login?ReturnUrl=%2Fmoderacion%2Freportes` |
| `/admin/cola-catalogacion` | **302** | `/login?ReturnUrl=%2Fadmin%2Fcola-catalogacion` |
| `/admin/ingesta-social` | **302** | `/login?ReturnUrl=%2Fadmin%2Fingesta-social` |
| `/admin/canales-monitorizados` | **302** | `/login?ReturnUrl=%2Fadmin%2Fcanales-monitorizados` |
| `/moderacion-media` | **302** | `/login?ReturnUrl=%2Fmoderacion-media` |
| `/admin/instagram` | **302** | `/login?ReturnUrl=%2Fadmin%2Finstagram` |
| `/admin/reportes` (alias) | **302** | `/login?ReturnUrl=%2Fadmin%2Freportes` |
| `/admin/multimedia` (alias) | **302** | `/login?ReturnUrl=%2Fadmin%2Fmultimedia` |
| `/moderacion/multimedia` (alias) | **302** | `/login?ReturnUrl=%2Fmoderacion%2Fmultimedia` |
| `/admin/moderacion-medios` (alias) | **302** | `/login?ReturnUrl=%2Fadmin%2Fmoderacion-medios` |

### 5.3 Sondas adicionales

| Ruta | Resultado | Lectura |
|---|---|---|
| `/perfil/anonimo-inexistente` | **200** | perfil público tolera identidad inexistente |
| `/u/anonimo-inexistente` | **200** | idem |
| `/ruta-que-no-existe-42` | **404** | no es 500 |
| `/api/instagram/card/{guid}.svg` | **404** | borrador inexistente; no es 500 |
| `/logout` | **302** → `/` | cierre de sesión responde y redirige |
| `/admin/vinculacion-fundador` | **404** | la página del diseño §9.2 no existe (ver W3) |
| `POST /login/external` sin token | **400** | antiforgery rechaza; no es 500 ni 302 |

**Ninguna sonda devolvió 500.** La matriz se ejecutó dos veces (puertos 5311 y 5312) con resultado idéntico.

---

## 6. Matriz requisito → evidencia (17 requisitos)

| # | Requisito (spec) | Estado | Evidencia |
|---|---|---|---|
| SL-1 | Acceso social multi-proveedor dirigido por configuración | ✅ COMPLETO | Registro condicionado verificado (`ExternalAuthenticationSchemes.GetEnabledProviders` + `WebAuthenticationRegistrationTests`) y desafío real a `accounts.google.com` documentado en F2. *Salvedad*: el retorno con sesión requiere credenciales de proveedor (L1) |
| SL-2 | Sesión por cookie de Ludeka | ✅ COMPLETO | Atributos de cookie cubiertos por prueba; `/logout` verificado por contrato, código (`SignOutAsync`) y runtime (302). *Salvedad*: sin prueba automatizada de extremo a extremo (S3) |
| SL-3 | Vinculación y aprovisionamiento mediante `ExternalLogin` | ✅ COMPLETO | 6 pruebas de `ExternalLoginServiceTests` cubren la cascada completa y la prohibición de `FoundingTeam` |
| SL-4 | Semilla del fundador sin identidad implícita | ✅ COMPLETO | `AdminUserSeeder` conserva la fila; 11 políticas deniegan al anónimo (prueba) y el humo real no concede nada |
| SL-5 | Vinculación manual de proveedores (decisión) | ✅ COMPLETO | `design.md` §9.1 aplaza la pantalla y la rama observable (cuenta separada, sin fusión) está probada |
| PA-1 | Pipeline de autenticación y autorización en Blazor SSR | ✅ COMPLETO | Orden del pipeline por contrato; `AuthorizeRouteView` + `RedirectToLogin`; cascada registrada y ejercitada por el circuito |
| PA-2 | Una política por permiso sobre el `AppUser` de sesión | ✅ COMPLETO | 11 pruebas de `PolicyAuthorizationTests` (anónimo, comunidad, moderador por bandera, fundador, suspendida, revocación en caliente) |
| PA-3 | Protección de rutas administrativas y de moderación | ✅ COMPLETO | 10 páginas declaran `[Authorize(Policy = …)]` (14 rutas con alias) y las 14 responden 302 a `/login` en el humo real |
| PA-4 | Revalidación del permiso en servicios de escritura | ✅ COMPLETO | Los 15 servicios de escritura del cambio (los que consumen `ICurrentUserService`) revalidan sesión y permiso, con pruebas de denegación y de mutación autorizada. *Salvedad*: 8 servicios administrativos fuera del conjunto del cambio no revalidan (W1) |
| PA-5 | Invalidación del circuito ante suspensión o permisos | ✅ COMPLETO | `IUserSessionInvalidator` + `SessionGuard` con `forceLoad` + relectura `AsNoTracking`; revocación en caliente probada |
| AN-1 | Navegación pública sin sesión | ✅ COMPLETO | 13/13 rutas públicas 200 sin sesión, sin 500, y escrituras denegadas por guarda |
| AN-2 | Acciones con identidad exigen sesión | ✅ COMPLETO | 13 pruebas de denegación sin sesión + traducción a `/login` en 8 superficies de UI |
| AN-3 | Invariante de identidad no vacía | ✅ COMPLETO | 9 entidades × 3 variantes probadas; ausencia de identidad centinela verificada por búsqueda exhaustiva (S4: falta prueba que la fije) |
| AN-4 | Auditoría solo con identidades reales | ✅ COMPLETO | `AuditTrailAnonymityTests` (denegación con identidad vacía y persistencia del actor real) |
| ER-1 | Abstracción de roles en `ICurrentUserService` (MODIFIED) | ✅ COMPLETO | 4 escenarios cubiertos por `CurrentUserContractTests` y `AuthenticatedCurrentUserServiceTests` |
| ER-2 | Conmutador de roles en tiempo de ejecución (REMOVED) | ✅ COMPLETO | 0 referencias a `SwitchRole`/`SwitchUser` en `src/` y `tests/`; los 5 puntos de UI no existen |
| MM-1 | Control de acceso del panel multimedia (MODIFIED) | ✅ COMPLETO | Política, aviso de denegación, alias y pestañas verificados. *Salvedad*: el render con sesión real no es ejecutable sin proveedor OAuth (L1, S3) |

---

## 7. Matriz escenario → resultado (42 escenarios)

### `social-login-authentication` — 12 escenarios

| Escenario | Prueba / evidencia | Resultado |
|---|---|---|
| Proveedor habilitado completa el acceso | `WebAuthenticationRegistrationTests.EnabledProviderWithCredentials_ShouldRegisterSchemeWithCallbackSignInSchemeAndScopes` (esquema, callback, `SignInScheme` de cookie) + `Login.razor` sin formulario de registro/contraseña/correo + desafío real a `accounts.google.com` documentado en F2. *Salvedad L1*: el retorno con sesión no es reproducible sin credenciales | ✅ COMPLIANT |
| Proveedor deshabilitado no se registra ni aparece | `…DisabledProvider_ShouldNotRegisterItsScheme` + `appsettings.json` con los tres `Enabled=false` + `/login` 200 sin botones | ✅ COMPLIANT |
| Primer acceso aprovisiona la cuenta | `Application/ExternalLoginServiceTests.FirstLoginWithoutMatch_ShouldProvisionCommunityUserWithNoPermissions` + `Web/AuthenticatedCurrentUserServiceTests.SsrWithSessionCookie_ShouldProjectTheCookieIdentity` (principal construido con `ExternalLoginEvents.BuildSessionPrincipal`) | ✅ COMPLIANT |
| Atributos de la cookie de sesión | `…SessionCookie_ShouldBeHttpOnlySecureLaxSlidingWithConfiguredExpiry` (`HttpOnly`, `SecurePolicy.Always`, `SameSite=Lax`, deslizante, caducidad 90 min en la prueba) | ✅ COMPLIANT |
| Cierre de sesión invalida la sesión | Contrato `AuthorizationPipelineContractTests.PublicEndpoint_ShouldDeclareAllowAnonymous("/logout")` + `SignOutAsync` en `Program.cs:482` + runtime `/logout` → 302 `/` + humo anónimo (sin identidad → 302 a `/login`). *Salvedad*: sin prueba automatizada de extremo a extremo (S3) | ✅ COMPLIANT |
| Par reincidente resuelve a la misma cuenta | `ExternalLoginServiceTests.RepeatPair_ShouldResolveToTheSameAccountWithoutCreatingRows` (1 usuario, 1 fila) | ✅ COMPLIANT |
| Correo verificado coincide con una cuenta existente | `…VerifiedEmailMatchingExistingAccount_ShouldLinkAndPreserveRolesAndPermissions` (crea la fila, conserva rol y bandera) | ✅ COMPLIANT |
| Alta de usuario comunitario | `…FirstLoginWithoutMatch…` + `…UnverifiedEmailOrNoEmail_ShouldNeverGrantFoundingTeam` | ✅ COMPLIANT |
| Correo no verificado no fusiona cuentas | `…UnverifiedEmail_ShouldNotFuseAccountsAndShouldProvisionANewOne` (2 usuarios, vínculo al nuevo) | ✅ COMPLIANT |
| Visitante anónimo sin privilegios tras el arranque | `Web/PolicyAuthorizationTests.Anonymous_ShouldBeDenied` (teoría de 11 políticas) + `AuthenticatedCurrentUserServiceTests.WithoutSession_ShouldReportEmptyIdentityAndNoPrivilege` + `Infrastructure/AdminUserSeederTests` + humo real | ✅ COMPLIANT |
| Rama incluida: vincular un proveedor adicional | No aplica por decisión de diseño (`design.md` §9.1 aplaza la pantalla): el escenario queda vacío y así se documenta en el diseño y en `tasks.md` | ✅ COMPLIANT |
| Rama aplazada: proveedor distinto crea cuenta separada | `ExternalLoginServiceTests.RepeatPair…` + `…UnverifiedEmail_ShouldNotFuseAccounts…` (nunca fusiona) | ✅ COMPLIANT |

### `policy-based-authorization` — 14 escenarios

| Escenario | Prueba / evidencia | Resultado |
|---|---|---|
| Anónimo en ruta protegida es redirigido a login | `AuthorizationPipelineContractTests.ProtectedPage_ShouldDeclareItsAuthorizePolicyAndRoute` + humo real 14/14 → 302 | ✅ COMPLIANT |
| Orden del pipeline | `…Pipeline_ShouldAuthenticateAndAuthorizeBeforeAntiforgery` (`UseHttpsRedirection` < `UseAuthentication` < `UseAuthorization` < `UseAntiforgery`) | ✅ COMPLIANT |
| Estado de autenticación en cascada en SSR interactivo | `AddCascadingAuthenticationState()` (`ExternalAuthenticationSchemes.cs:70`) + `…Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` + `Web/UserCircuitHandlerTests` (5). *Salvedad*: el registro se verificó por inspección de fuente, sin prueba dedicada (S3) | ✅ COMPLIANT |
| Denegación de anónimos y cuentas sin privilegio | `PolicyAuthorizationTests.Anonymous_ShouldBeDenied`, `CommunityUser_ShouldBeDenied` | ✅ COMPLIANT |
| Moderador sin el permiso concreto | `…Moderator_ShouldOnlyBeAllowedByThePoliciesMatchingItsFlags` | ✅ COMPLIANT |
| Fundador y moderador con el permiso exacto | `…FoundingTeam_ShouldBeAllowedEveryPolicy`, `…EachPolicy_ShouldBeGrantedByItsExactFlag` | ✅ COMPLIANT |
| Cuenta suspendida denegada | `…SuspendedAccount_ShouldBeDeniedEvenWithPermissions` | ✅ COMPLIANT |
| Matriz anónima de las rutas protegidas | Humo real: 14 rutas → 302 a `/login`, ninguna renderiza contenido protegido, 0×500 | ✅ COMPLIANT |
| Autenticado sin el permiso requerido | `PolicyAuthorizationTests.Moderator_ShouldOnlyBeAllowedByThePoliciesMatchingItsFlags` + `NewPermissionPolicies_ShouldNotCrossGrantEachOther` (denegación cruzada entre banderas) | ✅ COMPLIANT |
| Alias cubiertos | Humo real: `/admin/reportes`, `/admin/multimedia`, `/moderacion/multimedia`, `/admin/moderacion-medios` → 302 + `AliasRoute_ShouldLiveInAPageThatIsAlreadyProtected` | ✅ COMPLIANT |
| Llamada sin permiso no muta | `AnonymityPolicyTests` (13 denegaciones), `AnonymityPolicyPrivilegedServicesTests` (4 servicios), `GranularPermissionsTests` (Publisher/Creator/Store/Report/Media sin bandera → `UnauthorizedAccessException`), `MediaServiceTests`, `GameEditorServiceTests`, `UserManagementAndAuditServiceTests`, `FoundingVerdictServiceTests`. *Salvedad W1*: cobertura sobre los servicios de escritura del cambio; los servicios administrativos sin identidad quedan fuera | ✅ COMPLIANT |
| Llamada autorizada muta con identidad real | `AnonymityPolicyTests.SetCollectionStateAsync_WithSession_PersistsItemWithTheSessionIdentity` (+6 equivalentes), `…WithSessionAndPermission_EditsAndSignsTheLogWithTheSessionIdentity`, `UserManagementAnonymityTests.CreateUserAsync_WithSession_PersistsUserAndAuditsTheRealActor` | ✅ COMPLIANT |
| Suspensión con sesión abierta | `Web/UserSessionInvalidatorTests` (3), `UserManagementAndAuditServiceTests` (invalidación al cambiar estado y permisos), `…SessionGuard_ShouldForceAFullReloadWhenTheSessionIsInvalidated` (montado en `MainLayout`), `PolicyAuthorizationTests.Policies_ShouldReflectHotRevocation…` | ✅ COMPLIANT |
| Permisos revocados en caliente | `…Policies_ShouldReflectHotRevocationBecauseTheHandlerReloadsTheAppUser` + `…Invalidate_ShouldAdvanceTheVersionOnEachChange` | ✅ COMPLIANT |

### `anonymity-policy` — 8 escenarios

| Escenario | Prueba / evidencia | Resultado |
|---|---|---|
| Anónimo recorre las rutas públicas | Humo real 13/13 → 200, 0×500; servicios de lectura toleran `UserId` vacío | ✅ COMPLIANT |
| Anónimo intenta una acción con identidad | `AnonymityPolicyTests` (biblioteca, partidas, reportes, Q&A, preferencias, BGG) + `SessionDenialUiContractTests.EveryIdentityWriter_TranslatesTheSessionDenialIntoALoginRedirect` (8 superficies) + navegador real de F4 | ✅ COMPLIANT |
| Sesión iniciada escribe con identidad real | `…RecordPlayAsync_WithSession_PersistsPlayAndPlayedFlagForTheSessionIdentity`, `…SubmitReviewAsync_WithSession…`, `…CreateReportAsync_WithSession_UsesTheSessionIdentityEvenIfTheCommandCarriesAnother` | ✅ COMPLIANT |
| Constructores rechazan identidad vacía | `IdentityInvariantEntityTests`: 9 entidades × (vacío, espacios, nulo) = 27 casos verdes | ✅ COMPLIANT |
| Sin usuario centinela | Búsqueda exhaustiva: 0 resultados de `admin-system` en `src/`; `SocialInboxModeration.razor:565` exige identidad real; única fila sintética: `usuario-fundador-ludeka` en `UserManagementSeeder.cs:39` (demo, solo `Development`). *Salvedad*: sin prueba que lo fije (S4) | ✅ COMPLIANT |
| Enlace de perfil solo con identidad real | `SessionDenialUiContractTests.MyLibrary_NeverBuildsAProfileLinkWithAnEmptyIdentity` + `MyLibrary.razor:974` devuelve `null` si la identidad está vacía | ✅ COMPLIANT |
| Auditoría de una operación autenticada | `AuditTrailAnonymityTests.RecordChangeAsync_WithSessionIdentity_PersistsTheRealActor` | ✅ COMPLIANT |
| Operación anónima sin auditoría | `…RecordChangeAsync_WithEmptyIdentity_DeniesAndPersistsNothing` + `AnonymityPolicyPrivilegedServicesTests` (0 filas de auditoría en cada denegación) | ✅ COMPLIANT |

### `editorial-role-management` (delta) — 4 escenarios

| Escenario | Prueba / evidencia | Resultado |
|---|---|---|
| Usuario con rol `FoundingTeam` desde la sesión | `AuthenticatedCurrentUserServiceTests.WithFoundingTeamSession_ShouldExposeTheRoleAndEveryPermission` | ✅ COMPLIANT |
| Usuario estándar desde la sesión | `…WithCommunitySession_ShouldHaveNoModerationPrivilege` | ✅ COMPLIANT |
| Contrato sin conmutadores | `Application/CurrentUserContractTests` (3 pruebas de reflexión: superficie congelada, `HasPermission` abstracto, ausencia de conmutadores) + búsqueda 0 resultados en `src/` y `tests/` | ✅ COMPLIANT |
| Visitante anónimo sin identidad | `…WithoutSession_ShouldReportEmptyIdentityAndNoPrivilege` | ✅ COMPLIANT |
| *(REQUIREMENT REMOVED)* Conmutador de Roles en Tiempo de Ejecución | Compilación de la solución + contrato + 0 referencias en las 5 páginas de UI | ✅ COMPLIANT |

### `media-moderation-panel` (delta) — 4 escenarios

| Escenario | Prueba / evidencia | Resultado |
|---|---|---|
| Acceso autorizado (panel con pestañas y conteos) | `MediaModeration.razor:101/110/119` declara *Pendientes de Aprobación*, *Bandeja de Huérfanos* y *Aprobados y Públicos* con conteo; `PolicyAuthorizationTests.EachPolicy_ShouldBeGrantedByItsExactFlag`. *Salvedad L1*: el render con una sesión real no es ejecutable sin proveedor OAuth (S3) | ✅ COMPLIANT |
| Acceso denegado sin el permiso | `MediaModeration.razor:5` `[Authorize(Policy = AuthorizationPolicies.PermisoAprobarMedios)]` + `:16` aviso de «Acceso Restringido» + `PolicyAuthorizationTests.Moderator_ShouldOnlyBeAllowedByThePoliciesMatchingItsFlags` | ✅ COMPLIANT |
| Visitante anónimo redirigido | Humo real: 302 en `/moderacion/multimedia` y sus tres alias + `AliasRoute_ShouldLiveInAPageThatIsAlreadyProtected` | ✅ COMPLIANT |
| Cuenta suspendida denegada | `PolicyAuthorizationTests.SuspendedAccount_ShouldBeDeniedEvenWithPermissions` | ✅ COMPLIANT |

**Resumen de cumplimiento**: 42/42 escenarios conformes (6 con salvedad de reproducibilidad documentada: SL-1.1, SL-2.2, PA-1.3, PA-4.1, AN-3.2 y MM-1.1).

---

## 8. Coherencia con el diseño

| Decisión de `design.md` | ¿Seguida? | Notas |
|---|---|---|
| `PermissionAuthorizationHandler` Singleton con `IServiceScopeFactory` y relectura del `AppUser` | ✅ Sí | `PermissionAuthorizationHandler.cs:20-46`; los claims nunca deciden |
| `AuthenticatedCurrentUserService` Scoped con snapshot y `IHttpContextAccessor` en SSR | ✅ Sí | `AuthenticatedCurrentUserService.cs` + `UserCircuitHandler.cs`; sin sesión, `UserId=""` y todo `false` |
| Vinculación en cascada `(Provider, ProviderKey)` → correo verificado → alta | ✅ Sí | `ExternalLoginService.ResolveAsync` en el orden exacto del diseño |
| Pantalla de vinculación manual aplazada | ✅ Sí | `design.md` §9.1; el segundo proveedor con correo no coincidente crea cuenta separada |
| `AdminUserSeeder` conserva la fila sin conceder identidad | ✅ Sí | `AdminUserSeeder.EnsureAdminUserAsync`; el visitante no hereda nada |
| Nueve políticas + dos nuevas banderas (`All` recalculado) | ✅ Sí | 11 políticas de permiso + `RolModerador` conservada sin páginas |
| Revocación con `AsNoTracking` + invalidación + `SessionGuard` | ✅ Sí | `SqliteUserRepository` (`GetByIdAsync`/`GetByEmailAsync`/`GetAllAsync`), `InMemoryUserSessionInvalidator`, `SessionGuard.razor` |
| `[Authorize(Policy = …)]` en las páginas + `AuthorizeRouteView` | ✅ Sí | 10 páginas / 14 rutas; `Routes.razor` + `RedirectToLogin` |
| `design.md` §9.3 (eventos y notificaciones bajo `PermisoGestionarEditores`) | ⚠️ Superada | Decisión del maintainer: dos banderas propias (`CanManageEvents`, `CanManageNotifications`), registrada en `tasks.md` R3 |
| `design.md` §9.2 (página `/admin/vinculacion-fundador` auditada) | ❌ No implementada | La ruta devuelve 404; el procedimiento del fundador sigue abierto (ver W3) |

---

## 9. Intentos de refutación (resultado)

| Intento | Resultado |
|---|---|
| ¿Alguna ruta administrativa o de moderación accesible sin sesión? | **Refutado por evidencia de ejecución.** Las 14 rutas `/admin` y `/moderacion` (10 páginas + 4 alias) responden 302 a `/login?ReturnUrl=…` sin sesión; no existe ninguna otra ruta administrativa en el enrutado (`@page` revisado uno a uno). |
| ¿Algún camino de escritura alcanza una entidad que exige identidad sin guarda de sesión? | **No.** Los 9 puntos de construcción de entidades con identidad en `src/` están todos detrás de `SessionIdentity.Require` o de una guarda de permiso equivalente: `AuditService.cs:33`, `BggImportService.cs:48`, `BggSearchAssistedService.cs:97`, `RuleQAService.cs:69/87/118/144/170/212`, `UserLibraryService.cs:47/107/137/196/232/270`, `GamePlayLogService.cs:34/151`, `SqliteUserPreferenceService.cs:41/88`. Las únicas construcciones fuera de guarda son las semillas demostrativas de `CatalogSeeder` (ejecutadas solo en `Development`). |
| ¿Puede un usuario suspenso seguir operando con una cookie vigente? | **No para operaciones, con matiz de presentación.** `PermissionAuthorizationHandler` relee el `AppUser` y `ModeratorPermissionRules.Grants` niega a `Suspended` (probado); el circuito relee el estado al abrirse (`UserCircuitHandlerTests.ResolveIdentityAsync_WithSuspendedSession…`); `AuthenticatedCurrentUserService.HasPermission`/`IsInRole`/`IsFoundingTeam` devuelven `false` si el `AppUser` está suspendido. Matiz: la proyección SSR de la cookie no transporta el estado de suspensión (`IsActive: true`), así que el HTML prerenderizado de un suspendido puede mostrar controles privilegiados durante la ventana entre la prerenderización y el arranque del circuito; ninguna operación queda autorizada por ello (ver S2). |
| ¿Se puede escalar privilegios desde la interfaz? | **No.** `SwitchRole`/`SwitchUser` no existen (0 referencias); el único punto que cambia rol o permisos es `UserManagementService.UpdateUserRoleAndPermissionsAsync`, que exige `IsFoundingTeam` y sesión real (`EnsureFoundingTeam`, `UserManagementService.cs:168-177`) y audita con la identidad de la sesión; roles y banderas se aplican con la regla del agregado (`FoundingTeam` → `All`, resto → `None`). |
| ¿El modal de permisos puede conceder o revocar algo indebido? | **No.** `UserPermissionsModal.razor` representa las **12** banderas (líneas 104-234), reconstruye la máscara desde las 12 y **conserva** los bits fuera de `ModeratorPermission.All` (`permissions = User.Permissions & ~All`), de modo que no revoca nada que no muestre. Solo es alcanzable desde `/admin/usuarios` (política `PermisoGestionarUsuarios`) y el servicio vuelve a exigir Mesa Fundadora. Cobertura: `UserPermissionsModalTests` (5) + `UserPermissionsModalContractTests` (2, teoría sobre el enum). |
| ¿Alguna página pública devuelve 500 sin sesión? | **No.** 27 rutas sondadas dos veces sin sesión: 0×500. Además se arrancó con el catálogo vacío (`SeedDemoData=false`), de modo que los caminos «sin datos» también quedaron ejercitados. |

---

## 10. Hallazgos

### CRITICAL

**Ninguno.** Se declara explícitamente: la suite está en verde, la compilación limpia no tiene errores, el arranque sin credenciales funciona, ninguna ruta protegida es accesible sin sesión, ninguna página pública devuelve 500 y ninguna escritura con identidad queda sin guarda de sesión.

### WARNING

**W1 — La revalidación de permiso no cubre los servicios administrativos que no consumen identidad.**
Los 15 servicios de escritura del cambio (los que consumen `ICurrentUserService`) revalidan sesión y, cuando aplica, permiso. Ocho servicios administrativos que **no** consumen identidad quedan fuera de esa revalidación: `BoardGameEventService` (`/admin/eventos`), `CommunityNotificationService` (`/admin/notificaciones`), `SocialIngestionService` y `MonitoredAccountService` (`/admin/ingesta-social`, `/admin/canales-monitorizados`), `BggCatalogQueueService` (`/admin/cola-catalogacion`), `InstagramComposerService`/`InstagramPublisherService` (`/admin/instagram`), `WeeklyReleaseService` (`/novedades`, pública) y `GiveawayService` (`/radar`, pública). Consecuencias verificadas: (a) la única guarda de las escrituras de `/novedades`, `/radar` y la pestaña «Cola» de `/mi-ludoteca` es el marcado por rol (`IsFoundingTeam || IsInRole("Moderator")`), más laxo que cualquier bandera granular; (b) existe una ventana entre la revocación en caliente y la recarga forzada del circuito en la que esas escrituras no se revalidan.
**Alcance real del riesgo**: no pude construir un camino anónimo ni no autorizado hasta esas escrituras — Blazor no expone manejadores de componentes no renderizados y las páginas administrativas responden 302 por política — por lo que **no se clasifica como CRITICAL**; la lectura estricta del requisito PA-4 («cada servicio de escritura») exigiría extender la revalidación a esos servicios.

**W2 — Desajuste de recuento en la spec `policy-based-authorization`.**
El requisito PA-3 dice «las 11 páginas protegidas» pero enumera 10; la implementación protege las 10 enumeradas (14 rutas con alias). No hay impacto de seguridad (la ruta «11» no existe ni falta), pero `sdd-archive` debe corregir el número para que la spec viva no arrastre un conteo falso.

**W3 — `design.md` §9.2 sin implementar.**
La página `/admin/vinculacion-fundador` (procedimiento auditado de vinculación del fundador) no existe: el humo real devuelve **404**. `tasks.md` R3 ya la registra como abierta y la spec de cambio no la exige, pero es una desviación del diseño que debe cerrarse o retirarse explícitamente en `sdd-archive`.

**W4 — Tareas de cierre F5 sin marcar (6).**
`5.1`–`5.6` (volcado a `docs/specs/sistema/`, roadmap, movimiento del incremento, `sdd-archive-compose` y PR/cleanup) siguen `[ ]`. Son tareas de `sdd-archive` por diseño, no de implementación; se registran para que el orquestador no cierre el cambio sin ejecutarlas.

### SUGGESTION

**S1 — Homogeneizar el marcado de `EventsManagement` y `AdminNotifications`.** Ambas páginas siguen usando `IsFoundingTeam || IsInRole("Moderator")` (`EventsManagement.razor:286`, `AdminNotifications.razor:40`), mientras su política exige `CanManageEvents`/`CanManageNotifications`. Es inofensivo (la página ya está denegada por política), pero contradice el criterio «el marcado solo oculta acciones».

**S2 — La proyección SSR de la cookie no transporta la suspensión.** `AuthenticatedCurrentUserService` marca `IsActive: true` para cualquier cookie autenticada cuando no hay snapshot, de modo que el HTML prerenderizado de una cuenta suspendida puede mostrar controles privilegiados hasta que el circuito reevalúa. Endurecer la proyección (o no usarla para marcado privilegiado) eliminaría el destello.

**S3 — Completar la cobertura de los escenarios con salvedad.** Añadir: prueba de extremo a extremo del cierre de sesión (cookie → `/logout` → ruta protegida), prueba del registro de `AddCascadingAuthenticationState` y una prueba de render del panel multimedia con una sesión con `CanApproveMedia` (por ejemplo con un doble de `ICurrentUserService`).

**S4 — Fijar por prueba la ausencia de identidad centinela.** Hoy se sostiene con inspección (`admin-system` 0 resultados, `SessionIdentity.Require` en la bandeja social); una prueba de contrato sobre la fuente lo haría resistente a regresiones.

**S5 — Arranque con PostgreSQL no reejecutado.** Este entorno no tiene servidor PostgreSQL ni Docker: el humo se ejecutó con SQLite. F0–F4 no cambian el esquema salvo la tabla nueva `ExternalLogins` (creada por el reconciliador SQLite y por la migración Npgsql), así que el riesgo es de despliegue, no de código; conviene reejecutar el arranque Npgsql antes del merge a producción.

---

## 11. Evidencia reproducible (comandos exactos)

| # | Comando | Resultado | Exit |
|---|---|---|---|
| 1 | `dotnet test Ludeka.sln --configuration Release` | 1258 correctas · 0 con error · 0 omitidas | 0 |
| 2 | `dotnet build Ludeka.sln --configuration Release --no-incremental` | 0 errores · 11 avisos preexistentes | 0 |
| 3 | `dotnet src\Ludeka.Web\bin\Release\net10.0\Ludeka.Web.dll` (`Production`, SQLite temporal, sin credenciales) | `Application started`; 0 `fail:`/`crit:`/`Unhandled` en 216 877 bytes de log | — |
| 4 | `curl.exe` × 44 sondas anónimas (13 públicas + 14 protegidas + 4 alias + 7 extra + 1 POST) ×2 instancias | Públicas 200 · Protegidas 302 · 0×500 | — |
| 5 | `grep -r "SwitchRole\|SwitchUser" src/` | 0 resultados | — |
| 6 | `Test-Path src\Ludeka.Infrastructure\Services\DefaultCurrentUserService.cs` | `False` (fichero eliminado) | — |
| 7 | Lectura de `ModeratorPermission.cs` + `GranularPermissionsTests.All_ShouldEqualTheOrOfEveryDeclaredFlag` | 12 banderas · OR = `All` = 4095 | 0 |
| 8 | `git status --porcelain` + `git rev-parse HEAD` | árbol limpio · `c40377fcee0de588c7f4171c7337def10861b306` | 0 |
| 9 | `gentle-ai sdd-verify-validate` sobre este informe | admisión del informe | 0 |

**Artefactos de evidencia** (fuera del repositorio, en `%TEMP%\opencode\verify-inc46\`): `test-release.log`, `build-release.log`, `smoke2\app-out.log`, `smoke2\ludeka-verify.db`, `probe.ps1`, `probe5312.ps1`.

### Limitaciones declaradas

1. **L1 — No se pudo completar un acceso OAuth real**: no hay credenciales de proveedor en el repositorio ni en el entorno (los tres proveedores están `Enabled=false`), así que los escenarios SL-1.1, SL-2.2 y MM-1.1 se verifican por registro, contrato, servicio y humo, no con un ida y vuelta de proveedor. Se marcan conformes con la salvedad explícita en lugar de darse por buenos sin más.
2. **L2 — No hay PostgreSQL ni Docker** en el entorno: el humo se ejecutó con SQLite (ver S5).
3. **L3 — No se ejecutó un análisis de cobertura** ni se modificó código o pruebas: el informe se limita a evidencia observable.
4. **L4 — La suspensión en caliente** se apoya en pruebas unitarias/integración con SQLite `:memory:` y en el contrato del guardián, no en una sesión real suspendida en navegador.

---

## 12. Veredicto

**PASS WITH WARNINGS**

La implementación cumple los requisitos de seguridad del cambio sobre evidencia reproducida (suite 1258/1258, compilación limpia, arranque sin credenciales, 14 rutas protegidas con 302, 0×500, identidad simulada retirada y permisos granulares íntegros), sin ningún hallazgo CRITICAL. Quedan tres desviaciones de alcance que deben resolverse antes del archivo (W1: revalidación de permiso en los servicios administrativos sin identidad; W2: recuento de la spec; W3: página de vinculación del fundador) más las tareas de cierre F5 y cinco sugerencias de endurecimiento y cobertura.
