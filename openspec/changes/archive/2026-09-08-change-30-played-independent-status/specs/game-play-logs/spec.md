# Especificación Funcional: game-play-logs (INC-30)

## Requerimientos y Criterios de Aceptación (Gherkin)

### Requerimiento 1: Registro Rápido de Partidas Lúdicas
El usuario puede registrar partidas individuales de cualquier juego del catálogo indicando los datos esenciales de la sesión.

```gherkin
Escenario: Registro exitoso de una partida
  Dado un usuario autenticado en la ficha de "Catan"
  Cuando abre el modal de registrar partida
  Y especifica la fecha "08/09/2026", ubicación "Club Lúdico El Dado", 4 jugadores y comentario "Victoria ajustada a 10 puntos"
  Y confirma el guardado
  Entonces se almacena un nuevo registro en GamePlayLogs
  Y el juego pasa automáticamente a estar marcado como "Jugado" (IsPlayed = true)

Escenario: Marcado automático a Jugado al registrar partida
  Dado un juego que no estaba marcado como jugado (IsPlayed = false)
  Cuando el usuario registra su primera partida para ese juego
  Entonces el sistema actualiza o crea el ítem en la colección asegurando IsPlayed = true
  Y el juego queda habilitado para valoración
```

### Requerimiento 2: Diario y Consulta de Partidas en Perfil y Ficha
Los aficionados pueden consultar el historial cronológico de sus partidas registradas tanto a nivel global como específico por juego.

```gherkin
Escenario: Visualización del diario de partidas en Mi Ludoteca
  Dado un usuario con 5 partidas registradas en varios juegos
  Cuando accede a la pestaña "Partidas" de "Mi Ludoteca"
  Entonces se muestran las 5 partidas ordenadas de más reciente a más antigua
  Y cada tarjeta indica la carátula y título del juego, fecha, lugar, número de jugadores y comentarios

Escenario: Consulta de partidas propias en la ficha de un juego
  Dado un juego para el cual el usuario ha registrado 3 partidas
  Cuando visualiza la ficha del juego
  Entonces un indicador destacado muestra "Has jugado 3 partidas a este juego"
  Y un desplegable o panel permite ver el detalle de dichas sesiones
```

### Requerimiento 3: Analíticas y Estadísticas de Partidas Personales
El sistema calcula métricas lúdicas acumuladas sobre las partidas registradas por el usuario.

```gherkin
Escenario: Cálculo de métricas de sesiones
  Dado un usuario con partidas registradas
  Cuando consulta su panel de estadísticas de partidas
  Entonces se calcula:
    | Total de partidas jugadas |
    | Título más jugado (Top 1) |
    | Lugar más habitual de juego |
    | Tamaño de mesa más frecuente (Sweet Spot real de juego) |
    | Partidas registradas en el mes en curso |
```
