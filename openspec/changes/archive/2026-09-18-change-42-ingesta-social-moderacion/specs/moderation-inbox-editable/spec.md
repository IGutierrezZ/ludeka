# Especificación: moderation-inbox-editable

Bandeja de moderación interactiva y 100% editable antes de la publicación definitiva en el catálogo y radar.

---

## 1. Requerimientos Funcionales

### R2.1: Visualización y Filtros de la Bandeja de Moderación
- La bandeja se ubica en `/admin/ingesta-social` con acceso restringido a miembros con rol `FoundingTeam` o moderadores con permisos de aprobación.
- Muestra el listado de envíos agrupados por defecto en estado pendiente (`PendingReview`), con opción de consultar `Approved` y `Rejected`.
- Filtros por pestañas horizontales: `Todos`, `🎁 Sorteos`, `📰 Novedades`, `🎪 Eventos`, `🎬 Vídeos / Multimedia`.
- Cada tarjeta o fila muestra:
  - Imagen/miniatura procesada en WebP.
  - Título y organizador/canal.
  - Enlace externo clicable a la publicación original.
  - Píldora del tipo de contenido asignado.
  - Juego vinculado (con badge distintivo si coincide con un juego del catálogo).
  - Fecha clave (límite, evento o lanzamiento) y días restantes.
  - Texto original extraído (con opción de desplegar).

### R2.2: Edición Completa en la Bandeja antes de Aprobar
- El moderador debe poder pulsar "Editar" sobre cualquier ítem pendiente para rectificar los datos extraídos por la IA:
  - Modificar el título y organizador.
  - Ajustar o cambiar las fechas (fecha límite, inicio o fin de evento, fecha de salida de novedad).
  - Cambiar el juego vinculado utilizando un buscador reactivo integrado sobre el catálogo de Ludeka (`ICatalogService` / `IGameRepository`).
  - Cambiar la tipología de destino (ej. reclasificar un ítem de Novedad a Sorteo, o de Novedad a Evento).
  - Actualizar la URL de miniatura o subir una nueva imagen si la capturada no es la idónea.
  - Modificar badges multimedia (`PlayerCountBadge`, categoría de vídeo) o precios estimados (`EstimatedPvp`).
- Al guardar los cambios, el ítem se actualiza en la base de datos manteniendo su estado `PendingReview`.

### R2.3: Aprobación y Publicación Atómica
- Al pulsar "Aprobar y Publicar", el sistema:
  1. Si es `Giveaway`: crea la entidad de dominio `Giveaway`, la persiste en la tabla de sorteos y se hace visible de inmediato en `/sorteos`.
  2. Si es `WeeklyRelease`: crea la entidad `WeeklyRelease`, la persiste y se hace visible en `/novedades`.
  3. Si es `BoardGameEvent`: crea la entidad `BoardGameEvent`, la persiste y se hace visible en `/eventos`.
  4. Si es `MediaItem`: crea la entidad `MediaItem` vinculada al juego correspondiente con estado `Approved` (o `PendingApproval`), apareciendo en la ficha del juego y en `/multimedia`.
  5. Marca el ítem de la bandeja como `Approved`, registra la fecha de revisión, el usuario revisor y guarda el `CreatedEntityId`.

### R2.4: Descarte de Envíos
- Al pulsar "Descartar", el moderador puede indicar un motivo opcional.
- El ítem pasa al estado `Rejected` con auditoría de fecha y usuario, sin crear registros en las entidades públicas.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Moderador edita la fecha límite de un sorteo antes de publicarlo
  Dado un ítem en la bandeja con fecha límite "2026-10-15" calculada erróneamente por la IA
  Cuando el moderador edita el ítem y corrige la fecha límite a "2026-10-20"
  Entonces los datos se guardan en la bandeja
  Y la tarjeta refleja la fecha actualizada "20 de Octubre de 2026"
  Y el estado sigue siendo "PendingReview"

Escenario: Aprobación de un evento lúdico
  Dado un ítem en la bandeja de tipo "BoardGameEvent" para las jornadas "LudoCon Valencia"
  Cuando el moderador pulsa "Aprobar y Publicar"
  Entonces se inserta un nuevo registro en la tabla de eventos con título, fechas, cartel y recinto
  Y el evento es consultable en "/eventos"
  Y el ítem en la bandeja pasa a "Approved" vinculando el ID del evento creado

Escenario: Descarte de una publicación irrelevante
  Dado un ítem capturado que resulta ser un meme o contenido ajeno a los juegos de mesa
  Cuando el moderador pulsa "Descartar"
  Entonces el ítem pasa a estado "Rejected"
  Y no se crea ningún registro en las tablas de sorteos, novedades, eventos ni medios
```
