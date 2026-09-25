# INC-65: «Me gusta» en Editoriales, Tiendas, Creadores y Vídeos

> **Estado:** ⏳ En progreso (backlog 2026-09-22, Fase E)
> **Fecha de Inicio:** 2026-09-25
> **Rama de Trabajo:** `inc/likes-comunidad`
> **Worktree:** `C:\repos\ludeka-wt\likes-comunidad`
> **Dependencias:** INC-46 (Autenticación Real, archivado), INC-61 (menú de cuenta)
> **Especificación Viva:** [02. Ludoteca y Préstamos](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md) · [04. Hub Multimedia](file:///c:/repos/Ludeka/docs/specs/sistema/04-hub-multimedia.md)

---

## 1. Cómo se descubrió

Al planear la capa social del producto (gamificación en Fase F) se revisó qué interacciones de usuario existen ya y solo apareció un «me gusta» que no es del usuario: es el de los posts de Instagram.

## 2. El agujero, verificado

- **No existe «me gusta» de usuarios** sobre editoriales, tiendas, creadores ni vídeos multimedia (barrido sin resultados).
- Lo único parecido es `LikesCount` de `MediaItem`/posts Instagram (módulo `04-hub-multimedia.md`, heredado de INC-21): es un contador importado de la red social, **no** una interacción de Ludeka. Colisión de concepto nula si se nombran distinto, pero hay que declarar la frontera para no duplicar significado.
- `PublicProfile.razor` muestra actividad del usuario sin señal de afecto/comunidad.
- El spec funcional (§7) habla de valoraciones de usuario, pero no de señal social ligera tipo «me gusta».

## 3. Lo que pide el maintainer

> «agregar sistema de me gusta en editoriales, tiendas, creadores, y videos multimedia solo para personas logueadas, y el orden de estas listas o videos sera por los megusta, la editoriales con mas me gusta seran los que salen primeras en el listado, o si hay 4 vdeos de tutoriales en un juego saldran primero los que mas me gusta tienen.»

«Me gusta» SOLO para usuarios logueados sobre editoriales, tiendas, creadores y vídeos multimedia, y orden de esas listas (y de los vídeos de un juego) por número de me gusta.

## 4. Alcance y decisiones que hay que tomar

1. Entidad de «me gusta» de usuario (autor + destino polimórfico: editorial / tienda / creador / vídeo multimedia).
2. Toggle idempotente por usuario autenticado (un voto por usuario y destino) y conteo agregado visible. Solo logueados votan.
3. Ordenación por nº de me gusta: listados de editoriales, tiendas y creadores (primero el más gustado) y vídeos de un juego (si hay 4 tutoriales, primero el más gustado), con desempate estable documentado.
4. **Decisión abierta:** si el orden por me gusta es el por defecto o un orden más junto a «recientes/novedades»; si los likes son públicos (quién) o solo el conteo; y qué ve un invitado (botón deshabilitado vs invitación a entrar).
5. Nomenclatura explícita para no confundir con `LikesCount` de `MediaItem` (p. ej. `UserLike` / `LikesCount` de dominio de usuario).
6. Enlace con el perfil público (`PublicProfile`) sin exponer más de lo decidido en privacidad.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Comentarios, menciones, follows entre usuarios, valoraciones de juegos (ya existen por otro camino) y clasificaciones de personas (eso es materia de INC-68: tablas de jugadores, no el orden por likes de estos listados).

## 6. Criterios de aceptación

1. Un usuario autenticado puede dar y quitar «me gusta» una sola vez por destino de editorial/tienda/creador/vídeo (test de idempotencia).
2. El conteo mostrado cuadra con los votos persistidos (test de agregación).
3. Los listados de editoriales, tiendas y creadores salen ordenados por me gusta (desempate estable); con 4 tutoriales de un juego, primero el más gustado.
4. Sin sesión no se registra ningún voto (según la decisión de UX del invitado).
5. `LikesCount` de `MediaItem` (Instagram) sigue intacto y distinto del nuevo modelo.
6. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Confusión semántica con `LikesCount` de medios si no se nombra con frontera clara.
- Conteos desincronizados si se cachean agregados sin invalidación.
- Deriva hacia «concurso de popularidad» (riesgo de producto, no técnico) si se encadena con INC-68 sin decidir anonimato primero.
