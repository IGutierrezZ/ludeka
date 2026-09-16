# 32. Autenticación Social, Autorización por Permisos y Política de Anonimia

> **Estado:** Implementado y Verificado (1.345 pruebas unitarias en verde, 0 errores, 0 omitidas)
> **Incremento:** INC-46 (`change-46-autenticacion-real`)
> **Módulos relacionados:** [09. Arquitectura y Despliegue](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md) · [14. Gestión de Usuarios, Permisos y Auditoría](file:///c:/repos/Ludeka/docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md)

## 1. Visión General y Propósito

INC-46 sustituye la identidad simulada (un singleton mutable que concedía `FoundingTeam` y `Moderator` a cualquier visitante) por una **sesión autenticada y verificable**:

- El acceso es **social de un solo clic** con Google, Discord y Facebook (este último opcional), habilitables por configuración. No hay cuentas con correo/contraseña, ni formulario de registro, ni infraestructura de correo.
- La autorización se resuelve con **11 políticas de permiso** sobre el `AppUser` releído de la base de datos; los claims de la cookie nunca deciden.
- Cada **servicio de escritura revalida** sesión y permiso antes de mutar, porque `InteractiveServer` no reejecuta el pipeline HTTP por evento.
- La **navegación pública permanece intacta** sin sesión; ninguna escritura con identidad se ejecuta sin una identidad real (sin `UserId` vacío ni usuario centinela).

---

## 2. Sesión y Acceso Social

### 2.1 Cookie de sesión propia

| Atributo | Valor |
|---|---|
| Esquema / cookie | `Cookies` / `ludeka.session` (`ExternalAuthenticationSchemes`) |
| `HttpOnly` | `true` |
| `SecurePolicy` | `Always` |
| `SameSite` | `Lax` |
| Caducidad | deslizante (`SlidingExpiration = true`), `Authentication:Cookie:ExpireMinutes` (43200 min por defecto) |
| Rutas | `LoginPath` = `/login` · `LogoutPath` = `/logout` · `AccessDeniedPath` = `/login` |

### 2.2 Proveedores dirigidos por configuración

Un proveedor solo se registra si está `Enabled` **y** tiene credenciales; si está habilitado sin credenciales, la aplicación arranca, emite un aviso y no registra su esquema ni su botón.

| Proveedor | Esquema | Callback | Claims propios |
|---|---|---|---|
| Google | `GoogleDefaults` | `/signin-google` | `email_verified` → `ludeka:email_verified` |
| Discord | `DiscordAuthenticationDefaults` | `/signin-discord` | `verified` → `ludeka:email_verified` |
| Facebook | `FacebookDefaults` | `/signin-facebook` | `verified` → `ludeka:email_verified` |

Variables: `Authentication__Providers__{Google|Discord}__{Enabled|ClientId|ClientSecret}` y `Authentication__Providers__Facebook__{Enabled|AppId|AppSecret}`. En `appsettings.json` los tres vienen `Enabled=false` y sin credenciales; los secretos viven solo en Google Secret Manager. Registro y acceso son el mismo flujo: el primer inicio de sesión aprovisiona la cuenta.

### 2.3 Flujo

```
/login ─POST /login/external(provider)─▶ Challenge(esquema) ─▶ proveedor
  ─▶ /signin-{scheme} ─▶ ExternalLoginService.ResolveAsync(provider, key, email, emailVerified, displayName)
  ─▶ SignInAsync(cookie ludeka.session) ─▶ RedirectUri      (GET /logout = SignOutAsync + redirect)
```

El endpoint `POST /login/external` valida antiforgery antes de leer el formulario (sin token → 400). `/healthz` y `/ready` conservan `.AllowAnonymous()`.

---

## 3. Identidad Externa y Vinculación

- Entidad `ExternalLogin`: `Id`, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail?`, `LinkedAt`; índice único `(Provider, ProviderKey)` y FK `Cascade` a `AppUsers`. Tabla `ExternalLogins` (migración `AddExternalLogins` en PostgreSQL; reconciliador `SqliteSchemaMigrator` en SQLite).
- **Cascada de vinculación** en `ExternalLoginService.ResolveAsync`, en este orden:
  1. Por `(Provider, ProviderKey)` → misma cuenta, sin filas ni usuarios nuevos.
  2. Por correo **verificado** del proveedor contra `AppUser.Email` → se crea la fila `ExternalLogin` y la cuenta conserva sus roles y permisos.
  3. Alta de un `AppUser` nuevo con `UserRole.CommunityUser` y `ModeratorPermission.None`.
- **Nunca** se auto-concede `FoundingTeam` ni se fusionan cuentas por correo no verificado: un segundo proveedor con correo no coincidente crea una cuenta separada. Un correo sin verificar no se persiste en `AppUser.Email`; queda solo en `ExternalLogin.ProviderEmail` (un correo sintético no enrutable `<clave>@<proveedor>.ludeka.invalid` cubre las cuentas sin correo).
- `AdminUserSeeder` conserva la fila del fundador, pero **sin conceder identidad ni sesión implícita**: el visitante anónimo no hereda nada.
- Decisión de producto (INC-49, fuera de este incremento): la pantalla de vinculación manual de proveedores y la política de cuentas sin correo verificado se trasladan a un incremento aparte.

---

## 4. Modelo de Autorización

### 4.1 Las 12 banderas de permiso (`ModeratorPermission`)

`All` es la conjunción (OR) de las 12 banderas declaradas y vale **4095**. `CanUploadImages` no tiene política de página propia: se exige en `GameEditorService.UpdateGameAsync` al cambiar carátula o imágenes.

| Bandera | Valor | Capacidad |
|---|---|---|
| `None` | 0 | Sin permisos |
| `CanEditGames` | 1 << 0 · 1 | Editar fichas técnicas y metadatos |
| `CanUploadImages` | 1 << 1 · 2 | Cargar/actualizar carátulas e imágenes (sin política de página) |
| `CanManagePublishers` | 1 << 2 · 4 | Editoriales y sus redes |
| `CanManageCreators` | 1 << 3 · 8 | Creadores, autores e ilustradores |
| `CanApproveMedia` | 1 << 4 · 16 | Aprobar/descartar multimedia y bandejas de ingesta |
| `CanResolveReports` | 1 << 5 · 32 | Resolver reportes comunitarios |
| `CanManageStoreLinks` | 1 << 6 · 64 | Tiendas y enlaces de compra afiliados |
| `CanPublishInstagram` | 1 << 7 · 128 | Publicar en la cuenta oficial de Instagram |
| `CanManageUsers` | 1 << 8 · 256 | Gestionar cuentas, roles y máscaras de permisos |
| `CanViewAuditLog` | 1 << 9 · 512 | Consultar la bitácora de auditoría |
| `CanManageEvents` | 1 << 10 · 1024 | Grandes eventos lúdicos y sus carteles |
| `CanManageNotifications` | 1 << 11 · 2048 | Canales, webhooks y disparadores de notificaciones |
| `All` | 4095 | Todas las banderas declaradas |

### 4.2 Las 11 políticas de permiso

`AuthorizationPolicies.PermissionPolicies` fija el mapa política → bandera. La política `RolModerador` (`FoundingTeam` o `Moderator`) se conserva registrada por compatibilidad, pero **ninguna página la declara** ya.

| Política | Bandera | Página(s) protegida(s) |
|---|---|---|
| `PermisoGestionarUsuarios` | `CanManageUsers` | `/admin/usuarios` |
| `PermisoVerAuditoria` | `CanViewAuditLog` | `/admin/auditoria` |
| `PermisoEditarFichas` | `CanEditGames` | `/admin/cola-catalogacion` |
| `PermisoAprobarMedios` | `CanApproveMedia` | `/admin/ingesta-social`, `/admin/canales-monitorizados`, `/moderacion-media` + alias (`/moderacion/multimedia`, `/admin/moderacion-medios`, `/admin/multimedia`) |
| `PermisoResolverReportes` | `CanResolveReports` | `/moderacion/reportes` + alias `/admin/reportes` |
| `PermisoPublicarInstagram` | `CanPublishInstagram` | `/admin/instagram` |
| `PermisoGestionarEventos` | `CanManageEvents` | `/admin/eventos` |
| `PermisoGestionarNotificaciones` | `CanManageNotifications` | `/admin/notificaciones` |
| `PermisoGestionarEditores` | `CanManagePublishers` | (sin página propia; servicios de editoriales) |
| `PermisoGestionarCreadores` | `CanManageCreators` | (sin página propia; servicios de creadores) |
| `PermisoGestionarTiendas` | `CanManageStoreLinks` | (sin página propia; servicios de tiendas) |

**Protección de rutas:** **10 páginas** (14 rutas con alias) declaran `[Authorize(Policy = ...)]`. `Routes.razor` usa `AuthorizeRouteView` con `NotAuthorized` y `RedirectToLogin`; el pipeline ejecuta `UseAuthentication` → `UseAuthorization` **antes** de `UseAntiforgery`. Las comprobaciones de marcado quedan solo como ocultación de acciones, nunca como control de acceso.

**Evaluación:** `PermissionAuthorizationHandler` es Singleton con `IServiceScopeFactory`; resuelve `IUserRepository` en un scope, relee el `AppUser` sin rastreo (`AsNoTracking`) y aplica `HasPermission`, que **deniega a `Suspended` aunque conserve permisos**. Anónimo, `CommunityUser`, moderador sin la bandera exacta y cuenta suspendida quedan denegados.

---

## 5. Revalidación en Escritura y Política de Anonimia

- `SessionIdentity.Require(...)` es el punto único de la frontera de anonimia: sin sesión lanza `UnauthorizedAccessException` controlada (nunca una excepción de constructor por `UserId` vacío).
- `ISessionPermissionGuard` / `SessionPermissionGuard` (Scoped) exige sesión y relee el `AppUser` **sin rastreo** antes de cada escritura administrativa: una cuenta suspendida o con permisos revocados queda bloqueada en la siguiente operación aunque su cookie siga vigente. Los **15 servicios** administrativos de escritura declaran la guarda (contrato por reflexión `AdministrativeWriteGuardContractTests`):

| Permiso exigido | Servicios |
|---|---|
| `CanManageEvents` | `BoardGameEventService` |
| `CanManageNotifications` | `CommunityNotificationService` |
| `CanApproveMedia` | `SocialIngestionService`, `MonitoredAccountService`, `SocialCollectorService`, `YouTubeSearchService`, `WeeklyReleaseService`, `GiveawayService`, `MediaService.CheckBrokenLinksAsync` |
| `CanEditGames` | `BggCatalogQueueService`, `BggDiscoveryService`, `BggMassIngestionService`, `NightlyCatalogingService`, `GeminiGameSummaryService` |
| `CanPublishInstagram` | `InstagramPublisherService` |

- **Rutas de sistema:** los ciclos programados no tienen sesión; cada servicio compartido expone una ruta de sistema que los `HostedService` usan (`IngestFromCollectorAsync`, `RunScheduledCollectionAsync`, `RunExpiringGiveawaysScanAsync`, `RunFridayReleasesBulletinAsync`, `RunBggTrendsDiscoveryAsync`, `RunScheduledDrainCycleAsync`, `RunScheduledCatalogingAsync`), con prueba propia de funcionamiento sin sesión.
- Las escrituras administrativas disparables desde **páginas públicas** (`/novedades`, `/radar`, `/eventos` con modal exprés y la «Cola comunitaria» de `/mi-ludoteca`) deniegan sin la bandera granular y traducen la denegación en redirección a `/login` (`SessionDenialUiContractTests`).
- **Navegación pública (13 rutas) sin sesión:** sin 500 y sin escrituras con identidad. Colección, préstamos, partidas, reseñas, preguntas, reportes y preferencias exigen sesión y redirigen al acceso.
- **Invariante de identidad no vacía:** `GamePlayLog`, `UserCollectionItem`, `UserGameReview`, `GameLoan`, `RuleQuestion`, `RuleAnswer`, `RuleVote`, `AuditLogEntry` y `UserPreference` rechazan `UserId` vacío o nulo; no existe usuario centinela y `MyLibrary.razor` no construye enlaces `/u/` con identidad vacía. La auditoría solo registra identidades reales de sesión.

---

## 6. Invalidación de Circuito y Revocación en Caliente

| Pieza | Rol |
|---|---|
| `AuthenticatedCurrentUserService` | `ICurrentUserService` `Scoped`: resuelve identidad desde el snapshot del circuito y proyecta la cookie en SSR; sin sesión, `UserId = ""` y todo `false` |
| `UserCircuitHandler` | Al abrir el circuito relee el `AppUser` **sin rastreo** y puebla `UserIdentitySnapshot`; un fallo degrada a sesión anónima, nunca inventa identidad |
| `IUserSessionInvalidator` / `InMemoryUserSessionInvalidator` | Singleton con versión monótona por usuario; `UserManagementService` lo invoca al cambiar `Status` o permisos |
| `SessionGuard.razor` | Montado en `MainLayout`; ante una invalidación nueva fuerza recarga completa (`forceLoad: true`), de modo que el circuito se reabre con la identidad releída |

En un despliegue multirréplica el aviso en memoria no cruza instancias; la autorización sigue siendo correcta porque el handler de permisos y la guarda releen el `AppUser` en cada comprobación.

---

## 7. Estado de Verificación y Pendientes

- **Verificación independiente (INC-46):** `pass_with_warnings`; 17/17 requisitos y 42/42 escenarios conformes; **0 hallazgos CRITICAL**. El hallazgo W1 (revalidación ausente en servicios administrativos) se remedió después de la instantánea: 15 servicios con guarda, 1.345/1.345 pruebas en verde (base del incremento: 1.005; +340).
- **Pendientes menores documentados** (sugerencias del informe de verificación, no bloqueantes): homogeneizar el marcado de `EventsManagement`/`AdminNotifications` (S1); endurecer la proyección SSR de la cookie para no mostrar controles privilegiados de una cuenta suspendida durante la ventana de revocación (S2); ampliar cobertura de logout extremo a extremo, cascada y panel multimedia (S3); fijar por prueba la ausencia de identidad centinela (S4); y reejecutar el arranque con PostgreSQL real (S5; el humo se hizo con SQLite porque el entorno no dispone de servidor).
- **Traslados de producto:** la pantalla de vinculación manual de proveedores y la política de cuentas sin correo verificado quedan en el INC-49.
