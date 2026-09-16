# Propuesta de Cambio: change-28-instagram-direct-publisher

## 1. Resumen Ejecutivo
El **Incremento 28** dota a Ludeka de una pasarela directa entre la moderación comunitaria y la cuenta oficial de Instagram de la plataforma, cerrando la brecha entre la detección de contenidos y la difusión en redes sociales:

1. **Flujo Editorial desde Moderación a Redes (`/admin/instagram`):**
   - Desde la bandeja de sorteos (`/sorteos`), novedades editoriales (`/novedades`) o fichas de juego (`/catalogo/{slug}`), los moderadores con el permiso granular `CanPublishInstagram` (o miembros de la `FoundingTeam`) disponen del botón de acción **`[ 📸 Crear Post de Instagram ]`**.
   - Al pulsarlo, el sistema genera automáticamente un borrador persistido (`InstagramPostDraft`) con previsualización fidedigna del feed de Instagram, copy sugerido enriquecido con emojis, mención al organizador/editorial y batería de hashtags optimizados en español.

2. **Compositor de Tarjetas Editoriales de Marca (1:1 Cuadrado / SVG Vectorial & Servidor):**
   - Generación de plantillas de alta fidelidad para sorteos ("🎁 Sorteo Activo"), novedades ("📰 Novedad Editorial") y juegos base.
   - Incluye carátula original, logotipo de Ludeka, píldora contextual, organizador/editorial, fecha límite o fecha de lanzamiento y pie de marca.
   - Posibilidad de alternar entre tema **Oscuro** (fondo premium slate/carbón) o **Claro** (fondo editorial marfil/blanco).

3. **Zona de Moderación de Instagram (`/admin/instagram`):**
   - **Previsualizador Interactivo:** Simulación visual exacta de un post del feed de Instagram (avatar de Ludeka, cabecera de perfil, contenedor cuadrado de imagen, iconos de interacción social, copy formateado y hashtags).
   - **Editor en Vivo:** Permite al moderador ajustar el copy, añadir menciones a colaboradores, conmutar el tema visual de la tarjeta y seleccionar si publicar la imagen compuesta o la carátula original.
   - **Historial y Estado de Borradores:** Bandeja de borradores en curso (`Draft`), en proceso (`Publishing`), publicados exitosamente (`Published`) y con errores (`Failed`).

4. **Publicador Oficial vía Meta Graph API (`IInstagramApiClient` / `IInstagramPublisherService`):**
   - Proceso en 2 fases conforme a la Content Publishing API de Meta:
     - Fase 1: Creación del contenedor de medio (`POST /{ig-user-id}/media`) con `image_url` y `caption`.
     - Fase 2: Publicación final (`POST /{ig-user-id}/media_publish`).
   - Modo simulado/desarrollo (`Simulate = true` o credenciales ausentes) para pruebas unitarias e integración sin credenciales obligatorias.
   - Trazabilidad y badges: Registro de `InstagramMediaId` y `InstagramPermalink` en el borrador y en la entidad origen (`Giveaway` o `WeeklyRelease`), mostrando el badge *"Publicado en Instagram"* con enlace directo.
   - Auditoría: Registro inmutable en `AuditLogEntry` (`AuditEntityType.InstagramPost`, `AuditAction.Published`).

5. **Gobernanza y Permisos Granulares (INC-20):**
   - Nuevo permiso granular en `ModeratorPermission`: `CanPublishInstagram` (`1 << 7 = 128`).
   - Gestión desde el modal de permisos de usuario (`UserPermissionsModal.razor`).

---

## 2. Justificación y Valor
- **Ahorro de Tiempo Editorial:** Actualmente los moderadores deben redactar manualmente copies y preparar imágenes en herramientas externas antes de publicarlas en Instagram.
- **Identidad Coherente de Marca:** Las imágenes compuestas aseguran que todo contenido compartido lleve la tipografía, logotipo y estilo editorial de Ludeka.
- **Trazabilidad y Auditoría:** Queda constancia de qué moderador autorizó y publicó cada post, evitando duplicidades o publicaciones erróneas.
