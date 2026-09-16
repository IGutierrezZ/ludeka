# Especificación: health-checks (Diagnóstico de Salud y Disponibilidad)

## 1. Contexto y Propósito
Proporciona endpoints HTTP estándar para comprobación del estado del servicio (`/healthz` para liveness y `/ready` para readiness), permitiendo a Docker, Kubernetes, proxies inversos y herramientas de monitorización determinar si el proceso está vivo y si los subsistemas clave (SQLite, almacenamiento en disco y cola de mensajes) están listos para recibir tráfico.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Endpoint de Liveness (`/healthz`)
**Dado** la aplicación `Ludeka.Web` en ejecución  
**Cuando** un cliente HTTP o la instrucción `HEALTHCHECK` de Docker consulta `GET /healthz`  
**Entonces** el endpoint responde con código HTTP `200 OK`  
**Y** un payload JSON indicando estado `Healthy` y marca de tiempo.

### Escenario 2: Endpoint de Readiness (`/ready`) con dependencias saludables
**Dado** que la base de datos SQLite responde correctamente a `CanConnectAsync()`, el directorio de datos tiene permisos de escritura y la cola de notificaciones está disponible  
**Cuando** se consulta `GET /ready`  
**Entonces** el endpoint responde con código HTTP `200 OK`  
**Y** un desglose JSON con el estado de cada componente (`database`, `storage`, `notification_queue`).

### Escenario 3: Endpoint de Readiness (`/ready`) ante degradación o fallo
**Dado** que la base de datos SQLite no puede conectarse o el directorio de almacenamiento es inaccesible  
**Cuando** se consulta `GET /ready`  
**Entonces** el endpoint responde con código HTTP `503 Service Unavailable`  
**Y** detalla en el JSON el componente con fallo para diagnóstico operativo inmediato.
