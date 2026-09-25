# INC-68: Gamificación — Clasificaciones Públicas y Anonimato

> **Estado:** ⏳ En progreso (backlog 2026-09-22, Fase F)
> **Fecha de Inicio:** 2026-09-26
> **Rama de Trabajo:** `inc/clasificaciones-y-anonimato`
> **Worktree:** `C:\repos\ludeka-wt\clasificaciones-y-anonimato`
> **Dependencias:** INC-65 (likes), INC-67 (hitos), INC-62 (preferencias de privacidad)
> **Especificación Viva:** [02. Ludoteca y Préstamos](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md) · [16. Estadísticas y ADN del Jugador](file:///c:/repos/Ludeka/docs/specs/sistema/16-estadisticas-y-adn-del-jugador.md)

---

## 1. Cómo se descubrió

Cerrando la Fase F del backlog: el spec pide gamificación con comparación pública (§7.4), pero el producto nunca definió cómo comparar jugadores sin destruir la privacidad ni convertir la comunidad en un ring.

## 2. El agujero, verificado

- **No existen clasificaciones públicas (leaderboards) ni mecanismo de anonimato/seudónimo** en el repositorio (barrido sin resultados).
- `PublicProfile.razor` es público por defecto sin preferencia de visibilidad verificada (hueco de evidencia: no se localizó flag de privacidad en `UserPreference` — solo `preferredTheme` y `country`).
- §3.2 del spec define doble rating/ranking de **JUEGOS**: es un ranking de catálogo, **no** de jugadores. Declarado aquí para no confundir alcances.
- §289 menciona el rol de Mecenas Ko-fi: cualquier clasificación debe decidir si el mecenazgo pesa o no (decisión de producto, no técnica).
- La base de métricas ya existe (colección, diario de partidas, likes de INC-65, hitos de INC-67) sin capa de comparación.

## 3. Lo que pide el maintainer

Comparar jugadores con cabeza: clasificaciones que motiven sin exponer a nadie que no quiera ser expuesto, con anonimato/seudónimo real y con reglas que no inviten a inflar números.

## 4. Alcance y decisiones que hay que tomar

1. **Opt-in explícito:** nadie entra en clasificación sin decidirlo (preferencia nueva de visibilidad en `UserPreference`, colgando de INC-62).
2. Anonimato/seudónimo estable (no el correo ni el nombre de la cuenta OAuth) para quien participe sin identidad pública.
3. **V1 de clasificación fijada:** ordenada por actividad en mesa / partidas registradas en el mes (`GamePlayLog`), con ventana temporal mensual y condecoración visual de hitos conseguidos (`UserMilestone`) de INC-67.
4. Medidas anti-trampas básicas (idempotencia de eventos, sin doble conteo vía outbox).
5. Separación explícita del ranking de juegos (§3.2): este incremento es de **jugadores**.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Recompensas a los primeros puestos, clasificaciones por país/torneos, y ponderación por mecenazgo Ko-fi.

## 6. Criterios de aceptación

1. Un usuario sin opt-in no aparece en ninguna clasificación (test de exclusión).
2. El seudónimo no deriva del correo ni de datos de la cuenta OAuth (test de no-derivación).
3. La clasificación usa ventana temporal declarada y no acumula para siempre.
4. Eventos duplicados no alteran posiciones (test de idempotencia).
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- **Privacidad/RGPD:** exponer actividad de usuarios sin consentimiento explícito es el riesgo mayor del incremento.
- Toxicidad competitiva: clasificaciones públicas pueden destruir el tono de club que busca el producto.
- Inflado de métricas (crear entradas de diario falsas) si no hay anti-trampas.
- Derivar el seudónimo por error de un campo identificable (correo, nombre OAuth) = doxxing involuntario.

## 8. Plan de Tareas ODD (Work Units)

- [x] **Tarea 1 (Dominio)**: Extender `UserPreference` con `LeaderboardOptIn`, `LeaderboardAnonymous`, `LeaderboardPseudonym` y crear generador determinista no reversible `PseudonymGenerator` con validaciones anti-PII y pruebas unitarias.
- [x] **Tarea 2 (Contratos y DTOs)**: DTOs `LeaderboardEntryDto`, `MonthlyLeaderboardDto`, `LeaderboardParticipationDto` y contrato `ILeaderboardService` en `Ludeka.Application`.
- [x] **Tarea 3 (Servicio de Clasificación y Repositorio)**: Implementar `LeaderboardService` con filtrado estricto por opt-in, cálculo mensual por `GamePlayLog`, condecoración de hitos INC-67, anonimización rigurosa, soporte en `SqliteUserPreferenceService` y suite de pruebas unitarias.
- [ ] **Tarea 4 (Persistencia EF Core)**: Migración `AddLeaderboardPreferences`, snapshot del modelo y sincronización de migración defensiva para SQLite/PostgreSQL.
- [ ] **Tarea 5 (UI de Ajustes de Privacidad)**: Integrar controles de opt-in, anonimato y seudónimo en `AccountPrivacy.razor` con pruebas bUnit.
- [ ] **Tarea 6 (UI de Clasificación Pública)**: Crear página `/clasificaciones` (`Leaderboards.razor`) con navegación mensual, podio editorial, tabla accesible y transparencia de privacidad, con pruebas bUnit y enlace en comunidad.
- [ ] **Tarea 7 (Cierre y Documentación Viva)**: Verificación completa de suite, redacción de `docs/specs/sistema/43-clasificaciones-y-anonimato.md`, archivo del incremento y preparación de PR.
