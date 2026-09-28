# 44. Gestión de Usuarios — Baja de Usuarios, Anonimización y Derecho al Olvido (RGPD)

> **Incremento de Origen:** INC-75  
> **Alcance:** Dominio (`Ludeka.Core`), Infraestructura y Repositorios (`Ludeka.Infrastructure`), Casos de Uso y Servicios (`Ludeka.Application`), Endpoints HTTP y Componentes Blazor (`Ludeka.Web`), Pruebas Automatizadas.  
> **Estado:** ✅ Archivado y Verificado (2.001 pruebas automáticas superadas al 100%: 1.991 unitarias + 10 de integración).

---

## 1. Visión y Propósito Funcional

Este módulo materializa el cumplimiento normativo estricto del derecho de supresión de datos personales («derecho al olvido», RGPD art. 17) para cualquier miembro de la comunidad de Ludeka, resolviendo de forma armónica la privacidad individual y la integridad histórica comunitaria:

1. **Borrado Lógico e Irreversible (Anti-Hard Delete):** Queda terminantemente prohibido ejecutar `DELETE FROM "AppUsers"`. La eliminación física rompería claves foráneas, corrompería promedios de valoración comunitaria de juegos de mesa y alteraría historiales colectivos. En su lugar, se anonimizan irreversiblemente todos los datos personales identificables (PII) de la entidad `AppUser`.
2. **Preservación Referencial Comunitaria:** Las reseñas y valoraciones numéricas a juegos de mesa (`UserGameReview`), el historial de préstamos (`GameLoan`) y los registros de partidas de mesa jugadas (`GamePlayLog`) se conservan atribuidos públicamente al autor sintético neutro *«Usuario eliminado»*.
3. **Liberación Instantánea de Proveedores OAuth:** Al tramitarse la baja, se purgan de forma atómica todas las vinculaciones externas del usuario en `ExternalLogins`. De este modo, los proveedores sociales (Google, Discord, Facebook) quedan libres de inmediato para que el usuario pueda volver a registrarse con una cuenta limpia o vincularlos a otra cuenta sin errores de colisión.
4. **Expulsión Reactiva de Circuitos y Sesiones:** La baja invoca de inmediato `IUserSessionInvalidator.Invalidate(userId)`, forzando la expulsión de circuitos interactivos Blazor activos mediante `SessionGuard.razor` y revocando la cookie de sesión del navegador.
5. **Autoservicio y Gestión Administrativa Auditada:** Proporciona un flujo de autoservicio protegido contra pulsaciones accidentales mediante confirmación asertiva (`"DAR DE BAJA"`), así como una vía administrativa para la Mesa Fundadora con justificación obligatoria en la bitácora de auditoría.
6. **Protección Blindada de la Mesa Fundadora:** Se prohíbe taxativamente la baja o anonimización de cualquier usuario perteneciente a la Mesa Fundadora (`UserRole.FoundingTeam`), impidiendo orfandades en la administración de la plataforma.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Estado de Usuario (`UserStatus`)
Ubicado en `src/Ludeka.Core/Enums/UserStatus.cs`:
```csharp
public enum UserStatus
{
    Active = 0,
    Suspended = 1,
    Deleted = 2
}
```

### 2.2. Reglas de Permisos Granulares (`ModeratorPermissionRules`)
Ubicado en `src/Ludeka.Core/Enums/ModeratorPermissionRules.cs`:
- Si `user.Status != UserStatus.Active`, todos los permisos de moderación quedan revocados incondicionalmente, garantizando que una cuenta en estado `Deleted` carezca de cualquier capacidad operativa residual.

### 2.3. Operación de Dominio `AppUser.AnonymizeAndClose`
Ubicada en `src/Ludeka.Core/Entities/AppUser.cs`:
```csharp
public void AnonymizeAndClose(string? reason = null)
{
    if (Role == UserRole.FoundingTeam)
        throw new InvalidOperationException("No está permitido dar de baja a un miembro de la Mesa Fundadora.");

    Status = UserStatus.Deleted;
    Role = UserRole.CommunityUser;
    Permissions = ModeratorPermission.None;
    UserName = "Usuario eliminado";
    Email = $"deleted-{Guid.NewGuid():N}@deleted.ludeka.es";
    Country = null;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```
- **Correo Sintético Único:** El formato `deleted-{Guid:N}@deleted.ludeka.es` satisface incondicionalmente el índice único sobre `AppUsers.Email` en base de datos sin colisionar con cuentas existentes ni con otras cuentas eliminadas.

---

## 3. Infraestructura y Persistencia (`Ludeka.Infrastructure`)

### 3.1. Purga de Vínculos OAuth (`IExternalLoginRepository`)
Contrato en `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs` e implementación en `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs`:
```csharp
public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(userId)) return;
    var normalizedUserId = userId.Trim().ToLowerInvariant();
    var logins = await _context.ExternalLogins
        .Where(l => l.UserId == normalizedUserId)
        .ToListAsync(cancellationToken);

    if (logins.Count > 0)
    {
        _context.ExternalLogins.RemoveRange(logins);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

### 3.2. Compatibilidad EF Core y Migraciones DDL
- Al almacenarse `UserStatus` como columna entera por defecto en EF Core, el nuevo valor ordinal `Deleted = 2` no requiere alteración estructural de tablas (DDL) en SQLite ni en PostgreSQL.

---

## 4. Casos de Uso y Capa de Aplicación (`Ludeka.Application`)

### 4.1. Autoservicio de Baja Voluntaria (`IUserAccountService`)
Contrato en `src/Ludeka.Application/Contracts/IUserAccountService.cs` e implementación en `src/Ludeka.Application/Features/Account/UserAccountService.cs`:
- Valida la sesión activa a través de `string.IsNullOrWhiteSpace(_currentUserService.UserId)`.
- Recupera el `AppUser` y comprueba que no sea `FoundingTeam`.
- Si el usuario ya está en `UserStatus.Deleted`, responde con éxito de forma idempotente.
- Ejecuta `user.AnonymizeAndClose()`, persiste la entidad, purga `ExternalLogins`, registra la auditoría con `AuditAction.Deleted` e invalida la sesión con `_sessionInvalidator?.Invalidate(user.Id)`.

### 4.2. Baja Administrativa (`IUserManagementService.AnonymizeUserAsync`)
Extensión en `src/Ludeka.Application/Contracts/IUserManagementService.cs` e implementación en `src/Ludeka.Application/Features/Admin/UserManagementService.cs`:
- Exige pertenencia a la Mesa Fundadora o el permiso granular `CanManageUsers`.
- Valida la existencia del usuario y la presencia obligatoria del motivo/justificación textual.
- Ejecuta la anonimización, purga de vinculaciones OAuth, bitácora de auditoría detallada e invalidación de sesión reactiva.

---

## 5. Endpoints HTTP y Capa Web (`Ludeka.Web`)

### 5.1. Endpoint `POST /cuenta/baja`
Ubicado en `src/Ludeka.Web/Program.cs`:
- Protegido por `.RequireAuthorization()`.
- Valida el token antiforgery (`antiforgery.ValidateRequestAsync`).
- Invoca `accountService.CloseOwnAccountAsync(httpContext.RequestAborted)`.
- Cierra la cookie de autenticación mediante `httpContext.SignOutAsync(ExternalAuthenticationSchemes.SessionCookieScheme)`.
- Emite una redirección HTTP 302 hacia `/?aviso=cuenta-eliminada`.

### 5.2. Autoservicio en Interfaz de Usuario (`AccountPrivacy.razor`)
- Incorpora la tarjeta editorial «Zona de peligro / Dar de baja mi cuenta» al pie de la página `/cuenta/privacidad`.
- Despliega un diálogo accesible `EditorialModal` requiriendo escribir explícitamente `"DAR DE BAJA"`.
- Emite un formulario clásico HTML `method="post"` con `action="/cuenta/baja"` y `data-enhance="false"`, asegurando una navegación limpia de nivel superior sin retención de circuito.

### 5.3. Gestión en Panel Administrativo (`UserManagement.razor`)
- Filtro por estado con opción adicional *«Eliminados»*.
- Insignia de estado neutra para cuentas en `UserStatus.Deleted`.
- Inhabilitación total de acciones operativas sobre usuarios dados de baja (*«Cuenta anonimizada»*).
- Botón de baja administrativa (`trash-2`) para usuarios no fundadores con modal editorial que exige justificación para auditoría.

---

## 6. Cobertura y Verificación Automatizada

El incremento cuenta con cobertura integral en la suite de pruebas automatizadas:

| Componente / Prueba | Archivo de Test | Tipo de Verificación |
|---|---|---|
| Dominio `AppUser.AnonymizeAndClose` | `tests/Ludeka.UnitTests/Domain/UserManagementDomainTests.cs` | Reglas de PII, correo sintético y blindaje de Mesa Fundadora |
| Infraestructura `ExternalLoginRepository.DeleteByUserIdAsync` | `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginPersistenceTests.cs` | Purga atómica de vinculaciones OAuth |
| Caso de Uso `UserAccountService.CloseOwnAccountAsync` | `tests/Ludeka.UnitTests/Application/UserAccountServiceTests.cs` | Flujo completo de baja voluntaria, purga y auditoría |
| Caso de Uso `UserManagementService.AnonymizeUserAsync` | `tests/Ludeka.UnitTests/Application/UserManagementAndAuditServiceTests.cs` | Baja administrativa con justificación obligatoria |
| Endpoint HTTP `POST /cuenta/baja` | `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | Contrato de tubería, autorización, antiforgery y redirección |
| Interfaz Blazor `AccountPrivacy` y `UserManagement` | `tests/Ludeka.UnitTests/Web/AccountClosureAndAnonymizationUiContractTests.cs` | Contrato de zona de peligro, modal asertivo y vistas de administración |

**Total de Pruebas Superadas:** 2.001 pruebas automáticas (1.991 unitarias + 10 de integración) al 100% en verde.
