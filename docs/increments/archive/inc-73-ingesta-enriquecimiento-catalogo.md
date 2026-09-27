# INC-73: Ampliación de Ingesta Masiva BGG (>100 opiniones), Descarte de Duplicados, Enriquecimiento Integral y Localización Territorial Multipaís de Editoriales y Títulos

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-27 · **Fecha de Cierre:** Pendiente  
> **Rama de Trabajo:** `inc/ingesta-enriquecimiento-catalogo`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-enriquecimiento-catalogo`  
> **Pruebas Automatizadas:** 1.945 pruebas unitarias en verde al inicio (línea base de INC-72)  
> **Dependencias:** INC-41/INC-53 (Staging y Ranks Dump BGG), INC-26/INC-66 (Fundas de Cartas), INC-29/INC-54 (Directorio y Países), INC-59/INC-72 (Filtros, Dureza y Huella en Mesa)  
> **Especificación Viva:** `docs/specs/sistema/01-catalogo-juegos.md` y `docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`  
> **Metodología:** Implementación directa por capas sin ciclo estricto TDD, con verificación automatizada de la suite completa al cierre  

---

## 1. Contexto y Objetivos del Incremento

Tras la consolidación del catálogo inicial (~4.000 títulos más destacados con umbral >1.000 opiniones) y la mejora de los filtros y fichas de juego en INC-72, se requiere una segunda fase masiva orientada tanto a la expansión del fondo editorial como a la calidad profunda de los metadatos de mesa y la localización territorial:

### Bloque A: Ingesta Masiva y Metadatos de Calidad
1. **Ampliación del umbral de ingesta BGG (>100 opiniones):**
   - El umbral previo (`minUsersRated = 1000`) dejaba fuera grandes joyas lúdicas de nicho, juegos recientes con alta valoración y producciones nacionales y latinoamericanas.
   - Se ajusta el umbral por defecto a 100 opiniones (`minUsersRated = 100`), permitiendo absorber decenas de miles de títulos preservando un estándar mínimo de tracción y fiabilidad comunitaria.

2. **Blindaje anti-duplicados y actualización incremental:**
   - La ingesta masiva del nuevo lote debe convivir limpiamente con los ~4.000 títulos ya existentes en las bases de datos (SQLite en desarrollo, PostgreSQL en producción).
   - El pipeline comprueba la existencia previa por `BggId` (tanto en staging como en la tabla principal `Games`):
     - Si el título es nuevo: se inserta y promueve con normalidad.
     - Si ya existe: se descarta de la inserción para no provocar errores de clave única y se actualiza de forma incremental enriqueciendo campos previamente vacíos (escalabilidad, fundas, huella, tiempos, editoriales regionales).

3. **Enriquecimiento de escalabilidad comunitaria (Best / Recommended):**
   - Eliminar de raíz las fichas con «Sin datos de escalabilidad».
   - Parsear y mapear la encuesta comunitaria de BGG (`poll name="suggested_numplayers"`).
   - Si la encuesta carece de votos o es insuficiente, aplicar un fallback determinista y heurístico asignando recomendaciones coherentes (`Recommended` para el rango base, `MustPlay` si `min == max`).

4. **Ingesta y normalización de fundas (Sleeves):**
   - Capturar las especificaciones de fundas devueltas por los enlaces `boardgamecardsleeve` de la API de BGG.
   - Persistir las dimensiones en el registro de juego para alimentar la guía de fundas y enlaces de compra contextuales de INC-66 sin dejar la sección vacía.

5. **Corrección de tamaño en mesa (TableFootprint):**
   - Subsanar la asignación por defecto uniforme (`StandardTable`) en el catálogo actual.
   - Determinar analíticamente la huella real (`SmallTable`, `StandardTable`, `TableMonster`) combinando categorías BGG (ej. *Card Game*, *Wargame*, *Miniatures*, *Economic*), peso del juego y duración, complementado con síntesis IA.

6. **Consistencia de tiempos de juego y por jugador:**
   - Corregir el cálculo distorsionado de `Duration`: extraer `MinPlayTimeMinutes`, `MaxPlayTimeMinutes` reales de BGG y calcular de forma lógica `EstimatedPerPlayerMinutes` en función de la media de jugadores y escala temporal.

7. **Proceso de enriquecimiento retroactivo (Backfill):**
   - Proceso por lotes para enriquecer el catálogo actual de ~4.000 títulos mediante estrategia *Staging-First*.

### Bloque B: Localización Territorial Multipaís (Editoriales y Títulos en la Comunidad Ludeka)
8. **Padrón de Editoriales para Todos los Países Soportados por Ludeka:**
   - Soporte a los 7 países de `CountryCatalog`: España, México, Argentina, Chile, Colombia, Perú y Uruguay.
   - Ampliación de `seed-directory.json` incorporando las editoriales clave de cada territorio:
     - México: Devir México, Fractal Juegos México, Taj Mahal Games, Kokonem Games.
     - Argentina: Bureau de Juegos, El Troquel, Maldón, Ruibal, Pulga Escapista, ToyCo, Tinkuy.
     - Chile: Fractal Juegos, Devir Chile, Ludoismo, Dentro de la Caja.
     - Colombia: Devir Colombia, Borrasca Juegos.
     - Perú: Malabares Juegos.
     - Uruguay: Bicho Canasto.
     - España: 46 editoriales canónicas consolidadas.
9. **Detección y Mapeo Multipaís de Editoriales (`RegionalPublishers` y `SpanishPublisher`):**
   - Cruzar los múltiples enlaces `<link type="boardgamepublisher">` de BGG contra el padrón multipaís mediante `RegionalPublisherMatcher`.
   - Clasificar las editoriales asociadas por país en `RegionalPublishers` (y `SpanishPublisher` para España), preservando `Publisher` para el sello original internacional.
   - En la ficha de juego (`GameDetail.razor`): mostrar la editorial local correspondiente al país del usuario (`GetPublisherForCountry`) con enlace directo a `/directorios/editoriales/{slug}`, e indicar la editorial original si difiere.
   - En el directorio de editoriales (`PublisherDetail.razor`): los juegos asociados a una editorial de cualquier país soportado se listan en su ficha de catálogo.
10. **Títulos Comerciales Multipaís (`SpanishTitle`, `LocalizedTitles` vs `OriginalTitle`):**
    - Extraer y preservar el título comercial en español y las variantes comerciales según el país del usuario (`GetTitleForCountry`).
11. **Búsquedas y Filtros:**
    - Permitir encontrar juegos tanto por su título adaptado como original, y por sus editoriales en cualquiera de los países soportados en `SearchAsync` y `GetByPublisherAsync`.

---

## 2. Criterios de Aceptación

1. **Umbral >100 opiniones:** El volcado de clasificación y las opciones de ingesta admiten juegos con `UsersRated >= 100`.
2. **Cero colisiones o duplicados:** La ingestión y promoción maneja registros existentes de forma idempotente sin duplicar filas en `Games` ni lanzar violaciones de unicidad.
3. **Escalabilidad siempre presente:** Todo juego importado o enriquecido dispone de al menos una entrada de escalabilidad con estado semafórico (`MustPlay`, `Recommended`, `NotRecommended`).
4. **Fundas importadas:** Cuando BGG informa fundas, estas se persisten en `Game.Sleeves` con dimensiones válidas y cantidad.
5. **Huella en mesa diferenciada:** Juegos compactos se catalogan como `SmallTable`; wargames y miniaturas como `TableMonster`; juegos de tablero estándar como `StandardTable`.
6. **Tiempos coherentes:** `MinMinutes <= MaxMinutes` y `EstimatedPerPlayerMinutes` derivado de forma proporcional y realista.
7. **Editoriales multipaís asociadas:** Juegos licenciados identifican sus editoriales en España, México, Argentina, Chile, Colombia, Perú y Uruguay, y se vinculan a sus páginas en el directorio.
8. **Directorio multipaís enriquecido:** `seed-directory.json` cuenta con editoriales para los países soportados y lista sus juegos.
9. **Títulos comerciales localizados:** Las fichas presentan el nombre en español y referencian el título original.
10. **Backfill operativo:** Se dispone de método de backfill por lotes para enriquecer el catálogo actual.
11. **Suite de pruebas:** 100% de pruebas unitarias en verde (>1.950 tests).
