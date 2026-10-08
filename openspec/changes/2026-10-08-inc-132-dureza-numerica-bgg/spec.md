# Especificación Técnica: INC-130 — Dureza Numérica BGG, Extrapolación de Complejidad y Ordenación Precisa

> **ID del Cambio:** `2026-10-08-inc-130-dureza-numerica-bgg`  
> **Incremento Asociado:** INC-130  
> **Rama de Trabajo:** `inc/dureza-numerica-bgg`  
> **Estado:** ⏳ Especificación formalizada  

---

## 1. Resumen Ejecutivo
Este incremento introduce el valor continuo y numérico de dureza/complejidad de BoardGameGeek (`BggWeight`, en rango de 1.00 a 5.00) en el núcleo de Ludeka. Esto permite sustituir la ordenación discreta aproximada por una ordenación real en catálogo (`ComplexityAsc` y `ComplexityDesc`), unificar la extrapolación a categorías cualitativas (`Light`, `Medium`, `Heavy`) con umbrales nítidos y enriquecer la ficha técnica visual del juego con el dato numérico exacto.

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Atributo `BggWeight` en el Dominio (`Game`)
* **Descripción:** La entidad [`Game`](file:///f:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs) debe almacenar `BggWeight` como un valor decimal nullable (`double?`).
* **Invariantes:**
  * Si no es nulo, debe estar acotado entre 1.00 y 5.00 (`Math.Clamp(val, 1.0, 5.0)` o redondeado a 2 decimales).
  * Si el valor es 0.0 o negativo, debe normalizarse a `null` (indicando ausencia de votos de peso en BGG).
* **Métodos:**
  * Añadir `BggWeight` al constructor principal y de EF Core.
  * Añadir método `UpdateBggWeight(double? weight)` para actualizar este dato durante sincronizaciones, imports o backfills.

### REQ-2: Extrapolación Canónica de Complejidad
* **Descripción:** Definir un método canónico en dominio (o helper estático) `CalculateComplexity(double? bggWeight, GameStyle style, int maxMinutes, int communityAge) -> GameComplexity`:
  * Si `bggWeight.HasValue && bggWeight.Value > 0`:
    * `bggWeight < 2.20` $\rightarrow$ `GameComplexity.Light` (Ligero / Familiar).
    * `bggWeight >= 2.20 && bggWeight < 3.25` $\rightarrow$ `GameComplexity.Medium` (Medio / Intermedio).
    * `bggWeight >= 3.25` $\rightarrow$ `GameComplexity.Heavy` (Duro / Experto).
  * Si `bggWeight` es nulo o 0:
    * Se aplica la regla heurística previa por estilo, duración y edad como mecanismo de contingencia (*fallback*).

### REQ-3: Extracción en `BggXmlParser`
* **Descripción:** En [`BggXmlParser.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/BggXmlParser.cs), dentro de `ParseStatistics` (o extracción equivalente), leer:
  ```xml
  <statistics page="1">
    <ratings>
      ...
      <averageweight value="2.4632" />
    </ratings>
  </statistics>
  ```
* **Comportamiento:** Si el elemento `<averageweight>` existe y contiene un valor parseable $> 0$, asignar `Math.Round(val, 2)` a `bggWeight`; en caso contrario `null`. Pasarlo al constructor de `Game`.

### REQ-4: Persistencia Dual y Migración EF Core
* **Descripción:** Crear migraciones para SQLite (`SqliteLudekaDbContext`) y PostgreSQL (`PostgreSqlLudekaDbContext`) que incorporen la columna `BggWeight` (`REAL` / `double precision`, nullable) en la tabla `Games`.

### REQ-5: Backfill sin Red desde `BggRawSnapshots`
* **Descripción:** Desarrollar un método en `SqliteGameRepository` / `ICatalogQualityService` (o runner dedicado) capaz de procesar los registros de `BggRawSnapshots`:
  * Inspeccionar el JSON crudo en `RawJson`, extraer `averageweight` (por ejemplo, en la ruta `statistics.ratings.averageweight.@value` o `#text`).
  * Si existe y es válido, actualizar el `Game` correspondiente sin realizar peticiones HTTP a la API externa de BGG.

### REQ-6: Ordenación Real por Dureza en Repositorio
* **Descripción:** En [`SqliteGameRepository.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs):
  * **`GameSortOrder.ComplexityAsc`:**
    * Juegos con `BggWeight` ordenados ascendentemente (menor dureza primero: 1.0 $\rightarrow$ 5.0).
    * Los juegos sin peso numérico se ordenan por su complejidad heurística estimada o al final, con desempate por `BggRank` y `BggRating`.
  * **`GameSortOrder.ComplexityDesc`:**
    * Juegos con `BggWeight` ordenados descendentemente (mayor dureza primero: 5.0 $\rightarrow$ 1.0).
    * Los juegos sin peso se sitúan al final, con desempate por `BggRank` y `BggRating`.
  * **Filtrado por complejidad (`criteria.Complexities`):** Los filtros de `Light`, `Medium` y `Heavy` utilizan la extrapolación canónica unificada.

### REQ-7: Ficha Técnica Editorial (`GameDetail.razor`)
* **Descripción:**
  * En el eyebrow y en el bloque de ficha técnica / ADN lúdico de [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor), si el juego dispone de `BggWeight`, mostrar el formato exacto:
    * Ej: `2.4 / 5` o `Dureza: 2.4/5 · Ligero`.
  * Si no tiene `BggWeight`, mostrar la etiqueta extrapolada de contingencia.
