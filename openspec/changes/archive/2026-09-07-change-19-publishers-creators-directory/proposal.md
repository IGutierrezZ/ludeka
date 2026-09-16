# Propuesta de Cambio: change-19-publishers-creators-directory

## 1. Resumen Ejecutivo
El Incremento 19 dota a Ludeka del **Directorio Integral de Editoriales, Creadores y Tiendas de Juegos de Mesa**, permitiendo a la comunidad explorar el ecosistema lúdico hispanohablante a través de fichas ricas con biografías, redes sociales, catálogos asociados y programas de fidelidad. Adicionalmente, este incremento conecta orgánicamente el directorio con el motor de búsqueda en tiempo real de YouTube (INC-14), convirtiendo los canales oficiales registrados en el padrón de referencia algorítmica de mayor relevancia.

---

## 2. Justificación y Valor para el Usuario
1. **Identidad y Descubrimiento Editorial:** Los jugadores podrán consultar el catálogo completo de editoriales como Devir, Maldito Games, Tranjis Games o Asmodee, acceder a sus sitios web y canales oficiales de reglas o directos.
2. **Reconocimiento a Autores y Diseñadores:** Las fichas de creador ponen en valor a figuras de la autoría lúdica (Elizabeth Hargrave, Klaus Teuber, Uwe Rosenberg, etc.) y divulgadores del hobby, mostrando todas sus obras presentes en Ludeka y sus perfiles de BGG y redes.
3. **Ecosistema de Tiendas Lúdicas:** Los usuarios podrán ubicar tiendas físicas u online (Zacatrus!, Cuarto de Juegos, Dungeon Marvels, etc.), conocer sus ubicaciones, ventajas y ver de un vistazo qué juegos de su ludoteca o lista de deseos tienen stock u ofertas activas.
4. **Foco Audiovisual Dinámico para YouTube:** Todos los canales de YouTube registrados en el directorio se inyectan automáticamente en el motor de YouTube (INC-14), de modo que los tutoriales, unboxings y partidas de canales oficiales se priorizan algorítmicamente en las búsquedas en vivo de cada ficha de juego.
5. **Autogestión para Moderadores:** Los moderadores y miembros de la Mesa Fundadora dispondrán de herramientas web reactivas (modales de alta y edición inmediata con generación automática de slug) para mantener el directorio al día.

---

## 3. Alcance de la Propuesta

### 3.1 Dominio (`Ludeka.Core`)
- Enums: `SocialPlatform` (`Website`, `YouTube`, `Instagram`, `Twitter`, `Discord`, `Facebook`, `BoardGameGeek`, `Twitch`, `TikTok`, `Other`), `StoreType` (`PhysicalOnly`, `OnlineOnly`, `Hybrid`).
- Value Object: `SocialNetworkLink` (Plataforma, URL, Handle opcional, Título descriptivo).
- Entidades: `Publisher`, `Creator`, `Store` con identificador único Guid, Slug canónico, metadatos descriptivos y lista propia de enlaces a redes sociales.

### 3.2 Aplicación (`Ludeka.Application`)
- Interfaces de repositorio: `IPublisherRepository`, `ICreatorRepository`, `IStoreRepository`.
- Contratos de servicio: `IPublisherService`, `ICreatorService`, `IStoreService`.
- Abstracción de canales dinámicos: `IChannelDirectoryProvider` que extrae todos los canales de YouTube registrados para consumo de `IChannelFocusProvider`.
- DTOs para listados, detalles, altas y modificaciones.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- Persistencia en SQLite con EF Core 10 en `LudekaDbContext` (`DbSet<Publisher>`, `DbSet<Creator>`, `DbSet<Store>`), con índices por Slug y almacenamiento JSON nativo de `SocialLinks`.
- Implementaciones de repositorio: `SqlitePublisherRepository`, `SqliteCreatorRepository`, `SqliteStoreRepository`.
- Sembrado inicial en `DirectorySeeder` (o integración con `CatalogSeeder`) con editoriales canónicas (Devir, Maldito, Tranjis, Asmodee, Stonemaier), autores referentes y tiendas asociadas a las ofertas existentes.
- Integración dinámica en `ChannelFocusProvider` para combinar canales estáticos y canales de base de datos.

### 3.4 Presentación Web (`Ludeka.Web`)
- Directorios generales:
  - `/editoriales`: listado con buscador reactivo por nombre/país y tarjetas editoriales.
  - `/creadores` (y `/autores`): listado con buscador reactivo y badges de juegos asociados.
  - `/tiendas`: listado con filtros por tipo (física/online/híbrida) y buscador por localidad.
- Fichas de detalle:
  - `/editoriales/{slug}`: información editorial, redes y juegos publicados en el catálogo de Ludeka.
  - `/creadores/{slug}`: biografía, perfil BGG, redes sociales y obras diseñadas.
  - `/tiendas/{slug}`: ficha de tienda, ubicación, enlaces oficiales y juegos con ofertas activas.
- Navegación cruzada:
  - Enlaces desde `GameDetail.razor` (autor y editorial).
  - Enlaces desde `StoreOffersCard.razor` hacia la ficha de la tienda.
- Modales de administración y edición para roles `Moderator` y `FoundingTeam`.
- Enlaces de navegación en cabecera y pie de página de `MainLayout.razor`.

---

## 4. Criterios de Aceptación Clave
1. Un visitante puede navegar a `/editoriales`, `/creadores` y `/tiendas`, filtrar y visualizar las fichas con su respectiva información y enlaces a redes.
2. Desde la ficha de un juego (`/juegos/{slug}`), hacer clic en el nombre del diseñador o la editorial redirige a su ficha oficial si existe en el directorio.
3. En la ficha de una tienda (`/tiendas/{slug}`), se listan los juegos que tienen ofertas vinculadas a esa tienda.
4. Cualquier canal de YouTube vinculado a una editorial, creador o tienda en la base de datos es reconocido automáticamente por `IChannelFocusProvider` como canal de referencia priorizado con bono algorítmico.
5. Los moderadores autenticados pueden crear y editar editoriales, autores y tiendas mediante modales reactivos con validaciones.
6. La suite completa de pruebas unitarias supera el 100% de los tests en verde sin regresiones.
