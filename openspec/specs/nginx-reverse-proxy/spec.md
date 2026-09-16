# Especificación: nginx-reverse-proxy (Proxy Inverso con Soporte WebSocket para Blazor)

## 1. Contexto y Propósito
Define la configuración del servidor web Nginx como reverse proxy frontal en entornos de producción, gestionando la terminación TLS/SSL, la compresión de respuestas y, fundamentalmente, la actualización de protocolo WebSocket requerida por Blazor Server (`_blazor`).

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Conexión persistente de Blazor WebSockets
**Dado** un usuario navegando por Ludeka en el navegador  
**Cuando** Blazor Server establece la conexión SignalR en `/_blazor`  
**Entonces** Nginx reenvía las cabeceras `Upgrade $http_upgrade` y `Connection "upgrade"` hacia el contenedor upstream  
**Y** desactiva el buffering de proxy (`proxy_buffering off`) para garantizar latencia mínima en la interacción de la UI.

### Escenario 2: Cabeceras de seguridad estrictas (Security Headers)
**Dado** cualquier petición entrante servida por Nginx  
**Cuando** se entrega la respuesta HTTP al cliente  
**Entonces** incluye las cabeceras `X-Frame-Options: SAMEORIGIN`, `X-Content-Type-Options: nosniff`, `X-XSS-Protection: 1; mode=block` y `Referrer-Policy: strict-origin-when-cross-origin`.

### Escenario 3: Compresión gzip de recursos estáticos
**Dado** la solicitud de archivos CSS, JavaScript, SVG o JSON  
**Cuando** el cliente soporta compresión `Accept-Encoding: gzip`  
**Entonces** Nginx comprime el contenido al vuelo, reduciendo el ancho de banda transferido y el tiempo de carga móvil.
