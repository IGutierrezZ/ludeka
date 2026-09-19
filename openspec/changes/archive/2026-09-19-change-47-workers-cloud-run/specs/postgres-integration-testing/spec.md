# Especificación: `postgres-integration-testing`

> Cambio `change-47-workers-cloud-run` (INC-47), fase `sdd-spec`, 2026-09-18. Habilitador (rebanada R1) de pruebas de integración contra PostgreSQL real, del que dependen `notification-outbox` y la prueba de idempotencia bajo concurrencia de `background-jobs-scheduling`. El contexto transversal completo del cambio (resumen ejecutivo, convenciones del documento, tabla de trazabilidad de 19 filas, fuera de alcance, huecos de evidencia y recuento final) vive en [`../background-jobs-scheduling/spec.md`](../background-jobs-scheduling/spec.md), fichero dominante de este cambio para ese contenido compartido. Decisiones de diseño abiertas (propuesta §8, no presupuestas en este documento): empaquetado del host de trabajos y modelo de fila del outbox.

## Propósito

Habilitador del que dependen `notification-outbox` y la prueba de idempotencia bajo concurrencia de `background-jobs-scheduling`: dota a la suite de una vía para ejercitar primitivas específicas de PostgreSQL —en particular `SELECT ... FOR UPDATE SKIP LOCKED`— contra una base de datos real, dentro del runner contractual `dotnet test Ludeka.sln`. Debe implementarse **antes** de cualquier prueba que dependa de ella (propuesta §2.1, punto 1; §9, rebanada R1).

## Requirements

### Requirement: Ejecución de pruebas de integración contra PostgreSQL real dentro del runner contractual

La suite de pruebas DEBE poder ejercitar primitivas específicas de PostgreSQL (como mínimo, `SELECT ... FOR UPDATE SKIP LOCKED`) contra una instancia real de PostgreSQL, y esto DEBE ejecutarse como parte del único comando contractual `dotnet test Ludeka.sln`.

*Verificación: integración / CI — se demuestra en el propio runner de integración continua (`ubuntu-latest`, con demonio Docker ya evidenciado en `ci-cd.yml:47` dentro del mismo job que `dotnet test`), y de forma equivalente en cualquier máquina de desarrollo con Docker disponible.*

#### Scenario: La suite levanta PostgreSQL real y ejercita `FOR UPDATE SKIP LOCKED`

- GIVEN el comando `dotnet test Ludeka.sln` ejecutado en un entorno con motor de contenedores disponible
- WHEN se ejecuta la prueba de integración de concurrencia sobre una tabla real
- THEN la prueba levanta una base de datos PostgreSQL real (por ejemplo, mediante Testcontainers)
- AND ejecuta `SELECT ... FOR UPDATE SKIP LOCKED` contra ella
- AND reporta el resultado como parte de la misma ejecución de `dotnet test`.

### Requirement: Fallo ruidoso cuando no es posible ejecutar las pruebas de integración

Si el entorno que ejecuta `dotnet test Ludeka.sln` carece de la capacidad necesaria para ejecutar las pruebas de integración contra PostgreSQL (por ejemplo, sin demonio Docker disponible), la ejecución DEBE fallar de forma explícita y ruidosa. NO DEBE omitir en silencio las pruebas que demuestran la exclusión de reclamación concurrente y reportar la suite como en verde. La política concreta de implementación (categoría de prueba diferenciada, verificación previa explícita, u otro mecanismo) es una decisión de `sdd-design`; este requisito fija únicamente el comportamiento observable.

*Verificación: Escenario 1, prueba unitaria de la lógica de guarda/detección de capacidad en sí misma (aislable sin depender de que la máquina de pruebas realmente carezca de Docker). Escenario 2, integración/CI, en el camino en que la capacidad sí está presente.*

#### Scenario: Sin capacidad para las pruebas de integración, la ejecución falla explícitamente

- GIVEN una máquina o entorno de ejecución sin demonio Docker operativo (o sin la capacidad equivalente que exijan las pruebas de integración)
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN la ejecución señala explícitamente el fallo, por ejemplo con una prueba en rojo o un mensaje de error explícito
- AND en ningún caso el resultado global se reporta como suite en verde omitiendo en silencio las pruebas de concurrencia.

#### Scenario: Con la capacidad presente, las pruebas de integración se ejecutan y reportan de verdad

- GIVEN una máquina o runner de CI con demonio Docker operativo
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN las pruebas de integración contra PostgreSQL real se ejecutan
- AND se reportan con su resultado real, éxito o fallo, sin omisión.
