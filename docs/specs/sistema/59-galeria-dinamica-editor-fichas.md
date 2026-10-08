# 59. Galería Dinámica e Imágenes Adicionales en el Editor Editorial de Fichas

> **Estado:** Implementado, verificado y desplegado en producción (Google Cloud Run)  
> **Incremento:** INC-140 (PR #253)  
> **Cobertura de pruebas:** 2.760 pruebas unitarias + 10 de integración verificadas al 100% (2.770 pruebas en total)

---

## 1. Propósito y Contexto de Negocio
Con anterioridad a este incremento, las fichas de juego en el catálogo y su editor editorial (`GameEditorModal.razor`) se encontraban limitadas al modelo clásico de tres imágenes fijas:
- Portada oficial de caja (`BoxFrontUrl` / `ImageUrl`).
- Trasera de la caja (`BoxBackUrl`).
- Fotografía de despliegue en mesa (`TablePresenceUrl`).

Cuando el equipo editorial, los moderadores o la Mesa Fundadora necesitaban documentar componentes clave de una edición física (cartas singulares, tableros modulares, insertos organizadores, detalles de miniaturas o perspectivas complementarias de partida), no existía soporte en la interfaz ni en el modelo de dominio para incorporar imágenes adicionales sin sobrescribir las tres ranuras canónicas.

El módulo de **Galería Dinámica** soluciona esta carencia dotando al catálogo de una colección extensible de imágenes complementarias (`AdditionalImages`), persistidas estructuradamente en base de datos mediante JSONB, editables interactivamente desde el modal de staff y reflejadas tanto en el carrusel de fotografías como en el visor modal a pantalla completa de la ficha pública de juego.

---

## 2. Arquitectura y Modelo de Dominio

### Value Object `GameGalleryImage` (`Ludeka.Core`)
Ubicado en `src/Ludeka.Core/ValueObjects/GameGalleryImage.cs`, modela cada fotografía de la galería como un Value Object inmutable con validación defensiva:
- `Url` (`string`): Obligatoria. No puede ser nula ni vacía. Valida sintaxis absoluta de URL mediante `Uri.TryCreate` (soporta esquemas `http` y `https`) o URLs relativas administradas que comiencen por `/`.
- `Title` (`string?`): Opcional. Pie de foto o leyenda explicativa para el usuario y accesibilidad (alt text).

### Agregado `Game` (`Ludeka.Core`)
La entidad principal `Game` expone y gobierna la colección de imágenes adicionales:
- Propiedad de sólo lectura: `IReadOnlyCollection<GameGalleryImage> AdditionalImages`.
- Mutación controlada mediante métodos de dominio:
  - `UpdateAdditionalImages(IEnumerable<GameGalleryImage>? images)`: Reemplaza la colección de imágenes aplicando normalización y descarte de nulos o elementos inválidos.
  - `AddGalleryImage(GameGalleryImage image)`: Añade una nueva imagen a la colección.
  - `RemoveGalleryImage(string url)`: Elimina de la colección cualquier imagen que coincida con la URL especificada (comparación ordinal insensible a mayúsculas/minúsculas).

---

## 3. Persistencia e Infraestructura

### Mapeo EF Core (`LudekaDbContext`)
En `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs`, la colección se mapea como entidad propia propiedad del juego serializada a columna JSON:
```csharp
game.OwnsMany(g => g.AdditionalImages, b =>
{
    b.ToJson();
});
```
- **PostgreSQL (Supabase):** Mapeo nativo a columna de tipo `jsonb` (`AdditionalImages`).
- **SQLite (Desarrollo y Testing en Memoria):** `SqliteSchemaMigrator.cs` reconcilia defensivamente el esquema comprobando la existencia de la columna mediante `PRAGMA table_info("Games")` y ejecutando `ALTER TABLE "Games" ADD COLUMN "AdditionalImages" TEXT;` si no está presente, garantizando la compatibilidad total sin fallos de ejecución.
- **Canario de Migraciones:** Migración `20261008223448_AddGameAdditionalImages.cs` que eleva a 25 el recuento de migraciones de EF Core custodiado por `PostgresSchemaVerificationTests.cs`.

---

## 4. Capa de Aplicación y Auditoría

### DTOs y Contratos (`Ludeka.Application`)
- `GameDetailDto`: Expone `public IReadOnlyList<GameGalleryImageDto> AdditionalImages { get; init; } = [];`.
- `UpdateGameDetailsCommand`: Permite al cliente enviar `public List<GameGalleryImageInputDto>? AdditionalImages { get; init; }`.

### Reglas de Servicio y Auditoría (`GameEditorService`)
Al invocar `UpdateGameAsync`:
1. **Autorización:** Se exige el permiso granular `CanUploadImages` (o pertenencia a la Mesa Fundadora) si el comando incluye modificaciones en `AdditionalImages`.
2. **Detección de Cambios y Diff:** El servicio detecta si ha variado el recuento de imágenes, sus URLs o sus títulos.
3. **Registro de Auditoría:** Si la galería cambia, se registra en `GameEditLog` e `IAuditService` una entrada con el resumen: `Galería: N imágenes adicionales configuradas`.
4. **Invalidación de Caché:** Se invalida de forma inmediata la caché L1 en memoria de catálogo (`CachedCatalogService`) para que las vistas públicas sirvan la nueva información al instante.

---

## 5. Experiencia de Usuario y Componentes Blazor

### Editor Editorial (`GameEditorModal.razor`)
En la pestaña «Carátula y Galería», tras los bloques 1, 2 y 3 de imágenes fijas, se incorpora la sección «4. Galería Complementaria»:
- **Subida de Archivos Locales:** Componente `InputFile` conectado con `IMediaStorageService` para subir fotos directamente a Cloudflare R2 / almacén de medios bajo la clave de ranura `gallery-{timestamp}`.
- **Añadir por URL:** Formulario en línea para pegar una URL directa con título/pie de foto opcional.
- **Listado y Miniaturas:** Cuadrícula con previsualización en miniatura de cada fotografía adicional, indicador del título y botón de papelera para supresión individual interactiva antes de guardar los cambios.

### Visualización en Ficha Pública (`GameDetail.razor` y `GameImageCarousel.razor`)
- `GameImageCarousel.razor`: El carrusel fotográfico integra los elementos de `AdditionalImages` junto a la portada, trasera y despliegue de mesa, con contador dinámico de diapositivas (`1 / N`), navegación táctil/botones y pie de foto.
- `GameDetail.razor`: La lista calculada `AvailableHeroImages` incluye las imágenes adicionales para el visor modal a pantalla completa (lightbox con zoom y selector de diapositiva).
