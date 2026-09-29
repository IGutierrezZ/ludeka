# Incremento 80: Rediseño de Moderación Social a Carteles Compactos, Resiliencia y Reintento IA, y Limpieza de Simulación

> **ID:** INC-80  
> **Slug:** `moderacion-social-carteles-ia`  
> **Rama:** `inc/moderacion-social-carteles-ia`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 28 (`docs/specs/sistema/28-hub-ingesta-social-moderacion.md`), Módulo 30 (`docs/specs/sistema/30-recolector-canales-sociales.md`)  
> **Dependencias:** INC-42 (Bandeja de moderación), INC-44 (Worker recolector), INC-70 (Ingesta multimodal), PR #139 (Aislamiento EF Core en updates).

---

## 1. Contexto y Diagnóstico del Problema

1. **Inyección involuntaria de publicaciones simuladas:** En `appsettings.json`, la opción `SocialCollector:Simulate` estaba fijada a `true`. El servicio hospedado en segundo plano (`SocialCollectorHostedService`) y el botón «Sondear Canales» invocaban `InstagramFeedCollector.GenerateSimulatedPosts`, generando publicaciones simuladas con URLs `https://www.instagram.com/p/sim_{handle}_{i}/` y descripciones genéricas para cuentas monitorizadas (como Cuarto de Juegos).
2. **Fallback heurístico y títulos por defecto:** En entornos sin API Key de Gemini configurada (o ante caídas de red), la ingesta aplica extracción heurística local. Al no reconocer títulos entrecomillados ni entidades concretas, se generaron títulos vacíos o genéricos (*«Gran sorteo exclusivo de Cuarto de Juegos»*, *«Sorteo Lúdico de Comunidad»*).
3. **Ausencia de deduplicación en la ingesta manual/multimodal:** `ISocialIngestionService.IngestMultimodalAsync`, `IngestFromUrlAsync` e `IngestManualAdvancedAsync` no validaban `_inboxRepository.ExistsBySourceUrlAsync`, posibilitando la proliferación de entradas duplicadas en la bandeja.
4. **Diseño sobredimensionado y baja ergonomía móvil:** La bandeja de moderación (`SocialInboxModeration.razor`) empleaba tarjetas gigantes (`grid-cols-1 md:grid-cols-2 lg:grid-cols-3` con relación de aspecto 16:9 de gran altura), provocando que en dispositivos móviles solo cupiera una tarjeta y media por pantalla.
5. **Carencias en el modal de edición (`SocialInboxEditModal.razor`):**
   - El moderador no tenía visible el texto original extraído (`OriginalCaption`) para verificar bases, fechas y requisitos.
   - No existía un botón para abrir el enlace de la publicación original en nueva pestaña y capturar otra foto o comprobar datos.
   - No se informaba si una publicación no había sido procesada por IA ni se ofrecía mecanismo para reintentar el análisis con Gemini.
   - No era posible aprobar directamente desde el modal de edición tras corregir los datos.
6. **Vigencia y ámbito territorial en moderación de sorteos:**
   - La aprobación no debe inventar fechas `+7 días`, sino exigir explícitamente una fecha de fin igual o posterior a hoy (`rawDeadline >= UtcNow.Date`).
   - El ámbito territorial debe resolverse de forma automática si no viene en el texto consultando si la editorial, tienda o creador organizador o colaborador está dado de alta en la plataforma.

---

## 2. Objetivos del Incremento

1. **Configuración limpia de producción:**
   - Desactivar `SocialCollector.Simulate` en `appsettings.json` (fijar a `false`).
   - Crear / asegurar `appsettings.Production.json` con `SimulateApi: false`, `Simulate: false` en todos los subsistemas externos.
2. **Deduplicación estricta en el pipeline de ingesta:**
   - Bloquear entradas duplicadas por `SourceUrl` en `IngestFromUrlAsync`, `IngestMultimodalAsync` e `IngestManualAdvancedAsync`.
3. **Detección de análisis sin IA y reintento interactivo:**
   - Exponer en `SocialInboxItemDto` si la publicación fue procesada por IA (`IsAiProcessed`).
   - Añadir `ReanalyzeWithAiAsync(Guid inboxItemId)` en `ISocialIngestionService` para reprocesar publicaciones pendientes con Gemini AI.
   - En la UI, alertar cuando una publicación carezca de análisis IA y permitir reintentarlo con 1 clic.
4. **Rediseño a carteles compactos de la bandeja de moderación (`SocialInboxModeration.razor`):**
   - Rejilla adaptativa y compacta de carteles (estilo `rail-card` editorial: 2 columnas en móvil, 3 a 6 en escritorio).
   - Acceso a edición pulsando sobre la tarjeta o mediante botón dedicado.
   - Acciones al pie: edición, descarte y aprobación rápida.
   - Acción de purga/limpieza de publicaciones simuladas (`sim_*`).
5. **Enriquecimiento del modal de edición (`SocialInboxEditModal.razor`):**
   - Panel dedicado para el texto original extraído.
   - Botón directo para navegar a la URL original de la publicación en nueva pestaña.
   - Banner de advertencia de extracción heurística + botón de reintento con IA.
   - Botón de aprobación y publicación directa desde el propio modal.
6. **Reglas de negocio de sorteos y territorio:**
   - Bloqueo estricto de aprobación de sorteos sin fecha fin válida (hoy o posterior) y apertura asistida de edición.
   - Resolución automática de país desde `Publisher`, `Store` y `Creator` para organizador y colaborador.

---

## 3. Plan de Pruebas y Criterios de Aceptación (TDD)

- **Test 1:** `SocialIngestionService` rechaza la ingesta duplicada cuando la URL ya existe en la bandeja para todas las modalidades (Exprés, Multimodal y Manual).
- **Test 2:** `SocialIngestionService.ReanalyzeWithAiAsync` actualiza metadatos y notas cuando el servicio de IA responde exitosamente.
- **Test 3:** `SocialInboxItemDto.IsAiProcessed` identifica con precisión si el ítem proviene de IA o de fallback heurístico/manual.
- **Test 4:** `SocialIngestionService.ApproveAndPublishAsync` rechaza sorteos sin fecha o con fecha pasada, y resuelve el país desde repositorios de directorio.
- **Test 5:** Las pruebas unitarias de regresión pasan al 100% (2.079 unitarias + 10 integración = 2.089 pruebas).
