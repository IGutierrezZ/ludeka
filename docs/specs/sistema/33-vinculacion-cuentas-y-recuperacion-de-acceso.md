# 33. Vinculación de Cuentas entre Proveedores y Recuperación de Acceso

> **Estado:** Implementado y Verificado (1.711 pruebas unitarias en verde, 0 errores, 0 omitidas)
> **Incremento:** [INC-49](file:///c:/repos/Ludeka/docs/increments/archive/inc-49-vinculacion-cuentas.md) e [INC-63](file:///c:/repos/Ludeka/docs/increments/archive/inc-63-conexiones-oauth.md)
> **Dependencia:** INC-46, Autenticación Real (archivado) — ver módulo 32
> **Módulos relacionados:** [32. Autenticación Social, Autorización por Permisos y Política de Anonimia](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [14. Gestión de Usuarios, Permisos y Auditoría](file:///c:/repos/Ludeka/docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md) · [37. Área de Cuenta y Puerta de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/37-area-de-cuenta-y-puerta-de-acceso.md)

## 1. El Problema: por qué existe este módulo

INC-46 resolvió el acceso (Google, Discord, Facebook en un clic, sin correo ni contraseña) pero dejó abierta una consecuencia que podía costarle la cuenta a un usuario.

**Sin correo, perder el proveedor es perder la cuenta.** `AppUser.Email` es obligatorio: la entidad lanza `ArgumentException` si llega vacío. Cuando un proveedor no entrega correo —Facebook lo omite si el usuario no autoriza el permiso, o si la aplicación no ha superado la revisión de Meta—, la cuenta se aprovisiona con un correo sintético no enrutable en el dominio reservado `ludeka.invalid` (formato real `{clave}@{proveedor}.ludeka.invalid`, construido por `ExternalLoginService.BuildPlaceholderEmail`). Esa cuenta no tiene ninguna vía de contacto: si la persona pierde el acceso a su Discord, no hay correo al que enviar nada y no hay segundo método de acceso. Vincular un segundo proveedor es la única red de seguridad cuando se ha decidido deliberadamente no usar correo y contraseña.

**La cuenta partida en dos.** Si esa misma persona entra después con Google, que sí entrega correo verificado, la cascada de acceso (§3) no encontraba la cuenta bajo su correo real, sino bajo el sintético. El resultado, antes de este incremento, era una segunda cuenta: la ludoteca, los préstamos y el diario de partidas quedaban partidos en dos.

Este módulo entrega la pantalla `/cuenta/conexiones` —la única vía de recuperación posible para una cuenta sin correo verificado— y convierte la regla «solo se fusiona con correo verificado» en un comportamiento explícito y auditado, en vez de un efecto colateral de la cascada de acceso.

---

## 2. La Frontera de Seguridad

> **Solo se fusiona con correo verificado. Cualquier otra unión de identidades la inicia el usuario desde una sesión ya establecida.**

Si se fusionara por correo no verificado, bastaría registrarse en cualquier proveedor declarando el correo de otra persona para entrar en su cuenta de Ludeka. El `emailVerified` que entrega el proveedor —proyectado como `ludeka:email_verified` (ver módulo 32 §2.2)— es la única prueba de que quien llega controla esa dirección.

### Las cuatro conductas que materializan la regla

| Situación | Conducta | Por qué es segura |
|---|---|---|
| El par `(Provider, ProviderKey)` ya existe | Resuelve a la misma cuenta, sin filas nuevas | El proveedor certifica la identidad; el índice único `(Provider, ProviderKey)` lo garantiza |
| Correo **verificado** que coincide con una cuenta **sin proveedores previos** | Vinculación automática (rama **2a**, §3) | Excepción segura: no hay sesión ajena que secuestrar |
| Correo verificado que coincide con **otra cuenta que ya tiene proveedores** (rama **2b**), o identidad entrante nueva sobre cuenta poblada | **Avisar, nunca fusionar**: dirige a iniciar sesión con el método ya usado y a vincular desde Ajustes → Conexiones | La unión la decide quien demuestra controlar la cuenta destino, no quien llega |
| Vinculación desde `/cuenta/conexiones` | Se asocia **siempre** al `UserId` de la sesión activa; jamás crea cuenta | La sesión es la prueba de propiedad, el desafío OAuth es la prueba de la identidad entrante |

**Por qué vincular exige dos pruebas simultáneas.** La sesión de Ludeka demuestra la propiedad de la cuenta destino; el desafío OAuth —emitido por el proveedor, no por Ludeka— demuestra el control de la identidad entrante. Ninguna de las dos por separado basta. Por eso el `UserId` se captura en servidor antes del desafío (§6) y toda vinculación y desvinculación se audita (§9).

---

## 3. La Cascada de Acceso Actualizada

`ExternalLoginService.ResolveAsync` resuelve todo inicio de sesión social en este orden. **Antes de este incremento no existía la distinción 2a/2b: el paso 2 era un único caso que vinculaba automáticamente siempre que el correo llegara verificado y coincidiera con una cuenta existente, sin comprobar si esa cuenta ya tenía otros proveedores vinculados** — más amplio que la excepción segura que la propuesta de INC-49 daba por ya implementada.

| Rama | Condición | Conducta | Escrituras |
|---|---|---|---|
| **1** | Existe fila `(Provider, ProviderKey)` (`ExternalLoginService.cs:52-61`) | Resuelve a esa cuenta, con sus roles y permisos intactos | Ninguna |
| **2a** | Correo verificado coincide con `AppUser.Email` de una cuenta **sin ninguna** identidad externa vinculada (`:82-87`) | Vinculación automática — conducta idéntica a la de INC-46, ahora fijada por prueba de regresión explícita | Crea la fila `ExternalLogin` |
| **2b** | Correo verificado coincide con `AppUser.Email` de una cuenta **que ya tiene** al menos una identidad externa (`:72-80`) | Lanza `ExternalLoginCollisionException`; el login normal la traduce a un aviso fijo en `Login.razor` («Ya existe una cuenta con este correo. Inicia sesión con el método que ya usas y vincula este proveedor desde Ajustes → Conexiones.») | **Ninguna** — cero filas, cero fusión |
| **3** | Ninguna de las anteriores (`:91-105`) | Alta de `AppUser` nuevo como `CommunityUser` / `ModeratorPermission.None`; nunca `FoundingTeam` | Crea `AppUser` + fila `ExternalLogin` |

Las 6 pruebas de regresión de INC-46 (`ExternalLoginServiceTests.cs:51-146`) no se modificaron en ninguno de los 7 PRs de la cadena: siguen verdes byte a byte contra el código nuevo. La partición 2a/2b vive en `ExternalLoginCascadeRegressionTests.cs`, con 6 pruebas dedicadas que fijan cada rama por aserción, incluida la invariante de recuento (`AppUsers`/`ExternalLogins`) bajo colisión repetida.

---

## 4. Modelo de Datos: la marca de verificación por fila

### 4.1 Por qué una columna explícita, no una derivación

La exploración de este incremento encontró que `ExternalLoginService` persistía el correo crudo del proveedor en `ExternalLogin.ProviderEmail` **sin condicionarlo a que estuviera verificado** — y que ese comportamiento era deliberado: el comentario original declaraba que el correo sin verificar «queda únicamente como pista en `ExternalLogin.ProviderEmail`». Es decir, `ProviderEmail != null` **no** implica verificado, y derivar «¿tiene correo verificado?» de esa columna habría producido falsos positivos desde el primer commit. Condicionar la escritura del propio `ProviderEmail` (alternativa sin migración) tampoco se adoptó: habría revertido esa decisión deliberada de INC-46 y descansado en una convención de código que el esquema no impide romper.

La solución adoptada es una columna nueva y explícita, `ExternalLogins.ProviderEmailVerifiedAt` (`DateTimeOffset?`), que **conserva** la pista original y **además** refuerza el invariante por esquema, no por disciplina de código.

### 4.2 Dónde se calcula: el constructor de la entidad, no el llamador

```
ProviderEmailVerifiedAt = providerEmailVerified && ProviderEmail is not null ? LinkedAt : null;
```

El 6.º parámetro del constructor de `ExternalLogin`, `providerEmailVerified`, es **opcional y va al final** deliberadamente: el 5.º parámetro (`linkedAt`) ya se pasaba posicionalmente en pruebas de INC-46, y insertar el nuevo parámetro antes lo habría roto. Situar el cálculo en el constructor —no en `ExternalLoginService`— hace que sea **imposible** construir una fila con `ProviderEmailVerifiedAt` establecido y `ProviderEmail` nulo, sea cual sea el punto de llamada. Se escribe **una sola vez**, al crear la fila, y nunca se modifica después: una fila creada sin correo verificado conserva `null` para siempre, aunque ese mismo proveedor entregue correo verificado en un acceso posterior (limitación aceptada; la vía de reparación sería una promoción monótona explícita, deliberadamente no construida en este incremento porque exigiría un método de actualización sobre `ExternalLogin` — ver §10.2).

### 4.3 Doble frente de esquema

| Motor | Mecanismo | Ficheros |
|---|---|---|
| **PostgreSQL (producción)** | Migración EF Core aditiva: `AddColumn<DateTimeOffset>(nullable: true)` en `Up`; `DropColumn` en `Down` | `Migrations/20260917112154_AddProviderEmailVerifiedAtToExternalLogins.cs` |
| **SQLite (desarrollo y pruebas)** | **Dos** tratamientos, no uno: columna en el `CREATE TABLE` del bloque 22 (bases creadas desde cero) **y** un bloque 23 nuevo con `ALTER TABLE … ADD COLUMN`, protegido por `PRAGMA table_info`, para bases ya existentes | `SqliteSchemaMigrator.cs:854` (bloque 22) y `:865-876` (bloque 23) |

El bloque 22 por sí solo solo crea la tabla cuando no existe: sin el bloque 23, cualquier base SQLite creada antes de este incremento quedaría sin la columna. El bloque 23 es idempotente (si la columna se acaba de crear en el mismo arranque, `PRAGMA table_info` ya la ve y no hace nada), siguiendo el patrón que el propio fichero ya usa para `Giveaways.IsPromoted` y `Stores.Country`. Ninguna migración es destructiva ni pierde filas existentes.

---

## 5. La Pantalla `/cuenta/conexiones`

Primera página del repositorio protegida con `@attribute [Authorize]` **sin `Policy=`**: basta con tener sesión iniciada, sin ningún `ModeratorPermission` granular. Las 10 páginas administrativas existentes (módulo 32 §4.2) exigen todas una política concreta; esta es autoservicio de cualquier usuario autenticado sobre su propia cuenta. Un anónimo es redirigido a `/login` tanto en render estático (el middleware de autorización actúa antes de renderizar nada) como en render interactivo (`AuthorizeRouteView` → `RedirectToLogin`).

Muestra cada proveedor habilitado por configuración (mismo mecanismo de `ExternalAuthenticationSchemes` del módulo 32) junto con si está vinculado a la cuenta, y ofrece dos acciones con mecanismos deliberadamente distintos:

| Acción | Mecanismo | Por qué |
|---|---|---|
| **Vincular** | Formulario HTTP clásico (`<form method="post" data-enhance="false">` + `<AntiforgeryToken />`), mismo patrón que `Login.razor` | `Results.Challenge` escribe cabeceras HTTP de redirección; un circuito Blazor Server por SignalR no puede hacerlo cuando el evento llega, la respuesta HTTP ya se cerró |
| **Desvincular** | `@onclick` sobre el circuito interactivo | No necesita cabeceras: llama a `UnlinkAsync`, refresca el listado en el mismo circuito sin recarga |

**La guarda del último método de acceso es del servidor, no de la interfaz.** No se puede desvincular la última identidad externa de una cuenta sin ningún otro método: la comprobación vive en `ExternalLoginService.UnlinkAsync` (`:229-259`, guarda en `:246-250`) y lanza `LastAccessMethodException` si `links.Count <= 1`. Una petición fabricada que evite el botón se deniega igual, porque la guarda no depende de que el botón esté deshabilitado.

---

## 6. El Transporte de la Intención de Vinculación

Vincular reutiliza la misma tubería OAuth del acceso (módulo 32 §2.3) con una segunda intención marcada explícitamente, en vez de una tubería nueva.

1. Un endpoint propio (`POST /cuenta/conexiones/vincular`, mapeado tras el bloque `/logout` en `Program.cs`, con `.RequireAuthorization()`) lee el `UserId` **exclusivamente** de `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` — nunca de un campo del formulario, que solo aporta el proveedor elegido (`Program.cs:511`, `:517-520`).
2. Ese `UserId` se marca en `AuthenticationProperties.Items` mediante `ExternalLoginIntent.MarkLink` (clase estática y pura, claves `ludeka:intent=link` y `ludeka:link_user_id`), que viaja dentro del parámetro `state` protegido por Data Protection del manejador remoto del proveedor. El navegador no puede leerlo ni falsificarlo.
3. Al volver, `ExternalLoginEvents.HandleTicketReceivedAsync` bifurca hacia `LinkAsync` en vez de `ResolveAsync` cuando detecta la intención, pero **antes** reconfirma que la sesión que vuelve coincide con la que la emitió (`ExternalLoginEvents.cs:65,70`).

| Estado de la sesión al volver | Conducta |
|---|---|
| Coincide con el `UserId` marcado | Se vincula |
| **Sin sesión** (expiró o se cerró) | No se vincula nada; redirección a `/login?aviso=vinculacion-sin-sesion`; no se firma ninguna cookie |
| **Sesión de otro usuario** | No se vincula nada; redirección a `/cuenta/conexiones?resultado=sesion-cambiada` |

Ausencia de la intención en `Items` ⇒ el camino de acceso normal queda byte a byte igual que en INC-46: es la compatibilidad hacia atrás.

---

## 7. Aviso de Cuenta Sin Correo Verificado

Cuando ninguna identidad externa de la cuenta tiene `ProviderEmailVerifiedAt` establecido, aparece un aviso en `/cuenta/conexiones` **y** en la cabecera de la aplicación (`AccountEmailNotice.razor`, montado en `MainLayout.razor`), con enlace a la pantalla de conexiones. El descarte de la cabecera es un campo privado del componente (`_dismissed`, sin persistencia): al vivir toda la aplicación en un único circuito interactivo global, el componente sobrevive a la navegación de la pestaña sin recargar la página.

**Corrección de caché descubierta durante la aplicación.** El servicio de lectura (`IAccountConnectionsService`, `Scoped`) cachea su resultado durante la vida de la instancia; en Blazor Server con renderizado interactivo global, esa vida es **todo el circuito**, no una sola petición. Sin invalidación explícita, desvincular el único proveedor con correo verificado no habría actualizado ni la propia pantalla de conexiones ni el aviso de cabecera hasta una recarga completa. Se añadió `InvalidateCache()` + un evento `Invalidated`: la pantalla de conexiones invalida tras cada desvinculación, y `AccountEmailNotice` se suscribe al evento (`IDisposable`, con el mismo guard de interactividad que `SessionGuard.razor`) para refrescarse sin recarga.

El aviso **nunca** aparece en el perfil público (`PublicProfile.razor` no referencia el servicio ni el componente en ningún punto): publicar ahí que una cuenta carece de correo verificado filtraría a desconocidos una debilidad concreta de esa cuenta.

---

## 8. Reemplazo del Correo Sintético

Cuando una cuenta nacida con correo sintético (`{clave}@{proveedor}.ludeka.invalid`, o la forma corta histórica `@ludeka.invalid`) vincula explícitamente un proveedor que entrega un correo verificado, `AppUser.Email` se reemplaza por ese correo real (`TryReplacePlaceholderEmailAsync`, `ExternalLoginService.cs:171-202`). El rastro de que la cuenta nació sin correo no se pierde del todo: `ExternalLogin.ProviderEmail` y `ProviderEmailVerifiedAt` conservan el origen de cada vínculo.

**Solo se dispara en `LinkAsync` (vinculación explícita desde `/cuenta/conexiones`), nunca en la rama 1 de `ResolveAsync` (reautenticación normal).** La rama 1 es hoy de solo lectura y la recorre casi todo acceso; convertirla en escritura condicional habría multiplicado el riesgo de regresión sobre la cascada de INC-46 a cambio de un escenario secundario, y habría dejado la decisión de reemplazar en manos del proveedor en cada login, en vez de en manos del usuario en el momento en que decide vincular — el criterio que la frontera de seguridad de este módulo defiende.

**Es de mejor esfuerzo y nunca fatal.** Se intenta *después* de que la fila `ExternalLogin` ya esté persistida: si el correo ya pertenece a otro `AppUser` (comprobación previa con el mismo criterio que usa la cascada) o hay una carrera contra el índice único de `AppUsers.Email`, el reemplazo simplemente no ocurre y la cuenta conserva su correo sintético — la vinculación en sí **sigue siendo un éxito**.

---

## 9. Auditoría

`AuditAction` incorpora `LinkedProvider` (8) y `UnlinkedProvider` (9), con nombre visible propio en `AuditService.GetActionDisplayName` («Vinculación de Proveedor», «Desvinculación de Proveedor»; `AuditService.cs:123-124`) — ver también módulo 14. Cada vinculación o desvinculación completada con éxito registra una entrada con el `UserId` de la sesión que la ejecutó; cuando el éxito incluye el reemplazo del correo sintético (§8), se añade además la tupla `("Email", correo_anterior, correo_verificado)` junto a `("Provider", null, "{Proveedor}")`.

**Nunca se audita un intento denegado o rechazado.** La guarda del último método (`LastAccessMethodException`) y el rechazo de un proveedor ya vinculado a otra cuenta salen de la función **antes** de alcanzar la llamada de auditoría.

---

## 10. Decisiones de Arquitectura que Conviene que Perduren

### 10.1 `Ludeka.Application` no referencia EF Core

Verificado por `grep -rn "EntityFrameworkCore" src/Ludeka.Application` (salida vacía). Las colisiones de índice único se traducen a excepciones de dominio **en `Infrastructure`**, nunca se capturan como `DbUpdateException` en `Application`:

| Colisión | Traducida a | Dónde se traduce |
|---|---|---|
| `(Provider, ProviderKey)` duplicado | `DuplicateExternalLoginException` | `ExternalLoginRepository.AddAsync` |
| `AppUsers.Email` duplicado (reemplazo del sintético) | `DuplicateUserEmailException` | `SqliteUserRepository.UpdateAsync` |

Este patrón ya existía para el primer caso (commit `695898c`, previo a este incremento); INC-49 lo extiende al segundo en vez de introducir una excepción de EF Core en la capa de aplicación, tal como una lectura literal de la tarea original habría hecho.

### 10.2 `IExternalLoginRepository` no expone ningún método de actualización, a propósito

El contrato completo es `GetByProviderKeyAsync`, `AddAsync`, `ListByUserIdAsync` y `RemoveAsync`. **No existe un quinto método que actualice una fila existente.** Es deliberado: reasignar el `UserId` de una fila `ExternalLogin` entre cuentas queda **estructuralmente imposible**, no solo indeseable por convención. La comprobación previa en `LinkAsync` (antes de cualquier escritura) existe para poder dar un mensaje honesto; el índice único `(Provider, ProviderKey)` es la garantía real que cubre la carrera entre dos intentos simultáneos, y ambas rutas convergen en el mismo rechazo (`RejectedOwnedByAnotherAccount`).

---

## 11. Fuera de Alcance (y por qué)

| Excluido | Motivo |
|---|---|
| **Herramienta de fusión de cuentas ya duplicadas** | Decisión explícita del maintainer: no existe todavía base de datos de producción, así que el escenario de dos cuentas duplicadas no puede darse hoy. El inventario de las 13 entidades que referencian `AppUser` (solo `ExternalLogin` tiene FK real, con `Cascade`) queda documentado en `exploration.md` del incremento, como evidencia para el día en que el problema sea real |
| **Aviso de correo no verificado en el perfil público** | `PublicProfile.razor` lo ve cualquier visitante; publicar ahí una debilidad de seguridad de una cuenta concreta la expondría a desconocidos |

---

## 12. Estado de Verificación

- **Suite:** `dotnet test Ludeka.sln` → **1.417/1.417 en verde, 0 errores, 0 omitidas** (línea base previa, INC-46: 1.345; +72 pruebas). Las 6 pruebas de regresión de INC-46 (`ExternalLoginServiceTests.cs:51-146`) no tienen ninguna línea modificada en toda la cadena.
- **Veredicto de `sdd-verify`:** `pass_with_warnings` — 35/35 escenarios de especificación conformes (`account-provider-connections`, `social-login-authentication`, `user-management-permissions-audit`), 10/11 criterios de aceptación automatizables cumplidos.
- **WARNING abierto (no bloqueante):** `ExternalLoginEvents.HandleTicketReceivedAsync` no captura `UnauthorizedAccessException` en la rama de vinculación. Si la cuenta de la sesión se suspende o se elimina exactamente entre el desafío OAuth y el retorno del proveedor, la excepción se propaga sin capturar y degrada a la página `/Error` genérica (vía el manejador de excepciones global ya existente), en vez de a una redirección con mensaje fijo como las demás ramas de fallo de este módulo. No compromete ninguna garantía de seguridad (no se crea cuenta indebida, no se firma sesión ajena, no se pierde ninguna fila): es deuda técnica de seguimiento, candidata a un incremento de mantenimiento posterior.
- **Pendiente, correctamente declarado (no oculto):** el smoke test manual de navegador (ciclo vincular → desvincular → intento de desvincular el último, con verificación de accesibilidad por teclado y lector de pantalla) exige credenciales OAuth reales de al menos dos proveedores, que no existen en este entorno de aplicación. Sus pasos exactos quedan documentados en el historial de aplicación del incremento (`apply-progress.md`, PR #4) para que el maintainer los ejecute cuando disponga de credenciales de prueba.
- **Entrega:** 7 Pull Requests encadenados desde el mismo worktree (`inc/vinculacion-cuentas` → `…-07-reemplazo-correo`, PRs #23-#29), todos mergeados a `main`.

---

## 13. Consolidación Multi-Proveedor y Guarda Preventiva en UI (INC-63)

El incremento INC-63 consolidó la vinculación entre múltiples proveedores sin duplicación de cuentas y cerró los huecos identificados en la interfaz de `/cuenta/conexiones`:

### 13.1 Enriquecimiento del Contrato `AccountConnectionDto`
- `AccountConnectionDto` expone `ProviderEmail` (`string?`) y `ProviderEmailVerifiedAt` (`DateTimeOffset?`), obtenidos de la entidad `ExternalLogin` mediante `AccountConnectionsService.BuildViewAsync`.
- Esto brinda visibilidad al usuario sobre qué dirección de correo exacta respalda cada proveedor vinculado (por ejemplo, Google frente a Discord).

### 13.2 Guarda Preventiva en la Interfaz de Usuario (`AccountConnections.razor`)
- Se renderiza el correo vinculado y la fecha de verificación en la lista de conexiones activas.
- **Deshabilitación preventiva del único método de acceso:** Cuando `!_view.CanUnlink` (`links.Count <= 1`), el botón «Desvincular» se deshabilita preventivamente en el cliente (`disabled`, `aria-disabled="true"`, `cursor-not-allowed`) y se muestra un badge explicativo `Único método`. Esto evita pulsar un botón que desencadenaría una llamada innecesaria al backend que fallaría con `LastAccessMethodException`.

### 13.3 Garantía Verificada de Ciclo de Vida Multi-Proveedor y Prevención de Duplicados
La suite `MultiProviderLifecycleTests.cs` (`7ba4227`) garantiza de extremo a extremo:
1. **Acceso indistinto a la misma cuenta:** Un usuario que se registra con Google y vincula Discord puede cerrar sesión y acceder posteriormente con Discord, resolviendo siempre al mismo `AppUser` sin duplicar cuentas en la base de datos.
2. **Reversibilidad y retención de acceso:** Si se registra con Discord, vincula Google y posteriormente desvincula Discord, el acceso mediante Google permanece operativo y exclusivo.
3. **Protección anti-colisión estricta:** Si se intenta vincular un proveedor ya asignado a otra cuenta, se rechaza de forma determinista mediante `ExternalLoginCollisionException`, preservando invariantes y evitando asignaciones ilícitas.
4. **Idempotencia:** Re-vincular el mismo proveedor bajo la misma cuenta no altera el estado.

