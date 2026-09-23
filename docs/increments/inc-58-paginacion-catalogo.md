# INC-58: Paginación Real del Catálogo y Modos de Vista (Cuadrícula y Lista)

> **Estado:** ⏳ En progreso (iniciado el 2026-09-23 en modo ODD)
> **Fecha de Inicio:** 2026-09-23
> **Rama de Trabajo:** `inc/paginacion-catalogo`
> **Worktree:** `C:\repos\ludeka-wt\paginacion-catalogo`
> **Dependencias:** ninguna fuerte (coordinar el estado de URL con INC-59)
> **Especificación Viva:** [01. Catálogo y Fichas](file:///c:/repos/Ludeka/docs/specs/sistema/01-catalogo-y-fichas.md) · [35. Persistencia](file:///c:/repos/Ludeka/docs/specs/sistema/35-persistencia.md)

---

## 1. Cómo se descubrió

El maintainer navega el catálogo (~8.000 títulos tras INC-53) y solo ve una fracción mínima, sin forma de llegar al resto:

> «solo un puñado»

## 2. El agujero, verificado

**Contradicción documentada (declarada a propósito):** la paginación existe en teoría en el backend, pero el usuario no la percibe.

- `SqliteGameRepository.SearchAsync` ya pagina con `Skip`/`Take` sobre `page`/`pageSize`.
- `CatalogResult(Games, TotalCount, Page, PageSize)` ya devuelve el total y la página.
- Sin embargo, la UI del catálogo **no expone** controles de paginación ni el `TotalCount` al usuario (hueco de evidencia: falta la capa de presentación, no la de datos).
- Referencia de estilo ya en el repo: `AuditLogViewer.razor` sí tiene UI de paginación funcional.

Es decir: el motor de datos está listo y la UI del catálogo muestra «solo un puñado» sin paginar.

## 3. Lo que pide el maintainer

> «Ademas en el catalogo hay que meter paginacion, ya tenemos muchos juegos y solo se ven un puñado. Ademas agregaria para cambiar el modo de ver, si como esta como ahora o en modo lista.»

Que el catálogo se recorra entero (paginación visible y usable) y que además se pueda cambiar el modo de ver: la cuadrícula actual o una vista de lista.

## 4. Alcance y decisiones que hay que tomar

1. UI de paginación en el catálogo (patrón reutilizado de `AuditLogViewer.razor`).
2. Preservar filtros, página y modo de vista en la URL (compartible, sobrevive a recarga SSR).
3. Exponer `TotalCount` («X títulos») y controles anterior/siguiente o numerados.
4. Modo de vista conmutable: cuadrícula (actual) ↔ lista, con datos clave por fila (jugadores, tiempo, peso) sin romper el trabajo de imágenes de INC-57.
5. **Decisión abierta:** paginación clásica vs scroll infinito (tradeoff: accesibilidad y SSR vs inmediatez).
6. **Decisión abierta:** si el modo de vista elegido se recuerda como preferencia además de viajar en la URL.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Paginación de la ludoteca pública, del hub multimedia y del diario de partidas.

## 6. Criterios de aceptación

1. Desde la UI se alcanza la última página del catálogo sin tocar la base de datos a mano.
2. El total mostrado coincide con `CatalogResult.TotalCount`.
3. Filtros activos, página y modo de vista sobreviven a recarga y a compartir la URL.
4. El usuario alterna entre cuadrícula y lista sin perder filtros ni página, y sin descargar datos de más.
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Estado de página en Blazor SSR (el parámetro de URL debe ser la fuente de verdad).
- Coste de `COUNT(*)` sobre ~8.000 filas si se recalcula en cada render.
- Solape de alcance con INC-59 (filtros): conviene un solo estado de navegación compartido.
