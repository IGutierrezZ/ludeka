# Checklist de Tareas: INC-77 — Saneamiento de Calidad en Ingesta BGG (Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador)

## Fase 1: Dominio y Entidad `Game`
- [ ] 1.1 Modificar la validación de `yearPublished` en `Game.UpdateCatalogInformation` (`src/Ludeka.Core/Entities/Game.cs`) para admitir años históricos y futuros (`-5000` a `DateTime.UtcNow.Year + 10`).
- [ ] 1.2 Implementar `UpdateDna(GameStyle style, ConfrontationType confrontation, bool isOfficialSolo)` en `src/Ludeka.Core/Entities/Game.cs`.
- [ ] 1.3 Proteger `AdjustScalability` en `src/Ludeka.Core/Entities/Game.cs` para evitar que destruya o degrade votos comunitarios cuando la colección ya contiene votos reales (`TotalVotes > 0`).
- [ ] 1.4 Crear pruebas unitarias de dominio en `tests/Ludeka.UnitTests/Domain/GameTests.cs` (años históricos -2200, 1475, 0, preservación de votos en escalabilidad, y `UpdateDna`).

## Fase 2: Parser BGG e Inferencia de ADN Lúdico
- [ ] 2.1 Actualizar `BggXmlParser.InferGameDna` (`src/Ludeka.Infrastructure/Bgg/BggXmlParser.cs`) para procesar etiquetas `<link type="boardgamesubdomain" ...>` (`Thematic Games` ➔ `Ameritrash`, `Party Games` ➔ `PartyGame`, `Abstract Games` ➔ `FillerAbstract`, `Strategy Games` ➔ `Eurogame`).
- [ ] 2.2 Enriquecer la categorización secundaria por categorías y mecánicas temáticas (*Dungeon Crawl*, *Miniatures*, *Horror*, *Trivia*, etc.).
- [ ] 2.3 Crear pruebas unitarias en `tests/Ludeka.UnitTests/Infrastructure/BggXmlParserTests.cs` para verificar la inferencia de subdominios BGG y categorías temáticas.

## Fase 3: Ingesta de Staging y Cálculo de Duración
- [ ] 3.1 En `BggMassIngestionService.cs` (`ProcessPendingDetailsBatchAsync` y `ExecuteBackfillCatalogQualityBatchAsync`), pasar la duración total en `playingTimeMinutes` en las llamadas a `staging.MarkFetched`.
- [ ] 3.2 En `BggMassIngestionService.cs` (`PromoteReadyToCatalogBatchAsync`), calcular `EstimatedPerPlayerMinutes` correctamente a partir de la duración total y usar el estilo inferido en lugar de `GameStyle.Eurogame` fijo.
- [ ] 3.3 En `BggMassIngestionService.cs` (`ExecuteBackfillCatalogQualityBatchAsync`), actualizar incondicionalmente `game.UpdateDuration(fetched.Duration)` y `game.UpdateDna(...)` cuando BGG devuelva datos enriquecidos.
- [ ] 3.4 Añadir pruebas unitarias en `tests/Ludeka.UnitTests/Application/BggMassIngestionServiceTests.cs` comprobando el cálculo no colapsado de duraciones (~30 min/jugador en juegos de 120 min) y la actualización de ADN.

## Fase 4: Persistencia y Repositorio `SqliteGameRepository`
- [ ] 4.1 En `SqliteGameRepository.UpdateAsync` (`src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs`), transferir `Scalability`, `Sleeves`, `SpanishPublisher` y `RegionalPublishers` a la entidad rastreada `existing`.
- [ ] 4.2 En `SqliteGameRepository.GetGamesPendingQualityBackfillAsync` y `GetGamesPendingQualityBackfillCountAsync`, ampliar el criterio a `g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)`.
- [ ] 4.3 Añadir pruebas unitarias en `tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs` (o clase equivalente de pruebas) verificando que `UpdateAsync` conserva intactos `MustPlay`, `BestVotes` y `Sleeves`.

## Fase 5: Verificación Integral de Suite de Pruebas
- [ ] 5.1 Ejecutar `dotnet test` y comprobar el 100% de la suite de pruebas unitarias e integración en verde sin regresiones.
