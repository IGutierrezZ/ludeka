# INC-139: Preservación de Espacios en Buscador de Catálogo y Guarda Anti-Reentrada en Sincronización de URL

## 1. Contexto y Problema Detectado

Al escribir términos compuestos con espacios en el buscador del catálogo editorial (`/catalogo`, implementado en `src/Ludeka.Web/Components/Pages/Home.razor`), tras la pausa del debounce (250 ms), el componente se refresca y elimina el espacio que el usuario acababa de teclear. Si el usuario intenta escribir "Ark Nova", al pulsar el espacio tras "Ark", el componente recorta el valor a "Ark", provocando que al continuar escribiendo resulte en "ArkNova", además de disparar una recarga duplicada del catálogo.

### Causas Raíz Técnicas Identificadas:
1. **Recorte al serializar parámetros en `CatalogFilterState.cs`:** En el método `ToQueryDictionary()`, `q["q"] = SearchTerm.Trim();` recorta los espacios iniciales o finales antes de generar los query parameters para la URL (`/catalogo?q=got`).
2. **Reentrada asíncrona de SignalR en Blazor Server:** En `Home.razor`, el método `SyncUrl()` llama a `Navigation.NavigateTo(targetUrl, replace: true)`. En Blazor Interactive Server, `NavigateTo` envía un mensaje JS al navegador cliente y retorna inmediatamente. El bloque `finally { _isInternalNavigation = false; }` se ejecuta de inmediato. Cuando el navegador cliente procesa la URL y notifica de vuelta el cambio de ruta vía SignalR al servidor, `OnLocationChanged(object? sender, LocationChangedEventArgs e)` se invoca cuando `_isInternalNavigation` ya vuelve a ser `false`.
3. **Sobreescritura del estado y doble consulta:** Al considerar que la navegación fue externa (del usuario en el navegador), `OnLocationChanged` ejecuta `ReadQueryParameters()` y un segundo `LoadCatalogAsync()`. `ReadQueryParameters()` lee el parámetro `q` recortado de la URL (`"got"`), sobreescribe `_searchTerm` y vuelve a repintar el componente, eliminando el espacio del `<input>`.

---

## 2. Objetivos del Incremento

1. **Preservación de Espacios en `CatalogFilterState.ToQueryDictionary()`:**
   - Evitar el recorte artificial `.Trim()` en `SearchTerm` al construir el diccionario de parámetros de URL cuando el término contiene texto significativo (`!string.IsNullOrWhiteSpace(SearchTerm)`).
   - Mantener el término literal del usuario (e.g., `"Ark "` o `"got "`) para que la URL preserve la intención de escritura.
   - Constatar que la búsqueda en base de datos (`SqliteGameRepository.SearchAsync`) y la clave de caché (`CachedCatalogService`) sigan aplicando su recorte higiénico propio sin afectar al estado de interfaz.

2. **Guarda Anti-Reentrada en Sincronización de URL (`Home.razor`):**
   - Registrar la última URI absoluta/ruta y parámetros sincronizada internamente (`_lastSyncedUri`).
   - En `OnLocationChanged`, comparar la ruta y consulta (`PathAndQuery`) del evento entrante con `_lastSyncedUri`. Si coinciden, descartar el evento de forma temprana evitando llamadas redundantes a `ReadQueryParameters()` y `LoadCatalogAsync()`.
   - Garantizar que las navegaciones reales (botones Atrás/Adelante del navegador o clics a `/catalogo` desde el menú) sigan procesándose correctamente al no coincidir con `_lastSyncedUri`.

3. **Pruebas de Regresión y Contrato:**
   - Pruebas unitarias en `CatalogHybridContractTests.cs` que verifiquen la serialización y preservación de espacios en `SearchTerm`.
   - Pruebas de contrato en `HomeCatalogMarkupContractTests` o tests unitarios verificando la presencia de la guarda `_lastSyncedUri` y anti-reentrada en `Home.razor`.

---

## 3. Plan de Tareas (TDD)

- [ ] **Tarea 1 (Test & Core):** Añadir prueba unitaria en `CatalogHybridContractTests.cs` comprobando que `ToQueryDictionary` preserve los espacios en `SearchTerm` y corregir `CatalogFilterState.cs`.
- [ ] **Tarea 2 (Web & LifeCycle):** Implementar en `Home.razor` la guarda `_lastSyncedUri` en `SyncUrl()` y `OnLocationChanged`, evitando reentrada y doble carga.
- [ ] **Tarea 3 (Verificación):** Ejecutar la suite completa de pruebas unitarias (`dotnet test`), verificar 0 regresiones y documentar en el catálogo de incrementos.
