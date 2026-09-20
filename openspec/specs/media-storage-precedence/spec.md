# Especificación: media-storage-precedence (Selección de Almacenamiento de Medios con Precedencia)

## Propósito

Selecciona el backend de `IImageStorageService` por precedencia estricta de tres vías (Cloudflare R2 → disco local configurable → memoria), garantiza que el fallback en disco se sirva realmente por HTTP mediante un middleware propio, porque `MapStaticAssets()` solo sirve el manifiesto de activos generado en compilación y no ficheros escritos en tiempo de ejecución, y hace visible, sin bloquear el arranque, la degradación cuando `Production` carece de R2.

## Requirements

### Requirement: Selección de almacén de medios por precedencia de tres vías

El sistema DEBE seleccionar `IImageStorageService` evaluando, en este orden estricto: (1) si las credenciales de Cloudflare R2 son válidas, el almacenamiento R2; (2) si no lo son pero hay una ruta local de medios configurada (`Media__LocalStoragePath`), el almacenamiento en disco físico apuntando a esa ruta; (3) si ninguna de las dos se cumple, el almacenamiento en memoria.

#### Scenario: Credenciales R2 válidas seleccionan R2

- GIVEN credenciales de Cloudflare R2 configuradas y válidas
- WHEN el contenedor de dependencias resuelve `IImageStorageService`
- THEN se selecciona el servicio de almacenamiento Cloudflare R2

#### Scenario: Sin R2 pero con ruta local configurada seleccionan disco

- GIVEN credenciales de R2 ausentes o inválidas, y una ruta local de almacenamiento de medios configurada
- WHEN el contenedor de dependencias resuelve `IImageStorageService`
- THEN se selecciona el servicio de almacenamiento en disco físico apuntando a la ruta configurada
- AND una imagen guardada persiste en esa ruta tras recrear el proceso

#### Scenario: Sin R2 y sin ruta local seleccionan memoria

- GIVEN credenciales de R2 ausentes o inválidas, y ninguna ruta local configurada
- WHEN el contenedor de dependencias resuelve `IImageStorageService`
- THEN se selecciona el servicio de almacenamiento en memoria

### Requirement: Entrega HTTP del fallback en disco

Toda imagen escrita por el almacenamiento en disco físico DEBE poder recuperarse mediante una petición HTTP real a su URL pública, con independencia de si la ruta configurada coincide con `wwwroot`. El requisito no es que el fichero exista en disco: es que responda `200 OK` por HTTP. El mecanismo de activos estáticos basado en manifiesto de compilación (`MapStaticAssets`) no basta por sí solo, porque no sirve ficheros escritos en tiempo de ejecución fuera de ese manifiesto.

*Verificación: prueba de integración (`WebApplicationFactory` o equivalente) que escribe un fichero mediante el servicio en disco y realiza una petición HTTP real a la URL devuelta.*

#### Scenario: Una imagen escrita por el fallback local se descarga por HTTP

- GIVEN una imagen guardada mediante el servicio de almacenamiento en disco físico, con una ruta local configurada
- WHEN un cliente HTTP solicita la URL pública devuelta al guardarla
- THEN la respuesta es `200 OK` con el contenido binario de la imagen
- AND no se produce una respuesta `404 Not Found`

#### Scenario: Una imagen nunca guardada sigue devolviendo 404

- GIVEN el servicio de almacenamiento en disco físico sirviendo por HTTP
- WHEN un cliente solicita una URL de imagen que nunca fue guardada
- THEN la respuesta es `404 Not Found`

### Requirement: Aviso de degradación en Production sin R2

Cuando `ASPNETCORE_ENVIRONMENT=Production` y las credenciales de R2 no son válidas, el sistema DEBE emitir al arrancar un aviso explícito de nivel advertencia que identifique el almacenamiento de medios activo como degradado, sin impedir el arranque.

#### Scenario: Production sin R2 emite aviso explícito

- GIVEN `ASPNETCORE_ENVIRONMENT=Production` y credenciales de R2 ausentes o inválidas
- WHEN la aplicación arranca
- THEN se registra una entrada de log de nivel advertencia que identifica el almacenamiento de medios activo como degradado
- AND el arranque continúa sin abortar

#### Scenario: Production con R2 válido no emite aviso

- GIVEN `ASPNETCORE_ENVIRONMENT=Production` y credenciales de R2 válidas
- WHEN la aplicación arranca
- THEN no se registra ningún aviso de degradación del almacenamiento de medios
