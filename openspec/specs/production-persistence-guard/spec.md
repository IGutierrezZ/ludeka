# Especificación: production-persistence-guard (Guarda de Arranque contra Persistencia Efímera en Producción)

## Propósito

Impide que `Ludeka.Web` arranque en `Production` con una conexión que no resuelva a PostgreSQL, eliminando el fallback silencioso a una base SQLite efímera; y evita que la imagen de contenedor imponga por sí sola una cadena de conexión SQLite por defecto. Espeja el patrón ya probado de la Guarda 1 de `src/Ludeka.Jobs/StartupGuards.cs` (coherencia de proveedor); la Guarda 2 de ese fichero (migraciones pendientes) no se porta, porque el host web sí migra.

## Requirements

### Requirement: El arranque falla en Production sin PostgreSQL resoluble

Cuando `ASPNETCORE_ENVIRONMENT=Production`, el sistema DEBE evaluar si la cadena de conexión efectiva resuelve a un proveedor PostgreSQL antes de inicializar el esquema. Si no resuelve a PostgreSQL, el proceso DEBE finalizar con un mensaje explícito que identifique la configuración de conexión ausente o incoherente, y NO DEBE crear ni migrar ninguna base de datos SQLite efímera. En entornos distintos de `Production`, esta comprobación NO DEBE impedir el arranque.

*Verificación: prueba unitaria/de integración ligera que invoca la guarda con distintas combinaciones de nombre de entorno y cadena de conexión resuelta, sin necesitar una PostgreSQL real.*

#### Scenario: Production con PostgreSQL resoluble arranca con normalidad

- GIVEN `ASPNETCORE_ENVIRONMENT=Production` y una cadena de conexión que resuelve a PostgreSQL
- WHEN la aplicación arranca
- THEN el proceso continúa y aplica las migraciones de Entity Framework Core sobre PostgreSQL

#### Scenario: Production sin PostgreSQL resoluble falla explícitamente

- GIVEN `ASPNETCORE_ENVIRONMENT=Production` y una cadena de conexión que no resuelve a PostgreSQL (ausente o SQLite)
- WHEN la aplicación arranca
- THEN el proceso finaliza antes de crear ninguna base de datos
- AND el mensaje de fallo identifica explícitamente que falta una conexión PostgreSQL resoluble

#### Scenario: Entornos distintos de Production no activan la guarda

- GIVEN un entorno distinto de `Production` (por ejemplo `Development` o `Staging`) y una cadena de conexión SQLite
- WHEN la aplicación arranca
- THEN el proceso continúa sin activar la guarda de coherencia de proveedor
- AND se preserva el comportamiento existente de creación de esquema SQLite local

### Requirement: La imagen de contenedor no impone una cadena de conexión por defecto

La imagen de contenedor producida por la compilación NO DEBE fijar, como valor por defecto embebido en la imagen, una cadena de conexión SQLite ni ninguna otra cadena de conexión de base de datos. La configuración de conexión DEBE proceder exclusivamente de variables de entorno o secretos inyectados en tiempo de ejecución del contenedor.

*Verificación: combina la inspección del `Dockerfile` (ausencia de `ConnectionStrings__DefaultConnection` en el bloque `ENV`, ver `dockerfile-build`) con el escenario de arranque anterior.*

#### Scenario: Sin variable de entorno inyectada, Production falla en vez de usar un valor implícito

- GIVEN un contenedor construido a partir de la imagen final, sin ninguna variable `ConnectionStrings__DefaultConnection` inyectada
- WHEN se arranca con `ASPNETCORE_ENVIRONMENT=Production`
- THEN el proceso falla explícitamente, en lugar de usar un valor por defecto SQLite embebido en la imagen
