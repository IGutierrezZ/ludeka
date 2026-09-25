# INC-67: Gamificación — Hitos y Logros del Jugador

> **Estado:** ✅ Archivado y Verificado (2026-09-26 · 1.843 pruebas unitarias al 100%)
> **Fecha de Inicio:** 2026-09-25 · **Fecha de Cierre:** 2026-09-26
> **Rama de Trabajo:** `inc/hitos-y-logros`
> **Worktree:** `C:\repos\ludeka-wt\hitos-y-logros`
> **Dependencias:** INC-65 (señales de comunidad), INC-15 (ADN del Jugador, archivado)
> **Especificación Viva:** [42. Sistema de Hitos y Logros del Jugador (Gamificación No Invasiva)](file:///c:/repos/Ludeka/docs/specs/sistema/42-hitos-y-logros-del-jugador.md)

---

## 1. Cómo se descubrió

El spec funcional contempla gamificación (§7.4), pero al auditar el código solo existe un badge de volumen heredado de INC-15: no hay hitos, ni logros, ni historial de progreso del jugador.

## 2. El agujero, verificado

- **No existen hitos, logros ni leaderboard** en el repositorio (barrido sin resultados).
- Lo único parecido: `UserLibraryStatsService.ComputePlayerBadge` (badge por volumen de colección, INC-15) y los badges que `PublicProfile` ya muestra. Es un precursor legítimo, no un sistema de logros: no hay desbloqueo, ni fecha, ni catálogo de retos.
- `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md` §7.4 define gamificación que nunca llegó a tareas (hueco de implementación documentado).
- §3.2 define doble rating/ranking de **JUEGOS** — no confundir con ranking de jugadores (eso es INC-68).
- El diario de partidas (INC-30) y los estados de colección (`Tengo`/`Jugado`/`Deseado`/`Prestar`) ya generan los eventos que alimentarían hitos, pero nadie los consume para gamificación.

## 3. Lo que pide el maintainer

Reconocer el progreso del jugador de forma cálida y lúdica (hitos de colección, de partidas, de diario), con la personalidad de Ludeka — gamificación de club, no de casino — sin oscurecer los datos reales del jugador.

## 4. Alcance y decisiones que hay que tomar

1. Catálogo de hitos (colección: primer juego / 10 jugados / fondo de armario; diario: primera partida / X sesiones; comunidad: primer like recibido de INC-65).
2. Motor de desbloqueo idempotente con fecha de obtención y estado persistido por usuario.
3. Visualización en `PublicProfile` y en el área de cuenta (INC-50/61), en coherencia con `ComputePlayerBadge`.
4. **Decisión abierta:** relación con el badge de INC-15 (¿se absorbe como un hito más?) y notificación al desbloquear (¿silencioso vs aviso?).
5. Frontera explícita con INC-68: aquí no hay comparación entre usuarios.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Rankings entre usuarios, retos por tiempo, puntos canjeables, recompensas económicas y streaks con penalización.

## 6. Criterios de aceptación

1. Cada hito tiene test de desbloqueo con su evento disparador y es idempotente (no se desbloquea dos veces).
2. El historial de hitos del usuario es visible y fechado en su perfil/área.
3. `ComputePlayerBadge` no se rompe y su convivencia con los hitos queda decidida por escrito.
4. Cero comparación entre usuarios en esta entrega (garantía de alcance).
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Solapamiento confuso con `ComputePlayerBadge` si no se fija la frontera.
- Falsos desbloqueos por eventos duplicados del outbox (usar claves idempotentes).
- Deriva de alcance hacia rankings si no se vigila la frontera con INC-68.
