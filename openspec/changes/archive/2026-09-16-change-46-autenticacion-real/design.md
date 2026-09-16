# Diseño Técnico — INC-46: Autenticación Real, Autorización por Permisos y Retirada de la Identidad Simulada

## 1. Enfoque Técnico

Cookie propia de ASP.NET Core (sin Identity) con esquemas externos habilitados por configuración. La cookie transporta identidad —`NameIdentifier` = `AppUser.Id`, rol y máscara de permisos como *snapshot* de UI—, pero **ninguna decisión de autorización se apoya en claims**: las 9 políticas y cada servicio de escritura releen el `AppUser` de la base de datos y aplican `HasPermission`, que ya deniega a `Suspended`. `ICurrentUserService` pasa a `Scoped` y pierde los conmutadores.

## 2. Decisiones de Arquitectura

| Decisión | Elección y razón |
|---|---|
| Autorización | `PermissionAuthorizationHandler` (Singleton con `IServiceScopeFactory`) resuelve `IUserRepository` en un scope y aplica `HasPermission`; los claims nunca deciden porque caducan tarde |
| Identidad de UI | `AuthenticatedCurrentUserService` (`Scoped`, `Ludeka.Web`) lee un snapshot que puebla `UserCircuitHandler`, con `IHttpContextAccessor` en SSR; sin sesión, `UserId = ""` y todo `false` |
| Vinculación | `ExternalLoginService` (`Application/Features/Identity`): `(Provider, ProviderKey)` → correo verificado → alta `CommunityUser`/`None`; los eventos de proveedor solo extraen claims y firman la cookie |
| Vinculación manual | Aplazada; un segundo proveedor con correo no coincidente crea cuenta separada y la pantalla futura es UI más `LinkAsync(...)`, sin migración destructiva (§9.1) |
| `AdminUserSeeder` | Conserva la fila `FoundingTeam` sin conceder identidad; la vinculación es explícita y auditada en `/admin/vinculacion-fundador` (§9.2) |
| Nueve políticas | Se añaden `CanManageUsers` (1<<8) y `CanViewAuditLog` (1<<9), `All` se recalcula, y cada política mapea un flag: `PermisoEditarFichas`→`CanEditGames`, `PermisoAprobarMedios`→`CanApproveMedia`, `PermisoResolverReportes`→`CanResolveReports`, `PermisoGestionarEditores`→`CanManagePublishers`, `PermisoGestionarCreadores`→`CanManageCreators`, `PermisoGestionarTiendas`→`CanManageStoreLinks`, `PermisoPublicarInstagram`→`CanPublishInstagram`, `PermisoVerAuditoria`→`CanViewAuditLog`, `PermisoGestionarUsuarios`→`CanManageUsers` (§9.3) |
| Revocación | `AsNoTracking()` en las lecturas de identidad, más `IUserSessionInvalidator` y `SessionGuard` con `forceLoad: true`; sin él, el `DbContext` del circuito devolvería la entidad rastreada |
| Rutas | `@attribute [Authorize(Policy = "…")]` en las 11 páginas (en SSR responde el middleware) y `AuthorizeRouteView` con `RedirectToLogin`, guardado por `RendererInfo.IsInteractive` |

## 3. Flujo de Datos

```
/login ─POST /login/external(provider)─▶ Results.Challenge(esquema) ─▶ proveedor
  ─▶ /signin-{scheme} ─▶ ResolveAsync(provider,key,email,emailVerified)
        ├─ existe el par ─▶ AppUser existente
        ├─ correo verificado == AppUser.Email ─▶ fila nueva + AppUser existente
        └─ resto ─▶ alta CommunityUser / None
  ─▶ SignInAsync(cookie) ─▶ RedirectUri
Circuito: [Authorize] ─▶ PermissionAuthorizationHandler ─▶ IUserRepository ─▶ HasPermission
Escritura: servicio ─▶ relee AppUser ─▶ permiso o UnauthorizedAccessException, sin persistir ni auditar
```

## 4. Interfaces y Contratos

- `ICurrentUserService`: sin `SwitchRole`, sin `SwitchUser` y sin cuerpo por defecto en `HasPermission` (el actual concede todo a cualquier `Moderator`).
- `ExternalLogin`: `Guid Id`, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail?`, `LinkedAt`; constructor validante, sin columnas extra.
- `IExternalLoginRepository` e `IExternalLoginService.ResolveAsync(provider, providerKey, email?, emailVerified, displayName)`.
- `AuthenticationOptions`: `Authentication:Providers:{Google|Discord|Facebook}` (`Enabled`, `ClientId`/`ClientSecret`, `AppId`/`AppSecret`) y `Authentication:Cookie:ExpireMinutes`.
- Esquemas `GoogleDefaults`, `DiscordAuthenticationDefaults` y `FacebookDefaults`, con `SignInScheme` de cookie y callbacks `/signin-{google|discord|facebook}`.

## 5. Cambios de Archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `Core/Enums/ModeratorPermission.cs`, `AuditAction.cs`, `Entities/ExternalLogin.cs` | Modificar / Crear | Flags, `AuditAction.LinkedFounderIdentity`, entidad puente |
| `Application/Contracts/ICurrentUserService.cs`, `IExternalLoginRepository.cs` | Modificar / Crear | Contrato sin conmutadores; repositorio de vínculos |
| `Application/Features/Identity/*`, `Features/Admin/UserManagementService.cs` | Crear / Modificar | Vinculación, opciones, invalidación, `CanManageUsers` |
| `Infrastructure/Services/DefaultCurrentUserService.cs` | Eliminar | Identidad simulada |
| `Infrastructure/Data/*` (`LudekaDbContext`, `SqliteUserRepository`, `SqliteSchemaMigrator`) | Modificar | `DbSet`, índice único `(Provider, ProviderKey)`, FK `Cascade`, `AsNoTracking`, tabla SQLite |
| `Infrastructure/Migrations/*_AddExternalLogins.cs` (+ snapshot) | Crear | Migración PostgreSQL |
| `Web/Authentication/*`, `Web/Services/*` | Crear | Handler de permiso, 9 políticas, cookie, esquemas externos, snapshot e invalidación |
| `Web/Components/Pages/Login.razor`, `Shared/RedirectToLogin.razor`, `SessionGuard.razor` | Crear | Acceso, redirección y guardián |
| `Web/Program.cs` | Modificar | `AddScoped`, `UseAuthentication`/`UseAuthorization` antes de `UseAntiforgery`, `/login/external`, `/logout`, health anónimo |
| `Web/appsettings*.json`, `Ludeka.Web.csproj` | Modificar | Sección `Authentication`; paquetes `Microsoft.AspNetCore.Authentication.Google`, `Microsoft.AspNetCore.Authentication.Facebook`, `AspNet.Security.OAuth.Discord` (ninguno en el shared framework) |
| 11 páginas y 5 puntos de UI + `MyLibrary`, `Routes.razor`, `_Imports.razor` | Modificar | `[Authorize]`, sin simulación ni `/u/` vacío |
| `openspec/specs/{editorial-role-management,media-moderation-panel}/spec.md`, `tests/Ludeka.UnitTests` | Modificar / Crear | Encabezados (§8); 13 dobles y tests nuevos |

## 6. Estrategia de Pruebas

| Capa | Qué se prueba | Enfoque |
|---|---|---|
| Unit · políticas | Anónimo, `CommunityUser`, `Moderator` con y sin el flag, `FoundingTeam`, `Suspended` | `AuthorizationHandlerContext` sobre SQLite `:memory:` |
| Unit · vinculación | Par reincidente, correo verificado, alta `None`, correo sin verificar, nunca `FoundingTeam` | `ExternalLoginService` sobre SQLite |
| Unit · anonimia y escritura | Los servicios lanzan `UnauthorizedAccessException` sin sesión, sin persistir ni auditar; `AsNoTracking` ve cambios fuera de banda | xUnit con dobles anónimos |
| Web / DI | `Scoped`, 9 políticas, proveedor `Enabled=false` sin esquema, `/healthz` y `/ready` anónimos | `ServiceCollection` como en `tests/.../Web`; smoke manual del criterio 11 |

## 7. Matriz de Amenazas

N/A — el cambio no toca routing de agente, shell, subprocesos, VCS/PR ni clasificación de ejecutables; es autenticación de aplicación web.

## 8. Migración y Operación

- **Paso 1 (autorización)**: pipeline, políticas, `[Authorize]`, `/login` y `ExternalLogin`, con la simulación aún viva y la suite verde.
- **Paso 2 (retirada)**: `Scoped`, borrado de `DefaultCurrentUserService`, conmutadores, 5 puntos de UI y los 13 dobles migrados.
- **Migración**: `dotnet ef migrations add AddExternalLogins -p src/Ludeka.Infrastructure -s src/Ludeka.Web` con `Database__Provider=PostgreSql` (la factoría de diseño ya usa Npgsql) y arranque verificado con PostgreSQL y con SQLite (`EnsureCreatedAsync` + `SqliteSchemaMigrator`).
- **Salud**: sin política de *fallback*; `/healthz` y `/ready` con `.AllowAnonymous()` explícito para no romper los probes de Cloud Run.
- **Specs canónicas**: `### Requerimiento:`→`### Requirement:` y `#### Escenario:`→`#### Scenario:` (3+5 en `editorial-role-management`, 4+6 en `media-moderation-panel`), solo encabezados y preservando los CRLF actuales.
- **Reversión**: revertir el PR; `ExternalLogins` es tabla nueva y no altera datos existentes.

## 9. Preguntas Abiertas (confirmación del maintainer)

1. **Aplazar la pantalla de vinculación manual**, documentando que un segundo proveedor con correo no coincidente crea una cuenta separada.
2. **Procedimiento de vinculación del fundador**: página protegida y auditada, no la vinculación automática de `AdminUser__Email` al primer login. *(Resolución de archivo (2026-09-16, W3): no se implementa en este cambio —`/admin/vinculacion-fundador` no existe—; el maintainer traslada la pantalla de vinculación manual de proveedores y la política de cuentas sin correo verificado al INC-49, fuera del alcance de INC-46.)*
3. **Dos flags nuevos**, `PermisoGestionarEditores`→`CanManagePublishers` y `/admin/eventos` y `/admin/notificaciones` bajo `PermisoGestionarEditores` (hoy los abre cualquier `Moderator`).
