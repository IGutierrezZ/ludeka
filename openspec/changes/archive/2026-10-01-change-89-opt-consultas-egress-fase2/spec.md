# Especificación Técnica: INC-89 — Saneamiento Integral de Consultas SQL Restantes, Eliminación de Egress O(N) y Blindaje de Caché de Fichas Públicas

## 1. Resumen Ejecutivo

Este incremento complementa la optimización de INC-88 abordando las consultas restantes de `SqliteGameRepository` que aún descargan tablas enteras a memoria, blindando con caché en memoria las fichas públicas de editoriales y tiendas, y ordenando los veredictos fundadores en SQL nativo en PostgreSQL.

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Filtrado SQL Nativo en `GetByPublisherAsync`
- **Problema:** En `SqliteGameRepository.GetByPublisherAsync(string publisherName, ...)`, la implementación actual hace:
  ```csharp
  var games = await scope.Context.Games.AsNoTracking().ToListAsync(ct);
  ```
  descargando la tabla `Games` completa a través de internet antes de filtrar en memoria por nombre de editorial.
- **Solución Requerida:** Empujar la condición al motor SQL:
  ```csharp
  var pattern = $"%{clean}%";
  query = query.Where(g =>
      (g.Publisher != null && EF.Functions.Like(g.Publisher, pattern)) ||
      (g.SpanishPublisher != null && EF.Functions.Like(g.SpanishPublisher, pattern)));
  ```
  Aislar los candidatos en SQL. Para editoriales regionales en la colección JSON `RegionalPublishers`, combinar la búsqueda en SQL para minimizar radicalmente la transferencia.
- **Garantía:** Nunca se descargará la tabla `Games` completa; únicamente se transferirán los registros coincidentes con la editorial.

### REQ-2: Optimización de `GetGamesPendingQualityBackfillAsync` y Conteo
- **Problema:** `GetGamesPendingQualityBackfillAsync` ejecutaba `ToListAsync()` de toda la tabla para filtrar por `Scalability.Count == 0` en RAM.
- **Solución Requerida:**
  - Acotar la consulta con paginación basada en cursor `afterBggId` (reutilizando `GetGamesCursorPagedAsync`) o limitando la proyección a `Take(limit)`.
  - En `GetGamesPendingQualityBackfillCountAsync`, proyectar exclusivamente `Id` y `Scalability` o realizar conteo sobre los registros no verificados.

### REQ-3: Blindaje de Caché L1 en Fichas de Editorial (`CachedPublisherService`)
- **Problema:** `CachedPublisherService` cachea `GetAllAsync()`, pero `GetBySlugAsync(slug)` e `GetByIdAsync(id)` llaman directamente a `PublisherService`, disparando la consulta de juegos en cada visita.
- **Solución Requerida:**
  - `CachedPublisherService` debe almacenar en `IMemoryCache` el resultado de `GetBySlugAsync(slug)` y `GetByIdAsync(id)` durante 30 minutos.
  - Las operaciones de mutación (`CreateAsync`, `UpdateAsync`, `DeleteAsync`) deben invalidar no solo la clave general de editoriales sino también las claves específicas de la editorial afectada mediante un versionador o desalojo explícito.

### REQ-4: Optimización en `SqliteFoundingVerdictRepository.GetAllAsync`
- **Problema:** `GetAllAsync` ejecuta `scope.Context.FoundingVerdicts.ToListAsync(ct)` y ordena por `CreatedAt` en memoria.
- **Solución Requerida:**
  - Si el proveedor es PostgreSQL (`IsNpgsql()`), aplicar `OrderByDescending(v => v.CreatedAt)` en la consulta SQL directamente.

---

## 3. Criterios de Aceptación y Casos de Prueba (Gherkin)

### Escenario 1: Consulta de Juegos por Editorial con Cláusula WHERE en SQL
```gherkin
Dado un catálogo con múltiples juegos de diferentes editoriales
Cuando se invoca 'GetByPublisherAsync' para una editorial específica
Entonces la consulta generada aplica un filtro SQL sobre los nombres de editorial
Y únicamente se devuelven los juegos asociados a dicha editorial
Y no se materializa la tabla de juegos completa en memoria
```

### Escenario 2: Visita Repetida a Ficha de Editorial Servida desde Caché L1
```gherkin
Dado un servicio 'CachedPublisherService' con caché en memoria vacía
Cuando se solicita 'GetBySlugAsync' por primera vez
Entonces se consulta el servicio base y se almacena el DTO en caché
Y cuando se solicita 'GetBySlugAsync' por segunda vez inmediatamente
Entonces la respuesta se sirve desde la memoria sin invocar al servicio base ni a la base de datos
```

### Escenario 3: Invalidación Reactiva de Ficha de Editorial ante Actualización
```gherkin
Dado un servicio 'CachedPublisherService' con una editorial en caché
Cuando se invoca 'UpdateAsync' para modificar dicha editorial
Entonces las entradas de caché para dicha editorial quedan invalidadas
Y la siguiente llamada a 'GetBySlugAsync' recarga la información fresca
```
