# Especificación: sqlite-backup-maintenance (Copias de Seguridad en Caliente y Mantenimiento)

## 1. Contexto y Propósito
Garantiza la integridad y recuperabilidad de la base de datos SQLite en producción mediante scripts de copia en caliente compatibles con el modo WAL (Write-Ahead Logging), rotación automática de copias de seguridad y validación de integridad previa a la restauración.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Copia de seguridad en caliente sin bloqueo
**Dado** la base de datos `ludeka.db` operando en producción con escrituras activas  
**Cuando** se ejecuta el script de backup (`deploy/backup-sqlite.sh` o `.ps1`)  
**Entonces** utiliza el mecanismo `.backup` de SQLite o `VACUUM INTO`  
**Y** genera una copia snapshot consistente sin bloquear a los usuarios ni provocar inconsistencias por operaciones en el archivo WAL.

### Escenario 2: Política de retención automática de 7 días
**Dado** un directorio de copias de seguridad con múltiples archivos históricos  
**Cuando** finaliza la generación de un nuevo respaldo  
**Entonces** el script identifica y elimina de forma segura los archivos con más de 7 días de antigüedad  
**Y** preserva el espacio en disco del servidor VPS.

### Escenario 3: Verificación de integridad en la restauración
**Dado** un archivo de respaldo que se desea restaurar en la aplicación  
**Cuando** se ejecuta `deploy/restore-sqlite.sh`  
**Entonces** realiza primero una copia de seguridad preventiva de la base de datos actual  
**Y** ejecuta `PRAGMA integrity_check` sobre el respaldo antes de aplicarlo.
