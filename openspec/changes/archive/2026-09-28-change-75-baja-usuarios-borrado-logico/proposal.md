# Propuesta SDD: INC-75 — Baja de Usuarios, Anonimización y Borrado Lógico (RGPD / Derecho al Olvido)

## 1. Motivación y Diagnóstico Técnico

En el marco de la gestión de usuarios, auditoría y vinculación de identidades externas (INC-20, INC-46, INC-49 e INC-63):
1. **Ausencia de mecanismo de baja:** El sistema únicamente contempla la suspensión temporal de cuentas (`UserStatus.Suspended`), la cual retiene en texto claro toda la información personal identificable (PII): correo electrónico, nombre de usuario y enlaces a redes sociales.
2. **Imperativo legal (RGPD art. 17):** El usuario tiene derecho a la supresión de sus datos personales ("derecho al olvido"). Ni el área de cuenta (`/cuenta/privacidad`) ni el panel de administración (`/admin/usuarios`) ofrecen una vía de supresión.
3. **Inviabilidad del borrado físico (*Hard Delete*):** La entidad `AppUser` está vinculada por clave foránea directa o lógica con elementos nucleares del dominio comunitario: colecciones (`UserCollectionItem`), préstamos (`GameLoan`), reseñas (`UserGameReview`), votos de escalabilidad, hitos (`UserMilestone`) y registros de auditoría (`AuditLog`). Un borrado físico destruiría métricas consolidadas del catálogo o quebraría restricciones referenciales en bases de datos relacionales (SQLite / PostgreSQL).
4. **Bloqueo de proveedores OAuth en cuentas residuales:** Si un usuario vinculó accidentalmente su cuenta con Discord, Google o Facebook y desea liberar su proveedor para enlazarlo con su cuenta principal, actualmente no existe forma de desvincular o eliminar el acceso único sin intervención manual en base de datos.

## 2. Propuesta de Solución

Se propone un mecanismo de **borrado lógico con anonimización irreversible** estructurado en cuatro capas:

### 2.1. Dominio y Entidad de Usuario
- Añadir el estado `UserStatus.Deleted` a `Ludeka.Core.Enums.UserStatus`.
- Implementar el método de dominio `AppUser.AnonymizeAndClose(string? reason = null)`:
  - Asigna `Status = UserStatus.Deleted`.
  - Reemplaza la PII de forma irreversible:
    - `Email = $"deleted-{Guid.NewGuid():N}@deleted.ludeka.es"` (garantizando el cumplimiento del índice único en BD sin colisiones).
    - `UserName = "Usuario eliminado"`.
    - `Country = null`.
  - Revoca roles y permisos: `Role = UserRole.CommunityUser`, `Permissions = ModeratorPermission.None`.
  - Actualiza `UpdatedAt = DateTimeOffset.UtcNow`.

### 2.2. Capa de Aplicación y Desacople de Credenciales
- Extender `IUserManagementService` con `AnonymizeUserAsync(string userId, string reason, CancellationToken ct)` para tramitaciones administrativas por la Mesa Fundadora con registro en `IAuditService`.
- Extender `IAccountConnectionsService` (o nuevo `IUserAccountService`) con `CloseOwnAccountAsync(CancellationToken ct)` para autoservicio del usuario autenticado en sesión (`ICurrentUserService.UserId`).
- **Liberación de identidades externas:** Purgar todos los registros vinculados en `ExternalLogins` del usuario (`_externalLoginRepository.DeleteByUserIdAsync`), liberando de inmediato los proveedores externos para su posterior reutilización.
- **Invalidación inmediata de sesión:** Invocar `IUserSessionInvalidator.Invalidate(userId)` para expulsar de inmediato circuitos Blazor interactivos y forzar la reevaluación del estado de autenticación.
- **Limpieza de tokens pendientes:** Purgar tokens huérfanos en `MagicLinkTokens` asociados al usuario o a su correo previo.

### 2.3. Preservación de la Integridad Comunitaria
- Las reseñas (`UserGameReview`), histórico de partidas (`GamePlayLog`) y registros de préstamos permanecen intactos en la base de datos para no desvirtuar las estadísticas agregadas del catálogo, presentándose visualmente asociados al autor anónimo *«Usuario eliminado»*.

### 2.4. Interfaz de Usuario y Flujos de Acción
- **Autoservicio en `/cuenta/privacidad`:**
  - Sección diferenciada «Zona de peligro / Dar de baja mi cuenta».
  - Diálogo modal con advertencia explícita sobre la irreversibilidad de la acción y confirmación obligatoria (escribir "DAR DE BAJA" o confirmación asertiva con doble paso).
  - Cierre y limpieza de la cookie de autenticación (`ludeka.session`) con redirección a la portada `/` y mensaje informativo de confirmación.
- **Gestión Administrativa en `/admin/usuarios`:**
  - Botón *«Dar de baja»* con confirmación modal para administradores con permisos `CanManageUsers`, inhabilitado para cuentas de la Mesa Fundadora.
  - Trazabilidad y auditoría de la baja en `AuditLog`.

## 3. Criterios de Aceptación
1. **Anonimización irreversible:** La PII (Email, UserName, Country) del usuario es sustituida por valores pseudo-aleatorios y anónimos; el estado pasa a `UserStatus.Deleted`.
2. **Liberación de proveedores OAuth:** Las credenciales en `ExternalLogins` se eliminan atómicamente, permitiendo asociar de nuevo el proveedor a otra cuenta.
3. **Expulsión en tiempo real:** Los circuitos y sesiones activas del usuario se invalidan de inmediato mediante `IUserSessionInvalidator`.
4. **Cero rotura referencial:** Ningún registro de partidas, reseñas ni colecciones genera errores de clave foránea ni regresiones en la base de datos.
5. **Autoservicio verificado:** El usuario puede ejecutar su baja desde `/cuenta/privacidad` con confirmación explícita y es redirigido a portada como visitante.
6. **Administración verificada:** Miembros de la Mesa Fundadora pueden anonimizar cuentas desde `/admin/usuarios` con registro de auditoría.
7. **Línea base:** Se mantiene el 100% de la suite de pruebas unitarias y de integración en verde (1.972 unitarias + 10 de integración existentes más las nuevas pruebas).
