# INC-131: Calificación en Tarjetas de Expansión y Ordenación por Nota

**Estado:** ✅ Archivado (PR #243, 2026-10-08)  
**Rama:** `inc/expansiones-nota-ordenacion`  
**Objetivo:** Mostrar la calificación (nota cuantitativa) en cada tarjeta de expansión dentro de la pestaña editorial de expansiones (`ExpansionEcosystemSection.razor`) y asegurar que el listado se ordene de forma predeterminada de mayor a menor por calificación, tanto en la capa de aplicación (`ExpansionService.cs`) como en el renderizado del componente Blazor.

---

## 1. Contexto y Justificación

Tras el rediseño de INC-130 que simplificó la pestaña de expansiones y retiró el mezclador, las tarjetas de expansión presentan la carátula, la etiqueta de necesidad («Opcional», «Imprescindible»), el título, el año y el botón para añadir a la ludoteca. Sin embargo, no muestran la valoración o nota cuantitativa del título, lo que impide a los usuarios comparar de un vistazo qué expansiones son las más valoradas por la comunidad y la redacción.

Asimismo, las expansiones se listaban por defecto por año de publicación en lugar de destacar en primer lugar aquellas con mejor valoración.

## 2. Alcance Técnico

1. **Indicador de Calificación en Tarjeta:**
   - Incorporar una insignia compacta o badge con la nota cuantitativa (`★ X.X`) en la tarjeta de expansión.
   - Determinar la nota efectiva: priorizar `LudistRating` (consenso editorial de Ludeka) si es > 0, o en su defecto `BggRating`. Si ambas son 0 o no están disponibles, mostrar guion `-` o indicador neutro.
   - Ubicación: esquina superior derecha alineada con la etiqueta de necesidad para balancear visualmente la tarjeta sin saturar los metadatos.

2. **Ordenación Descendente por Calificación:**
   - En `ExpansionService.cs`: ordenar la colección de `ExpansionSummaryDto` por calificación efectiva de mayor a menor (`OrderByDescending`), desempatando por `BggRating` y alfabéticamente por título en español.
   - En `ExpansionEcosystemSection.razor`: ordenar la lista `Expansions` de forma reactiva por nota antes de iterar, garantizando que el usuario siempre vea primero las expansiones con mejor calificación.

3. **Pruebas de Contrato y Regresión:**
   - Nuevos tests unitarios comprobando la ordenación por nota en `ExpansionServiceTests`.
   - Pruebas de contrato en `ExpansionAndMediaCarouselUiContractTests` y `WebMarkupContractTests` verificando el renderizado de la nota y la ausencia de regresiones.
