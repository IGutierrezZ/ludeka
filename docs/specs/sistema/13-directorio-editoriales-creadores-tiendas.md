# 13. Directorio de Editoriales, Creadores y Tiendas con Redes Sociales y Foco Multimedia

## 1. Visión General y Propósito
El directorio lúdico de Ludeka constituye el mapa del ecosistema hispanohablante de juegos de mesa, articulando de forma interconectada tres tipos de entidades fundamentales:
1. **Editoriales (`Publisher`):** Firmas responsables de la publicación y localización de títulos en España y Latinoamérica (ej. Devir Iberia, Maldito Games, Tranjis Games, Asmodee Ibérica, TCG Factory, Arrakis Games, Zacatrus Ediciones, SD Games, Doit Games, Primigenia Juegos, 2Tomatoes, GDM, Salt & Pepper, Ludonova, Corvus Belli, etc.).
2. **Creadores de Contenido y Divulgadores (`Creator`):** Divulgadores, canales audiovisuales, podcasters y medios especializados en español (ej. Sergio de Análisis Parálisis, Meepletopía, El Agujero de Hobbit, Mesa de Guerra, Rincón del Meeple, Frikiguías, Océano de Juegos, 221B Juegos, etc.).
3. **Tiendas Lúdicas (`Store`):** Comercios físicos, en línea e híbridos especializados en juegos de mesa (ej. Zacatrus!, Jugamos Otra, Dungeon Marvels, 4Dados, Tablerum, Mathom Store, Cuarto de Juegos, JugarXJugar, Crash Comics, Kaburi, Dracotienda, Metropolis Comics, etc.).

Además de ofrecer una experiencia editorial de descubrimiento y fichas individuales ricas en enlaces a redes sociales oficiales (YouTube, Instagram, Twitter/X, Discord, Twitch, TikTok, BGG, Podcast), el directorio actúa como el **padrón algorítmico dinámico para el foco de búsqueda audiovisual en YouTube (INC-14)** y el radar social de novedades.

---

## 2. Modelo de Dominio e Invariantes (`Ludeka.Core`)

### 2.1 Enumerados y Value Objects
- **`SocialPlatform`**: Plataformas soportadas: `Website`, `YouTube`, `Instagram`, `Twitter`, `Discord`, `Facebook`, `BoardGameGeek`, `Twitch`, `TikTok`, `Other`.
- **`StoreType`**: Clasificación del canal de venta: `PhysicalOnly`, `OnlineOnly`, `Hybrid`.
- **`SocialNetworkLink`**: Value object inmutable que almacena `Platform`, `Url`, `Handle` (ej. `@AnalisisParalisis`) y `Title`. Incluye lógica de normalización y extracción del identificador del canal/perfil.

### 2.2 Entidades
- **`Publisher.cs`**: Nombre, slug único autogenerado o asignado, país, ciudad, descripción/historial editorial, logotipo, web oficial y colección `SocialLinks`.
- **`Creator.cs`**: Nombre, slug único, nacionalidad, biografía, fotografía/avatar, ID de persona BGG, web personal y colección `SocialLinks`.
- **`Store.cs`**: Nombre, slug único, tipo (`StoreType`), ciudad, dirección física, descripción de servicios, logotipo, web de compras, código de afiliado para ingresos compartidos, indicador de programa de fidelidad, lista de países de envío y colección `SocialLinks`.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

- Contratos de repositorio: [`IPublisherRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IPublisherRepository.cs), [`ICreatorRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/ICreatorRepository.cs), [`IStoreRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IStoreRepository.cs) y ampliación de [`IGameRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IGameRepository.cs).
- **Servicio de Carga y Reconciliación:**
  - [`IDirectorySeederService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IDirectorySeederService.cs): Contrato de alto nivel para la siembra e idempotencia del padrón nacional, devolviendo métricas estructuradas en `DirectorySeedResultDto`.
- **Servicios de negocio:**
  - [`PublisherService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Directory/PublisherService.cs): Consultas paginadas/filtradas, obtención de ficha por slug, catálogo asociado y alta/edición restringida a moderadores y fundadores.
  - [`CreatorService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Directory/CreatorService.cs): Fichas de divulgadores con cruce directo a vídeos y contenido asociado.
  - [`StoreService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Directory/StoreService.cs): Fichas de comercios vinculadas en tiempo real con las ofertas de compra activas registradas en `StoreOffersCard` (INC-11).
  - [`ChannelDirectoryProvider`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Directory/ChannelDirectoryProvider.cs): Implementa `IChannelDirectoryProvider` consolidando todos los canales de YouTube registrados en el directorio.

---

## 4. Persistencia, Padrón Canónico y Mecanismos de Carga (`Ludeka.Infrastructure` & `Ludeka.Jobs`)

### 4.1 Persistencia EF Core
- `LudekaDbContext`: DbSets para `Publishers`, `Creators` y `Stores`, con serialización JSON nativa para `SocialLinks` mediante `OwnsMany(..., b => b.ToJson())`.
- `SqliteSchemaMigrator`: Creación idempotente y defensiva de tablas `Publishers`, `Creators` y `Stores` con índices únicos por `Slug` e índices de búsqueda por `Name`.

### 4.2 Padrón Canónico Embebido (`seed-directory.json` — INC-54)
El padrón nacional exhaustivo reside en `src/Ludeka.Infrastructure/Seeding/seed-directory.json` configurado como recurso incrustado (`EmbeddedResource`):
- **46 Editoriales de España:** Cobertura de prácticamente la totalidad de firmas editoriales del país con enlaces web, logos y redes sociales.
- **37 Tiendas Especializadas:** Top 35 comercios de España + 2 de referencia internacional, con indicación de modalidad, ubicación y programa de fidelidad.
- **35 Creadores de Contenido:** Divulgadores referentes de YouTube, Instagram y blogs en español con sus @handles y canales oficiales.

### 4.3 Motor de Siembra e Idempotencia (`DirectorySeeder.cs` & `DirectorySeederService.cs`)
- **Lectura desacoplada:** Deserializa desde el recurso incrustado en assembly con fallback al sistema de archivos local.
- **Reconciliación aditiva no destructiva:** Si un registro ya existe por `Slug`, actualiza únicamente campos ausentes o vacíos (`LogoUrl`, `WebsiteUrl`, `City`, `Description`, `SocialLinks`), preservando ediciones manuales previas.
- **Purga de diseñadores legados (INC-31):** Retiene la purga estricta de autores internacionales de la tabla `Creators` para concentrar la entidad en divulgadores de contenido audiovisual.
- **Aislamiento de DbContext:** `DirectorySeederService` se apoya en `IDbContextFactory<LudekaDbContext>` para garantizar ejecuciones seguras en componentes interactivos Blazor y runners CLI concurrentes.

### 4.4 Mecanismos de Ejecución
1. **CLI en Segundo Plano (`Ludeka.Jobs`):**
   - Runner `SeedDirectoryJobRunner` registrado como `seed-directory` en `JobNames.SeedDirectory`.
   - Adquisición de lease mediante `IJobExecutionCoordinator` y ventana temporal por segundo.
   - Ejecución: `dotnet run --project src/Ludeka.Jobs -- seed-directory`.
2. **Interfaz Web (Blazor):**
   - Panel de Administración (`CatalogQueueAdmin.razor`): Tarjeta interactiva con botón "Sincronizar Padrón Completo", indicador de carga y resumen de entidades creadas/actualizadas.
   - Directorios Públicos (`PublishersDirectory.razor`, `StoresDirectory.razor`, `CreatorsDirectory.razor`): Botón "Sincronizar Padrón" restringido a miembros fundadores y moderadores.

---

## 5. Vistas Blazor y Navegación Cruzada (`Ludeka.Web`)

- **Vistas Principales y Detalle:**
  - `/editoriales` y `/editoriales/{Slug}`: Directorio y ficha de editorial con lista de juegos publicados.
  - `/creadores` y `/creadores/{Slug}`: Directorio y ficha de creador con biografía, redes sociales y contenidos.
  - `/tiendas` y `/tiendas/{Slug}`: Directorio y ficha de tienda con ofertas activas cruzadas de afiliación.
- **Navegación Cruzada:**
  - En `GameDetail.razor`, los créditos de Diseñador y Editorial son enlaces directos a sus fichas.
  - En `StoreOffersCard.razor`, los nombres de las tiendas enlazan a su ficha oficial.
  - Menú de navegación principal y pie de página en `MainLayout.razor`.
- **Herramientas de Moderación:**
  - Modales reactivos `PublisherEditModal.razor`, `CreatorEditModal.razor` y `StoreEditModal.razor` con protección de rol (`Moderator` o `FoundingTeam`), autogeneración de slug, gestión de redes dinámicas y cumplimiento WCAG 2.2 AA.
