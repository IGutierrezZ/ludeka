# Tareas de Implementación: INC-103 — Barrido y Auditoría Integral de Calidad de Catálogo desde Snapshots Locales de BGG

## Fase 1: Conversión Determinista JSON -> XML (`BggJsonToXmlConverter`)
- [ ] 1.1 Implementar `BggJsonToXmlConverter` en `Ludeka.Infrastructure.Bgg` para reconstituir `XElement` desde el `RawJson` del snapshot BGG respetando `@atributos` y `#text`.
- [ ] 1.2 Añadir pruebas unitarias exhaustivas en `Ludeka.UnitTests/Infrastructure/BggJsonToXmlConverterTests.cs` verificando fidelidad de conversión bidireccional contra `BggXmlToJsonConverter`.

## Fase 2: Estrategia Snapshot-First en `BggMassIngestionService`
- [ ] 2.1 Conectar `_snapshotRepo` en `EnrichSingleGameQualityAsync` para consultar el snapshot local antes de salir a la red.
- [ ] 2.2 Reutilizar `BggXmlParser.ParseQualityMetadata` y `BggXmlParser.InferGameDna` sobre el elemento reconstituido en memoria.
- [ ] 2.3 Reforzar idempotencia: verificar que no se marque `enriched = true` si la entidad ya cuenta con los mismos metadatos (especialmente en escalabilidad con 0 votos).
- [ ] 2.4 Añadir pruebas unitarias en `Ludeka.UnitTests/Application/BggMassIngestionSnapshotQualityTests.cs`.

## Fase 3: Saneamiento Anti-Bucle en Consulta de Pendientes
- [ ] 3.1 Añadir parámetro de cursor `afterBggId = 0` en `IGameRepository.GetGamesPendingQualityBackfillAsync` e implementarlo en `SqliteGameRepository`.
- [ ] 3.2 Actualizar `BggQualityBackfillResultDto` con `LastBggIdProcessed` y `HasMore` para soportar avance monotónico en lotes.
- [ ] 3.3 Actualizar `StartContinuousBackfill` en `CatalogQueueAdmin.razor` para iterar mediante cursor ascendente por `afterBggId`.
- [ ] 3.4 Añadir prueba de integración / unitaria comprobando que un lote de juegos sin votos no re-encola indefinidamente.

## Fase 4: Optimización del Barrido Total y CLI en `Ludeka.Jobs`
- [ ] 4.1 Verificar que `SweepCatalogQualityBatchAsync` procesa los lotes a velocidad de memoria local.
- [ ] 4.2 Ajustar `BackfillQualityJobRunner` en `Ludeka.Jobs` para reportar progreso en consola y permitir ejecución desatendida.
- [ ] 4.3 Ejecutar suite completa de pruebas automáticas (`dotnet test`) y verificar 100% verde.
