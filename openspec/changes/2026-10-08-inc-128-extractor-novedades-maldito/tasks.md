# Tareas de Implementación: INC-128 Extractor Determinista de Maldito Games

## Tarea 1: Migración Defensiva de SQLite para `IsMonthOnly`
- [x] 1.1 Añadir `IsMonthOnly` a la tabla `WeeklyReleases` en `SqliteSchemaMigrator.cs` para evitar errores locales de SQLite en consultas de novedades.

## Tarea 2: Rediseño del Extractor de Maldito Games (`MalditoReleasesExtractor.cs`)
- [x] 2.1 Implementar extracción seccional por bloques: `Últimas novedades`, `A puntito de llegar`, `Volverán a estar disponibles en breve` (marcando `IsReprint = true`) y `Lo que se viene`.
- [x] 2.2 Implementar parseador robusto de fechas en castellano (`"d 'de' MMMM"`, `"yyyy"`, etc.) calculando año actual/siguiente según contexto.
- [x] 2.3 Implementar extracción de EAN de 13 dígitos desde la URL de imagen del producto en el CDN.
- [x] 2.4 Decodificar entidades HTML en títulos y limpiar espacios redundantes.
- [x] 2.5 Corregir URL del catálogo si procede (`product_list_dir=desc`).

## Tarea 3: Limpieza y Fallback Heurístico en `EditorialReleasesSyncService.cs`
- [x] 3.1 Limpiar coletillas comerciales (`- Edición Kickstarter`, `- Senderos`, etc.) antes de la búsqueda en BGG si la búsqueda directa no arroja coincidencias.
- [x] 3.2 Asegurar que el cruce por EAN extraído de la imagen del CDN tenga máxima prioridad.

## Tarea 4: Pruebas Unitarias y Verificación
- [x] 4.1 Actualizar y expandir `MalditoReleasesExtractorTests.cs` con casos de prueba para cada sección de portada, formatos de fecha, EAN en URL de imagen y decodificación de entidades.
- [x] 4.2 Añadir prueba de integración unitaria en `EditorialReleasesSyncServiceTests.cs` con productos de Maldito Games.
- [x] 4.3 Ejecutar la suite completa de pruebas unitarias (`dotnet test`) asegurando 0 errores.
