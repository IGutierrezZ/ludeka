# Incremento 142: Filtro Estricto de Novedades Maldito Games, Enriquecimiento de Imágenes, EAN y Precios en Catálogo (Maldito y Devir)

## 📌 Identificador SDD
`inc-142-maldito-filtros-backfill-catalogo`

## 🎯 Estado
⏳ En progreso

---

## 1. Contexto y Objetivos
1. **Filtro estricto de novedades de Maldito Games**:
   - Limitar la extracción a «A puntito de llegar» y «Volverán a estar disponibles en breve».
   - Excluir «Últimas novedades», «Lo que se viene» y la lectura redundante de catálogo reciente.
2. **Galería completa en Maldito Games**:
   - Extraer caja 3D (`face3d`), contraportada, mesa, EAN-13 y PVP para enriquecer las novedades inminentes.
3. **Corrección de actualización de PVP y EAN en Devir**:
   - Modificar `DevirImagesBackfillJobRunner` para asignar EAN y registrar la oferta de «Devir» con su PVP en `PurchaseLinks`.
4. **Nuevo trabajo de barrido para catálogo de Maldito Games**:
   - Implementar `maldito-images-backfill` para recorrer el catálogo general de Maldito, cruzar con juegos existentes y actualizar imágenes, EAN y oferta de compra con PVP.
5. **Ejecución local garantizada**:
   - Habilitar la ejecución limpia y desasistida desde CLI para eludir los bloqueos de Cloudflare en entornos cloud.

---

## 2. Componentes Afectados
- `src/Ludeka.Application/Contracts/IMalditoReleasesExtractor.cs`
- `src/Ludeka.Application/DTOs/EditorialReleaseDtos.cs`
- `src/Ludeka.Infrastructure/Extractors/MalditoReleasesExtractor.cs`
- `src/Ludeka.Jobs/JobNames.cs`
- `src/Ludeka.Jobs/JobRunnerServiceCollectionExtensions.cs`
- `src/Ludeka.Jobs/Runners/DevirImagesBackfillJobRunner.cs`
- `src/Ludeka.Jobs/Runners/MalditoImagesBackfillJobRunner.cs`
- `tests/Ludeka.UnitTests/Extractors/MalditoReleasesExtractorTests.cs`
- `tests/Ludeka.UnitTests/Jobs/EditorialCatalogBackfillJobTests.cs`
