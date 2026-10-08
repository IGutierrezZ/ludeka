# Propuesta: INC-130 — Dureza Numérica BGG, Extrapolación de Complejidad y Ordenación Precisa

## Metadatos
- **Fecha:** 2026-10-08
- **Incremento:** INC-130
- **Rama:** `inc/dureza-numerica-bgg`
- **Slug:** `dureza-numerica-bgg`
- **Estado:** En curso (ODD / SDD)

## Contexto y Motivación
En Ludeka, la complejidad o dureza de un juego se ha venido calculando exclusivamente de forma aproximada mediante una regla heurística en memoria (`SqliteGameRepository.CalculateComplexity` y `GameDetail.GetComplexityShortText`). Dicha regla mapea los juegos a solo tres valores discretos (`Light`, `Medium`, `Heavy`) basándose en la duración máxima, edad recomendada y estilo.

Sin embargo:
1. **Pérdida de ordenación real:** Al ordenar el catálogo por "Menor dureza" o "Mayor dureza" (`GameSortOrder.ComplexityAsc` y `ComplexityDesc`), todos los juegos de una misma categoría (por ejemplo, cientos de juegos en `Medium`) quedan empatados a nivel de complejidad, ordenándose secundariamente por ranking BGG sin reflejar diferencias reales de dificultad (un juego de 2.22 se trata exactamente igual que uno de 3.19).
2. **Disponibilidad de datos desaprovechada:** La API XML2 de BoardGameGeek suministra el campo `<averageweight value="X.XX" />` dentro de `<statistics><ratings>`, e incluso en Ludeka contamos con la tabla satélite `BggRawSnapshots` donde los documentos JSON crudos ya contienen esta información.
3. **Experiencia de usuario enriquecida:** Los aficionados a los juegos de mesa valoran enormemente conocer la puntuación decimal de peso sobre 5.0 (ej. *Wingspan* 2.46/5, *Terraforming Mars* 3.26/5, *Brass: Birmingham* 3.88/5), además de la etiqueta cualitativa (*Ligero*, *Medio*, *Duro*).

## Objetivos
1. **Propiedad en Dominio:** Incorporar `BggWeight` (`double?`, 1.00 a 5.00) en la entidad `Game`.
2. **Extrapolación Canónica Unificada:** Centralizar la conversión de `BggWeight` a `GameComplexity` en un único método de dominio con umbrales nítidos (< 2.20 Ligero, 2.20 - 3.25 Medio, ≥ 3.25 Duro) y fallback a la heurística previa si no hay peso BGG registrado.
3. **Extracción en Ingesta:** Parsear `<averageweight>` en `BggXmlParser` y almacenar su valor en `Game`.
4. **Migración Dual EF Core:** Añadir la columna nullable `BggWeight` en SQLite y PostgreSQL.
5. **Backfill sin consumo de red:** Crear una rutina que recorra `BggRawSnapshots` y actualice `BggWeight` en `Game` directamente a partir del JSON crudo existente.
6. **Ordenación Matemática Continua:** Modificar `SqliteGameRepository` para que `ComplexityAsc` y `ComplexityDesc` ordenen por el valor numérico decimal de `BggWeight`.
7. **Presentación Editorial en Ficha:** Mostrar el peso decimal en `GameDetail.razor` junto a la etiqueta cualitativa en el bloque de ADN lúdico y eyebrow.

## No Objetivos (Out of Scope)
- No se elimina el enum `GameComplexity`, ya que los filtros facetados del catálogo (`dureza=light,medium,heavy`) y la frase conversacional seguirán operando con estas categorías familiares para el usuario.
- No se realizan peticiones masivas a la API externa de BGG; los datos se obtienen de los snapshots ya capturados o de nuevas sincronizaciones estándar.
