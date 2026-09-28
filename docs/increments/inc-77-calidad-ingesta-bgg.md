# INC-77: Saneamiento de Calidad en Ingesta BGG: Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-28 · **Fecha de Cierre:** Pendiente  
> **Rama de Trabajo:** `inc/calidad-ingesta-bgg`  
> **Worktree:** `C:\repos\ludeka-wt\calidad-ingesta-bgg`  
> **Dependencias:** INC-41/INC-53 (Staging y Ranks Dump BGG), INC-71/INC-73 (Calidad de Datos BGG y Enriquecimiento)  
> **Especificación Viva:** [`01. Catálogo y Ficha Inteligente`](../specs/sistema/01-catalogo-y-fichas.md) y [`27. Ingesta Masiva de Catálogo BGG`](../specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md)  
> **Metodología:** Spec-Driven Development (SDD) con verificación exhaustiva de suite unitaria  

---

## 1. Contexto y Diagnóstico del Incidente

Durante la ejecución en producción del backfill retroactivo sobre los ~4.300 juegos de catálogo (`ExecuteBackfillCatalogQualityBatchAsync`), el proceso se detuvo en los últimos 37 títulos debido a una excepción no controlada, revelando además tres desviaciones sistémicas en la calidad y fidelidad de los datos ingeridos desde BoardGameGeek:

1. **Excepción de año en títulos milenarios o sin fecha (`ArgumentOutOfRangeException`):**
   - En `Game.UpdateCatalogInformation` existe una restricción artificial (`yearPublished < 1900 || yearPublished > 2100`).
   - El constructor de `Game` no la posee, pero `SqliteGameRepository.UpdateAsync` invoca `UpdateCatalogInformation` para sincronizar las propiedades de la entidad, provocando el fallo fatal en juegos clásicos de la historia de la humanidad (Ajedrez con año 1475, Go con -2200, Backgammon con -3000, Senet con -3500, Mancala con -700, Crokinole 1876, etc.) o juegos sin año formal (año 0).

2. **Homogeneización a `Eurogame` en todo el catálogo:**
   - En la promoción masiva a catálogo (`BggMassIngestionService.PromoteReadyToCatalogBatchAsync`) se asignó `style: GameStyle.Eurogame` de forma fija para todos los juegos promovidos.
   - En `BggXmlParser.InferGameDna`, la heurística solo consultaba `boardgamecategory` y `boardgamemechanic`, omitiendo por completo los subdominios de BGG (`boardgamesubdomain`, donde BGG clasifica *Thematic Games*, *Party Games*, *Strategy Games*, *Abstract Games*, etc.).
   - Además, el backfill de calidad no actualizaba el estilo (`GameStyle`), dejando congelada la asignación errónea.

3. **Colapso de duración estimada a 15 min/jugador (caso Dune):**
   - En `BggMassIngestionService.ProcessPendingDetailsBatchAsync` y en la persistencia de staging, se guardaba `EstimatedPerPlayerMinutes` dentro del campo `playingTimeMinutes` (el cual debe almacenar la duración total de la partida).
   - Al dividirse posteriormente por el número máximo de jugadores en la promoción y el backfill (`playingTimeMinutes / maxPlayers`), 15 / 4 = 3 min, lo que disparaba la cláusula de salvaguarda `Math.Max(15, ...)` y fijaba ~15 min/jugador en juegos pesados o medios de 60 a 120 minutos (como Dune o Dune: Imperium).
   - En la consulta directa a BGG, si `game.Duration.MinMinutes != 0`, la duración ni siquiera se refrescaba.

4. **Pérdida de escalabilidad real ("Best") y rango artificial 1-4 (caso Brass: Birmingham):**
   - En `SqliteGameRepository.UpdateAsync`, **nunca se copiaban `Scalability`, `Sleeves` ni `RegionalPublishers`** hacia la entidad rastreada por EF Core.
   - Al llamarse a `existing.UpdateCatalogInformation(...)`, este método ejecutaba `existing.AdjustScalability(minPlayers, maxPlayers)` sobre una colección vacía, generando entradas sintéticas genéricas de 1 a 4 con estatus `Recommended` y 0 votos comunitarios.
   - Esto provocó la pérdida de los votos `Best`, `Recommended` y `NotRecommended`, destruyendo el semáforo comunitario y haciendo que `CalculateIdealPlayerCountText()` devolviera "Ideal: 1-4 jugadores" en lugar de "Ideal: 3-4 jugadores" para Brass: Birmingham.

---

## 2. Alcance de la Solución (INC-77)

### Componente 1: Dominio y Entidad `Game`
- Ampliar el rango de `yearPublished` en `Game.UpdateCatalogInformation` a un rango histórico válido (`-5000` a `DateTime.UtcNow.Year + 10`).
- Incorporar métodos de actualización selectiva y enriquecimiento: `UpdateStyle(GameStyle style, ConfrontationType? confrontation = null)` o integración limpia en `UpdateCatalogInformation`.

### Componente 2: Parser BGG y Heurística de ADN Lúdico
- Extender `BggXmlParser.InferGameDna` para procesar etiquetas `<link type="boardgamesubdomain" ...>`:
  - `Thematic Games` y `Wargames` ➔ `GameStyle.Ameritrash`.
  - `Party Games` y `Children's Games` ➔ `GameStyle.PartyGame`.
  - `Abstract Games` ➔ `GameStyle.FillerAbstract`.
  - `Strategy Games` ➔ `GameStyle.Eurogame`.
- Enriquecer la categorización secundaria por mecánicas y temas para mayor fidelidad.

### Componente 3: Pipeline de Duración e Ingesta
- Asegurar que `playingTimeMinutes` en staging preserve siempre la duración total (`MaxMinutes` o `PlayingTime`).
- Recalcular `EstimatedPerPlayerMinutes` de forma consistente (`totalPlayingTime / maxPlayers`), respetando una escala mínima realista según peso.
- En `ExecuteBackfillCatalogQualityBatchAsync`, actualizar incondicionalmente la duración cuando BGG devuelva información válida.

### Componente 4: Persistencia Íntegra en Repositorio (`SqliteGameRepository.UpdateAsync`)
- Corregir `SqliteGameRepository.UpdateAsync` para transferir fielmente `game.Scalability`, `game.Sleeves`, `game.SpanishPublisher` y `game.RegionalPublishers` a la entidad gestionada por el contexto.
- Evitar que `AdjustScalability` machaque la escalabilidad comunitaria rica con entradas sintéticas sin votos.
- Ajustar el criterio de selección de `GetGamesPendingQualityBackfillAsync` para considerar pendientes aquellos títulos cuya escalabilidad tenga 0 votos o esté sin enriquecer.

---

## 3. Criterios de Aceptación
1. **Zero ArgumentOutOfRangeException:** Los juegos históricos (< 1900) y sin año formal (0) se actualizan y persisten sin lanzar excepciones.
2. **Fidelidad de Estilo:** Juegos temáticos y party games no se etiquetan ciegamente como Eurogame; Dune, Nemesis o Codenames reciben su estilo apropiado.
3. **Duración Real:** Juegos de 60-120 minutos reflejan una estimación coherente (ej. ~25-30 min/jugador en Dune Imperium) y no 15 min planos.
4. **Semáforo Comunitario Intacto:** Brass: Birmingham y el resto de títulos conservan sus votos `BestVotes`, `RecommendedVotes` y estatus `MustPlay` tras el guardado en base de datos.
5. **Verificación de Suite:** 100% de la suite de pruebas unitarias en verde sin regresiones.
