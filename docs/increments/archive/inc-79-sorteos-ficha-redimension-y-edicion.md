# INC-79: Ficha Inteligente de Sorteos, Redimensionado a Proporciones de Catálogo y Subida R2 de Carátulas

> **Estado:** ✅ Completado y Verificado  
> **Fecha de Inicio:** 2026-09-28 · **Fecha de Cierre:** 2026-09-28  
> **Rama de Trabajo:** `inc/sorteos-ficha-y-tamano`  
> **Worktree:** `F:\repos\ludeka-wt\sorteos-ficha-y-tamano`  
> **Dependencias:** INC-06 (Radar de Sorteos), INC-40 (Almacenamiento Cloudflare R2), INC-42 (Hub Ingesta Social y Alta Exprés)  
> **Pruebas Automatizadas Verificadas:** Suite completa de pruebas unitarias y de integración en verde  
> **Especificación Viva:** [`06. Radar de Sorteos y Comunidad`](../specs/sistema/06-radar-de-sorteos-y-comunidad.md)  
> **Metodología:** Spec-Driven Development (SDD) con Strict TDD y contratos de marcado  

---

## 1. Contexto y Diagnóstico

El usuario detectó tres deficiencias clave en el flujo y visualización de sorteos en producción:

1. **Fallo en carga de foto desde «Alta Exprés»:**  
   Al crear un sorteo mediante el formulario exprés de moderación, la imagen de portada no cargaba en el navegador, mostrando únicamente la plantilla SVG por defecto (`sorteo-default.svg`).  
   *Causa raíz:* La variable de entorno de producción `Cloudflare__PublicCdnBaseUrl` estaba configurada con el dominio inactivo `https://cdn.ludeka.com` (parking page sin certificado SSL válido). Al intentar descargar la imagen de forma anónima desde el cliente, la petición fallaba y el evento `onerror` activaba el reemplazo por la plantilla por defecto. El dominio público funcional asignado por Cloudflare para el bucket R2 es `https://pub-a0b33b365ae446c58f5e80b3dc493970.r2.dev`.

2. **Dimensiones desproporcionadas («tarjetas enormes»):**  
   Las tarjetas de sorteos en el carril de portada (`HomeGiveawayCard`) y en la pestaña de sorteos del radar (`GiveawayCard`) utilizaban un formato horizontal 16:9 (`h-44`) con un grid de 1 a 3 columnas. Esto generaba un impacto visual discordante con el lenguaje editorial del catálogo de juegos, cuyas tarjetas son compactas y de proporción vertical/cuadrada.

3. **Ausencia de Ficha de Detalle y Edición Administrativa:**  
   Al hacer clic en un sorteo, no existía una página de detalle dedicada (`/sorteos/{id}` o `/sorteo/{id}`). El usuario requería poder acceder a una ficha completa (similar a la ficha de un juego) con toda la información del sorteo, colaboradores, fechas exactas, juego asociado y un panel modal donde un administrador/moderador pueda editar todos los campos y sustituir la carátula subiéndola a Cloudflare R2.

---

## 2. Alcance y Arquitectura de la Solución (INC-79)

### Componente 1: Corrección de CDN de Cloudflare R2 y Buffers de SignalR
- Configurada la URL pública oficial en `src/Ludeka.Web/appsettings.json`:
  ```json
  "Cloudflare": {
    "PublicCdnBaseUrl": "https://pub-a0b33b365ae446c58f5e80b3dc493970.r2.dev"
  }
  ```
- Sincronizados los workflows de GitHub Actions (`.github/workflows/ci-cd.yml`) para el servicio web y los Cloud Run Jobs.
- Ajustado en `Program.cs` el tamaño máximo de mensaje de SignalR a 32 MB (`HubOptions.MaximumReceiveMessageSize = 32 * 1024 * 1024`) para evitar desconexiones en subidas de ficheros desde Blazor Server.

### Componente 2: Modelo de Dominio y Contratos de Aplicación
- **Entidad `Giveaway` (`Ludeka.Core`):** Incorporado el método de mutación `Update(title, organizer, collaborator, url, platform, deadlineAt, thumbnailUrl, isCommunityExclusive, isPromoted, country, gameId, gameTitle)`.
- **DTOs (`Ludeka.Application`):** Creado el registro inmutable `UpdateGiveawayRequest`.
- **Servicio `IGiveawayService` / `GiveawayService`:**
  - Implementado `UpdateGiveawayAsync(UpdateGiveawayRequest, CancellationToken)` con validación de permisos de moderación (`CanApproveMedia` o Mesa Fundadora).
  - Implementado `DeleteGiveawayAsync(Guid, CancellationToken)` para borrado controlado por moderadores.
  - Actualizado `FakeGiveawayService` para pruebas de integración de dashboard.

### Componente 3: Rediseño de Tarjetas a Proporción de Catálogo
- **`HomeGiveawayCard.razor`:**
  - Rediseñada a ancho compacto idéntico a `HomeGameCard` (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`).
  - Portada cuadrada `rail-cover--square` (192x192) con badges de tiempo restante y plataforma/promocionado.
  - Enlace directo a la ficha del sorteo (`/sorteos/@Giveaway.Id`).
- **`GiveawayCard.razor`:**
  - Formato vertical compacto de catálogo (`rail-card` con `aspect-square` y dimensiones 240x240).
  - Badges flotantes: tiempo restante (con estado de urgencia para últimas horas), país, plataforma y estado de comunidad/promoción.
  - Enlace directo a la ficha (`/sorteos/@Giveaway.Id`) y botón de acción directa «Participar &rarr;».
- **`Radar.razor`:**
  - Cuadrícula responsive unificada con el catálogo: `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.

### Componente 4: Ficha Inteligente de Sorteo (`GiveawayDetail.razor`)
- Rutas públicas canónicas: `@page "/sorteos/{Id:guid}"` y `@page "/sorteo/{Id:guid}"`.
- Cabecera editorial con enlace de retorno al radar y accesos de moderación.
- Columna izquierda:
  - Cartel del sorteo en alta resolución con fallback anti-CLS por dominio (`DefaultImageDomain.Sorteo` / `sorteo-default.svg`).
  - Botón CTA prominente: «Participar en el sorteo oficial &rarr;» (enlace externo verificado con `target="_blank"` y `rel="noopener noreferrer"`).
  - Indicador de expiración si el sorteo ha concluido.
- Columna derecha:
  - Badges de ámbito territorial (bandera dinámica de `CountryCatalog`), plataforma y cuenta atrás.
  - Título editorial en gran formato.
  - Tarjetas de organizador oficial y colaborador.
  - Especificaciones técnicas: fechas en UTC y fecha de registro.
  - Ficha del juego de mesa asociado si está vinculado en catálogo.
- **Panel Modal de Edición de Sorteo:**
  - Accesible para moderadores (`CanApproveMedia`) y Mesa Fundadora.
  - Modificación de título, organizadores, URL, plataforma, país, fecha límite y juego.
  - Selector de archivo `<InputFile>` para subir carátula directamente a Cloudflare R2 vía `IImageStorageService.UploadOptimizedImageAsync`, guardando en `giveaways/{id:N}/cover.webp` con compresión WebP.
  - Modal de confirmación para eliminación definitiva del sorteo.

---

## 3. Verificación Automatizada

- `GiveawayServiceTests`: Pruebas unitarias para actualización y eliminación de sorteos con verificación de permisos y persistencia.
- `GiveawayDetailPageContractTests`: Pruebas de contrato para rutas, interactividad, anti-CLS y capacidades administrativas.
- `CatalogImageOptimizationContractTests`: Actualizada la prueba de dimensiones intrínsecas para `HomeGiveawayCard` al nuevo formato de catálogo (192x192).
- `WebMarkupContractTests`: Adaptada la regla de `HomeGiveawayCard` para el nuevo estándar `rail-cover--square` y verificados todos los contratos de marcado (107/107).
