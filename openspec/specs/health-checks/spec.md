# Especificación: health-checks (Diagnóstico de Salud y Disponibilidad)

## 1. Contexto y Propósito
Proporciona endpoints HTTP estándar para comprobación del estado del servicio (`/healthz` para liveness y `/ready` para readiness), permitiendo a Docker, Kubernetes, proxies inversos y herramientas de monitorización determinar si el proceso está vivo y si los subsistemas clave están listos para recibir tráfico. Los componentes de base de datos y almacenamiento DEBEN reportar el proveedor y backend realmente configurados, no valores codificados. Los endpoints se exponen como sondas de despliegue en Cloud Run para evitar que un servicio no disponible reciba tráfico.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Endpoint de Liveness (`/healthz`)
**Dado** la aplicación `Ludeka.Web` en ejecución  
**Cuando** un cliente HTTP o la instrucción `HEALTHCHECK` de Docker consulta `GET /healthz`  
**Entonces** el endpoint responde con código HTTP `200 OK`  
**Y** un payload JSON indicando estado `Healthy` y marca de tiempo.

### Escenario 2: Endpoint de Readiness (`/ready`) con dependencias saludables
**Dado** que la base de datos responde correctamente a `CanConnectAsync()`, el almacenamiento de medios configurado está operativo y la cola de notificaciones está disponible  
**Cuando** se consulta `GET /ready`  
**Entonces** el endpoint responde con código HTTP `200 OK`  
**Y** un desglose JSON con el estado de cada componente (`database`, `storage`, `notification_queue`).

### Escenario 3: Endpoint de Readiness (`/ready`) ante degradación o fallo
**Dado** que la base de datos no puede conectarse o el almacenamiento de medios configurado es inaccesible  
**Cuando** se consulta `GET /ready`  
**Entonces** el endpoint responde con código HTTP `503 Service Unavailable`  
**Y** detalla en el JSON el componente con fallo para diagnóstico operativo inmediato.

### Escenario 4: El componente `database` reporta el proveedor real
**Dado** la aplicación configurada con PostgreSQL como proveedor efectivo  
**Cuando** se consulta `GET /ready`  
**Entonces** el metadato `provider` del componente `database` reporta `Npgsql`, no `Sqlite`.

### Escenario 5: El componente `storage` verifica el almacén de medios configurado, no el directorio de datos
**Dado** Cloudflare R2 configurado con credenciales válidas como almacén de medios activo  
**Cuando** se consulta `GET /ready`  
**Entonces** el componente `storage` reporta el estado de disponibilidad de R2  
**Y** no reporta el directorio de la base de datos local como si fuera el almacenamiento de medios.

### Escenario 6: Exposición de sondas de despliegue en Cloud Run
**Dado** un despliegue a Cloud Run mediante el workflow de CI/CD  
**Cuando** el servicio se aprovisiona o actualiza  
**Entonces** `/healthz` queda configurado como sonda de liveness y `/ready` como sonda de readiness mediante el mecanismo que ofrezca la herramienta de despliegue (nativo o provisión manual documentada).
