# Especificación: Saneamiento Defensivo de Escalabilidad y Blindaje de Reconciliación

## Requisitos Funcionales

1. **Invariante de Escalabilidad Positiva:**
   - La colección `Game.Scalability` solo debe contener elementos con `PlayerCount >= 1`.
   - `BggXmlParser.ParseScalability` debe ignorar cualquier encuesta o dato con `playerCount <= 0`.

2. **Cálculo Defensivo de Intervalos de Jugadores:**
   - En `SqliteGameRepository.UpdateAsync`, `minPlayers` debe ser siempre $\ge 1$.
   - `maxPlayers` debe ser siempre $\ge minPlayers$.

3. **Resiliencia en Reconciliación Masiva:**
   - Si la actualización de un juego individual arroja una excepción, el error debe registrarse como advertencia en el log y el bucle debe continuar procesando los restantes juegos del lote.

## Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Actualización de juego con comensales cero en su escalabilidad
  Dado un juego existente en base de datos con una entrada de escalabilidad con PlayerCount = 0
  Y un snapshot de BGG que lo identifica como expansión
  Cuando se ejecuta la reconciliación masiva de expansiones
  Entonces el proceso concluye con éxito sin arrojar ArgumentOutOfRangeException
  Y el juego queda clasificado como GameType.Expansion
  Y la escalabilidad del juego queda saneada sin elementos con PlayerCount <= 0
```
