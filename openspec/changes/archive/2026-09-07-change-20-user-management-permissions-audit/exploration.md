# Exploración: change-20-user-management-permissions-audit (Incremento 20: Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora)

## 1. Estado Actual de la Solución y Análisis de Brechas (Gap Analysis)

### 1.1 Modelo de Usuarios y Roles Actual
- **Estado Actual (`ICurrentUserService.cs` y `DefaultCurrentUserService.cs`):**
  - Actualmente, `ICurrentUserService` expone `UserId`, `UserName`, `Roles` (`IReadOnlyList<string>`), `IsFoundingTeam` y `IsInRole(string role)`.
  - La implementación `DefaultCurrentUserService` cuenta con roles estáticos conmutables (`FoundingTeam`, `Moderator` y `User`) para fines de desarrollo y demo.
  - **Brecha Identificada:**
    - No existe una entidad persistente de usuario en el dominio (`AppUser`) almacenada en la base de datos relacional SQLite con roles formales (`FoundingTeam`, `Moderator`, `CommunityUser`) ni estado (`Active`, `Suspended`).
    - Los permisos de moderación son de tipo "todo o nada": si un usuario tiene el rol `Moderator`, se le asume capacitado para realizar cualquier acción en el sistema (editar juegos, resolver reportes, validar vídeos, etc.), o bien la interfaz no discrimina.
    - No existe una interfaz web para que la Mesa Fundadora liste, filtre, busque, promueva usuarios comunitarios a moderadores o configure qué permisos específicos ostenta cada uno.

### 1.2 Sistema de Permisos de Moderación
- **Estado Actual:**
  - En los incrementos 17 (Reportes), 18 (Editor de Fichas) y 19 (Directorio de Editoriales, Creadores y Tiendas), las comprobaciones se limitan a `CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")`.
  - **Brecha Identificada:**
    - Se necesita un sistema granular de permisos por especialidad (RBAC con flags/máscara):
      - `CanEditGames`: Modificación de datos generales y técnicos de juegos.
      - `CanUploadImages`: Carga de carátulas e imágenes en servidor.
      - `CanManagePublishers`: Alta y edición de editoriales y redes.
      - `CanManageCreators`: Alta y edición de autores y perfiles.
      - `CanApproveMedia`: Aprobación y descarte de contenidos multimedia.
      - `CanResolveReports`: Triaje y resolución de reportes comunitarios.
      - `CanManageStoreLinks`: Alta y edición de tiendas físicas/online y enlaces de afiliados.
    - Los miembros de la Mesa Fundadora deben poseer todos los permisos implícitamente sin configuración manual.
    - Si un usuario es suspendido (`Status = Suspended`), todos sus permisos quedan inhabilitados de inmediato.

### 1.3 Registro y Consulta de Auditoría
- **Estado Actual:**
  - En el Incremento 18 se creó `GameEditLog` únicamente para registrar ediciones en la ficha de un juego.
  - Los cambios en editoriales, creadores, tiendas, reportes, vídeos o asignación de roles no quedan registrados de manera estructurada en un registro centralizado.
  - **Brecha Identificada:**
    - Falta un servicio unificado de auditoría (`IAuditService`) y una entidad de auditoría universal (`AuditLogEntry`) que registre quién, cuándo (UTC), qué acción (`Created`, `Updated`, `Deleted`, `StatusChanged`, `RoleChanged`, `PermissionsChanged`), sobre qué entidad (`Game`, `Publisher`, `Creator`, `Store`, `Media`, `Report`, `User`), identificador del registro y detalle estructurado de campos modificados (*diff* con valor previo y nuevo).
    - No existe la vista `/admin/auditoria` para que la Mesa Fundadora inspeccione con filtros por usuario, entidad, acción y fecha, visualizando los cambios campo por campo.

---

## 2. Alternativas Técnicas y Decisiones Arquitectónicas

### 2.1 Modelado de Permisos Granulares en Dominio
- **Alternativa A: Tabla relacional normalizada `UserPermissions` (una fila por permiso y usuario).**
  - *Ventajas:* Totalmente relacional en SQL estándar.
  - *Desventajas:* Requiere un JOIN adicional por cada comprobación de usuario o múltiples llamadas a base de datos.
- **Alternativa B: Enumerado de bits con atributo `[Flags]` (`ModeratorPermission`) mapeado como entero en `AppUser` [DECISIÓN RECOMENDADA].**
  - *Ventajas:* Rendimiento inmejorable \(O(1)\), evaluación bit a bit con operadores lógicos (`&`, `|`), serializable limpiamente en SQLite como entero, compatible con EF Core 10 y extensible.
  - *Regla de Dominio:* La Mesa Fundadora (`Role == UserRole.FoundingTeam`) responde afirmativamente siempre a cualquier verificación de permiso sin importar la máscara.

### 2.2 Registro del Diff de Auditoría
- **Alternativa A: Texto libre arbitrario.**
  - *Desventajas:* Imposible reconstruir un visor estructurado de campos "antes vs. después".
- **Alternativa B: Colección de Value Objects `AuditFieldChange` mapeada con JSON nativo en EF Core 10 (`OwnsMany(..., b => b.ToJson())`) [DECISIÓN RECOMENDADA].**
  - *Ventajas:* Almacena `FieldName`, `OldValue` y `NewValue` de forma estructurada e inmutable en SQLite, permitiendo un renderizado visual enriquecido con badges y comparación lado a lado en la interfaz de usuario.

### 2.3 Seguridad y Autorización Reactiva en Capa Web y Capa de Aplicación
- **Decisión:** Doble barrera defensiva (Defense-in-depth):
  1. *Capa Web:* Los botones de edición, creación y acciones de moderación condicionan su renderizado a `CurrentUserService.HasPermission(ModeratorPermission.X)`. Si no se dispone del permiso, el botón ni siquiera se muestra en la UI.
  2. *Capa de Aplicación:* Los métodos de mutación en los servicios de aplicación (`GameEditorService`, `PublisherService`, `CreatorService`, `StoreService`, `GameIssueReportService`, `MediaService`, `UserManagementService`) validan explícitamente el permiso del usuario actual. Si no está autorizado, lanzan `UnauthorizedAccessException` inmediata, impidiendo cualquier elusión a nivel de API o formulario.

---

## 3. Plan de Integración en el Ecosistema Ludeka
- **Persistencia en SQLite:** Tablas `AppUsers` y `AuditLogs` configuradas en `LudekaDbContext`.
- **Seeder:** `UserManagementSeeder` que inicializa usuarios fundadores, moderadores con distintos perfiles de permisos y usuarios comunitarios, además de registros de auditoría históricos demostrativos.
- **Interoperabilidad:** Ampliación no destructiva de `ICurrentUserService` para que soporte `HasPermission(ModeratorPermission permission)` y `SwitchUser(string userId)` para facilitar pruebas interactivas en vivo.
