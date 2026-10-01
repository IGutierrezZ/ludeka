# Incremento 91: Rediseño y Despeje Visual del Hub Multimedia, Moderación Directa e Ingesta Flash de Vídeos

> **Slug:** `hub-multimedia-gestion-flash`  
> **Rama:** `inc/hub-multimedia-gestion-flash`  
> **Estado:** ⏳ En progreso  
> **Fecha:** 2026-10-01  
> **Pruebas automáticas asociadas:** 2.189 unitarias pasando al 100%

---

## 1. Contexto y Diagnóstico

En la ficha de detalle de juego (`/juegos/{slug}`), la pestaña secundaria «Hub Multimedia» presentaba varias oportunidades clave de mejora en experiencia de usuario y agilidad operativa:
1. **Redundancia textual en la cabecera:** La sección mostraba el título destacado *«Hub Multimedia en Español»* y el subtítulo descriptivo *«Tutoriales, partidas completas y reseñas en redes segregados sin mezclar formatos.»*, ocupando espacio vertical valioso cuando el usuario ya se encuentra explícitamente en la pestaña «Hub Multimedia».
2. **Espacio ocupado y ruido en tarjetas de vídeo:** Cada tarjeta incluía una barra inferior fija con textos como *«YouTube Tutorial»* y *«Reproducir →»* o *«Ver análisis →»*. Dado que hacer clic en la tarjeta o su miniatura ya reproduce el contenido en el modal embebido, este pie resultaba prescindible y restaba densidad al contenido visual.
3. **Falta de visibilidad de controles de moderación:** La edición de vídeos (cambiar de categoría o eliminar) estaba oculta tras un botón diminuto y de bajo contraste superpuesto en la esquina de la miniatura, dificultando la gestión rápida por parte de moderadores y fundadores.
4. **Ausencia de ingesta directa (Flash):** Para asociar un nuevo vídeo a un juego, un administrador debía recurrir a búsquedas externas o procesos indirectos de sincronización, en lugar de poder pegar directamente una URL de YouTube y publicarla de inmediato en la ficha.

---

## 2. Objetivos y Alcance Técnico

### 2.1. Despeje de Cabecera y Selector Compacto
- Supresión del encabezado y subtítulo estáticos redundantes en `MultimediaHub.razor`.
- Front-loading inmediato del selector de formatos (`Cómo Funciona`, `Tutoriales`, `Partidas Completas`, `Opiniones y Redes`) con contadores de ítems y badges compactos.
- Conservación estricta de iconografía Lucide (`tv`, `zap`, `clapperboard`, `dices`, `message-circle`, etc.) para satisfacer contratos de accesibilidad y marcado.

### 2.2. Tarjetas Limpias y Sin Ruido
- Eliminación de la barra inferior redundante en tarjetas de tutoriales, partidas, partidas rápidas, reseñas, Instagram y Reels.
- Supresión de botones superpuestos poco contrastados en la miniatura.
- Foco en la carátula, duración, título y canal, abriendo el modal de reproducción con clic en tarjeta o teclado (Enter/Espacio).

### 2.3. Barra de Moderación Directa para Administradores
- Incorporación de una barra inferior contextual visible únicamente para usuarios con permisos (`IsModeratorUser`).
- Botón **[🏷 Cambiar Categoría]**: abre el modal de moderación enfocado en el selector de categoría (`QuickOverview`, `Tutorial`, `Gameplay`, `ReviewOpinion`) y reasignación de juego.
- Botón **[🗑 Eliminar]**: activa la confirmación de desvinculación/borrado directo sin pasos intermedios.

### 2.4. Ingesta Flash de YouTube (`FlashIngestAsync`)
- **Firma del Contrato:** `Task<MediaItemDto> FlashIngestAsync(Guid gameId, string url, MediaCategory category, string? playerCountBadge = null, CancellationToken ct = default);` en `IMediaService`.
- **Validaciones:**
  - Validación de identificador de juego existente en catálogo.
  - Validación estricta de URL de YouTube (`youtube.com/watch?v=...`, `youtu.be/...`, `youtube.com/shorts/...`).
  - Detección de duplicados mediante `ExistsByUrlAsync`.
  - Extracción automática de metadatos mediante `ISocialMetadataExtractor` (título, autor, miniatura, duración en segundos).
  - Detección automática del número de jugadores (`PlayerCountExtractor`) en partidas completas cuando no se proporciona badge manual.
  - Creación con estado aprobado (`IsApproved = true`), registro de auditoría (`AuditAction.Created`) y persistencia atómica.
- **Interfaz en `MultimediaHub.razor`:**
  - Botón prominente `[⚡ Ingesta Flash]` en la barra superior para moderadores.
  - Modal interactivo con entrada de URL, selector de categoría, campo opcional de comensales, feedback de carga/error y refresco reactivo automático tras publicar.

---

## 3. Pruebas y Verificación

- **Pruebas de Servicio (`MediaServiceTests.cs`):**
  - Ingesta válida de YouTube con extracción de metadatos y cálculo de duración.
  - Bloqueo y excepción ante URLs duplicadas.
  - Rechazo de URLs con formato no válido.
  - Inferencia automática de comensales en partidas a partir del título.
  - Verificación de permisos de moderación requeridos (`CanApproveMedia`).
- **Pruebas de Contrato de Marcado e Interfaz (`MultimediaHubUiContractTests.cs`):**
  - Comprobación de ausencia de textos redundantes de cabecera.
  - Comprobación de retirada de botones repetitivos en pies de tarjetas.
  - Declaración y presencia de controles de Ingesta Flash y modal correspondiente.
  - Declaración de acciones de moderación directa (cambio de categoría y borrado).
  - Exposición del parámetro `GameId` en `MultimediaHub.razor`.
- **Suite Global:** 2.189 pruebas unitarias pasando al 100%.
