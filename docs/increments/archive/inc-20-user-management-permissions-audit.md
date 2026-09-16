# Incremento 20: Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora

- **Identificador SDD:** `change-20-user-management-permissions-audit`
- **Estado:** ✅ **Completado y Archivado** (`openspec/changes/archive/2026-09-07-change-20-user-management-permissions-audit/`)
- **Puntos del MVP cubiertos:** Gobernanza del sistema, Seguridad y autorización granular (RBAC), Auditoría y trazabilidad editorial.
- **Pruebas Automatizadas:** 433/433 pasadas con éxito (100%).
- **Objetivo Principal:** Proporcionar a los miembros de la Mesa Fundadora un panel centralizado para gestionar usuarios y roles, asignando a los moderadores permisos granulares por cada tipo de edición posible (juegos, imágenes, editoriales, creadores, multimedia, reportes comunitarios y tiendas), complementado con una ventana de auditoría que registre de forma indeleble quién modificó qué dato, en qué fecha y qué valores cambiaron.

---

## 1. Alcance Funcional Propuesto

1. **Gestión de Usuarios y Asignación de Roles (`/admin/usuarios`):**
   - Vista exclusiva para usuarios con rol `FoundingTeam` (Mesa Fundadora).
   - Listado de usuarios registrados en el sistema con buscador reactivo por nombre o email, filtro por rol (`FoundingTeam`, `Moderator`, `CommunityUser`) y estado (`Active`, `Suspended`).
   - Capacidad para ascender a un usuario comunitario a `Moderator` o revocar sus privilegios.
2. **Sistema Granular de Permisos de Moderación:**
   - La Mesa Fundadora puede configurar de forma individual qué puede hacer cada moderador mediante un panel de conmutadores/checkboxes:
     - `CanEditGames`: Editar información general y técnica de las fichas de juegos (título, autores, comensales, duración, edad, etc.).
     - `CanUploadImages`: Cargar y reemplazar carátulas y fotos de juegos en el servidor.
     - `CanManagePublishers`: Dar de alta y editar fichas de editoriales y sus redes oficiales.
     - `CanManageCreators`: Dar de alta y editar perfiles de creadores/diseñadores y sus redes.
     - `CanApproveMedia`: Aprobar o descartar vídeos en el panel de moderación multimedia (YouTube/Instagram).
     - `CanResolveReports`: Gestionar, clasificar y resolver incidencias reportadas por la comunidad (INC-17).
     - `CanManageStoreLinks`: Añadir, corregir o retirar enlaces de compra y programas de afiliados (INC-11).
   - Los miembros de la Mesa Fundadora poseen inherentemente todos los permisos del sistema.
3. **Control de Autorización Reactivo en la Interfaz:**
   - Si un moderador carece de un permiso específico (ej. `CanManagePublishers`), la interfaz no renderiza los botones de edición/alta de esa sección (`[ ➕ Nueva Editorial ]` o `[ ✏️ Editar ]`).
   - Los servicios de aplicación rechazan cualquier invocación no autorizada devolviendo excepciones tipadas (`UnauthorizedAccessException`).
4. **Ventana y Registro de Auditoría de Cambios (`/admin/auditoria`):**
   - **Motor de Auditoría (`IAuditService`):** Intercepta y registra cada operación de escritura en el sistema generando una entidad `AuditLogEntry`.
   - **Datos Registrados:** Identificador y nombre del usuario, marca temporal exacta (UTC), acción realizada (`Created`, `Updated`, `Deleted`, `StatusChanged`), entidad afectada (`Game`, `Publisher`, `Creator`, `Media`, `Report`, `UserRole`), identificador del elemento y detalle estructurado de cambios (*diff* con campos modificados, valor previo y valor nuevo).
   - **Panel de Consulta para la Mesa Fundadora:**
     - Historial ordenado cronológicamente con paginación fluida.
     - Filtros multicriterio: por moderador/usuario, por tipo de entidad, por acción y por rango de fechas.
     - Vista expandible para inspeccionar el desglose de cambios campo por campo.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Mesa Fundadora asigna permisos específicos a un nuevo moderador
  Dado que un miembro de la Mesa Fundadora accede a "/admin/usuarios"
  Cuando promueve al usuario "laura_mod" al rol "Moderator"
  Y activa únicamente los permisos "CanEditGames" y "CanUploadImages"
  Y guarda los cambios
  Entonces el usuario "laura_mod" puede editar fichas de juego y subir carátulas
  Pero al acceder al directorio de editoriales no visualiza el botón de alta ni puede editar fichas de editorial

Escenario: Registro automático de auditoría tras modificar una ficha
  Dado un moderador con permiso "CanEditGames" que actualiza la duración del juego "Catan" de "60-90 min" a "45-75 min"
  Cuando pulsa "Guardar Cambios"
  Entonces el servicio de auditoría genera un registro con fecha, usuario, entidad "Game: catan"
  Y el registro especifica: campo "Duration", valor anterior "60-90", valor nuevo "45-75"

Escenario: Mesa Fundadora audita las acciones de un moderador concreto
  Dado un usuario de la Mesa Fundadora en "/admin/auditoria"
  Cuando filtra el historial por el moderador "laura_mod"
  Entonces visualiza cronológicamente todas las acciones ejecutadas por ese usuario
  Y puede expandir cada entrada para verificar qué tocó y en qué momento exacto
```

---

## 3. Arquitectura y Componentes Clave

- **Dominio (`Ludeka.Core`):**
  - Entidad `AppUser.cs` con colecciones de permisos o máscara `ModeratorPermissions` (`[Flags]`).
  - Enumerado `ModeratorPermission.cs` (`EditGames`, `UploadImages`, `ManagePublishers`, `ManageCreators`, `ApproveMedia`, `ResolveReports`, `ManageStoreLinks`).
  - Entidad `AuditLogEntry.cs` con tipos de acción y detalles serializados de cambios.
- **Aplicación (`Ludeka.Application`):**
  - Contrato `IUserManagementService.cs` para control de roles y permisos.
  - Contrato `IAuditService.cs` con método `RecordChangeAsync(...)` y consulta `GetAuditLogsAsync(...)`.
  - Ampliación de `ICurrentUserService.cs` con `HasPermission(ModeratorPermission permission)`.
- **Infraestructura (`Ludeka.Infrastructure`):**
  - Repositorios `SqliteUserRepository.cs` y `SqliteAuditRepository.cs`.
  - Configuración de tablas `AppUsers`, `UserPermissions` y `AuditLogs` en SQLite con índices por fecha y usuario.
- **Presentación Web (`Ludeka.Web`):**
  - Páginas `/admin/usuarios` (`UserManagement.razor`) y `/admin/auditoria` (`AuditLogViewer.razor`).
  - Componente modal de configuración de permisos `UserPermissionsModal.razor`.
  - Verificación de permisos en botones de acción en toda la plataforma.

---

## 4. Sinergia con otros Incrementos

- **Incremento 17 (Reportes Comunitarios):** La resolución de reportes queda auditada y requiere el permiso `CanResolveReports`.
- **Incremento 18 (Editor de Fichas e Imágenes):** La edición de juegos y subida de imágenes queda restringida a moderadores con `CanEditGames` y `CanUploadImages`, generando entradas de auditoría con el diff exacto.
- **Incremento 19 (Editoriales y Creadores):** La creación y modificación de entidades requiere `CanManagePublishers` y `CanManageCreators`, quedando debidamente registrado en el log de cambios.
