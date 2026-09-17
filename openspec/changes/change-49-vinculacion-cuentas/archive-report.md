# Informe de Archivado — INC-49: Vinculación de Cuentas entre Proveedores

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-archive` · **Fecha:** 2026-09-18
> **Rama / worktree de esta fase:** `inc/vinculacion-cuentas-archivo` — `C:\repos\ludeka-wt\vinculacion-cuentas` (parte de `origin/main` ya actualizado con los 7 PRs del incremento, #23-#29, mergeados)
> **Resultado de esta fase:** **archivado parcial.** El volcado a la especificación viva del sistema y el archivado del documento de incremento están completos. El archivado SDD estándar (fusión de specs delta + traslado de la carpeta de cambio) está **bloqueado** para 2 de las 3 capacidades por un rechazo mecánico legítimo de `gentle-ai sdd-archive-compose`; no se ha aplicado ningún merge manual ni se ha movido la carpeta del cambio, tal como exige el contrato de composición mecánica de la skill `sdd-archive`.

---

## 1. Autoridad de estado final — fuentes leídas

Se leyeron íntegramente, desde el sistema de ficheros (modo `hybrid`, artefactos declarados como rutas de repositorio, no topic keys de Engram):

- `openspec/changes/change-49-vinculacion-cuentas/exploration.md`
- `openspec/changes/change-49-vinculacion-cuentas/proposal.md`
- `openspec/changes/change-49-vinculacion-cuentas/specs/account-provider-connections/spec.md`
- `openspec/changes/change-49-vinculacion-cuentas/specs/social-login-authentication/spec.md`
- `openspec/changes/change-49-vinculacion-cuentas/specs/user-management-permissions-audit/spec.md`
- `openspec/changes/change-49-vinculacion-cuentas/design.md`
- `openspec/changes/change-49-vinculacion-cuentas/tasks.md`
- `openspec/changes/change-49-vinculacion-cuentas/apply-progress.md`
- `openspec/changes/change-49-vinculacion-cuentas/verify-report.md` (opcional, presente)

No se leyó ninguna observación de Engram como entrada de esta fase; el espejo de Engram (sección 6) es exclusivamente de salida.

**Jerarquía de autoridad aplicada:** `verify-report.md` (2026-09-18, punta de la cadena `inc/vinculacion-cuentas-07-reemplazo-correo`, commit `a4479d7`) es la fuente más reciente sobre el estado funcional del incremento y no hay hechos de estado final más nuevos en el prompt de lanzamiento de esta fase que la contradigan; se cita como autoritativa. `apply-progress.md` es el histórico de aplicación (7 lotes, uno por PR) y se cita como evidencia de lo que era cierto en cada momento, no como estado final cuando `verify-report.md` lo cubre.

---

## 2. Estado funcional del incremento (para trazabilidad — no se ha tocado código ni pruebas en esta fase)

- **Suite:** `dotnet test Ludeka.sln` → **1.417/1.417 en verde, 0 errores, 0 omitidas** (línea base INC-46: 1.345; +72). Las 6 pruebas de regresión de `ExternalLoginServiceTests.cs:51-146` no tienen ninguna línea modificada en toda la cadena (`git diff 01b4b73..HEAD --name-only` vacío para ese fichero, confirmado en `verify-report.md`).
- **Veredicto de `sdd-verify`:** `pass_with_warnings` — 0 CRITICAL, 1 WARNING, 2 SUGGESTION; 35/35 escenarios de especificación conformes (19 `account-provider-connections`, 13 `social-login-authentication`, 3 `user-management-permissions-audit`); 10/11 criterios de aceptación automatizables cumplidos.
- **WARNING abierto (no bloqueante):** `ExternalLoginEvents.HandleTicketReceivedAsync` no captura `UnauthorizedAccessException` en la rama de vinculación; una cuenta suspendida o eliminada exactamente entre el desafío OAuth y el retorno degrada a la página `/Error` genérica en vez de a una redirección con mensaje fijo. Sin impacto en las garantías de seguridad centrales (nunca se crea cuenta indebida, nunca se firma sesión ajena, nunca se pierde una fila). Registrado como deuda técnica de seguimiento en el módulo 33 de la especificación viva.
- **Pendiente, declarado honestamente y sin ocultar:** la tarea 4.5 (smoke test manual de navegador: vincular → desvincular → intento de desvincular el último) sigue sin marcar en `tasks.md`. Exige credenciales OAuth reales de al menos dos proveedores, inexistentes en este entorno. No se ha marcado como completada ni se ha simulado su ejecución.
- **Entrega:** 7 Pull Requests encadenados (`inc/vinculacion-cuentas` → `…-07-reemplazo-correo`, PRs #23 a #29), todos ya mergeados a `main` antes del inicio de esta fase.

---

## 3. Volcado a la especificación viva del sistema — COMPLETO

**Creado** `docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md` (12 secciones: el problema, la frontera de seguridad, la cascada 2a/2b, el modelo de datos de `ProviderEmailVerifiedAt`, la pantalla `/cuenta/conexiones`, el transporte de la intención de vinculación, el aviso de correo no verificado y su corrección de caché, el reemplazo del correo sintético, la auditoría, las decisiones de arquitectura a preservar, lo fuera de alcance, y el estado de verificación con el WARNING y el pendiente manual expuestos sin maquillar). Construido íntegramente a partir de `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md`, `verify-report.md` y las 3 especificaciones delta — ningún dato inventado.

**Actualizado** `docs/specs/sistema/32-autenticacion-y-autorizacion.md`: añadida referencia cruzada al módulo 33 en la cabecera; la cascada de la sección 3 ahora documenta la partición 2a/2b (con nota explícita de que antes de INC-49 no existía esa distinción); la columna `ProviderEmailVerifiedAt` y el reemplazo del correo sintético se mencionan donde correspondía; la nota de "Decisión de producto (INC-49, fuera de este incremento)" se sustituyó por la referencia al módulo 33 ya entregado; el bullet de "Traslados de producto" de la sección 7 se actualizó de pendiente a resuelto. El resto del fichero (secciones 1, 2, 4, 5, 6 y el resto de la 7) no se tocó.

**Actualizado** `docs/specs/sistema/README.md`: añadida la entrada 33 con el mismo formato denso de una frase que las entradas 30-32, cerrando con "1.417 pruebas unitarias verificadas (INC-49)"; corregido el contador de la introducción arquitectónica de 1.345 a 1.417 pruebas para que no contradiga la entrada nueva en el mismo documento.

---

## 4. Documento de incremento y roadmap — COMPLETO

- `git mv docs/increments/inc-49-vinculacion-cuentas.md docs/increments/archive/inc-49-vinculacion-cuentas.md` — Git lo registró como renombrado puro (`R`), confirmando que el contenido no cambió de bytes en el movimiento. Cabecera de estado actualizada a `✅ Archivado (2026-09-18)` con el veredicto, los contadores de prueba y el aviso del smoke test pendiente; añadido el enlace al módulo 33.
- `docs/increments/ROADMAP.md`: fila de INC-49 en la tabla maestra actualizada de `⏳ En progreso` a `✅ Archivado`, con el enlace del documento apuntando a `archive/inc-49-vinculacion-cuentas.md`. Retirada la entrada de INC-49 de la sección «Incrementos en Curso», sustituida por una nota de una línea señalando que completó su cadena de 7 PRs y quedó archivada.

---

## 5. Archivado SDD estándar — PARCIAL (bloqueado en 2 de 3 capacidades)

### 5.1 Fusión de especificaciones delta

| Capacidad | Spec canónica | Resultado | Evidencia |
|---|---|---|---|
| `account-provider-connections` | No existía | **Aplicado.** Copia mecánica (`cp` + `mv` atómico vía fichero temporal), sin `sdd-archive-compose` porque no hay spec canónica previa contra la que componer — la delta ES la spec completa. | `diff -r` vacío (ver sección 7) |
| `social-login-authentication` | `openspec/specs/social-login-authentication/spec.md` | **RECHAZADO por la herramienta. Sin escritura.** | Ver 5.2 |
| `user-management-permissions-audit` | `openspec/specs/user-management-permissions-audit/spec.md` | **RECHAZADO por la herramienta. Sin escritura.** | Ver 5.2 |

### 5.2 Mensajes exactos del rechazo (`gentle-ai sdd-archive-compose`, salida verbatim)

```
$ gentle-ai sdd-archive-compose \
    --canonical "openspec/specs/social-login-authentication/spec.md" \
    --delta "openspec/changes/change-49-vinculacion-cuentas/specs/social-login-authentication/spec.md" \
    --output "openspec/specs/social-login-authentication/spec.md.compose-tmp"

Error: sdd-archive-compose: unapplied MODIFIED delta for requirement "Vinculación manual de proveedores desde sesión activa": no canonical requirement named "Vinculación manual de proveedores desde sesión activa"
EXIT: 1
```

```
$ gentle-ai sdd-archive-compose \
    --canonical "openspec/specs/user-management-permissions-audit/spec.md" \
    --delta "openspec/changes/change-49-vinculacion-cuentas/specs/user-management-permissions-audit/spec.md" \
    --output "openspec/specs/user-management-permissions-audit/spec.md.compose-tmp"

Error: sdd-archive-compose: CANONICAL: canonical spec has no "### Requirement:" headings to compose against
EXIT: 1
```

Verificado tras ambos rechazos: ningún fichero `.compose-tmp` quedó en disco y ninguna de las dos specs canónicas cambió de tamaño ni de fecha de modificación (`ls -la` antes/después idéntico). La herramienta cumplió su contrato documentado: "On an unapplied delta, writes nothing and fails naming the section and requirement."

### 5.3 Causa raíz de cada rechazo (diagnóstico, no una decisión tomada por esta fase)

1. **`social-login-authentication`.** La sección `## MODIFIED Requirements` de la delta renombra de hecho el requisito canónico `"Vinculación manual de proveedores (decisión pendiente de diseño)"` a `"Vinculación manual de proveedores desde sesión activa"`, documentando el cambio de nombre en una nota `(Previously: ...)` dentro del propio bloque `MODIFIED`, en vez de una sección `## RENAMED Requirements` con nombre antiguo y nuevo explícitos, que es lo que exige `skills/_shared/openspec-convention.md` ("Each rename MUST state old and new names explicitly") y lo que la propia composición mecánica necesita para localizar el requisito antes de aplicar el `MODIFIED`. Es un defecto de forma en la especificación delta ya aprobada en fases anteriores (`sdd-spec`), no un error de esta fase ni del contenido semántico del cambio.
2. **`user-management-permissions-audit`.** La spec canónica de esta capacidad está en un formato de prosa numerada con escenarios Gherkin embebidos (heredado de INC-20, anterior a la convención de encabezados `### Requirement:` / `#### Scenario:` que sí siguen `social-login-authentication` y la delta nueva `account-provider-connections`). No contiene ningún encabezado `### Requirement:`, así que la herramienta no tiene contra qué componer ni siquiera un `## ADDED Requirements` puro. Es una deuda estructural preexistente del repositorio, no introducida por INC-49.

### 5.4 Por qué esta fase no lo resolvió por su cuenta

La skill `sdd-archive` (Contrato de Composición Mecánica) es explícita e inequívoca: *"Treat this as a blocking failure — STOP the phase and report `blocked` with that exact message. Do NOT retry with a manual Read/Edit merge, and do NOT move the change into the archive."* Un merge manual de Application/Infrastructure — leer la spec canónica y aplicar yo mismo las secciones ADDED/MODIFIED — es exactamente el modo de fallo que ese contrato existe para impedir (issue #4119 citado en la propia skill: así es como el archivado histórico ha dejado requisitos sin aplicar mientras informaba éxito). Tampoco se ha editado la especificación delta del cambio para forzar la composición: es un artefacto ya aprobado por fases anteriores del ciclo, y esta fase no tiene autoridad para reescribir retroactivamente su contenido — ver «Honest Partial Archive» de la skill.

**Recomendación para el maintainer u orquestador (no decidida aquí):**
- Para `social-login-authentication`: corregir la delta con una sección `## RENAMED Requirements` explícita (nombre antiguo → nombre nuevo) antes de reintentar la composición — probablemente vía una corrección puntual de `sdd-spec` sobre este cambio, no una reescritura libre.
- Para `user-management-permissions-audit`: decidir, como acción deliberada y separada de este incremento, si se migra esa spec canónica al formato `### Requirement:`/`#### Scenario:` (permitiendo componer contra ella en el futuro) o si esta capacidad se sigue manteniendo fuera de la composición mecánica.

### 5.5 Traslado de la carpeta del cambio — NO REALIZADO

Consecuencia directa de 5.4: `openspec/changes/change-49-vinculacion-cuentas/` **permanece en su ubicación activa**, NO se ha movido a `openspec/changes/archive/2026-09-18-change-49-vinculacion-cuentas/`. La regla del propio contrato es "ALWAYS sync delta specs BEFORE moving to archive"; con 2 de 3 specs sin sincronizar, mover la carpeta habría dejado el archivo en un estado inconsistente (código y pruebas ya en `main`, pero la especificación canónica de dos capacidades sin la conducta nueva reflejada). Este mismo fichero (`archive-report.md`) se escribe, por tanto, dentro de la carpeta activa del cambio, no dentro de una carpeta archivada que todavía no existe.

---

## 6. Espejo en Engram

Guardado bajo `topic_key: sdd/change-49-vinculacion-cuentas/archive-report`, `project: ludeka`, `type: architecture`, `capture_prompt: false`. El contenido íntegro y autoritativo es este fichero, versionado en la rama `inc/vinculacion-cuentas-archivo`; la observación de Engram es un resumen compacto de traza, no una copia completa (evita el truncamiento silencioso de Engram por encima de ~50.000 caracteres por observación).

---

## 7. Evidencia mecánica — `diff -r` verbatim (única prueba admisible de copia sin alteración)

```
$ diff -r "openspec/changes/change-49-vinculacion-cuentas/specs/account-provider-connections/spec.md" "openspec/specs/account-provider-connections/spec.md"
(sin salida — diff vacío)
```

No hay ningún otro `diff -r` que reportar en esta fase: no hubo traslado de carpeta de cambio (5.5) y las dos composiciones de spec existentes fueron rechazadas sin escritura (5.2).

---

## 8. Resumen de artefactos por estado

| Artefacto | Estado | Ubicación |
|---|---|---|
| `docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md` | Creado | Especificación viva |
| `docs/specs/sistema/32-autenticacion-y-autorizacion.md` | Actualizado (secciones 3 y 7, cabecera) | Especificación viva |
| `docs/specs/sistema/README.md` | Actualizado (entrada 33 + contador) | Índice maestro |
| `docs/increments/archive/inc-49-vinculacion-cuentas.md` | Movido y cabecera actualizada | Archivo de incrementos |
| `docs/increments/ROADMAP.md` | Actualizado (tabla + sección en curso) | Roadmap |
| `openspec/specs/account-provider-connections/spec.md` | Creado (copia mecánica verificada) | Specs canónicas |
| `openspec/specs/social-login-authentication/spec.md` | **Sin cambios — composición rechazada** | Specs canónicas |
| `openspec/specs/user-management-permissions-audit/spec.md` | **Sin cambios — composición rechazada** | Specs canónicas |
| `openspec/changes/change-49-vinculacion-cuentas/` | **Sin mover — permanece activa** | Cambios activos |
| `openspec/changes/change-49-vinculacion-cuentas/archive-report.md` | Creado (este fichero) | Cambio activo |

**El ciclo SDD de INC-49 no está cerrado en `openspec/`.** El código, las pruebas y la especificación viva del sistema SÍ reflejan el estado final real e implementado. Queda pendiente, como trabajo de una fase futura (una corrección de `sdd-spec` sobre la delta de `social-login-authentication`, y una decisión del maintainer sobre `user-management-permissions-audit`), reintentar la sincronización de specs y el traslado de la carpeta.
