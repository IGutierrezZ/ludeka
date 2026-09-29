# Propuesta de Cambio: Rediseño de Moderación Social a Carteles Compactos, Resiliencia y Reintento IA, y Limpieza de Simulación

## 1. Motivación y Problema
En la bandeja de moderación social (`/admin/ingesta-social`), se detectó la presencia masiva de registros duplicados y sin datos reales (*«Gran sorteo exclusivo de Cuarto de Juegos»*, *«Sorteo Lúdico de Comunidad»*).
- **Causa raíz técnica:** `SocialCollector:Simulate` estaba fijado a `true` en `appsettings.json`, provocando que el servicio hospedado desatendido generara publicaciones ficticias de Instagram (`sim_...`) que luego eran procesadas con el analizador heurístico local sin clave IA.
- **Falta de deduplicación:** La ingesta manual y multimodal no validaba si la URL ya existía en la bandeja.
- **Ergonomía de moderación deficiente:** Las tarjetas ocupaban un tamaño desproporcionado (especialmente en móvil), impidiendo un barrido visual ágil.
- **Falta de visibilidad del texto original:** En el modal de edición, el moderador no podía consultar el texto original extraído ni contaba con enlace directo para revisar la publicación en redes o extraer fotos alternativas.
- **Falta de control sobre IA:** No se advertía al moderador si una publicación no había sido procesada por IA ni se ofrecía un botón para reintentar el análisis con Gemini.

## 2. Solución Propuesta
1. **Configuración y Prevención:**
   - Desactivar `SocialCollector.Simulate` en `appsettings.json` (pasar a `false`).
   - Crear `appsettings.Production.json` garantizando `Simulate: false` y `SimulateApi: false` para producción.
   - Incorporar validación anti-duplicados por `SourceUrl` en `ISocialIngestionService` (`IngestFromUrlAsync`, `IngestMultimodalAsync`, `IngestManualAdvancedAsync`).
2. **Resiliencia y Reintento de IA:**
   - Exponer `IsAiProcessed` en `SocialInboxItemDto`.
   - Implementar `ReanalyzeWithAiAsync` en `ISocialIngestionService`.
   - Mostrar un banner de aviso en el modal de edición cuando la publicación no fue procesada por IA y un botón de reintento.
3. **Rediseño de la Bandeja (`SocialInboxModeration.razor`):**
   - Transformar la vista a carteles compactos (estilo `rail-card` como en el Radar, 2 columnas en móvil y 3-6 en escritorio).
   - Abrir la edición al pulsar en cualquier parte del cartel o en el botón de edición.
   - Añadir acción de purga de publicaciones simuladas en la bandeja.
4. **Enriquecimiento del Modal de Edición (`SocialInboxEditModal.razor`):**
   - Incorporar panel con el texto original extraído (`OriginalCaption`).
   - Botón directo para abrir la URL original en pestaña nueva.
   - Botón para aprobar y publicar directamente desde el modal.

## 3. Criterios de Aceptación
- Ninguna ingesta permite URLs duplicadas en la bandeja.
- Los ítems sin IA son identificados visualmente y pueden reprocesarse interactivamente.
- La bandeja se visualiza en carteles compactos ergonómicos y clicables.
- La suite de pruebas de regresión pasa al 100%.
