# Especificación: docker-compose-orchestration (Orquestación y Persistencia)

## 1. Contexto y Propósito
Define la orquestación de servicios para despliegue local, staging y producción mediante Docker Compose, garantizando persistencia del archivo SQLite (`ludeka.db`), aislamiento de red, inyección segura de configuración mediante `.env` y políticas de tolerancia a fallos.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Persistencia de datos SQLite mediante volúmenes nombrados
**Dado** un contenedor `ludeka-web` levantado mediante `docker-compose.yml`  
**Cuando** se crea, modifica o elimina información en el catálogo, reseñas o ludoteca  
**Entonces** los datos se escriben en `/app/data/ludeka.db`  
**Y** al reiniciar o recrear el contenedor (`docker compose down && docker compose up -d`), los datos persisten intactos en el volumen `ludeka_data`.

### Escenario 2: Inyección de variables de entorno sin credenciales en código
**Dado** un archivo `.env` configurado a partir de `.env.example`  
**Cuando** Docker Compose levanta el servicio `ludeka-web`  
**Entonces** inyecta `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection` y las credenciales externas (BGG, Discord, Telegram)  
**Y** no se exponen valores sensibles en el historial de control de versiones.

### Escenario 3: Política de reinicio y tolerancia a fallos
**Dado** un reinicio del host VPS o una terminación inesperada del proceso  
**Cuando** el daemon de Docker detecta la caída  
**Entonces** el servicio aplica la política `restart: unless-stopped`  
**Y** reinicia automáticamente el contenedor sin requerir intervención manual del operador.
