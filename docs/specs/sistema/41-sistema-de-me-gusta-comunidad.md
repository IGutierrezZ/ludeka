# 41. Sistema de «Me gusta» Comunitario en Directorios y Vídeos

> **Incremento de Origen:** INC-65  
> **Alcance:** Dominio, Persistencia Dual (SQLite / PostgreSQL Supabase), Aplicación, Interfaz Web Blazor e Integración.  
> **Estado:** ✅ Archivado y Verificado (1.781 pruebas unitarias superadas al 100%).

---

## 1. Visión y Propósito Funcional

El sistema de «Me gusta» de Ludeka proporciona una mecánica de reconocimiento y valoración comunitaria interactiva, directa y determinista sobre las cuatro entidades clave del ecosistema lúdico:

1. **Editoriales (`Publisher`):** Reconocimiento a las casas editoriales y su labor editorial en el catálogo.
2. **Tiendas (`Store`):** Valoración de los comercios y puntos de venta lúdicos de confianza.
3. **Creadores de Contenido (`Creator`):** Apoyo a los divulgadores, canales y autores del hobby.
4. **Vídeos y Contenido Multimedia (`MediaItem`):** Apoyo directo a los tutoriales, reseñas, partidas completas y explicaciones rápidas de cada juego.

### Invariantes y Reglas de Negocio
- **Solo Usuarios Autenticados:** Solo los miembros registrados y con sesión activa pueden emitir o retirar votos.
- **Flujo de Invitados (Fricción Cero):** Si un visitante anónimo o invitado pulsa el botón de «Me gusta», el sistema no muestra errores hostiles ni bloquea la acción; en su lugar, lo redirige de inmediato a la pantalla de acceso (`/login?ReturnUrl=...`) preservando la URL exacta de origen para retornar automáticamente tras identificarse.
- **Toggle Idempotente:** Un usuario solo puede tener a lo sumo un voto activo por destino concreto `(UserId, TargetType, TargetId)`. Si vuelve a pulsar, el voto se retira y el contador se decrementa de forma atómica.
- **Segregación Estricta de Métricas Externas:** En el contenido multimedia, el campo existente `MediaItem.LikesCount` representa los me gusta importados de la plataforma externa (Instagram), mientras que la interacción comunitaria dentro de Ludeka se computa y expone de forma aislada como `UserLikesCount`, evitando colisiones semánticas.
- **Ordenación Determinista por Popularidad:** Los listados de directorios (Editoriales, Tiendas, Creadores) y los vídeos de cada categoría en la ficha de juego (`MultimediaHub`) se ordenan por defecto por número de me gusta comunitarios en orden descendente (`LikesCount DESC`), con criterios de desempate deterministas (alfabético por nombre o cronológico por fecha de publicación).

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Enumeración `LikeTargetType`
Ubicado en `Ludeka.Core.Enums.LikeTargetType`:
```csharp
public enum LikeTargetType
{
    Publisher = 1,
    Store = 2,
    Creator = 3,
    MediaItem = 4
}
```

### 2.2. Entidad de Dominio `UserLike`
Ubicada en `Ludeka.Core.Entities.UserLike`:
- Invariantes de construcción validados con `ArgumentException.ThrowIfNullOrWhiteSpace` y comprobación de `Guid.Empty`.
- Constructor inmutable:
  - `Guid Id` (identificador único autogenerado).
  - `string UserId` (identificador canónico de la cuenta comunitaria).
  - `LikeTargetType TargetType` (tipo de entidad votada).
  - `Guid TargetId` (identificador de la editorial, tienda, creador o medio).
  - `DateTimeOffset CreatedAt` (marca temporal UTC de emisión del voto).

---

## 3. Persistencia y Migraciones Duales (`Ludeka.Infrastructure`)

### 3.1. Contrato `IUserLikeRepository`
Define operaciones agregadas de alto rendimiento:
```csharp
public interface IUserLikeRepository
{
    Task<bool> HasUserLikedAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<bool> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<int> GetLikesCountAsync(LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> GetLikesCountsAsync(LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default);
    Task<HashSet<Guid>> GetUserLikedTargetIdsAsync(Guid userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default);
}
```

### 3.2. Modelo EF Core y Restricciones de Esquema
En `LudekaDbContext`:
- `DbSet<UserLike> UserLikes => Set<UserLike>();`
- Mapeo relacional con índice único estricto:
  ```csharp
  b.HasIndex(x => new { x.UserId, x.TargetType, x.TargetId }).IsUnique();
  b.HasIndex(x => new { x.TargetType, x.TargetId });
  ```
- Bloque 30 en `SqliteSchemaMigrator.cs` para entornos locales y tests en memoria.
- Migración EF Core PostgreSQL `20260925194347_AddUserLikes.cs` para entornos cloud.
- Sincronización completa con bloque idempotente en `docs/database/supabase_schema.sql` verificado por `SupabaseSchemaFreshnessTests`.

---

## 4. Capa de Aplicación y Servicios (`Ludeka.Application`)

### 4.1. Servicio `IUserLikeService` / `UserLikeService`
Proporciona la lógica de caso de uso para consultar estados y ejecutar alternancias (toggle) seguras:
- `GetStatusAsync(userId, targetType, targetId)`: Devuelve `LikeStatusDto(bool IsLiked, int LikesCount)`.
- `GetStatusesAsync(userId, targetType, targetIds)`: Consulta optimizada por lotes para listados.
- `ToggleLikeAsync(userId, targetType, targetId)`: Alterna el voto del usuario actual y retorna el nuevo estado sincronizado.

### 4.2. Ordenación en Directorios y Multimedia
Los servicios de consulta integran opcionalmente `IUserLikeRepository` para obtener los conteos agregados y aplicarlos a sus DTOs:
- **`PublisherService.GetAllAsync`:** Ordena por `LikesCount DESC, Name ASC`.
- **`StoreService.GetAllAsync`:** Ordena por `LikesCount DESC, Name ASC`.
- **`CreatorService.GetAllAsync`:** Ordena por `LikesCount DESC, Name ASC`.
- **`MediaService.GetGameMediaAsync`:** En cada pestaña (Cómo funciona, Tutoriales, Partidas, Reseñas), ordena los vídeos activos por `UserLikesCount DESC, PublishedAt DESC, Title ASC`.

---

## 5. Capa Web y Componente de Interfaz (`Ludeka.Web`)

### 5.1. Componente Accesible `LikeButton.razor`
Ubicado en `src/Ludeka.Web/Components/Shared/LikeButton.razor`:
- **Interactividad Reactiva:** Botón con `@onclick:stopPropagation="true"` para operar dentro de tarjetas interactivas sin disparar la navegación al elemento contenedor.
- **Redirección de Invitados:** Si el usuario no tiene sesión activa, invoca de inmediato `Navigation.ToLogin()`, redirigiendo a `/login?ReturnUrl=...`.
- **Accesibilidad WCAG 2.2 AA:**
  - `type="button"` con soporte nativo de foco por teclado (`focus-visible:ring-2 focus-visible:ring-rose-500`).
  - Atributos semánticos ARIA: `aria-pressed="true|false"` y `aria-label="Me gusta (N me gusta)"` / `aria-label="Ya no me gusta (N me gusta)"`.
  - Icono Lucide `heart` con transición de escala y relleno visual (`fill="currentColor"` cuando está activo, contorno `fill="none"` cuando no).
- **Variantes:** Modo normal para vistas de detalle y modo `Compact="true"` para tarjetas de directorios y fichas de vídeo.

### 5.2. Integración en Páginas
- `PublishersDirectory.razor` y `PublisherDetail.razor`.
- `StoresDirectory.razor` y `StoreDetail.razor`.
- `CreatorsDirectory.razor` y `CreatorDetail.razor`.
- `MultimediaHub.razor` y `MediaEmbedModal.razor`.

---

## 6. Verificación Automatizada

La implementación cuenta con cobertura integral de pruebas unitarias y de contrato en todas las capas:
- **Dominio:** `tests/Ludeka.UnitTests/Domain/UserLikeTests.cs` (7 pruebas).
- **Persistencia:** `tests/Ludeka.UnitTests/Infrastructure/SqliteUserLikeRepositoryTests.cs` (6 pruebas).
- **Frescura de Esquema:** `tests/Ludeka.UnitTests/Infrastructure/SupabaseSchemaFreshnessTests.cs`.
- **Aplicación y Ordenación:** `tests/Ludeka.UnitTests/Application/UserLikeServiceTests.cs` (8 pruebas).
- **Componente Web e Interacción:** `tests/Ludeka.UnitTests/Web/LikeButtonTests.cs` (4 pruebas).

**Total de la suite tras la verificación:** 1.781 pruebas unitarias ejecutadas al 100% en verde.
