# INC-75: Baja de Usuarios, Anonimización y Borrado Lógico (RGPD / Derecho al Olvido)

> **Estado:** ⏳ En progreso  
> **Fecha de Creación:** 2026-09-28  
> **Rama Prevista:** `inc/baja-usuarios-borrado-logico`  
> **Worktree Previsto:** `C:\repos\ludeka-wt\baja-usuarios-borrado-logico`  
> **Dependencias:** INC-20 (Gestión de usuarios y auditoría), INC-46 (Autenticación real e invalidación de sesiones), INC-49/63 (Conexiones OAuth y ExternalLogins), INC-62 (Área de cuenta y privacidad)  
> **Especificación Viva Prevista:** `docs/specs/sistema/45-baja-usuarios-y-derecho-al-olvido.md`

---

## 1. Cómo se descubrió

Tras la detección de colisiones de cuentas secundarias creadas accidentalmente durante la vinculación de proveedores OAuth (Discord / Facebook) y la posterior revisión de `/admin/usuarios` y `/cuenta/conexiones`:
- El mantenedor preguntó si era posible borrar cuentas duplicadas desde el panel de administración o si un usuario podía desvincular su método único.
- Se constató que `/admin/usuarios` únicamente dispone de acciones para cambiar rol, conceder permisos granulares y suspender (`UserStatus.Suspended`), careciendo de un flujo de baja o supresión de cuenta.
- El mantenedor confirmó la necesidad de contemplar la **baja de usuarios por imperativo legal (derecho de supresión / derecho al olvido, art. 17 RGPD)**, especificando explícitamente que debe realizarse mediante **borrado lógico**.

---

## 2. El agujero, verificado

1. **Ausencia de contrato de baja en capa de aplicación:** `IUserManagementService` solo expone `UpdateUserStatusAsync` (que conmuta entre `Active` y `Suspended`). No existe ningún método `DeleteUserAsync` ni `AnonymizeUserAsync`.
2. **Imposibilidad de hard delete sin rotura referencial:** `AppUser` está referenciado mediante claves foráneas en múltiples entidades del ecosistema de Ludeka:
   - `UserCollectionItem` (juegos en ludoteca personal).
   - `UserGameReview` (reseñas y notas a juegos).
   - `GameLoan` (préstamos activos o históricos).
   - `UserLike` (me gustas a editoriales, tiendas, creadores y vídeos).
   - `UserMilestone` (hitos de gamificación).
   - Votos de escalabilidad y huella en mesa (`UserPlayerCountVote`, `UserFamilyExperienceVote`).
   - `AuditLog` (registro de operaciones de moderación).  
   Un borrado físico (`DELETE FROM "AppUsers"`) eliminaría en cascada registros legítimos de la comunidad, alteraría los cálculos estadísticos globales de los juegos o fallaría por restricciones de integridad.
3. **Persistencia de PII en cuentas suspendidas:** La suspensión actual inhabilita el acceso pero mantiene en texto claro el correo (`Email`), nombre de usuario (`UserName`) y vínculos a redes (`ExternalLogins`), impidiendo que el usuario ejerza una supresión efectiva de sus datos personales.
4. **Colisión de proveedores OAuth liberados:** Si un usuario desea eliminar su cuenta secundaria o darse de baja para registrarse de nuevo, sus registros en `ExternalLogins` continúan asociados de forma permanente al `UserId` antiguo, bloqueando cualquier reutilización futura del proveedor.
5. **Falta de autoservicio para el usuario:** En el área de `/cuenta` (especialmente `/cuenta/privacidad`), el usuario no dispone de un botón ni flujo asistido para solicitar o ejecutar la baja de su propia cuenta.

---

## 3. Lo que pide el maintainer

1. Contemplar la baja de usuarios con pleno cumplimiento de derechos RGPD.
2. Aplicar un **borrado lógico con anonimización irreversible** de los datos personales para no destruir la integridad relacional de la base de datos ni los agregados comunitarios.
3. Permitir la baja tanto desde el **autoservicio del propio usuario** en su área personal como por **acción administrativa de la Mesa Fundadora** en el panel de gestión de usuarios.

---

## 4. Alcance y decisiones que hay que tomar

1. **Extensión del Dominio (`UserStatus` & `AppUser`):**
   - Incorporar `UserStatus.Deleted` (o `Anonymized`) en `src/Ludeka.Core/Enums/UserStatus.cs`.
   - Método de dominio `AppUser.AnonymizeAndClose(string auditReason)` que:
     - Asigne `Status = UserStatus.Deleted`.
     - Anonimice irreversiblemente la PII: `Email = $"deleted-{Guid.NewGuid():N}@deleted.ludeka.es"`, `UserName = "Usuario eliminado"`, `Country = null`.
     - Actualice `UpdatedAt = DateTimeOffset.UtcNow`.
2. **Purgado Atómico de Credenciales e Identificadores Externos:**
   - Eliminar todas las filas del usuario en `ExternalLogins` (`_externalLoginRepository.DeleteByUserIdAsync`), liberando de inmediato las cuentas sociales (Google, Discord, Facebook) para que puedan ser utilizadas o vinculadas en otra cuenta.
   - Purgar tokens pendientes de enlace mágico en `MagicLinkTokens` asociados al correo previo del usuario.
   - Invocar `IUserSessionInvalidator.Invalidate(userId)` para expulsar inmediatamente cualquier circuito Blazor activo o sesión abierta.
3. **Preservación y Desasociación de Contenido Comunitario:**
   - Las colecciones personales y préstamos históricos se marcan o archivan sin romper claves foráneas.
   - Las reseñas comunitarias (`UserGameReview`) permanecen para no desvirtuar la nota media del juego, pero su autor se presenta en la UI como *«Usuario eliminado»*.
   - Los votos de escalabilidad se preservan sin atribución personal.
4. **Flujo de Autoservicio (`/cuenta/privacidad` o `/cuenta/baja`):**
   - Diálogo modal con advertencia explícita y confirmación obligatoria (escribir "DAR DE BAJA" o confirmar con intención deliberada).
   - Al confirmar: ejecución de la baja, borrado de cookie de sesión (`ludeka.session`) y redirección a portada con aviso informativo.
5. **Flujo Administrativo (`/admin/usuarios`):**
   - Botón adicional *«Dar de baja»* con confirmación modal para miembros de la Mesa Fundadora con permiso `CanManageUsers`.
   - Registro en la bitácora de auditoría (`AuditLog`) documentando la acción administrativa sin registrar la PII eliminada.

---

## 5. Fuera de alcance

- Descarga / exportación masiva de datos en formato JSON/ZIP (Portabilidad RGPD art. 20, a evaluar en un incremento posterior de portabilidad).
- Periodo de gracia o "papelera de reciclaje" de 30 días para reactivación (la anonimización tras confirmación explícita es inmediata y definitiva).

---

## 6. Criterios de aceptación

1. **Anonimización irreversible:** Tras la baja, el registro en `AppUsers` no contiene el correo original ni el nombre del usuario; `Email` pasa a formato sintético pseudo-aleatorio único y `UserName` a "Usuario eliminado".
2. **Liberación de proveedores OAuth:** Todas las vinculaciones en `ExternalLogins` del usuario dado de baja se eliminan. El proveedor queda libre inmediatamente para vincularse a otra cuenta.
3. **Cierre inmediato de sesión:** La cookie de autenticación se invalida y los circuitos interactivos son abortados de inmediato mediante `IUserSessionInvalidator`.
4. **Integridad referencial intacta:** Ninguna clave foránea de reseñas, histórico de préstamos o partidas se rompe; `dotnet test` y las migraciones de base de datos se ejecutan sin errores tanto en SQLite como en PostgreSQL.
5. **Autoservicio verificado:** El usuario puede tramitar su baja desde `/cuenta/privacidad` tras confirmación modal y es redirigido a `/` como usuario anónimo.
6. **Administración verificada:** Un moderador con `CanManageUsers` puede ejecutar la baja desde `/admin/usuarios` con trazabilidad en auditoría.
7. **Línea base:** Suite completa de pruebas unitarias y de integración pasando al 100%.

---

## 7. Riesgos y mitigaciones

- **Riesgo:** Bajas accidentales por pulsaciones no deliberadas.  
  *Mitigación:* Modal con doble confirmación asertiva y requerimiento de confirmación textual antes de habilitar el botón final.
- **Riesgo:** Conflicto de unicidad en la columna `Email` si la base de datos tiene un índice único sobre correos.  
  *Mitigación:* Generar un correo sintético con UUID único (`deleted-{Guid:N}@deleted.ludeka.es`) para respetar el índice único sin colisionar con otros usuarios eliminados ni con correos reales.
- **Riesgo:** Ataque de suplantación para forzar la baja de un tercero.  
  *Mitigación:* Validar estrictamente en el endpoint/servicio que el `userId` provenga del `ICurrentUserService.UserId` autenticado en sesión, o que el usuario cuente con la política `PermisoGestionarUsuarios`.

---

## 8. Plan de Tareas ODD (Work Units)

- [ ] **Tarea 1 (Dominio y Estado de Usuario):**
  - Añadir `UserStatus.Deleted` a `src/Ludeka.Core/Enums/UserStatus.cs`.
  - Incorporar método de dominio `Anonymize()` en `AppUser.cs`.
  - Pruebas unitarias de dominio en `tests/Ludeka.UnitTests/Domain/AppUserTests.cs`.
- [ ] **Tarea 2 (Contratos y Casos de Uso de Aplicación):**
  - Extender `IUserManagementService` con `AnonymizeUserAsync(string userId, string reason, CancellationToken ct)`.
  - Crear `IUserAccountService` (o ampliar en `IAccountConnectionsService`) con `CloseOwnAccountAsync(CancellationToken ct)`.
  - Pruebas unitarias de aplicación con mocks de repositorios y validación de orquestación.
- [ ] **Tarea 3 (Implementación de Repositorios y Purgado de Credenciales):**
  - Implementar borrado por `UserId` en `IExternalLoginRepository` (`DeleteByUserIdAsync`).
  - Implementar lógica en `UserManagementService` para orquestar la anonimización, purga de logins y llamada a `IUserSessionInvalidator`.
  - Pruebas de integración/unitarias de repositorios en SQLite.
- [ ] **Tarea 4 (Migración de Base de Datos EF Core):**
  - Crear migración `AddDeletedUserStatus` (o sincronización de enum/constraints si aplica) para SQLite y PostgreSQL.
  - Verificar idempotencia y compatibilidad del índice único de correos.
- [ ] **Tarea 5 (UI Autoservicio en Área de Cuenta):**
  - Añadir sección «Zona de peligro / Dar de baja mi cuenta» en `AccountPrivacy.razor` (o subpestaña dedicada en `/cuenta`).
  - Modal interactivo de confirmación con teclado accesible y región aria.
  - Endpoint o acción de cierre de sesión interactivo con redirección limpia.
- [ ] **Tarea 6 (UI Administrativa en Gestión de Usuarios):**
  - Añadir botón de «Dar de baja» en `UserManagement.razor` para usuarios no fundadores.
  - Modal de confirmación administrativa e integración con registro de auditoría.
- [ ] **Tarea 7 (Cierre, Documentación Viva y Verificación):**
  - Ejecución de la suite completa de pruebas unitarias (`dotnet test`).
  - Redacción de la especificación viva `docs/specs/sistema/45-baja-usuarios-y-derecho-al-olvido.md`.
  - Actualización de `ROADMAP.md` y `docs/specs/sistema/README.md`.
