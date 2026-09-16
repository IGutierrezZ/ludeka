# Archive Report — `change-46-autenticacion-real` (INC-46)

> **Archivado:** 2026-09-16 · **Store:** openspec (repo-local) · **Rama:** `inc/autenticacion-real-cierre` · **Worktree:** `C:\repos\ludeka-wt\autenticacion-real`
> **Ciclo SDD:** propose → spec → design → tasks → apply → verify → **archive** (completo).

## 1. Estado final del cambio

| Hecho | Valor final |
|---|---|
| Veredicto de verificación | `pass_with_warnings` — 17/17 requisitos y 42/42 escenarios conformes, **0 hallazgos CRITICAL** (instantánea tomada en `039fd21`) |
| Suite final (reejecutada durante el archivo) | **1345/1345**, 0 errores, 0 omitidas (`dotnet test Ludeka.sln --configuration Release`, exit 0) |
| Punto de partida | 1.005 pruebas en `main` antes del incremento → **+340** |
| Tareas | **66/67**. Ejecutadas en esta fase 5.1–5.5 (autorización explícita del orquestador); 5.6 (PR/cleanup) **diferida a la entrega** por prohibición de `git push`/PR en la fase de archivo |
| Contradicciones de instantánea sin resolver | Ninguna |

### Reconciliación de estado final (posterior a la instantánea de verificación)

`verify-report.md` describe el estado en `039fd21`; el estado al cierre incluye trabajo posterior:

- **W1 RESUELTO** (slice `inc/autenticacion-real-cierre`, commits `47d93e9` … `1f8addc`, registro en `047b070`): la auditoría amplió el hallazgo de 8 a **15 servicios administrativos de escritura**, todos con `ISessionPermissionGuard` (relectura `AsNoTracking` del `AppUser`) y rutas de sistema auditables para los ciclos programados; el contrato por reflexión `AdministrativeWriteGuardContractTests` lo fija. La suite pasó de 1.258 a **1.345**.
- **Exposición real confirmada y corregida:** los botones de escritura administrativa de páginas públicas (`/novedades`, `/radar`, `/eventos` con modal exprés y «Cola comunitaria» de `/mi-ludoteca`) se mostraban a cualquier `Moderator` sin la bandera granular y a una cuenta suspendida durante la ventana de revocación. Los servicios ahora deniegan sin sesión/permiso y la interfaz traduce la denegación en redirección a `/login`.
- **Correcciones aplicadas en el archivo:** **W2** — el delta `policy-based-authorization` decía «11 páginas protegidas» donde hay 10 (14 rutas con alias); se corrigió durante la composición. Además, la enumeración PA-2 se alineó a las **11 políticas reales** añadiendo `PermisoGestionarEventos` y `PermisoGestionarNotificaciones` (verificadas por pruebas y por el propio informe). **W3** — `design.md` §9.2 anotado: `/admin/vinculacion-fundador` no existe en este cambio y la pantalla de vinculación manual de proveedores se traslada al **INC-49**.
- **Pendientes que siguen abiertos (no se presentan como resueltos):** sugerencias S1 (homogeneizar el marcado de `EventsManagement`/`AdminNotifications`), S2 (endurecer la proyección SSR de la cookie), S3 (cobertura extremo a extremo de logout, cascada y panel multimedia), S4 (fijar por prueba la ausencia de identidad centinela) y S5 (reejecutar el arranque con PostgreSQL real; el humo se hizo con SQLite por ausencia de servidor). Traslado de producto al INC-49: pantalla de vinculación manual de proveedores y política de cuentas sin correo verificado.

## 2. Specs sincronizadas (openspec)

| Dominio | Acción | Detalle |
|---|---|---|
| `editorial-role-management` | Actualizada (compose nativo) | 1 MODIFIED + 1 REMOVED aplicados; requisito no relacionado preservado |
| `media-moderation-panel` | Actualizada (compose nativo) | 1 MODIFIED aplicado; los 3 requisitos restantes preservados |
| `social-login-authentication` | Creada (copia mecánica) | `diff -r` vacío, `DIFF_EXIT=0` |
| `policy-based-authorization` | Creada (copia mecánica) | `diff -r` vacío, `DIFF_EXIT=0` (delta con W2 corregido) |
| `anonymity-policy` | Creada (copia mecánica) | `diff -r` vacío, `DIFF_EXIT=0` |

## 3. Volcado a la especificación viva

- **Módulo nuevo:** `docs/specs/sistema/32-autenticacion-y-autorizacion.md` — modelo social multi-proveedor, cookie de sesión, cascada de vinculación `ExternalLogin`, **12 banderas** con valores, **11 políticas** y mapa página→política (10 páginas / 14 rutas), revalidación en 15 servicios y rutas de sistema, política de anonimia, invalidación de circuito y estado de verificación.
- **`docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md`:** 12 banderas (`All = 4095`), contrato sin conmutadores, `ISessionPermissionGuard`, modal de 12 casillas con guardado sin pérdida, identidad de sesión real y `ExternalLogins`.
- **`docs/specs/sistema/09-arquitectura-y-despliegue.md`:** 32 tablas (con `ExternalLogins`), sesión autenticada, `AdminUserSeeder` sin identidad implícita y CI con 1.345 pruebas.
- **`docs/specs/sistema/README.md`:** enlace al módulo 32 y **total real de pruebas: 1.345**.

## 4. Roadmap y documentos de incremento

- `docs/increments/ROADMAP.md`: INC-46 → ✅ Archivado con enlace a `archive/inc-46-autenticacion-real.md`; bloqueo de salida a producción reducido a INC-47 e INC-48; sección de worktrees actualizada (sin incrementos activos).
- `docs/specs/ROADMAP_MVP_SLICES.md`: Incremento 46 incorporado con estado, verificación y módulos.
- `docs/increments/inc-46-autenticacion-real.md` → `docs/increments/archive/inc-46-autenticacion-real.md` (estado «✅ Archivado»), `git mv` + `diff -r` vacío.

## 5. Verificación mecánica del archivo (evidencia verbatim)

| Paso | Comando | Resultado |
|---|---|---|
| Composición `editorial-role-management` | `gentle-ai sdd-archive-compose --canonical … --delta … --output …compose-tmp` | `EXIT=0`; `.compose-tmp` → spec canónica |
| Composición `media-moderation-panel` | `gentle-ai sdd-archive-compose --canonical … --delta … --output …compose-tmp` | `EXIT=0`; `.compose-tmp` → spec canónica |
| Copia `social-login-authentication` | `diff -r <origen> <tmp>` | **sin salida** · `DIFF_EXIT=0` |
| Copia `policy-based-authorization` | `diff -r <origen> <tmp>` | **sin salida** · `DIFF_EXIT=0` |
| Copia `anonymity-policy` | `diff -r <origen> <tmp>` | **sin salida** · `DIFF_EXIT=0` |
| Movimiento del cambio | instantánea recursiva + `git mv` + `diff -r <snapshot> <destino>` | **sin salida** · `DIFF_EXIT=0`; origen ausente |
| Movimiento del documento de incremento | instantánea + `git mv` + `diff -r <snapshot> <destino>` | **sin salida** · `DIFF_EXIT=0` |

## 6. Contenido del archivo

- `proposal.md` ✅ · `specs/` (5 dominios) ✅ · `design.md` ✅ · `tasks.md` ✅ (66/67; 5.6 diferida y anotada) · `apply-progress.md` ✅ · `verify-report.md` ✅ · `archive-report.md` (este documento).

## 7. Commits del cierre

| Sha | Mensaje | Contenido |
|---|---|---|
| `acc73ce` | `docs(specs): sincronizar las specs canonicas de INC-46 y corregir el recuento de paginas` | Deltas + specs canónicas |
| `153f069` | `docs(sistema): documentar la autenticacion y la autorizacion en la especificacion viva` | Módulos 32/14/09 + README |
| `21ba335` | `docs(roadmap): archivar INC-46 y actualizar los incrementos en curso` | ROADMAP + hoja de ruta + documento archivado |
| (commit de archivo) | `docs(sdd): archivar el cambio change-46-autenticacion-real` | `tasks.md`, `design.md` §9.2, movimiento del cambio y este informe |

## 8. Estado del ciclo

El cambio quedó planificado, especificado, diseñado, implementado, verificado y archivado. La entrega (push de la rama y PR a `main`) queda fuera de la fase de archivo por instrucción del orquestador; la ejecuta el flujo de entrega posterior con `scripts/sdd-worktree.ps1`.
