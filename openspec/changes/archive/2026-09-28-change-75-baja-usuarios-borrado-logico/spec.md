# Especificación Técnica: INC-75 — Baja de Usuarios, Anonimización y Borrado Lógico (RGPD / Derecho al Olvido)

## 1. Resumen Ejecutivo

Este incremento formaliza y ejecuta el derecho de supresión de datos personales («derecho al olvido», RGPD art. 17) para los usuarios de Ludeka. La solución implementa un **borrado lógico irreversible** que anonimiza la información personal identificable (PII) en la entidad `AppUser`, purga sus credenciales en `ExternalLogins` para liberar proveedores OAuth (Google, Discord, Facebook), invalida de inmediato sus sesiones activas mediante `IUserSessionInvalidator` y preserva la integridad referencial de los registros comunitarios (reseñas, partidas, préstamos y colecciones).

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Estado y Operación de Dominio en `AppUser`
- **Nuevo Estado `UserStatus.Deleted`:** Se añade el valor `Deleted` a `Ludeka.Core.Enums.UserStatus`.
- **Método de Dominio `AnonymizeAndClose(string? reason = null)`:**
  - Asigna `Status = UserStatus.Deleted`.
  - Reemplaza la PII de forma irreversible:
    - `Email`: Se asigna un correo sintético único con formato `deleted-{Guid:N}@deleted.ludeka.es`. Esto cumple incondicionalmente el índice único de `AppUsers.Email` en la base de datos sin colisionar con cuentas reales ni con otros usuarios dados de baja.
    - `UserName`: Pasa al valor fijo `"Usuario eliminado"`.
    - `Country`: Se fija en `null`.
  - Despoja privilegios: `Role = UserRole.CommunityUser`, `Permissions = ModeratorPermission.None`.
  - Actualiza la marca temporal `UpdatedAt = DateTimeOffset.UtcNow`.
- **Protección de la Mesa Fundadora:** No se permite la baja de usuarios con rol `UserRole.FoundingTeam` (o del Administrador Fundador inicial si es el único guardián de la plataforma). Cualquier intento arrojará `InvalidOperationException`.

### REQ-2: Purgado Atómico de Credenciales en `ExternalLogins`
- Se extiende `IExternalLoginRepository` con `DeleteByUserIdAsync(string userId, CancellationToken ct)`.
- Al darse de baja la cuenta, se eliminan todas las vinculaciones externas del usuario en `ExternalLogins`.
- **Liberación de Proveedores:** Tras la eliminación del vínculo, el proveedor (Google, Discord, Facebook) queda inmediatamente disponible para que el usuario pueda crear una cuenta limpia o vincularlo a otra cuenta existente sin error de colisión.

### REQ-3: Invalidación Reactiva de Sesiones en Tiempo Real
- El servicio invoca `IUserSessionInvalidator.Invalidate(userId)` inmediatamente tras persistir la anonimización.
- Todos los circuitos interactivos Blazor asociados a dicho `userId` detectan la invalidación en el siguiente ciclo y revocan el acceso del usuario.

### REQ-4: Purgado de Tokens Temporales
- Se cancelan o eliminan los registros pendientes en `MagicLinkTokens` asociados al `userId` o al correo previo del usuario para evitar accesos residuales mediante enlaces mágicos enviados antes de la baja.

### REQ-5: Preservación de la Integridad Relacional y Comunitaria
- Los registros vinculados por clave foránea directa o por identificador lógico (`UserId`):
  - `UserGameReview` (reseñas y notas a juegos): Permanecen inalterados para preservar la puntuación media comunitaria del juego. En la visualización de la reseña, el autor se muestra como *«Usuario eliminado»*.
  - `GameLoan` (préstamos históricos): Se conservan en el historial de los juegos.
  - `GamePlayLog` (partidas): Se conservan para las estadísticas globales, excluyéndose de clasificaciones públicas personales.
  - `UserCollectionItem`: Permanecen archivados o vinculados al identificador anónimo sin lanzar excepciones de integridad en BD.
- Queda prohibido ejecutar `DELETE FROM "AppUsers"` (hard delete).

### REQ-6: Casos de Uso y Servicios de Aplicación
- **Autoservicio (`IUserAccountService.CloseOwnAccountAsync`):**
  - Permite al usuario con sesión activa solicitar la supresión de su propia cuenta.
  - Valida que `ICurrentUserService.UserId` sea válido y pertenezca a una cuenta activa.
  - Ejecuta la anonimización, purga de logins e invalidación de sesión.
- **Acción Administrativa (`IUserManagementService.AnonymizeUserAsync`):**
  - Permite a un miembro de la Mesa Fundadora con permiso `CanManageUsers` tramitar la baja de un usuario a petición legal o por resolución de incidencias.
  - Registra una entrada en `IAuditService` con `Action = AuditAction.Deleted`, `EntityType = AuditEntityType.User`, sin registrar la PII eliminada.

### REQ-7: Autoservicio en Interfaz de Usuario (`/cuenta/privacidad`)
- Se incorpora la sección editorial «Zona de peligro / Dar de baja mi cuenta» al pie de `AccountPrivacy.razor`.
- Se requiere confirmación modal asertiva (introducir el texto exacto `"DAR DE BAJA"` o confirmación en dos pasos) para evitar pulsaciones accidentales.
- Al confirmar la baja:
  - Se ejecuta la baja del usuario.
  - Se cierra la cookie de sesión (`SignOutAsync(SessionCookieScheme)`).
  - Se redirige al usuario a la portada `/` mostrando un aviso informativo accesible de cuenta suprimida.

### REQ-8: Gestión en Interfaz Administrativa (`/admin/usuarios`)
- En el listado de usuarios, para usuarios con estado activo o suspendido que no sean `FoundingTeam`, se añade el botón de acción *«Dar de baja»*.
- Si un usuario ya tiene estado `UserStatus.Deleted`, se muestra la insignia *«Eliminado»* y se inhabilitan las acciones de modificación de rol, suspensión y permisos.
- Modal de confirmación administrativa que solicita el motivo/justificación de la baja para la bitácora de auditoría.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Anonimización de entidad AppUser en dominio
  Dado un usuario activo con id "user-123", nombre "JugadorPro", correo "jugador@test.com" y país "ES"
  Cuando se ejecuta el método user.AnonymizeAndClose("Solicitud de baja voluntaria")
  Entonces su estado pasa a UserStatus.Deleted
  Y su correo coincide con el patrón "deleted-*@deleted.ludeka.es"
  Y su nombre de usuario es "Usuario eliminado"
  Y su país es nulo
  Y su rol es UserRole.CommunityUser
  Y sus permisos son ModeratorPermission.None

Escenario: Protección de miembros de la Mesa Fundadora
  Dado un usuario con rol UserRole.FoundingTeam
  Cuando se intenta ejecutar user.AnonymizeAndClose()
  Entonces se lanza una excepción InvalidOperationException impidiendo la baja

Escenario: Autoservicio de baja con liberación de OAuth
  Dado un usuario autenticado con sesión activa y vínculo con Discord en ExternalLogins
  Cuando el usuario confirma la baja de su cuenta en /cuenta/privacidad
  Entonces su AppUser queda anonimizado en estado Deleted
  Y el registro de Discord en ExternalLogins es eliminado
  Y la sesión del usuario queda invalidada
  Y el proveedor Discord puede volver a vincularse en un nuevo registro

Escenario: Preservación de reseñas comunitarias tras la baja
  Dado un usuario que ha valorado un juego con nota 9 y reseña "Excelente eurogame"
  Cuando el usuario se da de baja
  Entonces la reseña permanece en la base de datos asociada al id del usuario
  Y el juego conserva la reseña para el cálculo de nota media
  Y en la interfaz la autoría se presenta como "Usuario eliminado"

Escenario: Baja administrativa por la Mesa Fundadora con auditoría
  Dado un administrador autenticado con permiso CanManageUsers
  Cuando tramita la baja del usuario "user-999" desde /admin/usuarios con el motivo "Petición RGPD por soporte"
  Entonces el usuario "user-999" queda anonimizado
  Y se genera una entrada en AuditLog con acción Deleted y tipo User
  Y la sesión activa de "user-999" es expulsada inmediatamente
```

---

## 4. Requerimientos No Funcionales

1. **Rendimiento:** La operación de baja y purgado de credenciales debe completarse en menos de 200 ms bajo bases de datos SQLite y PostgreSQL.
2. **Seguridad y Autorización:**
   - La baja voluntaria solo puede operar sobre el `UserId` inyectado por el contexto autenticado del servidor.
   - La baja administrativa exige la política `RequirePermission(ModeratorPermission.CanManageUsers)` o `FoundingTeam`.
3. **Determinismo y Compatibilidad:** Cero migraciones destructivas; total compatibilidad con índices existentes en SQLite y PostgreSQL.
4. **Cobertura de Pruebas:** Preservación del 100% de la suite de pruebas previa (1.972 unitarias + 10 de integración) y cobertura unitaria estricta de todos los nuevos casos de uso y componentes.
