# Tareas ODD — INC-139: Preservación de Espacios en Buscador de Catálogo y Guarda Anti-Reentrada en Sincronización de URL

- [x] **1. Test & Core (`CatalogFilterState`)**
  - [x] 1.1 Crear prueba unitaria en `CatalogHybridContractTests.cs` verificando que `ToQueryDictionary()` no recorte espacios finales o compuestos en `SearchTerm` (ej. `"Ark Nova "`, `"got "`).
  - [x] 1.2 Modificar `ToQueryDictionary()` en `src/Ludeka.Application/DTOs/CatalogFilterState.cs` para asignar `SearchTerm` íntegro sin `.Trim()`.

- [x] **2. Web & Ciclo de Vida (`Home.razor`)**
  - [x] 2.1 Incorporar en `Home.razor` el campo `_lastSyncedUri`.
  - [x] 2.2 En `SyncUrl()`, guardar la URI absoluta de destino en `_lastSyncedUri` y evitar llamadas redundantes si la URI actual ya coincide.
  - [x] 2.3 En `OnLocationChanged`, añadir la guarda comparando `PathAndQuery` con `_lastSyncedUri` para descartar eventos generados por navegaciones internas.
  - [x] 2.4 Actualizar o añadir prueba de contrato en `CatalogPaginationContractTests.cs` o `CatalogHybridContractTests.cs` validando la presencia de la guarda anti-reentrada.

- [x] **3. Verificación y Suite Completa**
  - [x] 3.1 Ejecutar `dotnet test` y verificar 100% verde sin regresiones (2.752 superadas).
