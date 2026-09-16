# Especificación: multichannel-webhooks (Clientes HTTP de Discord y Telegram)

## 1. Contexto y Propósito
Este requerimiento define la integración con las APIs externas de Discord (mediante Webhooks) y Telegram (mediante Bot API) para la difusión de mensajes enriquecidos con la identidad visual de Ludeka, protegiendo a la aplicación ante errores de red y rate limits.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Envío de notificación enriquecida a Discord
**Dado** un mensaje de notificación de la comunidad con título, descripción, color de marca `#E05A38`, URL de destino y campos  
**Cuando** se despacha a través de `IDiscordWebhookClient` con una URL de webhook configurada  
**Entonces** el cliente construye un payload JSON con la estructura `embeds` de Discord  
**Y** envía una petición HTTP POST con cabecera `Content-Type: application/json`  
**Y** devuelve un resultado de éxito (`Success = true`).

### Escenario 2: Envío de mensaje enriquecido a Telegram
**Dado** un mensaje de notificación con texto y URL opcional de imagen  
**Cuando** se despacha a través de `ITelegramBotClient` con token y ChatId configurados  
**Entonces** si contiene imagen, llama al endpoint `/sendPhoto` con pie de foto en HTML  
**Y** si no contiene imagen, llama al endpoint `/sendMessage` con `parse_mode: HTML`  
**Y** devuelve un resultado de éxito.

### Escenario 3: Modo Simulado (Dry-Run) sin conexión externa
**Dado** que la opción `DryRun` está activa o las URLs/tokens no están configurados  
**Cuando** se solicita el envío a Discord o Telegram  
**Entonces** el sistema NO realiza ninguna llamada HTTP externa  
**Y** registra la operación como exitosa en modo simulado (`Status = DryRun`).

### Escenario 4: Resiliencia ante error HTTP o Rate Limit (429)
**Dado** que el servidor de Discord o Telegram responde con código de error HTTP (ej. 429 Too Many Requests o 500)  
**Cuando** el cliente procesa la respuesta  
**Entonces** captura el código y detalle del error sin lanzar excepciones no controladas  
**Y** devuelve un resultado fallido con el mensaje explicativo para su auditoría y reintento.
