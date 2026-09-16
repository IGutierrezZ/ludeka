# Especificación Técnica: change-28-instagram-direct-publisher

## 1. Requerimientos Funcionales y Criterios de Aceptación (Gherkin)

### Requerimiento 1: Creación de Borrador de Instagram
- **R1.1:** Desde la tarjeta o fila de un sorteo (`Giveaway`), novedad (`WeeklyRelease`) o ficha de juego (`Game`), un moderador con permiso `CanPublishInstagram` o `FoundingTeam` puede hacer clic en `[ 📸 Crear Post de Instagram ]`.
- **R1.2:** El sistema crea una entidad `InstagramPostDraft` con estado inicial `Draft`, rellenando:
  - `SourceType` (`Giveaway`, `WeeklyRelease`, `Game`).
  - `SourceId` (Id del elemento origen).
  - `Title` (Título del sorteo, novedad o juego).
  - `Caption` (Copy estructurado con emojis, mención a la cuenta organizadora o editorial, resumen y hashtags en español).
  - `SvgContent` (Tarjeta editorial compuesta en formato cuadrado 1:1, con carátula, logotipo de Ludeka, píldora distintiva y pie de marca).
  - `Theme` (`Dark` por defecto).
  - `ImageUrl` (URL pública de la imagen de portada o endpoint del SVG rasterizado).
- **R1.3:** El usuario es redirigido a `/admin/instagram` con el borrador activo seleccionado para su inspección y edición.

```gherkin
Escenario: Creación automática de borrador para sorteo
  Dado un moderador autenticado con permiso "CanPublishInstagram"
  Y un sorteo activo de "Devir Iberia" con fecha límite "15/10/2026"
  Cuando pulsa "Crear Post de Instagram"
  Entonces se genera un borrador con estado "Draft"
  Y el copy incluye mención a "@deviriberia", la fecha límite y hashtags temáticos
  Y se compone la imagen cuadrada 1:1 con la píldora "🎁 Sorteo Activo"
```

### Requerimiento 2: Compositor de Tarjeta Editorial (1:1 Cuadrado)
- **R2.1:** El servicio de composición genera representaciones vectoriales SVG limpias de 1080x1080 px sin artefactos ni dependencias externas frágiles.
- **R2.2:** Soporta dos temas visuales:
  - `Dark`: Fondo carbón/slate oscuro (`#0B0F17`), acentos naranja Ludeka (`#F97316`) y textos blancos de alto contraste.
  - `Light`: Fondo marfil/crema claro (`#F8FAFC`), bordes sutiles y textos oscuros.
- **R2.3:** Incluye:
  - Logotipo oficial de Ludeka en la esquina superior izquierda.
  - Píldora identificativa: "🎁 Sorteo Activo", "📰 Novedad Editorial" o "🎲 Ficha Destacada".
  - Carátula centrada o en marco con sombra suave.
  - Organizador / Editorial y fechas relevantes.
  - Pie de marca: "Descubre más en ludeka.app".

```gherkin
Escenario: Alternancia entre tema oscuro y claro en el previsualizador
  Dado un borrador de Instagram abierto en "/admin/instagram"
  Cuando el moderador selecciona el tema "Light"
  Entonces la previsualización se recalcula con fondo claro y textos de alto contraste
  Y el borrador almacena "Theme = Light"
```

### Requerimiento 3: Interfaz Editorial y Previsualizador del Feed (`/admin/instagram`)
- **R3.1:** La página `/admin/instagram` está restringida a usuarios con rol `FoundingTeam` o moderadores con `CanPublishInstagram`.
- **R3.2:** Dispone de dos paneles en escritorio (y apilado en móvil):
  - Panel izquierdo / superior: Mockup interactivo del feed de Instagram (cabecera con cuenta @ludeka.app, botón de opciones, contenedor 1:1 con la imagen, iconos de Me Gusta, Comentarios, Compartir y Guardar, y pie de foto con copy expandible).
  - Panel derecho / inferior: Editor del post con:
    - Selector de imagen: Tarjeta compuesta por Ludeka vs. Carátula original.
    - Selector de tema de tarjeta: Oscuro / Claro.
    - Área de texto para el Copy con contador de caracteres (máx. 2200 de Instagram).
    - Botones de inserción rápida de hashtags comunes (`#juegosdemesa`, `#boardgames`, `#ludeka`, `#sorteojuegos`, `#novedadesludicas`).
    - Botón de acción: `[ 🚀 Validar y Publicar en Instagram ]`.
- **R3.3:** Listado/Bandeja de borradores con pestañas: "Borradores pendientes", "Publicados", "Fallidos".

```gherkin
Escenario: Personalización del copy antes de publicar
  Dado un moderador en "/admin/instagram" revisando un borrador
  Cuando añade texto extra al copy y hace clic en guardar borrador
  Entonces los cambios se persisten en la base de datos
  Y el previsualizador del feed actualiza el texto mostrado en tiempo real
```

### Requerimiento 4: Publicación vía Meta Graph API y Trazabilidad
- **R4.1:** Al pulsar `[ 🚀 Validar y Publicar en Instagram ]`:
  - El estado del borrador pasa transitoriamente a `Publishing`.
  - Se invoca `IInstagramApiClient.CreateMediaContainerAsync(imageUrl, caption)`.
  - Si tiene éxito, se invoca `IInstagramApiClient.PublishMediaAsync(creationId)`.
  - Se recupera el enlace permanente `Permalink` del post (`https://www.instagram.com/p/...`).
  - El estado del borrador pasa a `Published`, registrando `InstagramMediaId`, `InstagramPermalink` y `PublishedAt`.
- **R4.2:** Si el borrador procede de un Sorteo (`Giveaway`) o Novedad (`WeeklyRelease`), se actualiza la entidad origen marcando `InstagramMediaId` e `InstagramPermalink`.
- **R4.3:** Si ocurre un error de la API de Meta (token inválido, imagen inaccesible, etc.), el estado pasa a `Failed`, registrando el mensaje de error para que el moderador pueda subsanarlo y reintentar.
- **R4.4:** En modo simulado (`Simulate = true` o sin credenciales configuradas), la llamada retorna un resultado exitoso simulado con un ID y permalink ficticio válido para garantizar testing automatizado y desarrollo offline.

```gherkin
Escenario: Publicación exitosa de un post en Instagram
  Dado un moderador con permiso "CanPublishInstagram" en "/admin/instagram"
  Cuando pulsa "[ 🚀 Validar y Publicar en Instagram ]"
  Entonces el sistema envía el medio y copy a la API de Meta
  Y el borrador adquiere estado "Published" con su Permalink asignado
  Y el sorteo de origen queda marcado con el badge "Publicado en Instagram"
  Y se registra una entrada en la bitácora de auditoría con la acción "Published"
```

### Requerimiento 5: Bitácora de Auditoría (INC-20)
- **R5.1:** Toda publicación completada añade un `AuditLogEntry` con:
  - `EntityType = AuditEntityType.InstagramPost`.
  - `Action = AuditAction.Published`.
  - `UserId` y `UserName` del moderador.
  - `EntityId` (Id del borrador) y `EntityName` (Título del post).
  - `Summary` con el permalink obtenido.
