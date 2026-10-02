# Tareas: Saneamiento Defensivo de Escalabilidad y Blindaje de Reconciliación

- [x] 1. Dominio: Invariante en `Game.UpdateScalability` para filtrar `s.PlayerCount > 0`.
- [x] 2. Parser: Blindaje de `BggXmlParser.ParseScalability` ante encuestas con `playerCount <= 0`.
- [x] 3. Repositorio: Cálculo defensivo de `minPlayers >= 1` y `maxPlayers >= minPlayers` en `SqliteGameRepository.UpdateAsync`.
- [x] 4. Aplicación: Saneamiento preventivo en `BggMassIngestionService`, `ExpansionService`, `InstagramComposerService`, `BggSimulationDataset` y `GameEditorModal`.
- [x] 5. Resiliencia: Aislamiento `try-catch` granular por juego en `BggRawSnapshotSyncService`.
- [x] 6. Pruebas: Batería en `BggExpansionReconciliationTests.cs` (2.291 pruebas en verde).
- [x] 7. Documentación: Registro de INC-100 archivado e INC-101 en curso.
