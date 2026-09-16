# Reporte de Verificación: change-19-publishers-creators-directory (Incremento 19)

- **Fecha:** 07 de Septiembre de 2026
- **Incremento:** 19 - Directorio de Editoriales, Creadores y Tiendas con Redes Sociales y Foco Audiovisual para YouTube
- **Estado:** ✅ **SUPERADO (100% de Pruebas en Verde: 413/413)**

---

## 1. Resumen de Pruebas Automatizadas

Se ejecutó la suite completa de pruebas unitarias y de integración sobre la solución `Ludeka.sln` (.NET 10 / C# 13):

```text
Serie de pruebas para C:\repos\Ludeka\tests\Ludeka.UnitTests\bin\Debug\net10.0\Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error: 0, Superado: 413, Omitido: 0, Total: 413, Duración: 3 s - Ludeka.UnitTests.dll (net10.0)
```

### Detalle de Pruebas Incorporadas en el Incremento 19:

1. **Dominio (`DirectoryDomainTests.cs`):**
   - `Publisher_Constructor_ValidatesAndInitializesCorrectly`: Valida nombre, slug autogenerado a partir del nombre, país, sitio web, logotipo y lista de redes sociales.
   - `Publisher_InvalidData_ThrowsArgumentException`: Rechaza nombres vacíos y formatos de slug inválidos.
   - `Creator_Constructor_ValidatesAndInitializesCorrectly`: Valida inicialización de creadores/autores, biografía, redes sociales y slug.
   - `Store_Constructor_ValidatesAndInitializesCorrectly`: Valida inicialización de tiendas (físicas, online, híbridas), ubicación y enlaces.
   - `SocialNetworkLink_Validations`: Valida el Value Object `SocialNetworkLink`, normalización de URLs por plataforma (YouTube, Instagram, X/Twitter, Twitch, TikTok, Facebook, Bluesky, Mastodon, Podcast) y detección de identificador/handle.

2. **Capa de Aplicación y Servicios (`DirectoryServicesTests.cs`):**
   - `PublisherService_GetAllAndGetBySlug`: Comprueba listado ordenado, filtrado por país y búsqueda insensible a mayúsculas/minúsculas.
   - `PublisherService_CreateAndEdit_WithModerator`: Valida alta y edición de editoriales por moderadores y fundadores, impidiendo el acceso a usuarios estándar.
   - `CreatorService_GetCatalogGamesByCreator`: Verifica la asociación cruzada con el catálogo para obtener los juegos diseñados por un autor.
   - `StoreService_GetActiveOffersForStore`: Verifica el cruce bidireccional entre la tienda del directorio y las ofertas de afiliación registradas en `StoreOffersCard` (INC-11).
   - `ChannelDirectoryProvider_ExtractsYouTubeChannels`: Verifica la extracción unificada de canales de YouTube registrados en editoriales, creadores y tiendas para alimentar el padrón dinámico de creadores de contenido.

3. **Infraestructura y Persistencia SQLite (`SqliteDirectoryRepositoriesTests.cs`):**
   - `SqlitePublisherRepository_CrudOperations`: Inserción, consulta por slug, actualización y eliminación de editoriales en SQLite.
   - `SqliteCreatorRepository_CrudOperations`: Inserción y consulta de autores con enlaces de redes serializados en JSON (`OwnsMany(..., b => b.ToJson())`).
   - `SqliteStoreRepository_CrudOperations`: Inserción y consulta de tiendas físicas y online con soporte para índice único por slug.
   - `SqliteSchemaMigrator_CreatesDirectoryTables`: Verifica la idempotencia de la migración defensiva para tablas `Publishers`, `Creators` y `Stores`.

4. **Integración con Foco Audiovisual (`DynamicChannelFocusProviderTests.cs`):**
   - `ChannelFocusProvider_IncludesDirectoryYouTubeChannels`: Comprueba que `ChannelFocusProvider` (INC-14) recupera dinámicamente los canales de YouTube registrados en el directorio y los incorpora al radar algorítmico sin romper el singleton gracias a `IServiceScopeFactory`.

---

## 2. Criterios de Aceptación Cumplidos (Gherkin)

- [x] **Escenario: Exploración del directorio de editoriales:**
  - El usuario accede a `/editoriales`, visualiza la cuadrícula editorial con logotipos, país de origen, cantidad de juegos editados en catálogo y enlaces a redes oficiales.
  - Al pulsar en una editorial (ej. `/editoriales/devir-iberia`), visualiza su ficha con sinopsis, redes y la lista de juegos publicados en Ludeka.
- [x] **Escenario: Ficha de creador y vinculación desde el catálogo:**
  - En la ficha de un juego (ej. Wingspan en `GameDetail.razor`), el autor "Elizabeth Hargrave" es un enlace clicable que dirige a `/creadores/elizabeth-hargrave`.
  - En `/creadores/elizabeth-hargrave`, se visualizan sus redes, biografía y todos sus títulos catalogados.
- [x] **Escenario: Ficha de tienda y vinculación con ofertas de afiliación:**
  - En `StoreOffersCard.razor`, el nombre de tiendas registradas (ej. "Zacatrus!", "Jugamos Otra") enlaza a su ficha `/tiendas/{slug}`.
  - En `/tiendas/{slug}`, se visualiza la dirección física (si aplica), web oficial, redes y la lista de ofertas activas en el catálogo.
- [x] **Escenario: Canal de YouTube de una entidad alimenta el buscador audiovisual:**
  - Cuando una editorial o creador tiene configurado su enlace de YouTube en el directorio, `IChannelFocusProvider` incluye automáticamente el canal en las búsquedas en vivo de YouTube (INC-14) para dar visibilidad prioritaria a reseñas y tutoriales en español.
- [x] **Escenario: Moderación y gobernanza de entidades:**
  - Los moderadores y fundadores disponen de modales de creación y edición (`PublisherEditModal`, `CreatorEditModal`, `StoreEditModal`) con validación de accesibilidad WCAG 2.2 AA y refresco reactivo.

---

## 3. Conclusión

El Incremento 19 ha superado satisfactoriamente todas las pruebas de regresión, integración y accesibilidad, cumpliendo estrictamente los estándares de Clean Architecture y la metodología Spec-Driven Development de Ludeka.
