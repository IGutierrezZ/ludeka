# Delta for nightly-batch-continuous-ingest

> Cambio `change-47-workers-cloud-run` (INC-47), fase `sdd-spec`, 2026-09-18. Añade el disparo de vida corta e idempotencia por ventana temporal a la orquestación ya vigente del lote nocturno de catalogación. El contexto transversal completo del cambio (resumen ejecutivo, convenciones del documento, tabla de trazabilidad de 19 filas, fuera de alcance, huecos de evidencia y recuento final) vive en [`../background-jobs-scheduling/spec.md`](../background-jobs-scheduling/spec.md), fichero dominante de este cambio para ese contenido compartido.

> **Nota de clasificación:** la propuesta (§5) etiqueta esta capacidad como "modificada" porque su comportamiento de disparo cambia. Sin embargo, ninguno de los requisitos ya existentes en la especificación viva (`RF-05` fase 1.5 de descubrimiento BGG, `RF-06` botón manual de escaneo, `RF-07` filtros y badges por origen) se reescribe: siguen vigentes sin cambios. Este delta introduce, por tanto, un requisito **nuevo** sobre el disparo y la idempotencia de la ejecución de este mismo trabajo, bajo `## ADDED Requirements`, y no toca el texto existente.

## ADDED Requirements

### Requirement: Disparo de vida corta e idempotencia por ventana del lote nocturno

La ejecución del lote nocturno de catalogación (la misma orquestación que ejecuta la fase 1.5 de descubrimiento BGG del requisito `RF-05` ya vigente) DEBE determinar si ya se completó su ventana temporal consultando exclusivamente la bitácora persistida, respaldada por la restricción única por clave de ventana definida en `background-jobs-scheduling`, en lugar de cualquier campo en memoria del proceso.

*Verificación: prueba unitaria (SQLite). El escenario de reclamación concurrente bajo `UNIQUE` real es el mismo cubierto, a nivel de integración contra PostgreSQL, por el requisito "Ejecución única por ventana temporal..." de `background-jobs-scheduling`; no se repite aquí para evitar duplicar la misma prueba en dos capacidades.*

#### Scenario: Ventana ya completada, una nueva instancia no repite el trabajo

- GIVEN una ventana de ejecución diaria ya marcada como completada en la bitácora persistida
- WHEN una instancia nueva del trabajo, sin estado previo en memoria, evalúa si debe ejecutar esa ventana
- THEN no ejecuta de nuevo la fase de catalogación para esa ventana.

#### Scenario: Ventana pendiente, la instancia ejecuta y persiste el resultado

- GIVEN una ventana de ejecución diaria todavía sin marca de finalización en la bitácora persistida
- WHEN el trabajo se dispara para esa ventana
- THEN el trabajo reserva la ventana en base de datos, ejecuta su unidad de trabajo
- AND persiste el resultado, incluidas las métricas mínimas, en la bitácora al finalizar.
