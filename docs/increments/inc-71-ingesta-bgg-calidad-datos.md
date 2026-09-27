# INC-71: Ingesta BGG — Calidad de Datos, Escalabilidad, Fundas, Tiempos y Huella en Mesa

> **Estado:** ✅ Completado (Listo para PR)  
> **Fecha de Inicio:** 2026-09-27 · **Fecha de Cierre:** 2026-09-27  
> **Rama de Trabajo:** `inc/ingesta-bgg-calidad-datos`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-bgg-calidad-datos`  
> **Pruebas Automatizadas:** 1.949 pruebas unitarias en verde (+11 pruebas netas)  
> **Dependencias:** INC-26 (Fundas), INC-41/INC-46 (Staging y BggMassIngestion), INC-53 (Volcado Ranks)  
> **Especificación Viva:** `docs/specs/sistema/01-catalogo-juegos.md`  

---

## 1. Contexto y Detección

Tras completar la ingesta masiva de más de 4.000 juegos desde BoardGameGeek (BGG), se detectó que los títulos ingestados presentaban inconsistencias severas en catálogo:
1. **Escalabilidad vacía:** Ningún juego contaba con el semáforo comunitario de escalabilidad («A cuántos jugadores funciona bien»), mostrando «Sin datos de escalabilidad» en la ficha.
2. **Fundas no importadas:** A pesar de que BGG XML API2 devuelve los enlaces `boardgamecardsleeve`, los juegos ingestados carecían de especificaciones de fundas de cartas.
3. **Huella en mesa uniforme:** Todos los juegos tenían asignado el valor fijo `TableFootprint.StandardTable`, independientemente de ser juegos de cartas compactos o wargames de gran despliegue.
4. **Tiempos de partida y por jugador distorsionados:** La duración máxima se calculaba artificialmente como `PlayingTimeMinutes * 1.5` y el tiempo por jugador como una división rígida sin considerar el rango real de BGG.

## 2. Diagnóstico Técnico y Evidencia de Código

- En `BggXmlParser.cs`, tanto `ParseScalability` como `BggSleeveParser.ParseSleeves` funcionan y extraen los datos correctamente.
- Sin embargo, en `BggCatalogStagingItem.cs`:
  - No existían campos para almacenar ni `ScalabilityJson` ni `SleevesJson`.
  - En `BggMassIngestionService.ProcessPendingDetailsBatchAsync`, `item.MarkFetched(...)` omitía estos datos y pasaba erróneamente `fetchedGame.Description` a `rawXml`.
  - En `PromoteReadyToCatalogBatchAsync`, la instanciación de `new Game(...)` no pasaba ni escalabilidad ni fundas, fijaba `footprint: TableFootprint.StandardTable` a mano y calculaba tiempos con multiplicadores arbitrarios.
  - Al actualizar juegos existentes, solo refrescaba URLs de imágenes (`UpdateMediaUrls`), dejando escalabilidad, fundas y huella en mesa permanentemente vacías.

## 3. Requerimientos del Maintainer

1. Corregir el pipeline de ingesta masiva para capturar y persistir la escalabilidad comunitaria, las fundas de cartas, los tiempos reales de juego (mínimo, máximo, estimado por jugador) y la huella en mesa inferida (vía IA o categorización heurística).
2. Aplicar estas correcciones de forma retroactiva sobre los juegos ya existentes en el catálogo mediante un proceso de enriquecimiento/backfill.
3. Garantizar que la preparación para el filtrado por más de 100 opiniones opere sin descartar ni duplicar los juegos ya existentes.

## 4. Criterios de Aceptación

1. `BggCatalogStagingItem` almacena `ScalabilityJson`, `SleevesJson`, tiempos reales (`MinPlayTimeMinutes`, `MaxPlayTimeMinutes`) y huella en mesa inferida (`InferredFootprint`).
2. `BggMassIngestionService` traslada escalabilidad y fundas a las entidades `Game` tanto en nuevas promociones como actualizando registros que las tengan vacías.
3. El cálculo de duración (`GameDuration`) respeta los valores reales de BGG y computa tiempos por jugador lógicos según la escala de la mesa.
4. La huella en mesa se infiere analíticamente (apoyada en IA y heurística de mecánicas/estilos) en lugar de fijarse uniformemente a `StandardTable`.
5. Se incluye un mecanismo o método de enriquecimiento (`BackfillCatalogQualityAsync`) para actualizar los títulos del catálogo ya existentes.
6. 100% de la suite de pruebas en verde, con tests unitarios específicos que cubran cada corrección.
