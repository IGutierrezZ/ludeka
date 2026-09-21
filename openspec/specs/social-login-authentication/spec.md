# Especificación: social-login-authentication

> Capacidad nueva (INC-46). Acceso social multi-proveedor, sesión por cookie, aprovisionamiento y vinculación de identidad.
> Fuente: `docs/increments/inc-46-autenticacion-real.md` §2.1–2.2 y §2.7.

## Propósito

Definir cómo un visitante inicia sesión con Google, Discord o Facebook (habilitables por configuración), cómo se emite y cierra la sesión por cookie y cómo se vincula o aprovisiona un `AppUser` mediante `ExternalLogin`, sin formulario de registro, contraseña ni infraestructura de correo.

## Requirements

### Requirement: Acceso social multi-proveedor dirigido por configuración

El sistema DEBE registrar cada esquema externo (Google, Discord, Facebook) solo cuando su configuración `Authentication__Providers__{Proveedor}__Enabled` sea `true`. Un proveedor con `Enabled=false` NO DEBE registrarse ni aparecer en la pantalla de acceso. Registro y acceso DEBEN ser el mismo flujo: sin formulario de registro, sin contraseña y sin solicitud de correo electrónico.

#### Scenario: Proveedor habilitado completa el acceso

- GIVEN un proveedor con `Enabled=true` y credenciales configuradas
- WHEN un visitante pulsa su botón en la pantalla de acceso y autoriza en el proveedor
- THEN vuelve a Ludeka con sesión iniciada
- AND no se muestra ningún formulario de registro, contraseña ni correo.

#### Scenario: Proveedor deshabilitado no se registra ni aparece

- GIVEN Facebook con `Authentication__Providers__Facebook__Enabled=false`
- WHEN se inspecciona el registro de esquemas y la pantalla de acceso
- THEN el esquema de Facebook no existe y su botón no se renderiza
- AND el resto de proveedores habilitados no se ve afectado.

#### Scenario: Primer acceso aprovisiona la cuenta

- GIVEN un visitante sin cuenta previa y un proveedor habilitado
- WHEN completa el acceso por primera vez
- THEN obtiene una sesión y una cuenta aprovisionada, sin pasos de registro adicionales.

### Requirement: Sesión por cookie de Ludeka

Tras el acceso, el sistema DEBE emitir su propia cookie de sesión con `HttpOnly`, `SecurePolicy = Always`, `SameSite = Lax` y caducidad deslizante (`Authentication__Cookie__ExpireMinutes`). El cierre de sesión DEBE invalidarla.

#### Scenario: Atributos de la cookie de sesión

- GIVEN un acceso social completado
- WHEN se inspecciona la cookie de sesión emitida
- THEN es `HttpOnly`, `Secure` y `SameSite=Lax`, con la caducidad configurada.

#### Scenario: Cierre de sesión invalida la sesión

- GIVEN una sesión iniciada
- WHEN el usuario cierra sesión y solicita una ruta protegida
- THEN es redirigido al inicio de sesión, sin identidad previa.

### Requirement: Vinculación y aprovisionamiento mediante ExternalLogin

El sistema DEBE persistir la identidad externa en `ExternalLogin` (`Id`, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail`, `ProviderEmailVerifiedAt`, `LinkedAt`) con índice único sobre `(Provider, ProviderKey)`. La resolución automática en el inicio de sesión DEBE seguir esta cascada: (1) por `(Provider, ProviderKey)` — si existe, resuelve a esa cuenta sin filas nuevas; (2) por correo verificado del proveedor contra `AppUser.Email`, distinguiendo dos casos — (2a) la cuenta encontrada no tiene ninguna identidad externa vinculada todavía: se crea la fila `ExternalLogin` y se vincula automáticamente; (2b) la cuenta encontrada ya tiene al menos una identidad externa vinculada: el sistema NO DEBE crear ninguna fila ni fusionar la sesión entrante con esa cuenta, y DEBE informar del conflicto sin fusionar en silencio; (3) si ninguna de las anteriores aplica, alta de un `AppUser` nuevo con `UserRole.CommunityUser` y `ModeratorPermission.None`. El sistema NO DEBE auto-conceder `FoundingTeam`, NO DEBE fusionar cuentas por correo no verificado, y NO DEBE fusionar en silencio en el caso (2b).

(Previously: la cascada resolvía el caso 2 en un único paso que vinculaba automáticamente siempre que el correo estuviera verificado y coincidiera con una cuenta existente, sin distinguir si esa cuenta ya tenía identidades externas vinculadas; `ExternalLogin` tampoco incluía `ProviderEmailVerifiedAt`.)

#### Scenario: Par reincidente resuelve a la misma cuenta

- GIVEN una fila `ExternalLogin` existente para `(Provider, ProviderKey)`
- WHEN el mismo usuario vuelve a iniciar sesión con ese proveedor
- THEN resuelve a la misma cuenta, sin crear filas ni usuarios adicionales.

#### Scenario: Correo verificado coincide con una cuenta sin identidades externas previas (no regresión de INC-46)

- GIVEN un acceso cuyo correo viene verificado y coincide con `AppUser.Email` de una cuenta que no tiene ninguna identidad externa vinculada todavía
- WHEN no existe fila para `(Provider, ProviderKey)`
- THEN se crea la fila `ExternalLogin` vinculada a esa cuenta existente
- AND la cuenta conserva sus roles y permisos
- AND el resultado es exactamente el de la rama 2 original de INC-46: ninguna prueba de regresión existente sobre este caso cambia de resultado.

#### Scenario: Correo verificado coincide con una cuenta que ya tiene identidades vinculadas

- GIVEN un acceso cuyo correo viene verificado y coincide con `AppUser.Email` de una cuenta que ya tiene al menos una identidad externa vinculada
- WHEN no existe fila para `(Provider, ProviderKey)` de este acceso
- THEN el sistema no fusiona: no crea ninguna fila `ExternalLogin` nueva ni concede la sesión sobre esa cuenta
- AND informa del conflicto, dirigiendo a iniciar sesión con el método ya usado y a vincular desde Ajustes → Conexiones.

#### Scenario: Alta de usuario comunitario

- GIVEN un acceso sin fila previa y sin correo verificado coincidente
- WHEN se completa el flujo
- THEN se aprovisiona un `AppUser` nuevo con `CommunityUser` y `ModeratorPermission.None`
- AND nunca se concede `FoundingTeam`.

#### Scenario: Correo no verificado no fusiona cuentas

- GIVEN un correo del proveedor sin verificar que coincide con otra cuenta
- WHEN se completa el flujo
- THEN NO se vincula ni fusiona: se aplica el alta nueva
- AND la cuenta preexistente no gana `ExternalLogin` alguna.

### Requirement: Semilla del administrador fundador sin identidad implícita

`AdminUserSeeder` DEBE conservar la fila del fundador en la base de datos, pero esa fila NO DEBE conceder identidad ni sesión implícita a ningún visitante.

#### Scenario: Visitante anónimo sin privilegios tras el arranque

- GIVEN la aplicación iniciada con la fila del fundador sembrada
- WHEN un visitante anónimo navega sin iniciar sesión
- THEN ninguna evaluación de rol o permiso le concede identidad de fundador.

### Requirement: Vinculación manual de proveedores desde sesión activa

El sistema DEBE permitir que un usuario autenticado vincule un proveedor de identidad externo adicional desde su propia sesión activa. La vinculación manual asocia siempre la nueva identidad externa al `UserId` de la sesión activa y no aprovisiona nunca un `AppUser` nuevo. El mecanismo completo —pantalla, guarda del último método de acceso, desvinculación, rechazo de proveedores ya vinculados a otra cuenta y auditoría— se especifica en la capacidad `account-provider-connections`; este requisito fija la garantía observable desde la perspectiva de la cascada de identidad.

(El renombrado del requisito se declara en la sección `## RENAMED Requirements` de la cabecera de este delta.)

#### Scenario: Vincular un proveedor adicional desde sesión activa

- GIVEN un usuario con sesión activa y sin fila `ExternalLogin` previa para el proveedor elegido
- WHEN vincula ese proveedor desde `/cuenta/conexiones`
- THEN se crea una fila `ExternalLogin` para el mismo `UserId` de la sesión
- AND no se aprovisiona ningún `AppUser` nuevo.
### Requirement: Marca de verificación del correo por identidad externa

El sistema DEBE persistir, por cada fila `ExternalLogin`, si el correo del proveedor estaba verificado en el momento en que se escribió esa fila, mediante la columna `ProviderEmailVerifiedAt` (`DateTimeOffset?`, nula cuando no hay verificación). Esta columna DEBE existir tanto en una base de datos migrada desde cero como en una base de datos SQLite preexistente reconciliada por el mecanismo de arranque, sin que ninguna migración destructiva sea necesaria.

#### Scenario: Columna presente en una base de datos migrada desde cero

- GIVEN una base de datos migrada desde cero con las migraciones de este incremento aplicadas
- WHEN se inspecciona el esquema de `ExternalLogins`
- THEN existe la columna `ProviderEmailVerifiedAt` de tipo `DateTimeOffset?`.

#### Scenario: Columna reconciliada en una base SQLite preexistente

- GIVEN una base de datos SQLite creada antes de este incremento, sin la columna `ProviderEmailVerifiedAt`
- WHEN el mecanismo de reconciliación de esquema se ejecuta al arrancar
- THEN la columna se añade
- AND ninguna fila `ExternalLogin` existente se pierde, se trunca ni se elimina.

#### Scenario: Verificación registrada cuando el correo llega verificado

- GIVEN un proveedor que entrega un correo con `emailVerified=true`
- WHEN se escribe la fila `ExternalLogin` correspondiente
- THEN `ProviderEmailVerifiedAt` queda establecido.

#### Scenario: Ausencia de verificación cuando el correo no llega verificado

- GIVEN un proveedor que entrega un correo con `emailVerified=false`, o ningún correo
- WHEN se escribe la fila `ExternalLogin` correspondiente
- THEN `ProviderEmailVerifiedAt` permanece nulo.

### Requirement: Reemplazo del correo sintético por el correo verificado del proveedor

Cuando una cuenta cuyo `AppUser.Email` es un correo sintético del dominio reservado `ludeka.invalid` vincula explícitamente un proveedor que entrega un correo verificado, el sistema DEBE reemplazar `AppUser.Email` por ese correo verificado, siempre que ningún otro `AppUser` posea ya ese correo. Si otro `AppUser` ya lo posee, el sistema NO DEBE realizar el reemplazo, y la cuenta conserva su correo sintético. El sistema NO DEBE violar nunca el índice único de `AppUsers.Email`. Queda a criterio del diseño si esta misma sustitución también se dispara al reautenticar por la vía de `(Provider, ProviderKey)` de la cascada de acceso; de implementarse, DEBE respetar el mismo invariante de unicidad fijado aquí.

#### Scenario: Reemplazo exitoso del correo sintético

- GIVEN una cuenta con `AppUser.Email` en el dominio `ludeka.invalid` y ningún otro `AppUser` con el correo entrante
- WHEN esa cuenta vincula un proveedor que entrega un correo verificado
- THEN `AppUser.Email` pasa a ser ese correo verificado
- AND la fila `ExternalLogin` conserva `ProviderEmail` y `ProviderEmailVerifiedAt` como rastro del origen del vínculo.

#### Scenario: Reemplazo rechazado por colisión de correo

- GIVEN una cuenta con `AppUser.Email` en el dominio `ludeka.invalid`, y otro `AppUser` que ya tiene ese correo entrante como su `Email`
- WHEN esa cuenta vincula un proveedor que entrega ese mismo correo verificado
- THEN el `AppUser.Email` de la cuenta original no cambia y conserva el correo sintético
- AND no se viola el índice único de `AppUsers.Email`.

#### Scenario: Cuenta sin correo sintético no se ve afectada

- GIVEN una cuenta cuyo `AppUser.Email` ya es un correo real, no sintético
- WHEN vincula un proveedor que entrega un correo verificado
- THEN `AppUser.Email` no cambia.

### Requirement: Aviso de arranque cuando ningún proveedor social resulta utilizable

Además de los avisos existentes por proveedor habilitado sin credenciales, el sistema DEBE evaluar en el arranque si existe al menos un proveedor social utilizable (habilitado y con credenciales completas). Cuando ninguno lo sea, el sistema DEBE emitir una línea de registro distinguible de los avisos por proveedor individual, que indique inequívocamente que ningún proveedor de autenticación social está operativo. Esa línea DEBE emitirse con severidad de error cuando el entorno de ejecución es `Production`, y con severidad de aviso en cualquier otro entorno. Cuando exista al menos un proveedor utilizable, el sistema NO DEBE emitir esa línea.

*Verificación: prueba de la función de avisos existente, extendida con el parámetro de entorno; mismo patrón que `WebAuthenticationRegistrationTests` y que el requisito equivalente de avisos de almacenamiento de medios (`media-storage-precedence`).*

#### Scenario: Production con cero proveedores utilizables registra un error (caso negativo, defecto B4 sin corregir)

- GIVEN el entorno `Production` y los tres proveedores sociales no utilizables (deshabilitados, o habilitados sin credenciales completas) —el estado de la configuración versionada por defecto—
- WHEN la aplicación arranca
- THEN se registra una línea de severidad de error que indica que ningún proveedor de autenticación social está operativo

#### Scenario: Entorno distinto de Production con cero proveedores utilizables registra un aviso, no un error

- GIVEN un entorno distinto de `Production` (por ejemplo `Development`) con los tres proveedores sociales no utilizables
- WHEN la aplicación arranca
- THEN se registra una línea de severidad de aviso, no de error, que indica que ningún proveedor está operativo

#### Scenario: Al menos un proveedor utilizable no emite el aviso agregado

- GIVEN al menos un proveedor social habilitado y con credenciales completas
- WHEN la aplicación arranca
- THEN no se emite la línea de "ningún proveedor operativo"
- AND los avisos existentes por proveedor individual sin credenciales, si los hay, se siguen emitiendo sin cambios

