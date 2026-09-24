# INC-59: Filtros del Catálogo y Tamaño en Mesa

> **Estado:** ✅ Archivado (entregado el 2026-09-24, PR #108, 1.687 pruebas al 100% en verde)
> **Fecha de Inicio:** 2026-09-24 · **Fecha de Cierre:** 2026-09-24
> **Rama de Trabajo:** `inc/filtros-y-tamano-mesa`
> **Worktree:** `C:\repos\ludeka-wt\filtros-y-tamano-mesa`
> **Dependencias:** ninguna fuerte (coordinar el estado de URL con INC-58)
> **Especificación Viva:** [01. Catálogo y Fichas](file:///c:/repos/Ludeka/docs/specs/sistema/01-catalogo-y-fichas.md)

---

## 1. Cómo se descubrió

Al auditar el catálogo para INC-58 se revisó el motor de filtros existente y aparecieron dos problemas: no está verificado qué filtros fallan en la práctica, y el criterio «tamaño en mesa» que pide el maintainer no está expuesto pese a existir en el dominio.

## 2. El agujero, verificado

- `GameFilterCriteria` ya es facetado (`SearchTerm`, `EspecialParejas`, `TypeFilter`, etc.) en `src/Ludeka.Application/DTOs/GameFilterCriteria.cs`.
- **Hueco de evidencia:** no está verificado qué filtros están rotos o desconectados de la UI — diagnóstico pendiente dentro del incremento.
- `TableFootprint` **existe en el dominio** (`GameEditorDomainTests` usa `TableFootprint.SmallTable`), pero no se ha verificado que llegue como filtro a la UI del catálogo.
- `BggCatalogStagingItem.PlayingTimeMinutes` existe y habilitaría filtro por duración real de partida.
- La especificación (`LUDIST_SPEC_FUNCIONAL_MVP.md` §9, Motor de Filtros) pide exploración facetada completa que no coincide con lo verificado en UI.

## 3. Lo que pide el maintainer

Poder encontrar juegos por lo que importa en una mesa real: cuánta gente, cuánto dura y **cuánto espacio ocupa** («¿me cabe en la mesa?»). Y que los filtros que ya existen funcionen de verdad.

## 4. Alcance y decisiones que hay que tomar

1. Auditoría empírica de cada filtro de `GameFilterCriteria` frente a su exposición y comportamiento en UI.
2. Exponer `TableFootprint` (tamaño en mesa) como filtro y como dato visible en la ficha.
3. Filtro por jugadores y por tiempo de partida (aprovechando `PlayingTimeMinutes`).
4. **Decisión abierta:** taxonomía de tamaños de mesa (mantener la existente vs ampliar) y qué hacer con filas sin dato BGG.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Recomendador por dificultad/mejor número de jugadores (ficha inteligente) y búsqueda semántica.

## 6. Criterios de aceptación

1. Informe de auditoría por filtro: existe / conectado en UI / comportamiento correcto.
2. Se puede filtrar el catálogo por tamaño en mesa con la taxonomía del dominio.
3. Filtros combinables y persistidos en URL junto con la página (coherente con INC-58).
4. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Datos BGG incompletos: filtros que excluyen filas sin dato pueden dar listas engañosas.
- Colisión semántica entre `TableFootprint` y el semáforo de escalabilidad ya existente en fichas.
- Alcance creciente si la auditoría destapa filtros rotos en cadena.
