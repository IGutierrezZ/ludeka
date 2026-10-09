# Incremento 144: Extractor de Próximos Lanzamientos de Arrakis Games, Barrido de Catálogo y Aviso de Reimpresión

## 📌 Identificador ODD / SDD
`inc-144-arrakis-releases-and-backfill`

## 🎯 Estado
⏳ En progreso

---

## 1. Contexto y Objetivos
1. **Extractor especializado de Arrakis Games (`IArrakisReleasesExtractor` / `ArrakisReleasesExtractor`)**:
   - Descarga y parseo de lanzamientos oficiales en `https://arrakisgames.com/` y `https://arrakisgames.com/noticias/`.
   - **Filtro estricto solicitado**: capturar exclusivamente **próximos lanzamientos** y **reimpresiones futuras / preventas**.
   - Descartar explícitamente «últimas novedades» que ya están disponibles en tiendas (`Ya disponible!`).
   - Enriquecer cada ítem desde su ficha oficial con imagen de alta calidad, EAN-13, PVPr, BGG ID/URL y fecha estimada.
2. **Integración en el servicio de sincronización editorial (`EditorialReleasesSyncService`)**:
   - Incorporar `Arrakis Games` al flujo periódico de sincronización oficial junto a Devir y Maldito Games.
   - Cruce determinista por EAN y título, con soporte para auto-importar desde BGG mediante el BggId extraído directamente de la ficha de Arrakis.
3. **Proceso único de barrido de catálogo (`arrakis-images-backfill`)**:
   - Nuevo job autónomo en `Ludeka.Jobs` (`JobNames.ArrakisImagesBackfill = "arrakis-images-backfill"`).
   - Recorre el catálogo de Arrakis Games (`/catalogo/` o `product-sitemap.xml`) para actualizar EAN, precios (PVP), imágenes de alta resolución y oferta de compra oficial de Arrakis Games en `PurchaseLinks`.
   - Optimizado para ejecución local contra la réplica de base de datos de producción.
4. **Aviso contextual de reimpresión en ficha de juego**:
   - En `StoreOffersCard` y `GameDetail`, cuando todas las tiendas estén sin stock (o como aviso oficial destacado), si el juego cuenta con una reimpresión anunciada (`WeeklyRelease` con `IsReprint == true`), mostrar una tarjeta informativa visual:
     *«🔔 Reimpresión anunciada: [Fecha] por [Editorial]»*.

---

## 2. Componentes Afectados
- `src/Ludeka.Application/Contracts/IArrakisReleasesExtractor.cs`
- `src/Ludeka.Application/DTOs/EditorialReleaseDtos.cs`
- `src/Ludeka.Application/Features/Releases/EditorialReleasesSyncService.cs`
- `src/Ludeka.Infrastructure/Extractors/ArrakisReleasesExtractor.cs`
- `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs`
- `src/Ludeka.Jobs/JobNames.cs`
- `src/Ludeka.Jobs/JobRunnerServiceCollectionExtensions.cs`
- `src/Ludeka.Jobs/Runners/ArrakisImagesBackfillJobRunner.cs`
- `src/Ludeka.Web/Components/Shared/StoreOffersCard.razor`
- `src/Ludeka.Web/Components/Pages/GameDetail.razor`
- `tests/Ludeka.UnitTests/Infrastructure/ArrakisReleasesExtractorTests.cs`
- `tests/Ludeka.UnitTests/Jobs/ArrakisImagesBackfillJobRunnerTests.cs`
- `tests/Ludeka.UnitTests/Web/StoreOffersCardTests.cs`
