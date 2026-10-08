# Propuesta: INC-136 — Refresco de Versiones BGG de Novedades, Soporte Editorial Lúdilo y Saneamiento de Catálogo

## 1. Por qué
En juegos de reciente lanzamiento como *Got Five!* (BggId `453526`, novedad 2026 de Yoann Levet), nuestro catálogo en producción muestra exclusivamente el nombre original en inglés y como editorial *Asmodee Ibérica*, ignorando la edición española oficial de **Lúdilo** titulada **«Código 5»** (`Código 5 - Spanish edition (2026)` en BGG).

El problema radica en:
1. `RegionalPublisherMatcher` carece de la entrada para Lúdilo (sello editorial clave en España), asignando Asmodee por descarte de distribución global.
2. Los snapshots crudos de juegos recientes se guardan una sola vez cuando el juego aún no tiene registrada su edición española en BGG. El método `GetBggIdsMissingVersionsAsync` solo busca juegos que no tengan la clave `versions`; al ya poseer versiones internacionales previas, jamás se vuelve a refrescar desde BGG.
3. El saneador de base de datos no puede inferir por sintaxis que un juego en inglés tiene edición en español si no se dispone del snapshot actualizado.

## 2. Qué cambiará
1. **Registro Editorial:** Agregar Lúdilo en `RegionalPublisherMatcher.cs` con slug `ludilo` y país `ES`.
2. **Saneamiento Prioritario:** Actualizar `CatalogDataSanitizer` para que garantice la reparación inmediata de *Got Five!* (453526) actualizando su título a `Código 5` y su editorial a `Lúdilo`.
3. **Mecanismo de Detección de Snapshots Candidatos a Refresco:** Añadir a `IBggRawSnapshotRepository` y `SqliteBggRawSnapshotRepository` un método para identificar snapshots de novedades (`yearpublished >= DateTime.UtcNow.Year - 1`) que carecen de versión española en su JSON, permitiendo su re-sincronización periódica o dirigida.
4. **Resincronización en `BggRawSnapshotSyncService`:** Ampliar el flujo de sincronización de versiones para permitir refrescar snapshots identificados como pendientes de versión española.

## 3. Impacto esperado
- *Got Five!* se mostrará como **Código 5** con editorial **Lúdilo**.
- Nuevas ediciones españolas añadidas tardíamente a BGG para juegos recientes podrán ser detectadas y actualizadas en el catálogo de forma transparente y determinista.
