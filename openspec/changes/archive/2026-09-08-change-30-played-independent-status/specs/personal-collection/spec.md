# Especificación Funcional: personal-collection (INC-30)

## Requerimientos y Criterios de Aceptación (Gherkin)

### Requerimiento 1: Reestructuración de la Colección en 3 Estados Claros
La colección de un usuario se simplifica eliminando el estado ambiguo `Wishlist` ("Deseado") y consolidando tres estados de alto valor funcional:
1. `InCollection` (📚 *En mi ludoteca*): Copia física en propiedad del usuario.
2. `Played` (🎲 *Jugado*): Experiencia lúdica directa (partida jugada).
3. `WantToBuy` (🛒 *Comprar* / *Deseo de compra*): Intención de adquisición en tiendas con radar de avisos.

```gherkin
Escenario: Usuario marca un juego para comprar
  Dado un usuario en la ficha de un juego que no posee
  Cuando pulsa el botón "Comprar"
  Entonces el juego se guarda en su colección con estado WantToBuy
  Y se le informa de que recibirá alertas sobre sorteos, descuentos y reimpresiones

Escenario: Fusión y migración automática de títulos deseados anteriores
  Dado un registro histórico en base de datos con estado Wishlist
  Cuando se ejecuta la migración del esquema
  Entonces el estado se actualiza automáticamente a WantToBuy
```

### Requerimiento 2: Desacoplamiento Ortogonal de «Jugado»
El estado `Jugado` (`IsPlayed = true`) es una dimensión de actividad que convive de forma independiente y simultánea con los estados de posesión e intención de compra.

```gherkin
Escenario: Marcar como jugado y además para comprar
  Dado un juego marcado previamente como "Comprar"
  Cuando el usuario pulsa el botón "Jugado"
  Entonces el juego mantiene su estado "Comprar" (WantToBuy)
  Y el juego pasa a tener IsPlayed = true
  Y ambos botones ("Comprar" y "Jugado") se muestran activos en la barra de acciones

Escenario: Marcar como jugado un juego propio en estantería
  Dado un juego marcado como "En mi ludoteca" (InCollection)
  Cuando el usuario pulsa el botón "Jugado"
  Entonces el juego conserva InCollection y tiene IsPlayed = true
  Y la barra de acciones refleja "En mi ludoteca · Jugado"

Escenario: Juego en estantería no jugado (Pila de la vergüenza)
  Dado un juego recién añadido a "En mi ludoteca"
  Cuando el usuario no ha marcado el botón "Jugado"
  Entonces el juego tiene Status = InCollection e IsPlayed = false
  Y en la estantería se identifica como pendiente de estrenar

Escenario: Juego únicamente jugado sin posesión ni compra
  Dado un juego sin estado previo en la colección
  Cuando el usuario pulsa el botón "Jugado"
  Entonces el juego se guarda con Status = null e IsPlayed = true
  Y aparece en la pestaña de "Jugados" pero no en "En mi ludoteca" ni en "Comprar"

Escenario: Desmarcado completo y eliminación
  Dado un juego con Status = WantToBuy e IsPlayed = true
  Cuando el usuario desmarca "Comprar" y luego desmarca "Jugado"
  Entonces el ítem se elimina completamente de la base de datos
```

### Requerimiento 3: Regla de Integridad de Valoración (Solo Jugados)
Para poder calificar un juego (puntuación de 1 a 10), emitir micro-reseña o votar comensales, el juego debe estar obligatoriamente marcado como `Jugado` (`IsPlayed = true`).

```gherkin
Escenario: Intento de valorar un juego no jugado
  Dado un juego con IsPlayed = false (aunque esté en "En mi ludoteca" o en "Comprar")
  Cuando el usuario intenta pulsar "Valorar juego"
  Entonces el sistema bloquea la acción o le solicita confirmar que lo ha jugado
  Y muestra el mensaje explicativo "Debes haber jugado al juego para poder puntuarlo y valorarlo"

Escenario: Valoración permitida tras marcar como jugado
  Dado un juego con IsPlayed = true
  Cuando el usuario pulsa "Valorar juego"
  Entonces se abre el bottom sheet de valoración en 45 segundos
  Y permite emitir la puntuación y votos de comensales con éxito
```
