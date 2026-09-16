# Apply Progress: change-46-autenticacion-real (INC-46) — Fases F0, F1 y F2

> Fase SDD `sdd-apply`. Store: openspec (este archivo + `tasks.md`).
> Worktree: `C:\repos\ludeka-wt\autenticacion-real`.
> · Slice **PR-1 / F0**: rama `inc/autenticacion-real`, mergeado a `main` en `3ec23d5`.
> · Slice **PR-2 / F1**: rama `inc/autenticacion-real-f1`, base `3ec23d5`, mergeado a `main` en `06e53cc`.
> · Slice **PR-3 / F2** (este): rama `inc/autenticacion-real-f2`, base `06e53cc` (`origin/main`).
> Modo: **TDD estricto**. Runner contractual: `dotnet test Ludeka.sln`. Cadena `stacked-to-main` (PRs 3–4 de 8).
> Alcance: Paso 1 de la migración — autorización efectiva con `DefaultCurrentUserService` **todavía vivo**.

## Estado por fase

| Fase | Tareas | Estado |
|---|---|---|
| F0 — Artefactos y dominio de permisos | 6/6 | ✅ mergeada en `main` (`3ec23d5`) |
| F1 — Persistencia de identidad externa | 8/8 | ✅ mergeada en `main` (`06e53cc`) |
| F2 — Autenticación social y autorización por política | 13/13 | ✅ completada (este slice) |
| F3 — Retirada de la identidad simulada | 0/8 | ⬜ sin tocar (Paso 2) |
| F4 — Revalidación en escritura y anonimia | 0/6 | ⬜ sin tocar |
| F5 — Verificación, documentación y archive | 0/6 | ⬜ sin tocar |

## Estado F2: COMPLETADO ✅

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 2.1 RED de `WebAuthenticationRegistrationTests` (esquema condicionado a configuración, cookie y configuración versionada) | ✅ | ROJO por compilación (CS0234/CS0246) → VERDE 5/5 focal | `333e37a` |
| 2.2 `AuthenticationOptions` + sección `Authentication` en `appsettings.json` | ✅ | VERDE 5/5 focal | `333e37a` |
| 2.3 `ExternalAuthenticationSchemes` + paquetes Google/Facebook/Discord y callbacks `/signin-*` | ✅ | VERDE 5/5 focal; paquetes restaurados (Google/Facebook 10.0.11, Discord 10.0.0) | `333e37a` |
| 2.4 RED de `ExternalLoginServiceTests` (5 escenarios de la spec) | ✅ | ROJO por compilación (CS0246 de `IExternalLoginService`) → VERDE 6/6 focal | `4078b13` |
| 2.5 `IExternalLoginService` + `ExternalLoginService.ResolveAsync` y eventos de proveedor | ✅ | VERDE 6/6 focal | `4078b13` |
| 2.6 `Login.razor` (`/login`), `POST /login/external` y `GET /logout` | ✅ | VERDE 6/6 focal; verificado en navegador real (desafío a Google) | `4078b13`, `70ac33e` |
| 2.7 RED de `PolicyAuthorizationTests` sobre SQLite `:memory:` | ✅ | ROJO por compilación (CS0246) → VERDE 48/48 focal | `0373876` |
| 2.8 `PermissionAuthorizationHandler` + `AuthorizationPolicies` (9 políticas) | ✅ | VERDE 48/48 focal (incluye revocación en caliente) | `0373876` |
| 2.9 Registro, `AddCascadingAuthenticationState`, pipeline y sondas anónimas | ✅ | VERDE 79/79 focal + arranque real | `28b6464` |
| 2.10 `[Authorize]` en las páginas administrativas y de moderación | ✅ | VERDE 20/20 del contrato de rutas | `28b6464` |
| 2.11 Páginas de ingesta social y canales monitorizados | ✅ | VERDE 20/20 (mapeo fijado por el maintainer: `PermisoAprobarMedios`) | `28b6464` |
| 2.12 `AuthorizeRouteView` + `RedirectToLogin` | ✅ | VERDE 20/20 | `28b6464` |
| 2.13 `dotnet test Ludeka.sln` verde | ✅ | VERDE **1120/1120** en Release | (docs) |

### Commits del slice F2 (rama `inc/autenticacion-real-f2`, base `06e53cc`)

| Sha | Mensaje |
|---|---|
| `333e37a` | `feat(web): registrar la cookie de sesion y los esquemas sociales dirigidos por configuracion` |
| `4078b13` | `feat(identity): vincular identidades externas y exponer el acceso y el cierre de sesion` |
| `0373876` | `feat(web): autorizar por permiso con nueve politicas sobre el AppUser de la sesion` |
| `28b6464` | `feat(web): proteger las rutas administrativas y de moderacion con autorizacion real` |
| `70ac33e` | `fix(web): responder 400 ante un token antiforgery ausente en el acceso social` |
| (docs) | `docs(sdd): registrar el progreso de apply de la fase F2 de INC-46` |

### Commits de los slices previos

| Slice | Shas |
|---|---|
| F0 (`inc/autenticacion-real`, → `3ec23d5`) | `ff6eaee` specs canónicas y Markdown LF · `c18f652` `CanManageUsers`/`CanViewAuditLog` · `ee582cb` `AuditAction.LinkedFounderIdentity` |
| F1 (`inc/autenticacion-real-f1`, → `06e53cc`) | `b4ab43a` entidad `ExternalLogin` · `21eb78a` persistencia e índice único · `36cb30f` migración PostgreSQL `AddExternalLogins` · `959f222` docs de apply |

## TDD Cycle Evidence (F2)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 + 2.2 + 2.3 | `Web/WebAuthenticationRegistrationTests.cs` | Unit + DI real | ✅ 1041/1041 base | ✅ CS0234 `Ludeka.Application.Features.Identity` y CS0246 `ExternalProviderOptions` | ✅ 5/5 focal | ✅ Deshabilitado con credenciales, habilitado sin credenciales, habilitado con credenciales, cookie, `appsettings.json` versionado | ✅ Alias de tipo para evitar la colisión con `Microsoft.AspNetCore.Authentication.AuthenticationOptions`; scopes con guarda anti-duplicado |
| 2.4 + 2.5 | `Application/ExternalLoginServiceTests.cs` | Integration (SQLite `:memory:`) | ✅ 1046/1046 acumulada | ✅ CS0246 `IExternalLoginService` | ✅ 6/6 focal | ✅ Par reincidente, correo verificado con caja distinta, alta comunitaria, correo sin verificar, sin correo, claves vacías | ✅ El correo sin verificar deja de persistirse en `AppUser` (índice único de `Email`) y queda solo en `ExternalLogin.ProviderEmail` |
| 2.7 + 2.8 | `Web/PolicyAuthorizationTests.cs` | Integration (SQLite `:memory:`) | ✅ 1052/1052 acumulada | ✅ CS0246/CS0311 `IAuthorizationHandler`, `IAuthorizationService` | ✅ 48/48 focal | ✅ 9 políticas × anónimo, comunitario, fundador y suspendida + moderador con flag exacto + mapeo bandera→política + revocación en caliente | ✅ `PermissionRequirement` y `LudekaRoleNames` viven junto a las políticas; `Assert.NotEqual` sustituido por `IsNullOrWhiteSpace` (xUnit2000 evitado) |
| 2.9 + 2.10 + 2.11 + 2.12 | `Web/AuthorizationPipelineContractTests.cs` | Contract (fuente) + arranque real | ✅ 1100/1100 acumulada | ✅ 5/20 casos en rojo: páginas sin `[Authorize]`, `Routes.razor` con `RouteView`, sondas sin `AllowAnonymous` | ✅ 20/20 focal (79/79 con el resto de F2) | ✅ 10 páginas + 4 alias + 4 endpoints anónimos + orden del pipeline + `RedirectToLogin` guardado | ✅ Ruta de login tomada del identificador `ExternalAuthenticationSchemes.LoginPath` en el contrato |
| 2.6 (antiforgery) | `Web/AuthorizationPipelineContractTests.cs` + arranque real | Runtime | ✅ 1120/1120 acumulada | ✅ POST sin token → 500 observado con `[FromForm]` | ✅ POST sin token → **400** y 0 excepciones en el log | ✅ POST con token desde el formulario interactivo completa el desafío del proveedor (navegador real) | ✅ Validación explícita de antiforgery antes de leer el formulario |

## Work Unit Evidence (F2)

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U2-A Registro / `333e37a` | `dotnet test Ludeka.sln --filter FullyQualifiedName~WebAuthenticationRegistrationTests` → **5/5** | Arranque real con SQLite y `Authentication__Providers__Google__Enabled=true` sin credenciales: la app arranca, registra el aviso y sirve `/healthz` **200** | Revertir el commit: desaparecen opciones, registro y sección de configuración; `appsettings.json` vuelve a su estado previo |
| U2-B Identidad externa / `4078b13` | `dotnet test Ludeka.sln --filter FullyQualifiedName~ExternalLoginServiceTests` → **6/6** | Navegador real: `/login` interactivo con botones y token; clic en Google → 302 a `accounts.google.com` con el `client_id` configurado | Revertir el commit: se eliminan servicio, eventos, pantalla y endpoints; `Program.cs` conserva solo el registro de esquemas |
| U2-C Políticas / `0373876` | `dotnet test Ludeka.sln --filter FullyQualifiedName~PolicyAuthorizationTests` → **48/48** | SQLite `:memory:` con usuarios reales; la revocación fuera de banda deja de autorizar sin reiniciar | Revertir el commit: las páginas quedan sin políticas que las respalden |
| U2-D Protección / `28b6464` + `70ac33e` | `dotnet test Ludeka.sln --filter FullyQualifiedName~AuthorizationPipelineContractTests` → **20/20** | Arranque real: `/healthz` 200, `/ready` 200, `/login` 200 y **302 a `/login`** en las 12 rutas administrativas y de moderación; POST sin token → 400 | Revertir los commits: pipeline, atributos y rutas vuelven atrás; la simulación nunca se tocó |

## Verificación observada (registro)

| Comando / comprobación | Resultado observado |
|---|---|
| `dotnet test Ludeka.sln --configuration Release` | **1120 correctas, 0 con error, 0 omitidas** (línea base `06e53cc`: 1041 → +79 casos nuevos). Avisos preexistentes sin cambios (CS8629 `BggImportService.cs:149`, CS8604 `UserCollectionItemTests.cs:35` y `GameLoanTests.cs:38`, CS8625 `SocialIngestionServiceTests.cs:108`, xUnit2013 `WebMarkupContractTests.cs:736` y `SocialIngestionServiceTests.cs:257`); no se añadió ningún aviso nuevo |
| Arranque real con SQLite y **sin credenciales OAuth** (`ASPNETCORE_ENVIRONMENT=Production`, `Database__SeedDemoData=false`, `ConnectionStrings__DefaultConnection=Data Source=<temp>\ludeka-f2-final.db`, `dotnet bin\Release\net10.0\Ludeka.Web.dll`) | Arranca y sirve `/healthz` **200**; sin avisos de proveedor con la configuración por defecto (los tres `Enabled=false`) |
| Arranque real con `Authentication__Providers__Google__Enabled=true` y **sin** `ClientId`/`ClientSecret` | Arranca igual; aviso `El proveedor de autenticación 'Google' está habilitado pero no tiene ClientId/ClientSecret configurados; su esquema no se registrará…`; `/healthz` **200** |
| `curl /healthz` · `curl /ready` | **200** · **200** (ambas `.AllowAnonymous()`, sin política de fallback) |
| `curl` sin sesión a `/admin/auditoria`, `/admin/usuarios`, `/moderacion/reportes` | **302** → `/login?ReturnUrl=…` en los tres (también `/admin/reportes`, `/moderacion/multimedia`, `/admin/multimedia`, `/admin/cola-catalogacion`, `/admin/instagram`, `/admin/ingesta-social`, `/admin/canales-monitorizados`, `/admin/notificaciones` y `/admin/eventos`) |
| `curl /login` | **200** (con `Enabled=false` muestra «El acceso social aún no está configurado» y ningún botón de proveedor) |
| Navegador real (Chrome DevTools) con Google configurado con credenciales de prueba | `/login` renderiza el formulario con `__RequestVerificationToken` y el botón «Continuar con Google»; el formulario se envía sin mejora progresiva y el desafío redirige a `accounts.google.com` con el `client_id` y el `redirect_uri` del esquema (Facebook deshabilitado: no aparece) |
| POST `curl` sin token a `/login/external` | **400** (`Token antiforgery ausente o inválido`) y **0** excepciones en el log de la aplicación |
| Proveedores efectivamente registrados con la configuración por defecto | **Ninguno**: `Authentication:Providers:{Google,Discord,Facebook}.Enabled=false` y sin credenciales; Facebook desactivado por defecto |

## Desviaciones y hallazgos

1. **Mapeo de 2.10/2.11 fijado por el maintainer (no por `design.md`)**: `/admin/notificaciones` y `/admin/eventos` usan la política de rol `RolModerador` (`RequireRole("FoundingTeam", "Moderator")`) en lugar de `PermisoGestionarEditores`, y `/admin/ingesta-social` y `/admin/canales-monitorizados` usan `PermisoAprobarMedios`. El comportamiento público observable no cambia respecto al marcado actual. **Punto a confirmar por el maintainer** (R3/§9.3): si esas cuatro páginas deben pasar a un permiso granular, sobre todo las dos últimas, que hoy admitían cualquier `Moderator`.
2. **Correo sin verificar**: no se persiste en `AppUser.Email` (índice único) sino que se registra solo en `ExternalLogin.ProviderEmail`; las cuentas cuyo proveedor no entrega correo reciben un correo sintético no enrutable (`<clave>@<proveedor>.ludeka.invalid`). Evita fusiones por correo no confiable.
3. **Trazas de correo verificado**: se mapean claims propios (`ludeka:email_verified`) desde `email_verified` (Google) y `verified` (Discord/Facebook) porque los handlers oficiales no los exponen de forma uniforme.
4. **Antiforgery explícito**: el endpoint `/login/external` valida el token antes de leer el formulario y responde 400; sin ello, un POST sin token producía 500 al reejecutar el manejador de errores sobre una petición con formulario ya invalidado.
5. **`IExternalLoginRepository` y `ExternalLoginRepository` ya registrados** en `Program.cs` como `Scoped`, junto a `IExternalLoginService` (handoff desde F1).
6. **Simulación intacta** (fuera de alcance, según lo acordado): `DefaultCurrentUserService` sigue registrado como `Singleton`, y `SwitchRole`/`SwitchUser`, el conmutador de `MainLayout.razor` y los botones de `AuditLogViewer`, `MediaModeration`, `GameReportsModeration` y `UserManagement` no se tocaron. F3 es quien los retira.
7. **Fuera de alcance**: F3, F4 y F5 sin tocar; no se revalidó permiso en los servicios de escritura, no se modificó la política de anonimia de las entidades y no se ejecutó `sdd-archive`. Tampoco hubo cambios de esquema de base de datos ni efectos de despliegue.

## Presupuesto y frontera de PR

- **Líneas cambiadas del slice F2**: `git diff --shortstat 06e53cc` → **1567 inserciones y 73 supresiones en 30 archivos**, de las que **1472 inserciones y 4 supresiones (28 archivos) son código y pruebas** (`src/` + `tests/`) y 95 inserciones / 69 supresiones son los artefactos SDD (`tasks.md` y este `apply-progress.md`). Sin migraciones ni artefactos generados por EF. Dentro del presupuesto de 1500 líneas de código y pruebas.
- **Modo**: chained/stacked PR slice (`stacked-to-main`), PR 3–4 de 8. Frontera: de `06e53cc` a la autorización efectiva por política con la identidad simulada todavía viva.
- **Reversión**: revertir los commits devuelve el árbol a `06e53cc`; no hay migraciones ni cambios de esquema en este slice.

## Estado acumulado

- **F0: 6/6** (mergeada en `3ec23d5`). **F1: 8/8** (mergeada en `06e53cc`). **F2: 13/13** (este slice).
- Suite completa: **1120/1120** en Release, 0 con error, 0 omitidas.
- Listo para la verificación independiente de `sdd-verify` sobre el slice F2.
