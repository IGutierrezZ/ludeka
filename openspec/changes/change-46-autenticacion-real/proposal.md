# Propuesta SDD — INC-46: Autenticación Real, Autorización por Roles y Retirada de la Identidad Simulada

## 1. Intención

Ludeka no tiene autenticación: `ICurrentUserService` es un singleton mutable (`Program.cs:227`) que concede `FoundingTeam` y `Moderator` a cualquier visitante, la escalada de rol es un botón público y las 11 rutas de `/admin` y `/moderacion` solo se protegen en el marcado. Este incremento convierte la identidad en una sesión verificable y cierra la escalada, preservando el modelo de permisos de INC-20.

## 2. Alcance

**Dentro del alcance:**

- Acceso social de un solo clic con Google (OpenID Connect), Discord y Facebook (desactivable por configuración); sin correo/contraseña ni infraestructura de correo.
- `ExternalLogin` con índice único `(Provider, ProviderKey)`: vinculación por proveedor-clave → correo verificado → alta como `CommunityUser`; nunca concede `FoundingTeam`.
- `AuthenticatedCurrentUserService` (`Scoped`) sustituye a `DefaultCurrentUserService`; se eliminan `SwitchRole`/`SwitchUser` y sus cinco puntos de UI.
- Políticas de permiso, `UseAuthentication`/`UseAuthorization` antes de `UseAntiforgery`, `AuthorizeRouteView` y `AddCascadingAuthenticationState`.
- Revalidación de permiso en cada servicio de escritura e invalidación de circuito al suspender o cambiar permisos.
- Anonimia: navegación pública intacta; escrituras con identidad exigen sesión; nunca `UserId` vacío ni usuario centinela.

**Fuera del alcance:**

- Cuentas locales con correo/contraseña y toda la infraestructura de correo (verificación, recuperación).
- ASP.NET Core Identity y sus tablas propias.
- Ludoteca efímera anónima en el navegador (incremento aparte).

## 3. Capacidades

**Nuevas:**

- `social-login-authentication`: acceso social multi-proveedor, cookie de sesión, aprovisionamiento y vinculación.
- `policy-based-authorization`: políticas por permiso, protección de rutas y revalidación en servicios y circuitos.
- `anonymity-policy`: frontera entre navegación pública y acciones con identidad.

**Modificadas:**

- `editorial-role-management`: se elimina el conmutador de roles y la abstracción pasa a identidad de sesión.
- `media-moderation-panel`: el control de acceso pasa de comprobación de marcado a autorización efectiva.

## 4. Enfoque

Cookie de ASP.NET Core con esquemas externos dirigidos por configuración (`Authentication__Providers__*`). `ExternalLogin` es el puente hacia `AppUser`; los permisos siguen leyéndose con `HasPermission`. `AdminUserSeeder` conserva la fila del fundador sin identidad implícita. Migración en dos pasos con la suite en verde entre ambos: primero autorización por política; después, retirada de la simulación.

**Pregunta abierta para diseño (no se resuelve aquí):** ¿entra en este incremento la pantalla de vinculación manual de proveedores o se aplaza? Sin ella, quien acceda con Discord y luego con Google puede acabar con dos cuentas si el correo no coincide o no está verificado.

## 5. Áreas Afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/Ludeka.Web/Program.cs` | Modificado | Pipeline de autenticación, registro `Scoped`, políticas |
| `DefaultCurrentUserService` → `AuthenticatedCurrentUserService` | Eliminado / Nuevo | Identidad desde la sesión (`Scoped`) |
| `ICurrentUserService.cs` | Modificado | Sin `SwitchRole`/`SwitchUser` |
| `Core/Entities/ExternalLogin.cs` | Nuevo | Puente de identidad externa |
| `Routes.razor`, `MainLayout.razor` | Modificado | `AuthorizeRouteView`; sin conmutador de rol |
| 11 páginas admin y de moderación + `UserManagement`, `AuditLogViewer`, `MediaModeration`, `GameReportsModeration` | Modificado | `[Authorize(Policy = ...)]`; sin botones de simulación |
| 29 componentes `.razor` y 16 servicios `.cs` | Modificado | Consumen identidad de sesión |
| `tests/Ludeka.UnitTests` | Modificado | Tests de política, vinculación y anonimia; 12 dobles |

## 6. Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Sustituir la identidad afecta a 29 componentes y 16 servicios | Alta | Migración en dos pasos con la suite en verde entre ambos |
| 9 entidades lanzan con `UserId` vacío | Alta | Guardar cada acción tras comprobar sesión; tests de anonimia |
| `InteractiveServer` no reejecuta el pipeline | Alta | Revalidar permiso en cada servicio de escritura |
| Proveedores sin correo verificado | Media | Vinculación primaria por `(Provider, ProviderKey)` |
| Suspensión sin efecto inmediato | Media | Invalidación de circuito al cambiar `Status` o permisos |

## 7. Plan de Reversión

Revertir el PR de `inc/autenticacion-real`: el cambio solo añade la tabla `ExternalLogin` y no altera datos existentes. `main` actual queda como línea base conocida hasta que la suite y el smoke test estén en verde.

## 8. Dependencias

- Credenciales OAuth (Google, Discord, Meta) y secretos en Google Secret Manager.
- URIs `/signin-google`, `/signin-discord` y `/signin-facebook`, más `localhost` en desarrollo.
- INC-38/INC-39 para el despliegue; no bloquean el desarrollo local.

## 9. Criterios de Éxito

- [ ] Ninguna ruta `/admin` o `/moderacion` accesible sin sesión y permiso; `/admin/auditoria` redirige a login.
- [ ] `SwitchRole`/`SwitchUser` no existen en contrato ni UI (lo garantiza la compilación).
- [ ] Navegación anónima sin error 500 ni `UserId` vacío.
- [ ] `(Provider, ProviderKey)` reincidente resuelve a la misma cuenta; nunca se auto-concede `FoundingTeam`.
- [ ] Un proveedor `Enabled=false` no se registra ni aparece en el acceso.
- [ ] Un usuario `Suspended` no accede a moderación; la auditoría solo registra identidades reales.
- [ ] `dotnet test Ludeka.sln` en verde, con tests de política, vinculación y anonimia.
