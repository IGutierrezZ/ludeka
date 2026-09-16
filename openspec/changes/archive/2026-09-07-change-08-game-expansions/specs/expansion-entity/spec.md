# Especificación: expansion-entity (Fichas Propias de Expansión y Atributos de Aporte)

## 1. Contexto y Propósito
Este requerimiento define el modelado de las expansiones de juegos de mesa en Ludeka como entidades completas de primer nivel que reutilizan todas las capacidades de `Game` (identidad, carátula, vídeos de YouTube/Instagram en `MediaItem`, valoraciones de 1 a 10 y micro-reseñas en `UserGameReview`, estados de colección y medidas de fundas `Sleeves`), vinculándose a su juego base nodriza y definiendo qué aportan a la mesa.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Creación de una expansión vinculada a un juego base
**Dado** un juego base existente en el catálogo (ej: "Wingspan" con identificador `base-id`)  
**Cuando** se registra una nueva expansión "Wingspan: Expansión Oceanía" con `GameType.Expansion` y `BaseGameId = base-id`  
**Entonces** la expansión tiene su propio `Slug` ("wingspan-expansion-oceania"), su propio `BggId` (300580) y su propia carátula  
**Y** expone la referencia a su juego base nodriza  
**Y** el juego base nodriza incluye a la expansión en su colección de expansiones asociadas.

### Escenario 2: Definición del veredicto de necesidad y etiquetas de aporte lúdico
**Dado** una expansión "Terraforming Mars: Preludio"  
**Cuando** se configuran sus atributos de aporte con:
  - `ExpansionNecessity = MustHave` ("Imprescindible, mejora el juego base")
  - `ImpactTags = [FixesBalance, TightensTime]`
  - `ExtraDurationMinutes = -30`
  - `WhatItBringsSummary = "Acelera las primeras generaciones otorgando cartas de preludio que reducen el arranque lento en ~30 minutos."`  
**Entonces** la entidad expone estos valores correctamente  
**Y** permite consultar de forma inmediata las etiquetas de impacto en texto legible en español para la interfaz.

### Escenario 3: Modificación del rango de jugadores por una expansión
**Dado** un juego base "Carcassonne" con rango de 2 a 5 jugadores  
**Cuando** se consulta la expansión "Carcassonne: Posadas y Catedrales" que tiene `ExtraPlayerCount = 1` y la etiqueta `AddsPlayers`  
**Entonces** el sistema calcula que la combinación permite jugar hasta 6 jugadores  
**Y** refleja el distintivo visual "+1 Jugador (hasta 6J)" en sus píldoras de aporte.

### Escenario 4: Reseñas y vídeos independientes en la expansión
**Dado** la ficha de una expansión (ej: "Wingspan: Expansión Asia")  
**Cuando** un usuario registra una valoración de 9/10 con micro-reseña *"El modo Dúo para 2 personas es sublime"*  
**Entonces** la reseña se asocia al `GameId` de la expansión  
**Y** la nota comunitaria de la expansión se actualiza de forma independiente sin alterar la nota del juego base.
