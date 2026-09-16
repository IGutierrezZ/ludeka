# Exploración: change-19-publishers-creators-directory (Incremento 19: Directorio de Editoriales, Creadores y Tiendas con Foco Audiovisual)

## 1. Estado Actual de la Solución y Análisis de Brechas (Gap Analysis)

### 1.1 Situación Actual de Editoriales y Diseñadores en el Catálogo
- **Ficha de Juego (`GameDetail.razor`):**
  - Actualmente, cada juego almacena `Publisher` (string) y `Designer` (string).
  - En la interfaz de `GameDetail.razor`, estos campos se presentan como texto plano no interactivo:
    `Diseñado por <strong class="text-[var(--text-primary)]">@Game.Designer</strong> • Publicado en España por <strong class="text-[var(--text-primary)]">@Game.Publisher</strong>`.
  - **Brecha:** No existe una entidad de primera clase ni páginas dedicadas para navegar la bibliografía o catálogo de una editorial ni las obras de un autor. Los usuarios no pueden hacer clic para descubrir más juegos de la misma casa o creador, ni consultar su biografía, historia, web oficial o redes sociales.

### 1.2 Situación Actual de Tiendas y Afiliados (INC-11)
- **Enlaces de Compra (`GamePurchaseLink`):**
  - En el Incremento 11 se introdujo la colección `PurchaseLinks` en la entidad `Game` y el componente visual `StoreOffersCard.razor`.
  - Las ofertas contienen `StoreName`, `AffiliateUrl`, `Price`, `InStock` y `Badge`.
  - **Brecha:** Las tiendas solo existen como una cadena de texto arbitraria incrustada en el enlace de compra del juego. No existe una ficha central de la tienda donde se detallen su naturaleza (tienda física, online o híbrida), ciudad, dirección, ventajas comunitarias (programas de fidelidad, eventos de club) o enlaces a sus canales oficiales de unboxing y tutoriales.

### 1.3 Situación Actual del Foco de Canales de YouTube (INC-14)
- **Padrón Estático de Canales (`ChannelFocusProvider`):**
  - En el Incremento 14 se introdujo `IChannelFocusProvider` y su implementación concreta `ChannelFocusProvider.cs` con una lista estática en memoria de 29 canales hispanohablantes.
  - **Brecha:** El padrón de canales de YouTube es estático y requiere recompilar el código fuente para añadir o corregir canales de editoriales, divulgadores o tiendas. No existe integración dinámica entre las entidades del directorio y el proveedor de foco algorítmico de YouTube.

### 1.4 Herramientas de Gestión y Moderación
- **Rol de Moderador / Mesa Fundadora:**
  - El sistema dispone de infraestructura de roles (`Moderator`, `FoundingTeam`) y modales reactivos en `GameDetail.razor` y `/moderacion/reportes`.
  - **Brecha:** No existen formularios ni modales para dar de alta o actualizar editoriales, creadores y tiendas desde la interfaz web sin manipular directamente la base de datos.

---

## 2. Alternativas Técnicas y Decisiones Arquitectónicas

### 2.1 Modelo de Entidades vs. Normalización Relacional Fuerte en `Game`
- **Alternativa A (Clave foránea estricta `PublisherId` y `DesignerId` en `Game`):**
  - Obligaría a migrar todas las tablas existentes, rompería retrocompatibilidad con importaciones automáticas de BGG XMLAPI2 (donde los nombres vienen en texto libre) y requeriría crear registros obligatorios antes de guardar cualquier juego.
- **Alternativa B (Entidades ricas independientes con enlace flexible por Slug/Nombre) [DECISIÓN RECOMENDADA]:**
  - Mantener `Publisher` y `Designer` como strings legibles en `Game` para compatibilidad total con BGG XMLAPI2 y datos preexistentes.
  - Crear entidades de primera clase `Publisher`, `Creator` y `Store` con su propio `Slug` canónico único.
  - Resolver la navegación cruzada de forma inteligente: si existe una entidad registrada cuyo nombre o slug coincida (normalizado), se genera enlace activo (`/editoriales/{slug}`, `/creadores/{slug}`, `/tiendas/{slug}`); de lo contrario, se renderiza texto enriquecido con opción para que los moderadores creen la ficha en 1 clic.
  - En las fichas de entidad, consultar los juegos asociados mediante consultas optimizadas (`GetGamesByPublisherAsync`, `GetGamesByDesignerAsync`, `GetGamesByStoreAsync`).

### 2.2 Modelado de Redes Sociales
- **Alternativa A (Columnas independientes en la tabla para cada red: `TwitterUrl`, `YoutubeUrl`, `InstagramUrl`, etc.):**
  - Inflexible: si surge una nueva red (ej. Bluesky, TikTok, Mastodon), requiere alterar el esquema de base de datos.
- **Alternativa B (Value Object `SocialNetworkLink` mapeado como JSON nativo en EF Core 10) [DECISIÓN RECOMENDADA]:**
  - Permite múltiples canales (por ejemplo, canal de YouTube principal y canal secundario, cuenta de Twitch, Instagram, web corporativa).
  - Integración nativa con `OwnsMany(..., b => b.ToJson())` en SQLite / EF Core 10.

### 2.3 Proveedor Dinámico de Foco de Canales (`IChannelDirectoryProvider`)
- **Decisión:**
  - Crear una abstracción `IChannelDirectoryProvider` que extraiga automáticamente todos los enlaces de YouTube (URLs o handles) configurados en las editoriales, creadores y tiendas persistidas en la base de datos.
  - Actualizar `ChannelFocusProvider` para combinar el padrón base curado con los canales dinámicos registrados en el directorio, asegurando que cualquier canal nuevo reciba de inmediato el bono de relevancia algorítmica en búsquedas de tutoriales y partidas (INC-14).

### 2.4 Navegación y Usabilidad (Mobile-First y Anti-Plantillas)
- **Vistas de Directorio:**
  - `/editoriales`: Directorio con buscador reactivo, tarjetas editoriales con logo, país, recuento de juegos y enlaces a redes.
  - `/creadores` (y `/autores`): Directorio con buscador por nombre, país/nacionalidad, avatar y juegos diseñados.
  - `/tiendas`: Directorio con selector por tipo (`Física`, `Online`, `Híbrida`), buscador por localidad y badges de fidelidad/envío.
- **Vistas de Detalle:**
  - `/editoriales/{slug}`, `/creadores/{slug}`, `/tiendas/{slug}` con diseño editorial limpio, sin relleno genérico, con microtextos en español y catálogo cruzado.
- **Modales de Moderación:**
  - Botones visibles solo para `Moderator` y `FoundingTeam` con modales Blazor reactivos para crear y editar entidades al vuelo sin recargar la página.

---

## 3. Conclusión de la Exploración
El diseño propuesto preserva al 100% la compatibilidad con los 395 tests existentes, añade un valor inmenso al ecosistema lúdico hispano, potencia el motor de YouTube (INC-14) con datos vivos y sienta las bases para el directorio integral de Ludeka.
