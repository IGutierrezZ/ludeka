# Especificación del Cambio: Moderación Social a Carteles Compactos y Reintento IA

## Requerimientos Funcionales

### RF-01: Deduplicación en Ingesta Social
- DADO que una URL ya existe en `SocialInboxItems` (en cualquier estado: `PendingReview`, `Approved` o `Rejected`)
- CUANDO se intenta registrar dicha URL vía `IngestFromUrlAsync`, `IngestMultimodalAsync` o `IngestManualAdvancedAsync`
- ENTONCES el sistema lanza una `InvalidOperationException` descriptiva impidiendo la creación de un registro duplicado.

### RF-02: Detección y Reintento de Análisis con IA
- DADO un ítem en la bandeja de moderación cuyo análisis fue realizado por heurística local (o falló Gemini)
- CUANDO el moderador abre el modal de edición
- ENTONCES el sistema muestra una advertencia visual indicando que la extracción fue heurística y proporciona un botón interactivo para «Reintentar con IA».
- CUANDO el moderador pulsa el botón de reintento y Gemini responde satisfactoriamente
- ENTONCES se actualizan los campos del formulario con los metadatos enriquecidos de la IA y se marca la publicación como procesada por IA.

### RF-03: Carteles Compactos en la Bandeja
- DADO el acceso a `/admin/ingesta-social`
- CUANDO se listan las publicaciones
- ENTONCES se muestran en cuadrícula compacta tipo cartel (2 columnas en móviles, 3-6 en escritorio), mostrando miniatura, tipología, organizador, título, fechas y badge de advertencia si no fue analizado por IA.
- Y al pulsar sobre el cartel se abre de inmediato el modal de edición.

### RF-04: Texto Original y Enlace de Origen en Edición
- DADO el modal de edición `SocialInboxEditModal`
- CUANDO está abierto
- ENTONCES el moderador visualiza en un contenedor dedicado el texto original extraído (`OriginalCaption`) para cotejo de bases y requisitos.
- Y dispone de un botón visible para abrir la URL de origen (`SourceUrl`) en una nueva pestaña del navegador.
- Y dispone de un botón para «Aprobar y Publicar» directamente desde el modal.

### RF-05: Purga de Simulación y Configuración de Producción
- DADO el archivo `appsettings.json`
- ENTONCES `SocialCollector:Simulate` está establecido en `false`.
- Y se provee `appsettings.Production.json` con todos los subsistemas de simulación en `false`.
- Y la bandeja de moderación ofrece una acción administrativa para purgar publicaciones de prueba simuladas (`sim_*`).
