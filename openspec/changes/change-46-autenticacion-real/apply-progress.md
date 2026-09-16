# Apply Progress: change-46-autenticacion-real (INC-46) — Fases F0, F1, F2 y F3

> Fase SDD `sdd-apply`. Store: openspec (este archivo + `tasks.md`).
> Worktree: `C:\repos\ludeka-wt\autenticacion-real`.
> · Slice **PR-1 / F0**: rama `inc/autenticacion-real`, mergeado a `main` en `3ec23d5`.
> · Slice **PR-2 / F1**: rama `inc/autenticacion-real-f1`, base `3ec23d5`, mergeado a `main` en `06e53cc`.
> · Slice **PR-3 / F2**: rama `inc/autenticacion-real-f2`, base `06e53cc`, mergeado a `main` en `83063bd` (PR #14).
> · Slice **PR-5/PR-6 / F3** (este): rama `inc/autenticacion-real-f3`, base `83063bd` (`origin/main`).
> Modo: **TDD estricto**. Runner contractual: `dotnet test Ludeka.sln`. Cadena `stacked-to-main`.
> Alcance: Paso 2 de la migración — retirada de la identidad simulada **sin** revalidar todavía los
> servicios de escritura (F4) y **sin** ejecutar `sdd-archive` (F5).

## Estado por fase

| Fase | Tareas | Estado |
|---|---|---|
| F0 — Artefactos y dominio de permisos | 6/6 | ✅ mergeada en `main` (`3ec23d5`) |
| F1 — Persistencia de identidad externa | 8/8 | ✅ mergeada en `main` (`06e53cc`) |
| F2 — Autenticación social y autorización por política | 13/13 | ✅ mergeada en `main` (`83063bd`) |
| F3 — Retirada de la identidad simulada | 8/8 | ✅ completada (este slice) |
| F4 — Revalidación en escritura y anonimia | 0/6 | ⬜ sin tocar |
| F5 — Verificación, documentación y archive | 0/6 | ⬜ sin tocar |

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

1. **`Icon.razor` — paso directo de atributos adicionales (`CaptureUnmatchedValues`)**: doce puntos de la interfaz pasaban `class`, `Class` o `aria-hidden` a `<Icon>`, que no los declara, y el render lanzaba `InvalidOperationException` (`Icon does not have a property matching the name 'class'`). Afectaba a rutas **públicas** (`/radar` con la pestaña de ofertas, la galería de la ficha y el hub multimedia de la ficha) y a tres páginas protegidas. El arreglo es aditivo y no cambia el contrato propio del icono (`Name`, `Size`, `StrokeWidth`, `Title`) ni su contrato de markup. **F4/verify deben comprobar las tres páginas protegidas** (`/admin/cola-catalogacion`, `/admin/ingesta-social`, `/admin/canales-monitorizados`), que quedaron con `class`/`Class` sobre `<Icon>` y ahora se renderizan con el nuevo paso directo.
2. **`SqliteUserRepository` — `AsNoTracking()` en las lecturas de identidad** (`GetByIdAsync`, `GetByEmailAsync`, `GetAllAsync`): sin él, el `DbContext` de larga vida del circuito devolvería la entidad rastreada y la suspensión o revocación en caliente no surtiría efecto (riesgo R5 del `tasks.md`).
3. **Alineación del marcado con el permiso de la política** (solo experiencia de uso, nunca control de acceso): `MainLayout` muestra el bloque de Gobernanza con `CanManageUsers` o `CanViewAuditLog`, y las páginas `UserManagement`, `AuditLogViewer`, `MediaModeration` y `GameReportsModeration` condicionan su bloque de aviso al permiso de su propia política (`CanManageUsers`, `CanViewAuditLog`, `CanApproveMedia`, `CanResolveReports`). Antes dependían de `IsFoundingTeam`, que con la identidad simulada siempre era verdadero.
4. **`App.razor`**: el respaldo de tema en `localStorage` ya no cae al identificador simulado `usuario-fundador-ludeka`; sin identidad se usa el tema global. Es la última traza de la simulación fuera del ámbito estrictamente UI.
5. **Guardas de anonimia en servicios: NO se añadieron en este slice** (es el objeto de F4). La navegación pública anónima no necesitó ninguna: todas las lecturas toleran `UserId` vacío y devuelven vacío, y las escrituras públicas siguen esperando a la revalidación de F4.

## Desviaciones y hallazgos

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

## Presupuesto y frontera de PR

- **Líneas cambiadas del slice F3**: `git diff --shortstat 83063bd` → **1180 inserciones y 378 supresiones en 42 archivos** de `src/` y `tests/`, con un único borrado (`DefaultCurrentUserService.cs`). Artefactos SDD (`tasks.md` y este `apply-progress.md`) aparte. Dentro del presupuesto de 3000 líneas del slice.
- **Modo**: chained/stacked PR slice (`stacked-to-main`). Los PR 5 y 6 del desglose se entregan como una unidad cohesionada (el contrato sin conmutadores es la precondición de compilación de la identidad real y de la invalidación).
- **Frontera**: de `83063bd` a la identidad de sesión real con invalidación de circuito; sin revalidación de escritura (F4) y sin migraciones ni cambios de esquema.
- **Reversión**: revertir los commits devuelve el árbol a `83063bd`.
- **Slice F2 (histórico)**: `git diff --shortstat 06e53cc` → **1567 inserciones y 73 supresiones en 30 archivos**, de las que 1472/4 (28 archivos) eran código y pruebas; modo chained/stacked PR slice, frontera de `06e53cc` a la autorización efectiva por política con la identidad simulada todavía viva.

## Estado acumulado

- **F0: 6/6** (mergeada en `3ec23d5`). **F1: 8/8** (mergeada en `06e53cc`). **F2: 13/13** (mergeada en `83063bd`). **F3: 8/8** (este slice).
- Suite completa: **1142/1142** en Release, 0 con error, 0 omitidas (línea base `83063bd`: 1120).
- Listo para la verificación independiente de `sdd-verify` sobre el slice F3. F4 y F5 quedan sin tocar.
