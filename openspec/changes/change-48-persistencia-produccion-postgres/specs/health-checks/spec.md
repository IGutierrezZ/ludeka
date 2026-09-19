# Delta for health-checks

> Cambio `change-48-persistencia-produccion-postgres` (INC-48), fase `sdd-spec`, 2026-09-19. Corrige que `/ready` mienta sobre el proveedor de base de datos y sobre lo que realmente verifica el componente de almacenamiento; añade la exposición de `/healthz`/`/ready` como sondas de despliegue en Cloud Run.

> **Nota de convención:** la especificación viva actual (`openspec/specs/health-checks/spec.md`) predata la convención canónica `### Requirement:` / `#### Scenario:` y agrupa 3 escenarios sueltos («Escenario 1/2/3»). Este delta reformatea, bajo un único requisito nombrado, la porción de `/ready` que cambia (los componentes `database` y `storage`), preservando sin alterar el resto de la estructura de los dos escenarios existentes de `/ready` (dependencias sanas / degradación). El escenario de liveness `/healthz` (Escenario 1) no cambia con este incremento y no se repite aquí.

## MODIFIED Requirements

### Requirement: Diagnóstico de disponibilidad del endpoint `/ready`

El endpoint de disponibilidad (`/ready`) DEBE informar, para cada uno de sus tres componentes, el estado real del subsistema correspondiente. El componente de base de datos DEBE reportar el proveedor efectivamente en uso (`Npgsql` o `Sqlite`), nunca un valor fijo. El componente de almacenamiento DEBE verificar el almacén de medios realmente configurado (credenciales de R2 válidas, o escritura real contra la ruta local configurada), en lugar de inferir un directorio a partir de la cadena de conexión de la base de datos.
(Previously: el componente de base de datos fijaba el metadato `"provider": "Microsoft.EntityFrameworkCore.Sqlite"` y mensajes en español codificados a SQLite con independencia del proveedor real — verificado en `SqliteDatabaseHealthCheck.cs:23,34,37,41` —, y el componente de almacenamiento extraía un directorio con una expresión regular sobre `Data Source=` de la cadena de conexión de la base de datos, sin relación con el almacenamiento de medios — verificado en `StorageHealthCheck.cs:20-22`.)

*Verificación: prueba unitaria que fuerza cada proveedor de base de datos y cada backend de medios (R2 simulado con credenciales válidas/ausentes, disco local, memoria) y comprueba el metadato reportado, sin PostgreSQL real.*

#### Scenario: Endpoint de disponibilidad con dependencias saludables (sin cambios)

- GIVEN que la base de datos responde correctamente a `CanConnectAsync()`, el almacenamiento de medios configurado está operativo y el mecanismo de notificaciones está disponible
- WHEN se consulta `GET /ready`
- THEN el endpoint responde con código HTTP `200 OK`
- AND el desglose JSON incluye el estado de cada componente (`database`, `storage`, `notification_queue`)

#### Scenario: Endpoint de disponibilidad ante degradación o fallo (sin cambios)

- GIVEN que la base de datos no puede conectarse o el almacenamiento de medios configurado es inaccesible
- WHEN se consulta `GET /ready`
- THEN el endpoint responde con código HTTP `503 Service Unavailable`
- AND detalla en el JSON el componente con fallo

#### Scenario: El componente `database` reporta el proveedor real (modificado)

- GIVEN la aplicación configurada con PostgreSQL como proveedor efectivo
- WHEN se consulta `GET /ready`
- THEN el metadato `provider` del componente `database` reporta `Npgsql`, no `Sqlite`

#### Scenario: El componente `storage` verifica el almacén de medios configurado, no el directorio de datos (modificado)

- GIVEN Cloudflare R2 configurado con credenciales válidas como almacén de medios activo
- WHEN se consulta `GET /ready`
- THEN el componente `storage` reporta el estado de disponibilidad de R2
- AND no reporta el directorio de la base de datos local como si fuera el almacenamiento de medios

## ADDED Requirements

### Requirement: Exposición de sondas de despliegue en Cloud Run

El servicio desplegado en Cloud Run DEBE exponer `/healthz` como sonda de *liveness* y `/ready` como sonda de *readiness* ante el orquestador de contenedores, de modo que un despliegue con dependencias no disponibles no reciba tráfico.

*Verificación: manual/despliegue. No existe entorno de producción ni de staging con Cloud Run real para una prueba automática; y no está verificado si `google-github-actions/deploy-cloudrun@v2` admite configurar estas sondas de forma nativa (riesgo abierto declarado en la propuesta, a resolver antes de `sdd-apply`; si la capacidad no existe, la alternativa es provisión manual documentada).*

#### Scenario: El despliegue configura las sondas de liveness y readiness (verificación manual)

- GIVEN un despliegue a Cloud Run mediante el workflow de CI/CD
- WHEN el servicio se aprovisiona o actualiza
- THEN `/healthz` queda configurado como sonda de liveness y `/ready` como sonda de readiness, mediante el mecanismo que ofrezca la herramienta de despliegue (nativo o provisión manual documentada)
