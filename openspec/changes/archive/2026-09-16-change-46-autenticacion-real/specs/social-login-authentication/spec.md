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

El sistema DEBE persistir la identidad externa en `ExternalLogin` (`Id`, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail`, `LinkedAt`) con índice único sobre `(Provider, ProviderKey)`. La vinculación DEBE resolverse en cascada, en este orden: (1) por `(Provider, ProviderKey)`; (2) por correo verificado del proveedor contra `AppUser.Email`, creando entonces la fila; (3) alta de un `AppUser` nuevo con `UserRole.CommunityUser` y `ModeratorPermission.None`. El sistema NO DEBE auto-conceder `FoundingTeam` y NO DEBE fusionar cuentas por correo no verificado.

#### Scenario: Par reincidente resuelve a la misma cuenta

- GIVEN una fila `ExternalLogin` existente para `(Provider, ProviderKey)`
- WHEN el mismo usuario vuelve a iniciar sesión con ese proveedor
- THEN resuelve a la misma cuenta, sin crear filas ni usuarios adicionales.

#### Scenario: Correo verificado coincide con una cuenta existente

- GIVEN un acceso cuyo correo viene verificado y coincide con `AppUser.Email`
- WHEN no existe fila para `(Provider, ProviderKey)`
- THEN se crea la fila `ExternalLogin` vinculada a esa cuenta existente
- AND la cuenta conserva sus roles y permisos.

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

### Requirement: Vinculación manual de proveedores (decisión pendiente de diseño)

**Estado: PENDIENTE DE DISEÑO (INC-46 §2.2).** Este cambio debe decidir si incluye una pantalla de vinculación manual de proveedores o la aplaza. La vía automática por `(Provider, ProviderKey)` y correo verificado rige siempre; la spec fija el comportamiento observable de ambas ramas.

#### Scenario: Rama incluida — vincular un proveedor adicional

- GIVEN la decisión de incluir la pantalla y una sesión activa
- WHEN el usuario reautentica un proveedor adicional desde su cuenta
- THEN se crea una fila `ExternalLogin` para el mismo `UserId` y no un usuario nuevo.

#### Scenario: Rama aplazada — proveedor distinto crea cuenta separada

- GIVEN la decisión de aplazar la pantalla
- WHEN un usuario inicia sesión con un segundo proveedor cuyo correo no coincide
- THEN obtiene una cuenta separada, sin fusión automática.
