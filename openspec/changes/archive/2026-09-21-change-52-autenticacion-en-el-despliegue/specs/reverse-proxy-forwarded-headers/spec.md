# Especificación: reverse-proxy-forwarded-headers (Reconstrucción del Esquema Original Detrás de un Proxy Inverso)

## Propósito

Define cómo `Ludeka.Web` reconstruye el esquema (`http`/`https`) de la petición original cuando el proceso se ejecuta detrás de un proxy inverso que termina TLS por delante de él —Cloud Run en producción, y Nginx en el despliegue VPS documentado en `deploy/nginx/default.conf`—, qué cabecera de reenvío procesa para eso, cuál excluye deliberadamente, y qué garantiza sobre las URL absolutas que la aplicación construye a partir de la petición actual, en particular el `redirect_uri` que los manejadores OAuth de Google, Discord y Facebook envían a cada proveedor.

Esta capacidad es nueva y complementa a `nginx-reverse-proxy`, que especifica la configuración del lado del proxy (WebSockets, cabeceras de seguridad, compresión) pero no cubre `X-Forwarded-Proto` ni ningún comportamiento del lado del host ASP.NET Core. Ambas capacidades describen los dos extremos del mismo salto de red y no se solapan.

## Requirements

### Requirement: Procesamiento incondicional de `X-Forwarded-Proto` sin restricción por origen del proxy

El sistema DEBE determinar `Request.Scheme` a partir de la cabecera `X-Forwarded-Proto` cuando esté presente en la petición entrante, sin restringir esa confianza a un conjunto de proxies o redes de origen conocidas de antemano. Esta evaluación DEBE aplicarse de forma incondicional, sin depender de `ASPNETCORE_ENVIRONMENT`: todo despliegue documentado del sistema coloca un proxy que termina TLS por delante del proceso, y cuando no hay proxy delante (por ejemplo, en desarrollo local) la cabecera simplemente no llega, por lo que la comprobación no tiene efecto.

*Verificación: requiere un servidor de pruebas en memoria capaz de simular una petición entrante con cabeceras reenviadas y una dirección remota concreta (dependencia de prueba nueva, pendiente de aprobación — ver `proposal.md` §10 pregunta 6). Sin ese servidor, este requisito no es comprobable con una prueba unitaria pura sobre una función aislada.*

#### Scenario: Proxy no loopback corrige el esquema a `https`

- GIVEN una petición entrante con la cabecera `X-Forwarded-Proto: https`, procedente de una dirección de red remota que no es de bucle invertido
- WHEN la petición llega al pipeline de `Ludeka.Web`
- THEN `Request.Scheme` vale `https` para el resto del procesamiento de esa petición

#### Scenario: Sin vaciar las listas de confianza, el esquema no cambia (caso negativo, defecto B1 sin corregir)

- GIVEN el sistema configurado con las listas de proxies y redes de confianza en su estado por defecto, sin vaciar, y una petición con `X-Forwarded-Proto: https` procedente de una dirección remota que no es de bucle invertido
- WHEN la petición llega al pipeline
- THEN `Request.Scheme` permanece en `http`
- AND cualquier URL absoluta construida a partir de esa petición, incluido un eventual `redirect_uri` OAuth, conserva el esquema `http`

#### Scenario: Mismo comportamiento en un entorno distinto de Production

- GIVEN un despliegue detrás de Nginx (`deploy/nginx/default.conf`) con `ASPNETCORE_ENVIRONMENT` distinto de `Production`, y una petición con `X-Forwarded-Proto: https` desde una dirección remota que no es de bucle invertido
- WHEN la petición llega al pipeline
- THEN `Request.Scheme` vale `https`, igual que en `Production`, sin ninguna configuración adicional condicionada al entorno

#### Scenario: Sin proxy delante, el comportamiento local no cambia

- GIVEN una petición local sin ninguna cabecera `X-Forwarded-Proto` (por ejemplo, desarrollo con Kestrel sin proxy delante)
- WHEN la petición llega al pipeline
- THEN `Request.Scheme` conserva el valor que Kestrel determinó para la conexión real, sin alteración

### Requirement: `X-Forwarded-Host` queda excluido de la confianza

El sistema NO DEBE alterar `Request.Host` a partir de la cabecera `X-Forwarded-Host`, incluso cuando confía en `X-Forwarded-Proto` sin restricción de origen. La configuración de hosts permitidos del sistema admite cualquier valor, por lo que procesar esa cabecera permitiría a un origen que alcance el proceso suplantar el host con el que la aplicación genera sus enlaces.

*Verificación: mismo servidor de pruebas en memoria del Requirement anterior; alternativamente, prueba de la función de configuración pura si esta expone de forma comprobable qué cabeceras habilita.*

#### Scenario: `X-Forwarded-Host` no altera el host de la petición

- GIVEN una petición con `X-Forwarded-Proto: https` y `X-Forwarded-Host: dominio-suplantado.ejemplo`
- WHEN la petición llega al pipeline
- THEN `Request.Host` conserva el host real de la conexión entrante, sin adoptar el valor de `X-Forwarded-Host`

### Requirement: El `redirect_uri` de los manejadores OAuth refleja el esquema corregido

Cuando un manejador de autenticación externa (Google, Discord o Facebook) construye el `redirect_uri` que envía en la solicitud de autorización al proveedor, DEBE hacerlo a partir del `Request.Scheme` ya corregido por el primer Requirement de esta especificación, no del esquema interno con el que el proxy reenvía la petición.

*Verificación: mismo servidor de pruebas en memoria del primer Requirement, con un proveedor habilitado y credenciales configuradas.*

#### Scenario: Acceso social detrás del proxy genera un `redirect_uri` en `https`

- GIVEN un proveedor social habilitado y con credenciales configuradas, y una petición de inicio de sesión que llega con `X-Forwarded-Proto: https` desde una dirección remota que no es de bucle invertido
- WHEN el manejador del proveedor construye el `redirect_uri` de la solicitud de autorización
- THEN ese `redirect_uri` usa el esquema `https`, coincidiendo con el registrado en la consola del proveedor

#### Scenario: Desarrollo local sin proxy sigue construyendo el `redirect_uri` en `http`

- GIVEN el mismo proveedor habilitado, en un arranque local sin proxy delante y sin ninguna cabecera `X-Forwarded-Proto` en la petición
- WHEN el manejador del proveedor construye el `redirect_uri`
- THEN ese `redirect_uri` conserva el esquema `http`, coherente con el acceso local real
