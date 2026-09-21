# Especificación: production-auth-bootstrap (Configuración Mínima de Autenticación e Identidad de Fundador para un Despliegue de Production Válido)

## Propósito

Define qué configuración de identidad del administrador fundador y de proveedores de autenticación DEBE estar explícitamente presente para que un arranque en `Production` sea válido, y qué hace el sistema cuando esa configuración falta. Es hermana de `production-persistence-guard` (misma familia de guardas de arranque exclusivas de `Production`, en el mismo host, evaluadas de forma independiente entre sí) y complementa a `social-login-authentication`, que gobierna cómo se resuelve una sesión una vez que al menos un proveedor está operativo, incluida la cascada de vinculación que hoy ya constituye el único camino de acceso del administrador fundador (escenario "Correo verificado coincide con una cuenta sin identidades externas previas" de `social-login-authentication`). Esta capacidad no repite ni modifica esa cascada: solo garantiza que su precondición —un correo de fundador informado explícitamente, y al menos un proveedor operativo con el que iniciar sesión— no pueda quedar fijada por accidente o ausente en el primer despliegue.

## Requirements

### Requirement: Correo del administrador fundador explícito y obligatorio en Production

Cuando el entorno de ejecución es `Production`, el sistema DEBE exigir un valor explícito y no vacío para la configuración `AdminUser:Email` antes de completar el arranque. Si, tras resolver la configuración efectiva de ese valor, resulta vacío o en blanco, el proceso DEBE finalizar antes de crear o promover ninguna fila de administrador fundador, con un mensaje que identifique explícitamente `AdminUser:Email` como la configuración ausente. En entornos distintos de `Production`, esta comprobación NO DEBE impedir el arranque, y el sembrado con un valor de reserva DEBE seguir funcionando exactamente igual que antes de este incremento.

*Verificación: prueba de función pura con el entorno y la configuración inyectados, mismo patrón que el requisito equivalente de `production-persistence-guard`.*

#### Scenario: Production con correo de fundador explícito arranca con normalidad

- GIVEN el entorno `Production` y una configuración que resuelve `AdminUser:Email` a un valor explícito no vacío
- WHEN la aplicación arranca
- THEN el proceso continúa y procede a sembrar o promover la cuenta del administrador fundador con ese correo

#### Scenario: Production sin correo de fundador explícito aborta (caso negativo, defecto B3 sin corregir)

- GIVEN el entorno `Production` sin ninguna variable de entorno ni secreto que fije `AdminUser:Email`, es decir, dependiendo únicamente de la configuración empaquetada por defecto
- WHEN la aplicación arranca
- THEN el proceso finaliza antes de crear o promover ninguna fila de administrador fundador
- AND el mensaje de fallo nombra explícitamente `AdminUser:Email` como la configuración ausente

#### Scenario: Entornos distintos de Production no activan la comprobación

- GIVEN un entorno distinto de `Production` (`Development` o `Staging`) sin `AdminUser:Email` configurado explícitamente
- WHEN la aplicación arranca
- THEN el proceso continúa y siembra el administrador fundador con el valor de reserva existente, sin activar la comprobación anterior

#### Scenario: El despliegue Docker Compose de Production no reintroduce un valor por defecto

- GIVEN el fichero de despliegue Docker Compose usado para `Production` (`docker-compose.prod.yml`)
- WHEN se resuelve el valor de `AdminUser__Email` en ausencia de una variable de entorno del host exportada explícitamente
- THEN ese fichero no sustituye la ausencia por un correo por defecto embebido en el propio fichero
- AND la ausencia de la variable del host se traduce en un valor vacío hacia el proceso, activando el Scenario anterior

#### Scenario: El pipeline de CI/CD suministra el correo del fundador al desplegar a Production

- GIVEN la configuración de despliegue de `Production` para el servicio web en el pipeline de CI/CD
- WHEN se inspecciona su contenido
- THEN suministra un valor para `AdminUser__Email`, de modo que el arranque en `Production` recibe un valor explícito sin depender de ninguna intervención manual fuera del pipeline declarado

### Requirement: Al menos un proveedor de autenticación operativo en el despliegue de Production

La configuración de despliegue de `Production` para el servicio web en el pipeline de CI/CD DEBE declarar, para al menos un proveedor de autenticación externo, tanto su marca de habilitado como su identificador y secreto de cliente resueltos, de modo que el primer arranque en `Production` no quede con cero proveedores utilizables. Todo secreto o clave de cliente de un proveedor DEBE proceder del almacén de secretos del proveedor de despliegue y NO DEBE aparecer en texto plano en el bloque de variables de entorno de esa misma configuración.

*Verificación: prueba de contrato que lee `.github/workflows/ci-cd.yml` como texto — patrón nuevo en el repositorio, ya que hoy ninguna prueba lee ficheros de `.github/workflows/` — análoga en técnica a `AuthorizationPipelineContractTests`, pero sobre YAML en vez de sobre `Program.cs`. Qué proveedor concreto entra en el primer despliegue es una decisión del maintainer pendiente (`proposal.md` §10 pregunta 1); este requisito se cumple con cualquiera de los tres.*

#### Scenario: El despliegue declara al menos un proveedor utilizable

- GIVEN la configuración de despliegue de `Production` para el servicio web
- WHEN se inspecciona su contenido
- THEN al menos uno de los tres proveedores de autenticación (Google, Discord o Facebook) aparece marcado como habilitado, con su identificador y secreto de cliente resueltos desde el almacén de secretos del proveedor de despliegue

#### Scenario: El secreto de un proveedor habilitado no viaja en texto plano

- GIVEN un proveedor de autenticación declarado como habilitado en la configuración de despliegue de `Production`
- WHEN se inspecciona el bloque de variables de entorno en texto plano de esa configuración
- THEN el secreto o clave de cliente de ese proveedor no aparece en ese bloque; solo aparece referenciado desde el bloque de secretos
