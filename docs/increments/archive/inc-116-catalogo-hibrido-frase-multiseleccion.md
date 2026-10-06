# INC-116: Catálogo Híbrido (Frase Interactiva Mad-Lib, Panel Multiselección y Chips Activos)

## Estado
✅ Verificado (Fase 4 del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/catalogo-hibrido-frase-multiseleccion`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
El catálogo (`Home.razor` en `/catalogo`) debe conjugar la inmediatez visual de la frase conversacional mad-lib propuesta en el diseño con la potencia de los filtros técnicos que ya tiene Ludeka (INC-59, INC-72):
1. **Banda verde a todo el ancho:** cabecera con la frase interactiva: *«Busco un juego para [X jugadores], de dureza [Y], estilo [Z] y que dure [T min].»*
2. Cada hueco en píldora abre directamente su panel modal/acordeón de multiselección.
3. El botón **«+ Más filtros · N»** despliega el panel completo con todos los grupos de filtrado:
   - Jugadores (multiselección de 1 a 7+).
   - Dureza cognitiva.
   - Categorías y estilos lúdicos.
   - Duración máxima.
   - Tipo de producto (Base, Expansión, Caja independiente).
   - Espacio en mesa (Pequeña, Mediana, Grande).
   - Nivel de confrontación (Solitario, Pacífico, Indirecta, Directa).
   - Edad mínima.
4. **Contador dinámico en cada opción:** muestra cuántos juegos encajan en tiempo real, atenuando opciones con 0 resultados.
5. **Chips de filtros activos:** bajo la frase, píldoras con `✕` para desmarcar criterios individualmente.
6. **Modos de visualización:** Cuadrícula con numerales grandes superpuestos (escritorio) y lista compacta obligatoria en móvil (<760 px) para máxima densidad y fluidez. Paginación accesible con elipsis en píldoras de 44 px.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Pages/Home.razor`**:
   - Integración de la banda verde con la frase conversacional reactiva.
   - Panel desplegable de multiselección con recuentos predictivos.
   - Sincronización bidireccional entre la frase, el panel modal y los parámetros de query string en la URL.
   - Conmutador de cuadrícula/lista y paginación estilizada con píldoras de 44 px.
2. **Componente `CatalogSearchBar.razor` y `CatalogSentenceFilter.razor`**:
   - Componentes ligeros con soporte de debounce para evitar llamadas excesivas al servidor en Blazor Server.
3. **Optimización Móvil**:
   - Forzado de vista de lista compacta en móvil, ocultando el toggle redundante.
4. **Pruebas de Contrato y Regresión**:
   - `CatalogQueryContractTests.cs` y tests de sincronización de filtros multiselección.

## Criterios de Aceptación
- La frase conversacional refleja con exactitud la selección múltiple (ej. «2 o 3 jugadores»).
- Desmarcar un chip activo actualiza la frase, el panel y la URL.
- Rendimiento ágil en Blazor Server con respuesta en $<200$ ms.
