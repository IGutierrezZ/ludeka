# Propuesta: Saneamiento Defensivo de Escalabilidad (minPlayers) y Blindaje de Reconciliación Masiva de Expansiones

## 1. Problema
Durante la ejecución de la acción administrativa «Reconciliar Expansiones desde Snapshots» (INC-100), el proceso falló con la excepción:
`El número mínimo de jugadores debe ser mayor a 0. (Parameter 'minPlayers')`.

Causas:
1. Determinadas encuestas de jugadores de BGG incluyen la opción con 0 jugadores, parseada a `ScalabilityEntry(PlayerCount = 0)`.
2. `SqliteGameRepository.UpdateAsync` calculaba `int minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;`, resultando en `minPlayers = 0` y violando la invariante del dominio en `UpdateCatalogInformation`.
3. Ausencia de captura granular por entidad en el bucle de actualización masiva de `BggRawSnapshotSyncService`.

## 2. Propuesta de Solución
- Invariante de dominio en `Game.UpdateScalability` para filtrar `s.PlayerCount > 0`.
- Descarte en el parser de XML `BggXmlParser.ParseScalability` de comensales $\le 0$.
- Cálculo blindado de `minPlayers` y `maxPlayers` en repositorio (`SqliteGameRepository`) y servicios de aplicación.
- Aislamiento `try-catch` por juego en `BggRawSnapshotSyncService`.
