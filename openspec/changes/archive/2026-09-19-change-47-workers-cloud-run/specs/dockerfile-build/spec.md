# Delta for dockerfile-build

> Cambio `change-47-workers-cloud-run` (INC-47), fase `sdd-spec`, 2026-09-18. Permite que el mismo artefacto de contenedor arranque en modo servicio web o en modo trabajo de fondo con nombre, seleccionado en tiempo de arranque del contenedor. El contexto transversal completo del cambio (resumen ejecutivo, convenciones del documento, tabla de trazabilidad de 19 filas, fuera de alcance, huecos de evidencia y recuento final) vive en [`../background-jobs-scheduling/spec.md`](../background-jobs-scheduling/spec.md), fichero dominante de este cambio para ese contenido compartido. Decisión de diseño abierta directamente relevante (propuesta §8, decisión 1, no presupuesta en este documento): empaquetado del host de trabajos.

> **Nota de convención:** igual que en `health-checks`, la especificación viva actual predata la convención canónica y agrupa cuatro escenarios sin requisito nombrado. Este delta modifica únicamente el escenario de arranque/ejecución (hoy "Escenario 3: Ejecución segura bajo usuario no-root", verificado en `Dockerfile:81` — `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]`, fijo, sin lógica de argumentos — y `Dockerfile:78-79`, la sonda `HEALTHCHECK` contra `/healthz`). Los escenarios de compilación de Tailwind y de `dotnet publish` no cambian con este incremento y no se repiten aquí.

## MODIFIED Requirements

### Requirement: Arranque del contenedor en modo servicio web o modo trabajo de fondo

El artefacto de contenedor producido por la compilación DEBE poder arrancar tanto un proceso de servicio web persistente como un proceso de ejecución de un único trabajo de fondo con nombre, seleccionado en tiempo de arranque del contenedor —no en tiempo de compilación de la imagen—, ejecutando en ambos casos bajo un usuario sin privilegios. La forma concreta de selección (modo del mismo host frente a artefacto de trabajos independiente) es una decisión de `sdd-design` (propuesta §8, decisión 1) y este requisito permanece válido con cualquiera de las dos.
(Previously: el `ENTRYPOINT` era fijo — `["dotnet", "Ludeka.Web.dll"]`, sin ninguna lógica de argumentos — y el contenedor solo podía arrancar el servicio web.)

*Verificación: manual/mixta. El arranque del servicio web bajo usuario no-root ya es hoy una característica de la imagen y no se reprueba aquí. La selección de modo en tiempo de arranque del contenedor (Docker/`ENTRYPOINT`) no es ejercitable con `dotnet test` y queda como verificación manual del maintainer mediante construcción y arranque real del contenedor; el despacho de modo a nivel de composición .NET (qué servicio se invoca según el argumento o variable recibida) sí es unitariamente comprobable y queda cubierto, para el trabajo concreto, por los requisitos de `background-jobs-scheduling`.*

#### Scenario: Ejecución segura bajo usuario no-root en modo servicio web (sin cambios)

- GIVEN el contenedor en ejecución a partir de la imagen base de runtime
- WHEN se arranca en modo servicio web
- THEN el proceso se ejecuta bajo un usuario sin privilegios de administrador
- AND escucha en el puerto interno configurado (`8080` por defecto, vía `ASPNETCORE_HTTP_PORTS`).

#### Scenario: Arranque en modo trabajo de fondo con nombre (nuevo)

- GIVEN el mismo artefacto de contenedor producido por la compilación
- WHEN se arranca indicando en tiempo de ejecución el nombre de un trabajo de fondo concreto (por ejemplo, el lote nocturno o el despachador de notificaciones)
- THEN el contenedor ejecuta únicamente la unidad de trabajo de ese trabajo, bajo un usuario sin privilegios, y termina el proceso al finalizar
- AND no arranca el servicio web ni ningún trabajo distinto del indicado.

#### Scenario: La sonda `HEALTHCHECK` no aplica a una ejecución en modo trabajo (nuevo)

- GIVEN un contenedor arrancado en modo trabajo de fondo
- WHEN el trabajo completa su unidad de trabajo y termina
- THEN no se evalúa ninguna sonda `HEALTHCHECK` periódica para esa ejecución
- AND la señal de éxito o fallo de la ejecución es exclusivamente el código de salida del proceso, no una sonda HTTP.
