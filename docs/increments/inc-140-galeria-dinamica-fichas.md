# INC-140: Galería Dinámica e Imágenes Adicionales en el Editor Editorial de Fichas

## 1. Contexto y Motivación
Hasta ahora, la ficha editorial de juego en `GameEditorModal.razor` únicamente permitía editar o sustituir las tres imágenes estáticas predefinidas del modelo clásico:
1. Portada frontal oficial (`BoxFrontUrl` / `ImageUrl`).
2. Contraportada o trasera de la caja (`BoxBackUrl`).
3. Foto de despliegue en mesa (`TablePresenceUrl`).

Cuando el equipo editorial, moderadores o colaboradores deseaban documentar componentes especiales, cartas, tableros modulares, insertos o perspectivas adicionales de la mesa, no existía mecanismo en la interfaz ni en el modelo de dominio para incorporar más fotografías.

El incremento **INC-140** dota al catálogo de Ludeka de soporte integral para colecciones dinámicas de imágenes adicionales (`AdditionalImages`), persistidas en base de datos mediante JSONB, editables desde el modal de staff (subida directa de archivos a storage/R2 o adición por URL con pie de foto opcional) y visibles en el carrusel polaroid y en el visor a pantalla completa de la ficha pública de juego.

---

## 2. Objetivos
1. **Modelo de Dominio (`Ludeka.Core`):**
   - Crear el Value Object `GameGalleryImage` con `Url` obligatoria y `Title` opcional.
   - Enriquecer la entidad `Game` con la colección de lectura `AdditionalImages`, y métodos de mutación controlados: `UpdateAdditionalImages`, `AddGalleryImage` y `RemoveGalleryImage`.
2. **Persistencia e Infraestructura (`Ludeka.Infrastructure`):**
   - Mapear la colección en `LudekaDbContext` mediante `OwnsMany(g => g.AdditionalImages, b => b.ToJson())` compatible con PostgreSQL (`jsonb`) y SQLite.
   - Generar la migración de Entity Framework Core `AddGameAdditionalImages`.
   - Extender `SqliteSchemaMigrator` para garantizar la reconciliación y añadir defensivamente la columna `AdditionalImages` en SQLite sin fallos de esquema.
3. **Capa de Aplicación y Auditoría (`Ludeka.Application`):**
   - Exponer `AdditionalImages` en `GameDetailDto` y `GameEditorDtos` (`UpdateGameDetailsCommand`).
   - Implementar en `GameEditorService` la verificación del permiso `CanUploadImages`, detección de diferencias en la galería y registro detallado en la traza de auditoría `GameEditLog`.
4. **Experiencia Editorial y Web (`Ludeka.Web`):**
   - Actualizar `GameEditorModal.razor` incorporando la sección «4. Galería Complementaria» con previsualización en miniatura, eliminación individual, subida de archivos locales (`InputFile`) y alta manual por URL con título.
   - Actualizar `GameImageCarousel.razor` para incorporar dinámicamente las imágenes de la galería en los slides con su leyenda/pie de foto.
   - Actualizar `GameDetail.razor` integrando `AdditionalImages` en `AvailableHeroImages` y en el visor modal a pantalla completa.

---

## 3. Criterios de Aceptación
- [x] La entidad de dominio `Game` gestiona una colección de `GameGalleryImage` respetando invariantes de negocio.
- [x] La base de datos persiste `AdditionalImages` sin regresiones y `SqliteSchemaMigrator` asegura compatibilidad completa con entornos de testing y desarrollo local.
- [x] El editor editorial `GameEditorModal.razor` permite añadir N imágenes tanto subiendo archivos locales como especificando URLs con título.
- [x] Las imágenes añadidas pueden eliminarse individualmente antes o después de guardar.
- [x] Los cambios en la galería quedan registrados en el log de auditoría del juego.
- [x] El carrusel de la ficha de juego (`GameImageCarousel.razor`) y la vista pública (`GameDetail.razor`) renderizan las fotos añadidas.
- [x] El 100% de la suite de pruebas unitarias y de integración pasa en verde (2.760 tests).
