# Especificación: expansion-synergies (Matriz de Sinergias, Recetas y Mezclador de Mesa)

## 1. Contexto y Propósito
Uno de los mayores desafíos al jugar con expansiones es saber qué títulos combinan bien entre sí y cuáles provocan saturación de reglas, duración inmanejable o colisión de componentes. Esta especificación define el modelado de relaciones de sinergia entre expansiones hermanas, las recetas prediseñadas y el motor de evaluación de combinaciones ("Mezclador de Mesa").

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Definición de sinergia óptima entre expansiones hermanas
**Dado** dos expansiones de "Wingspan" ("Expansión Europa" y "Expansión Oceanía")  
**Cuando** se registra una sinergia entre ellas con:
  - `Level = PerfectCombo` (🟢 Sinergia Óptima)
  - `Reason = "Ambas añaden variedad limpia: Europa aporta poderes de final de ronda y Oceanía rebalancea el motor con néctar sin generar conflicto."`  
**Entonces** la consulta de sinergia devuelve `PerfectCombo` independientemente del orden en que se pasen los identificadores (A con B o B con A)  
**Y** provee la explicación lúdica en español.

### Escenario 2: Detección de incompatibilidad o conflicto entre expansiones
**Dado** dos expansiones hipotéticas o con conflicto directo que intentan sustituir el mismo tablero o mazo central  
**Cuando** se define una relación de tipo `Incompatible` (🔴 Incompatible / Conflicto) con su motivo  
**Y** el usuario selecciona ambas expansiones en el Mezclador de Mesa  
**Entonces** la evaluación global del mezclador marca el estado como `Conflict`  
**Y** genera una alerta explícita detallando las dos expansiones que colisionan y el motivo.

### Escenario 3: Diagnóstico en vivo del Mezclador de Mesa para combinación con reservas
**Dado** tres expansiones de un juego que añaden cada una +30 minutos y ocupan gran espacio en mesa  
**Cuando** el evaluador analiza la combinación de las 3 expansiones  
**Y** existen sinergias marcadas como `CompatibleWithCaution` (🟡 Compatible con Reservas)  
**Entonces** el resultado del diagnóstico señala estado `Caution` ("Mesa Exigente / Sobrecarga")  
**Y** avisa al jugador del incremento significativo de tiempo y espacio en mesa requerido.

### Escenario 4: Carga y aplicación de Recetas de Mesa Recomendadas
**Dado** un juego base con la receta oficial "Duelo Rápido a 2" asociada a "Wingspan: Expansión Asia"  
**Cuando** el usuario consulta las recetas de mesa del juego base  
**Entonces** el sistema lista la receta con su título, descripción, público ideal ("2 jugadores en 45 min") y la lista de expansiones que incluye  
**Y** permite cargar la receta en el Mezclador de Mesa con un solo clic.
