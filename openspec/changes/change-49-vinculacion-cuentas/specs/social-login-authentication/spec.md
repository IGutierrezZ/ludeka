# Delta for social-login-authentication

> Cambio `change-49-vinculacion-cuentas` (INC-49). Cierra el requisito abierto de vinculación manual por la rama incluida, añade la marca de verificación por fila y el reemplazo del correo sintético, y fija el comportamiento observable de la colisión en el inicio de sesión.

## RENAMED Requirements

### Requirement: Vinculación manual de proveedores (decisión pendiente de diseño) → Vinculación manual de proveedores desde sesión activa

(Reason: INC-46 dejó el requisito abierto con dos ramas posibles, "rama incluida" y "rama aplazada". INC-49 lo cierra por la rama incluida, así que el nombre deja de describir una decisión pendiente y pasa a describir el comportamiento entregado.)
(Migration: la rama aplazada deja de ser un comportamiento válido del sistema y se retira de esta especificación. El bloque `MODIFIED` de más abajo sustituye el requisito completo con su contenido definitivo.)

## ADDED Requirements

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

## MODIFIED Requirements

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

### Requirement: Vinculación manual de proveedores desde sesión activa

El sistema DEBE permitir que un usuario autenticado vincule un proveedor de identidad externo adicional desde su propia sesión activa. La vinculación manual asocia siempre la nueva identidad externa al `UserId` de la sesión activa y no aprovisiona nunca un `AppUser` nuevo. El mecanismo completo —pantalla, guarda del último método de acceso, desvinculación, rechazo de proveedores ya vinculados a otra cuenta y auditoría— se especifica en la capacidad `account-provider-connections`; este requisito fija la garantía observable desde la perspectiva de la cascada de identidad.

(El renombrado del requisito se declara en la sección `## RENAMED Requirements` de la cabecera de este delta.)

#### Scenario: Vincular un proveedor adicional desde sesión activa

- GIVEN un usuario con sesión activa y sin fila `ExternalLogin` previa para el proveedor elegido
- WHEN vincula ese proveedor desde `/cuenta/conexiones`
- THEN se crea una fila `ExternalLogin` para el mismo `UserId` de la sesión
- AND no se aprovisiona ningún `AppUser` nuevo.
