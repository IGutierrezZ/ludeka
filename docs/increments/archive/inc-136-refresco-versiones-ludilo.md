# INC-136: Refresco de Versiones BGG de Novedades, Soporte Editorial Lúdilo y Saneamiento de Catálogo

**Estado:** ✅ Archivado  
**Fecha:** 2026-10-08  
**Tipo:** Feature / Bugfix / Data Integrity  
**Alcance:** Core, Application, Infrastructure, Seeding, Tests  
**Rama:** `inc/refresco-versiones-ludilo`

---

## 1. Contexto y Justificación del Problema

Tras resolver en INC-135 la contaminación producida por descriptores genéricos de tirada y acrónimos multilingües (como `"ENG/GER/FRE/SPA edition"` en *Queen Alice*), se constata una problemática complementaria en el catálogo de Ludeka:
Juegos de publicación reciente (ej. año 2025 o 2026) como *Got Five!* (BggId `453526`, de Yoann Levet) figuran en el catálogo de producción exclusivamente con su título original en inglés y con una editorial inadecuada (*Asmodee Ibérica* en lugar de *Lúdilo*), a pesar de que en BoardGameGeek existe y está dada de alta formalmente la versión oficial en español:
`"Código 5 - Spanish edition (2026)"` de la editorial **Lúdilo**.

### Causas Raíz Diagnosticadas:
1. **Desfase temporal en el registro de ediciones en BGG:**
   Los juegos novedosos se dan de alta en BGG inicialmente con su ficha en inglés y sus primeras ediciones internacionales (ej. holandesa, francesa o alemana). La edición en español de editoriales locales (en este caso Lúdilo) suele añadirse a BGG semanas o meses más tarde.
2. **Carencia de re-sincronización de snapshots con versiones existentes:**
   El mecanismo de sincronización de versiones (`GetBggIdsMissingVersionsAsync`) únicamente consulta a BGG aquellos juegos cuyos snapshots crudos carecen por completo de la clave JSON `versions`. Si un juego ya se almacenó con versiones internacionales, el sistema lo considera completo y jamás vuelve a consultar a BGG para comprobar si se ha registrado una nueva edición en español.
3. **Omisión de Lúdilo en el catálogo editorial:**
   [`RegionalPublisherMatcher.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/RegionalPublisherMatcher.cs) no contiene registrada a la editorial española **Lúdilo** (sello histórico fundamental en juegos de lógica, infantiles y deducción). En consecuencia, el sistema asignó por descarte de distribución global a *Asmodee Ibérica*.
4. **Indistinguibilidad sintáctica en el saneador pasivo:**
   A diferencia de *Queen Alice* (donde el título contenía cadenas reconocibles como `edition` o acrónimos con barras), en *Got Five!* el campo `SpanishTitle` coincidía con el título original inglés (`Got Five!`), resultando indistinguible de un juego no traducido sin contrastar contra la información viva de versiones.

---

## 2. Objetivos del Incremento

1. **Incorporación Oficial de Lúdilo:** Registrar `"Lúdilo"`, `"Ludilo"` y sus variantes comerciales en [`RegionalPublisherMatcher.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/RegionalPublisherMatcher.cs) con país `"ES"`, nombre oficial `"Lúdilo"` y slug `"ludilo"`.
2. **Saneamiento Determinista y Prioritario del Catálogo:** Ampliar [`CatalogDataSanitizer.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Seeding/CatalogDataSanitizer.cs) para asegurar que *Got Five!* (BggId `453526`) actualice su título a `"Código 5"` y su editorial a `"Lúdilo"`.
3. **Mecanismo de Detección y Refresco de Versiones para Novedades:** En [`IBggRawSnapshotRepository`](file:///f:/repos/Ludeka/src/Ludeka.Application/Contracts/IBggRawSnapshotRepository.cs) y [`SqliteBggRawSnapshotRepository`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteBggRawSnapshotRepository.cs), proveer consulta para detectar snapshots de juegos recientes o con título idéntico al original que carecen de versión en español en el snapshot, habilitando su re-sincronización controlada.
4. **Verificación Estricta con TDD:** Desarrollar pruebas unitarias completas en `RegionalPublisherMatcherTests`, `CatalogDataSanitizerTests`, `BggRawSnapshotParserVersionsTests` y sincronización de snapshots sin regresiones.
