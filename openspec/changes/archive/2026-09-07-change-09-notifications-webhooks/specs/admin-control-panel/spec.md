# Especificación: admin-control-panel (Panel de Control y Diagnóstico en UI)

## 1. Contexto y Propósito
Define la vista de gestión y diagnóstico en Blazor para administradores y miembros de la Mesa Fundadora, permitiendo auditar el historial de despachos, comprobar la conectividad en vivo y reintentar envíos fallidos.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Acceso restringido al panel de notificaciones
**Dado** un usuario que navega a `/admin/notificaciones`  
**Cuando** el usuario NO posee el rol `FoundingTeam` ni `Moderator`  
**Entonces** la vista muestra un mensaje informativo indicando que se requiere pertenecer al equipo fundador para gestionar los webhooks.

### Escenario 2: Visualización de estado de canales
**Dado** un usuario con rol `FoundingTeam`  
**Cuando** accede al panel `/admin/notificaciones`  
**Entonces** visualiza las tarjetas de estado de Discord y Telegram indicando si están activos, en modo simulado (DryRun) o desactivados  
**Y** visualiza botones directos para enviar un ping de prueba a cada canal.

### Escenario 3: Envío de ping de prueba interactivo
**Dado** el panel de control  
**Cuando** el usuario pulsa el botón "Enviar Ping de Prueba"  
**Entonces** se genera un mensaje de tipo `CustomTestPing`  
**Y** el mensaje se procesa y aparece reflejado en la tabla de historial inferior.

### Escenario 4: Reintento de notificación fallida en 1 clic
**Dado** un registro en el historial con estado `Failed`  
**Cuando** el usuario pulsa el botón "Reintentar" en la fila del registro  
**Entonces** el sistema reejecuta el despacho del mensaje para ese canal  
**Y** actualiza el estado del registro a `Sent` si tiene éxito o actualiza el mensaje de error.
