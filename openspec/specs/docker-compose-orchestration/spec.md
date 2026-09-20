# Especificación: docker-compose-orchestration (Orquestación y Persistencia)

## 1. Contexto y Propósito
Define la orquestación de servicios para despliegue local y staging mediante Docker Compose, garantizando persistencia del archivo SQLite (`ludeka.db`), aislamiento de red, inyección segura de configuración mediante `.env`, políticas de tolerancia a fallos y declaración explícita del entorno de ejecución (`Development` o `Staging`) para evitar que un entorno local active accidentalmente una guarda de arranque de producción.

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

### Escenario 4: La configuración de `docker-compose.yml` declara un entorno local sin Production
**Dado** el fichero `docker-compose.yml` sin sobrescribir `ASPNETCORE_ENVIRONMENT`  
**Cuando** se evalúa el entorno efectivo de ejecución  
**Entonces** el valor por defecto NO es `Production` sino un entorno local (`Development` o equivalente)  
**Y** la combinación resultante no activa la guarda de arranque de `production-persistence-guard`.

### Escenario 5: `docker-compose.staging.yml` declara explícitamente entorno Staging
**Dado** que `docker-compose.staging.yml` fija `ASPNETCORE_ENVIRONMENT=Staging`  
**Cuando** se evalúa la guarda de arranque de `production-persistence-guard` con esa combinación  
**Entonces** la guarda no se activa, permitiendo el uso de SQLite en staging local.

### Escenario 6: Ambos ficheros incluyen comentario explícito de entorno local
**Dado** `docker-compose.yml` y `docker-compose.staging.yml`  
**Cuando** se revisa su cabecera o comentarios  
**Entonces** ambos identifican explícitamente que son entornos locales/de pruebas con SQLite, no producción.
