# Especificación: automated-events (Eventos Automatizados de Comunidad)

## 1. Contexto y Propósito
Define la lógica de negocio para detectar y disparar los 4 eventos comunitarios automatizados: alerta de sorteo por expirar, boletín semanal de viernes, publicación de veredicto fundador y resolución de duda de reglas.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Alerta de sorteo próximo a expirar (24 horas)
**Dado** un sorteo activo cuya fecha límite (`DeadlineAt`) se encuentra a menos de 24 horas del momento actual  
**Y** el sorteo aún no ha expirado  
**Cuando** se ejecuta el escaneo de sorteos próximos (`TriggerExpiringGiveawaysScanAsync`)  
**Entonces** se genera un mensaje de tipo `GiveawayExpiring` con el título, organizador, tiempo restante y URL  
**Y** se encola para su difusión en Discord y Telegram.

### Escenario 2: Publicación del Boletín de Lanzamientos de Viernes
**Dado** uno o más lanzamientos de la semana registrados en `WeeklyRelease`  
**Cuando** se dispara el boletín de viernes (`TriggerFridayReleasesBulletinAsync`) de forma programada o manual  
**Entonces** se genera un mensaje consolidado `FridayReleasesSummary`  
**Y** lista los títulos, editorial, PVP estimado e indicación de si es novedad o reimpresión  
**Y** se encola para su difusión.

### Escenario 3: Nuevo Veredicto de la Mesa Fundadora
**Dado** que un moderador o miembro fundador publica un nuevo veredicto para un juego  
**Cuando** se invoca la notificación de veredicto  
**Entonces** se emite un mensaje `FoundingVerdictPublished` con el sello (`Imprescindible`, `Recomendado`), frase destacada y enlace a la ficha del juego.

### Escenario 4: Duda de Reglas Resuelta
**Dado** una duda de reglas (`RuleQuestion`) con una respuesta marcada como aceptada (`RuleAnswer.IsAccepted`)  
**Cuando** se emite la notificación  
**Entonces** se difunde un mensaje `RuleQuestionAnswered` con la pregunta, autor de la solución y enlace al consultorio de reglas.
