# Especificación: account-provider-connections

> Capacidad nueva (INC-49). Autoservicio de conexiones de la propia cuenta: vinculación y desvinculación de identidades externas desde sesión activa.
> Fuente: `openspec/changes/change-49-vinculacion-cuentas/proposal.md` §2.1, §4 y §11.

## Propósito

Definir la pantalla `/cuenta/conexiones` y las operaciones de vincular y desvincular una identidad externa desde la propia sesión de un usuario autenticado. Es la única vía de recuperación de acceso para una cuenta sin correo verificado y sin segundo método de acceso, y fija el patrón de ruta nuevo «exige solo sesión iniciada, sin permiso granular», distinto de las rutas administrativas y de moderación que cubre `policy-based-authorization`.

## Requirements

### Requirement: Acceso restringido a sesión iniciada, sin permiso granular

El sistema DEBE exponer la ruta `/cuenta/conexiones`, accesible únicamente para un usuario con sesión iniciada. La ruta NO DEBE exigir ningún `ModeratorPermission` granular: basta con tener sesión activa, a diferencia de las rutas administrativas y de moderación. Un visitante anónimo NO DEBE ver el contenido de la página en ningún modo de renderizado.

#### Scenario: Anónimo en render estático (SSR) no accede al contenido

- GIVEN un visitante sin sesión que solicita `/cuenta/conexiones` como primera petición HTTP (render estático)
- WHEN el servidor procesa la petición
- THEN el pipeline de autorización actúa antes de que se renderice cualquier proveedor o estado de vinculación
- AND el visitante termina en el inicio de sesión, sin ver ninguna fila de proveedores.

#### Scenario: Anónimo en render interactivo es redirigido al inicio de sesión

- GIVEN un visitante sin sesión cuyo circuito ya es interactivo
- WHEN navega a `/cuenta/conexiones` desde dentro de la aplicación
- THEN es redirigido a la pantalla de inicio de sesión mediante navegación forzada del cliente.

#### Scenario: Autenticado sin ningún permiso de moderación accede a la página

- GIVEN un `CommunityUser` autenticado sin ningún `ModeratorPermission`
- WHEN solicita `/cuenta/conexiones`
- THEN accede al contenido sin ninguna denegación por falta de permiso.

#### Scenario: Listado de proveedores habilitados y su estado de vinculación

- GIVEN un usuario autenticado y el conjunto de proveedores habilitados por configuración
- WHEN abre `/cuenta/conexiones`
- THEN ve cada proveedor habilitado junto con si está vinculado o no a su cuenta
- AND ningún proveedor deshabilitado por configuración aparece en el listado.

### Requirement: Vinculación de un proveedor adicional desde sesión activa

El sistema DEBE permitir a un usuario con sesión activa iniciar el desafío de un proveedor de identidad adicional y, al completarlo, asociar la identidad entrante al `UserId` de esa sesión. La vinculación NO DEBE aprovisionar nunca un `AppUser` nuevo, sea cual sea el resultado del desafío.

#### Scenario: Vincular un proveedor no vinculado previamente

- GIVEN una sesión activa sin fila `ExternalLogin` para el proveedor elegido
- WHEN el usuario completa el desafío de vinculación de ese proveedor
- THEN se crea una fila `ExternalLogin` para el `UserId` de la sesión
- AND no se aprovisiona ningún `AppUser` nuevo.

#### Scenario: La identidad vinculada es siempre la de la sesión, no la del cliente

- GIVEN una sesión activa con un `UserId` conocido
- WHEN se fabrica una petición de vinculación que intenta sugerir un `UserId` distinto al de la sesión
- THEN el sistema ignora cualquier identificador propuesto por el cliente
- AND vincula exclusivamente al `UserId` de la sesión autenticada en servidor.

> Nota: esta misma operación puede además disparar el reemplazo del correo sintético de la cuenta cuando el proveedor entrega un correo verificado; ese comportamiento se especifica en la capacidad `social-login-authentication`.

### Requirement: Rechazo de vincular una identidad ya vinculada a otra cuenta

Cuando la identidad entrante del desafío de vinculación (`Provider` + `ProviderKey`) ya está vinculada a un `UserId` distinto al de la sesión activa, el sistema DEBE rechazar la vinculación sin moverla ni duplicarla. El mensaje de rechazo NO DEBE prometer una vía de resolución automática que el sistema no ofrece. La fila `ExternalLogin` existente NO DEBE cambiar nunca de `UserId` como consecuencia de este intento.

#### Scenario: Rechazo cuando el proveedor ya pertenece a otra cuenta

- GIVEN una fila `ExternalLogin` existente para `(Provider, ProviderKey)` asociada al `UserId` B
- WHEN el usuario de la sesión A, distinto de B, intenta vincular ese mismo proveedor
- THEN la vinculación se rechaza
- AND no se crea ninguna fila nueva
- AND la fila existente conserva el `UserId` B sin cambios.

#### Scenario: El mensaje de rechazo no promete una resolución inexistente

- GIVEN el rechazo del escenario anterior
- WHEN se presenta el mensaje al usuario de la sesión A
- THEN el mensaje no ofrece ni sugiere una fusión de cuentas ni una transferencia automática del proveedor entre cuentas.

#### Scenario: La fila en conflicto nunca cambia de propietario

- GIVEN repetidos intentos de vinculación en conflicto sobre el mismo `(Provider, ProviderKey)`
- WHEN se repiten esos intentos desde distintas sesiones
- THEN el índice único `(Provider, ProviderKey)` sigue apuntando exclusivamente a la fila original
- AND ningún intento la duplica ni la reasigna.

### Requirement: Guarda del último método de acceso

El sistema NO DEBE permitir desvincular la última identidad externa vinculada a una cuenta que no tiene ningún otro método de acceso. La comprobación DEBE aplicarse en servidor, de modo que una petición que evite la interfaz también sea denegada.

#### Scenario: Intento de desvincular el único proveedor es denegado

- GIVEN una cuenta con exactamente una fila `ExternalLogin` vinculada
- WHEN el usuario intenta desvincular ese proveedor
- THEN la operación se deniega
- AND la fila permanece vinculada.

#### Scenario: Una petición que evita la interfaz también se deniega

- GIVEN la misma cuenta con un único proveedor vinculado
- WHEN se invoca la operación de desvinculación directamente contra el servidor, sin pasar por el botón de la interfaz
- THEN el servidor deniega igualmente la operación.

#### Scenario: Desvincular procede cuando queda al menos otro método

- GIVEN una cuenta con dos o más filas `ExternalLogin` vinculadas
- WHEN el usuario desvincula una de ellas
- THEN la fila elegida se elimina
- AND la cuenta conserva al menos un método de acceso.

### Requirement: Auditoría de vincular y desvincular

Toda vinculación y desvinculación de un proveedor completada con éxito DEBE registrarse en la bitácora de auditoría mediante `IAuditService`, identificando al usuario de la sesión que ejecuta la operación.

#### Scenario: Vincular registra una entrada de auditoría

- GIVEN una vinculación completada con éxito
- WHEN se inspecciona la bitácora de auditoría
- THEN existe una entrada de vinculación asociada al `UserId` de la sesión que la ejecutó.

#### Scenario: Desvincular registra una entrada de auditoría

- GIVEN una desvinculación completada con éxito
- WHEN se inspecciona la bitácora de auditoría
- THEN existe una entrada de desvinculación asociada al `UserId` de la sesión que la ejecutó.

#### Scenario: Un intento denegado o rechazado no registra una auditoría de éxito

- GIVEN un intento de desvincular el último método (denegado) o de vincular un proveedor ya usado por otra cuenta (rechazado)
- WHEN se inspecciona la bitácora de auditoría
- THEN no existe ninguna entrada de vinculación o desvinculación exitosa correspondiente a ese intento.

### Requirement: Aviso de cuenta sin correo verificado

Cuando ninguna identidad externa vinculada a la cuenta de la sesión activa tiene el correo verificado (`ProviderEmailVerifiedAt` no nulo en ninguna fila), el sistema DEBE mostrar un aviso tanto en `/cuenta/conexiones` como en la cabecera de la aplicación, dirigiendo a `/cuenta/conexiones`. El aviso NO DEBE mostrarse nunca en el perfil público del usuario.

#### Scenario: Aviso visible en la pantalla de conexiones

- GIVEN una cuenta sin ninguna identidad externa con correo verificado
- WHEN el usuario abre `/cuenta/conexiones`
- THEN ve un aviso indicando la falta de correo verificado.

#### Scenario: Aviso visible y descartable en la cabecera

- GIVEN la misma cuenta sin correo verificado
- WHEN navega a cualquier página estando autenticado
- THEN la cabecera muestra el mismo aviso, con un enlace a `/cuenta/conexiones`
- AND el usuario puede descartarlo sin recargar la página completa.

#### Scenario: Aviso ausente cuando existe correo verificado

- GIVEN una cuenta con al menos una identidad externa cuyo correo está verificado
- WHEN el usuario abre `/cuenta/conexiones` o cualquier página autenticada
- THEN no se muestra ningún aviso de correo no verificado.

#### Scenario: El aviso nunca aparece en el perfil público

- GIVEN una cuenta sin correo verificado
- WHEN cualquier visitante, incluido el propio usuario, ve su perfil público
- THEN el perfil no menciona el estado de verificación del correo de esa cuenta.
