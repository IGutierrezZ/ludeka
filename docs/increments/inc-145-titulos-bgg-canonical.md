# INC-145: Priorización de canonicalname en Versiones BGG y Saneamiento Sistemático de Títulos de Catálogo

**Estado:** ⏳ En progreso  
**Fecha:** 2026-10-09  
**Tipo:** Bugfix / Data Integrity / Parser Optimization  
**Alcance:** Application, Infrastructure, Seeding, Tests  
**Rama:** `inc/titulos-bgg-canonical`

---

## 1. Contexto y Justificación del Problema

Al revisar la ficha de *Pandemic Legacy: Season 2* (`/juegos/pandemic-legacy-season-2`, BggId `221107`), se constató que la aplicación mostraba como título oficial en español **`"Z-Man Spanish edition"`** y como editorial *Asmodee Ibérica*, mientras que la carátula 3D obtenida del catálogo de Devir mostraba claramente el logo de **Devir** y el título oficial **`"Pandemic Legacy: Segunda temporada"`**.

### Causas Raíz Diagnosticadas:
1. **Omisión de `canonicalname` en `BggRawSnapshotParser`:**
   En BoardGameGeek, cada subárbol de versión (`<item type="boardgameversion">`) dispone de:
   - `<name type="primary">`: Etiqueta física o descriptor de tirada de la versión (ej. `"Z-Man Spanish edition"`, `"Devir Spanish edition"`, `"Iberian edition"`).
   - `<canonicalname>`: Título traducido oficial, limpio y legítimo correspondiente a esa versión (ej. `"Pandemic Legacy: Segunda temporada"`, `"Beacon Patrol"`, `"The White Castle Duel"`, `"Código 5"`).
   El parser leía únicamente el campo `name`, ignorando por completo `canonicalname`.
2. **Huecos en `IsGenericEditionTitle`:**
   - La lista de editoriales conocidas contenía `devir`, por lo que `"Devir Spanish edition"` fue descartada (`Title = null`), pero **no incluía `z-man` o `zman`**. Al llevar además un guion, se saltaba la comprobación de longitud corta de palabras, resultando en que `"Z-Man Spanish edition"` fue clasificado erróneamente como un título legítimo no genérico.
   - Otros descriptores como `"Iberian edition"` (*Beacon Patrol*, *Piña Coladice*), `"CAT/ENG/ITA/POR/SPA edition"` o `"Print & Play edition"` tampoco eran reconocidos, ensuciando más de 1.000 títulos en catálogo.
3. **Filtro restrictivo en nombres alternativos de raíz:**
   `ResolveSpanishTitleFromRootNames` descartaba cualquier título alternativo que no contuviera explícitamente la palabra `"español"`, `"spanish"` o `"castellano"`, ignorando traducciones comerciales legítimas.

---

## 2. Objetivos del Incremento

1. **Priorizar `canonicalname` en `BggRawSnapshotParser`:** Extraer prioritariamente el título limpio de `canonicalname` de la versión española en BGG antes de evaluar `name`.
2. **Robustecer `IsGenericEditionTitle`:** Incorporar sellos editoriales (`z-man`, `lúdilo`, etc.), regiones (`iberian`, `chilean`, etc.), códigos multilingües y formatos de tirada (`pnp`, `cube box`).
3. **Saneamiento Prioritario y Sistemático:** Actualizar `CatalogDataSanitizer` para reparar de forma garantizada BggId `221107` a `"Pandemic Legacy: Segunda temporada"` (editorial `"Devir"`), y detectar y sanear candidatos con `"iberian"` o títulos genéricos restantes.
4. **Verificación Estricta con TDD:** Suite completa de pruebas unitarias en `BggRawSnapshotParserVersionsTests` y `CatalogDataSanitizerTests`, asegurando 100% verde sin regresiones.
