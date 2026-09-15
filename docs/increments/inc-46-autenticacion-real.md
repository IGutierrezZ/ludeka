# INC-46: Autenticación Real, Autorización por Roles y Retirada de la Identidad Simulada

> **Estado:** ⏳ En progreso (pendiente de aprobación de alcance)
> **Fecha de Inicio:** 2026-09-15
> **Rama de Trabajo:** `inc/autenticacion-real`
> **Worktree:** `C:\repos\ludeka-wt\autenticacion-real`
> **Dependencias:** INC-20 (Gestión de Usuarios, Permisos y Auditoría), INC-38 (PostgreSQL/Supabase), INC-39 (Docker/Cloud Run), INC-46 (este incremento) es **bloqueante de la salida a producción**
> **Especificación Viva del Sistema:** [14. Gestión de Usuarios, Permisos y Auditoría](file:///c:/repos/Ludeka/docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md)

---

## 1. Motivación y Visión

Ludeka **no tiene autenticación**. No es una carencia parcial: es ausencia total. La evidencia verificada en `main` (`a54cb31`):

1. **La identidad es un singleton mutable compartido.** `Program.cs:227` registra `ICurrentUserService` como `AddSingleton<ICurrentUserService, DefaultCurrentUserService>()`. El comentario del propio código lo justifica: *"Singleton para permitir alternancia interactiva de roles en la sesión"*.
2. **Todo visitante es la Mesa Fundadora.** `DefaultCurrentUserService.cs:14-18` inicializa `_roles = ["FoundingTeam", "Moderator"]`, `_permissions = ModeratorPermission.All` y `_isSuspended = false`. No existe ninguna ruta que exija sesión.
3. **La escalada de privilegios es un botón público.** `SwitchRole` (`:50-79`) y `SwitchUser` (`:81-139`) se exponen en la interfaz: `MainLayout.razor:431-445` ("Cambiar"), `UserManagement.razor:350-360`, `AuditLogViewer.razor:360-364`, `MediaModeration.razor:451-455` y `GameReportsModeration.razor:598-602`. Cualquier visitante puede pulsar "SwitchToFounder" y obtener permisos totales.
4. **No existe ninguna primitiva de autorización.** Búsqueda de `AddAuthentication`, `AddAuthorization`, `AddIdentity`, `AddCookie`, `JwtBearer`, `OpenIdConnect`, `AuthorizeView`, `AuthorizeRouteView`, `[Authorize]` y `RequireAuthorization` en `src/`: **cero resultados**. `Routes.razor` usa `RouteView`, no `AuthorizeRouteView`. `Program.cs:355-365` no llama a `UseAuthentication` ni `UseAuthorization`.
5. **Las 11 páginas administrativas se protegen solo en el marcado.** `/admin/usuarios` (`UserManagement.razor:15`), `/admin/ingesta-social` (`SocialInboxModeration.razor:494-496`), `/admin/notificaciones` (`AdminNotifications.razor:39`), `/admin/canales-monitorizados` (`MonitoredAccountsDirectory.razor:432-434`), `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor:551-552`), `/admin/auditoria` (`AuditLogViewer.razor:14`), `/admin/instagram` (`InstagramModeration.razor:600`), `/admin/eventos` (`EventsManagement.razor:285`), `/moderacion-media` (`MediaModeration.razor:16`), `/moderacion/reportes` (`GameReportsModeration.razor:16`) y `/admin/multimedia`. Son condiciones `@if (!CurrentUserService.IsFoundingTeam)` que no impiden ni el render del componente ni la ejecución de sus handlers.
6. **El despliegue es público por diseño.** `.github/workflows/ci-cd.yml:102` despliega con `--allow-unauthenticated`.

**Consecuencia:** hoy, cualquier persona con la URL puede leer el registro de auditoría, suspender cuentas, aprobar medios, resolver reportes, publicar en Instagram y editar el catálogo. La autorización existe en el modelo de dominio (`AppUser`, `UserRole`, `UserStatus`, `ModeratorPermission`) pero **no está conectada a ninguna sesión real**.

**Visión del incremento:** que la identidad deje de ser un estado global mutable y pase a ser una sesión autenticada y verificable, preservando intacto el modelo de permisos ya construido en INC-20.

---

## 2. Objetivos y Alcance Técnico

### 2.1. Proveedor de identidad y sesión

Se propone **autenticación por cookie de ASP.NET Core** con **OpenID Connect externo (Google)** como método principal, en lugar de ASP.NET Core Identity completo.

- `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)` con cookie `HttpOnly`, `SecurePolicy = Always`, `SameSite = Lax` y caducidad deslizante.
- `AddOpenIdConnect` hacia Google, solicitando únicamente `openid`, `email` y `profile`.
- Razón para descartar Identity completo: Ludeka ya posee su propio modelo de usuario (`AppUser` con `Role`, `Status` y `Permissions`), su repositorio (`SqliteUserRepository`) y su servicio de gestión (`UserManagementService`). Identity impondría tablas propias (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`), duplicaría el modelo y exigiría infraestructura de correo que hoy no existe en ninguna variable de configuración.

### 2.2. Puente entre la sesión y `AppUser`

- Nueva implementación `AuthenticatedCurrentUserService` que resuelve la identidad desde la sesión, **registrada como `Scoped`** (nunca `Singleton`).
- Vinculación por `email` verificado del proveedor externo contra `AppUser.Email` (`Core/Entities/AppUser.cs:13`).
- **Auto-aprovisionamiento:** primer inicio de sesión de un correo desconocido crea un `AppUser` con `UserRole.CommunityUser` y `ModeratorPermission.None`. Nunca se auto-concede `FoundingTeam`.
- `AdminUserSeeder.EnsureAdminUserAsync` (`Program.cs:343`) se conserva como garantía de existencia del administrador fundador, pero **deja de otorgar identidad implícita**: solo asegura la fila.
- Los roles y permisos siguen leyéndose de `AppUser` (`HasPermission`, `Core/Entities/AppUser.cs:69-90`), de modo que el modelo de INC-20 no cambia.

### 2.3. Autorización en rutas y componentes

- `AddAuthorization` con una política por permiso: `PermisoEditarFichas`, `PermisoAprobarMedios`, `PermisoResolverReportes`, `PermisoGestionarEditores`, `PermisoGestionarCreadores`, `PermisoGestionarTiendas`, `PermisoPublicarInstagram`, `PermisoVerAuditoria`, `PermisoGestionarUsuarios`. Cada política evalúa `ModeratorPermission` sobre el `AppUser` de la sesión.
- `UseAuthentication()` y `UseAuthorization()` en el pipeline, **antes** de `UseAntiforgery()` (`Program.cs:364`).
- `Routes.razor` pasa de `RouteView` a **`AuthorizeRouteView`** con `NotAuthorized` y `RedirectToLogin`.
- `AddCascadingAuthenticationState()` para que `AuthorizeView` funcione en SSR interactivo.
- Sustitución de las 11 comprobaciones de marcado por `[Authorize(Policy = ...)]` a nivel de página, manteniendo las comprobaciones de UI solo como mejora de experiencia (ocultar acciones no permitidas), nunca como control de seguridad.

### 2.4. Retirada de la identidad simulada

- Eliminar `DefaultCurrentUserService` y su registro `Singleton`.
- Eliminar `SwitchRole` / `SwitchUser` del contrato `ICurrentUserService` (`Application/Contracts/ICurrentUserService.cs:13,21-23`) y todos sus puntos de uso.
- Eliminar el conmutador de rol de `MainLayout.razor:99-116,431-445`.
- Eliminar los botones `SwitchToFounder` de `AuditLogViewer.razor:362`, `MediaModeration.razor:453` y `GameReportsModeration.razor:600`.
- Eliminar la simulación de usuario de `UserManagement.razor:350-360`; la página pasa a listar y administrar usuarios reales.
- Los 12 dobles de test que implementan `ICurrentUserService` se simplifican al nuevo contrato.

### 2.5. Política de anonimia (requisito duro derivado de la evidencia)

Introducir sesión real **rompe hoy** cualquier flujo con `UserId` vacío, porque las entidades de dominio rechazan el valor por constructor: `GamePlayLog.cs:34-35`, `UserCollectionItem.cs:34-35`, `UserGameReview.cs:37-38`, `GameLoan.cs:23-24`, `RuleQuestion.cs:36-37`, `RuleAnswer.cs:32-33`, `RuleVote.cs:17-18`, `AuditLogEntry.cs:41-42` y `UserPreference.cs:22-24`.

Regla de diseño obligatoria:

- **Navegación anónima:** catálogo, fichas públicas, directorios, radar de ofertas, eventos, sorteos y novedades permanecen totalmente accesibles sin sesión.
- **Acciones con identidad:** "Mi Ludoteca", colección, préstamos, partidas, reseñas, preguntas de reglas, reportes de error y preferencias exigen sesión iniciada y redirigen a login si no la hay.
- **Nunca** se escribe un `UserId` vacío ni un usuario centinela. Si un flujo no tiene identidad, no se ejecuta.
- `MyLibrary.razor:463` deja de construir `/u/@UserId` con valor vacío.

### 2.6. Cierre de la superficie de escalada

- Los endpoints interactivos de Blazor Server viajan por el circuito; la autorización debe aplicarse por página y por invariante de servicio, porque `InteractiveServer` no reejecuta el pipeline HTTP por evento. Cada servicio de escritura revalida el permiso, no solo la UI.
- Añadir un `IHostedService` o filtro que invalide el circuito cuando el `AppUser` asociado cambia de `Status` a `Suspended` o sus permisos mutan, para que un cambio de rol sea efectivo sin esperar al cierre de sesión.

### 2.7. Configuración y secretos

| Variable | Uso |
|---|---|
| `Authentication__Google__ClientId` | ID de cliente OAuth 2.0 de Google Cloud Console |
| `Authentication__Google__ClientSecret` | Secreto del cliente OAuth (Secret Manager) |
| `Authentication__Google__Authority` | `https://accounts.google.com` |
| `Authentication__Cookie__ExpireMinutes` | Caducidad de la cookie |
| `AdminUser__Email` | Correo del administrador fundador autorizado inicial |

### 2.8. Pruebas (Strict TDD activo)

- Runner contractual: `dotnet test Ludeka.sln`.
- Tests de política de autorización: usuario anónimo denegado en las 11 rutas admin; `CommunityUser` denegado; `Moderator` denegado sin el permiso concreto; `FoundingTeam` permitido.
- Tests de vinculación de identidad: auto-aprovisionamiento crea `CommunityUser` y nunca `FoundingTeam`; correo desconocido no hereda permisos; usuario `Suspended` no obtiene sesión útil.
- Tests de anonimia: cada uno de los 9 constructores que rechazan `UserId` vacío permanece inalcanzable sin sesión.
- Test de regresión: `SwitchRole`/`SwitchUser` ya no existen en el contrato (la compilación lo garantiza).

---

## 3. Decisiones pendientes para el maintainer

1. **Método de acceso.** Se propone **solo Google OAuth**. Alternativa: añadir además cuentas locales con correo y contraseña. Implica infraestructura de correo (verificación y recuperación) que hoy no está configurada en ninguna variable del proyecto.
2. **Comportamiento del `AdminUserSeeder`.** Se propone conservar la fila del fundador pero sin vincularla a ninguna sesión automática. Alternativa: además vincular el `AdminUser__Email` a la sesión de Google que coincida, para garantizar acceso inicial del maintainer.
3. **Alcance de "Mi Ludoteca" para anónimos.** Se propone exigir sesión. Alternativa: ludoteca efímera en almacenamiento local del navegador, que sería un incremento aparte.

---

## 4. Criterios de Aceptación y Verificación

1. Ninguna ruta bajo `/admin` ni `/moderacion` es accesible sin sesión autenticada y con el permiso correspondiente.
2. No existe en el código ninguna forma de cambiar de rol o de usuario sin autenticarse.
3. Un visitante anónimo navega catálogo, fichas, directorios y radar sin error 500 y sin escrituras con identidad vacía.
4. `ICurrentUserService` deja de ser `Singleton` mutable y pasa a resolver la identidad de la sesión.
5. Un usuario con `UserStatus.Suspended` no obtiene acceso a funciones de moderación.
6. La auditoría registra únicamente identidades reales; ningún `AuditLogEntry` se crea con identidad simulada.
7. Suite completa en verde con `dotnet test Ludeka.sln`, incluyendo los nuevos tests de política y de anonimia.
8. Smoke test con navegador real: intento de acceso directo a `/admin/auditoria` sin sesión redirige a login.

---

## 5. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Sustituir el servicio de identidad toca **29 componentes `.razor` y 16 servicios `.cs`** | Alto: regresiones amplias | Cambio en dos pasos: primero autorización por política sin quitar el servicio; después retirada de la simulación |
| Constructores que lanzan con `UserId` vacío | Alto: error 500 en flujos públicos | Guardar cada acción tras comprobación de sesión antes de invocar el servicio |
| `InteractiveServer` no reejecuta el pipeline HTTP por evento | Alto: falsa sensación de protección | Revalidar permiso dentro de cada servicio de escritura |
| Invalidación de sesión al suspender una cuenta | Medio | Suscripción a cambios de usuario que fuerce revalidación |
| Añadir OpenID Connect introduce dependencia externa | Medio | Cookie de sesión propia tras el primer login; configuración íntegra por variables |
