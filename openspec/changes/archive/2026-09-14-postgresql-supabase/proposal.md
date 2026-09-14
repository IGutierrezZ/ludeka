# Propuesta: INC-38 — Persistencia PostgreSQL en Supabase, Estrategia Dual y Herramientas de Migración y Backup

## 1. Contexto y Justificación
Ludeka ha operado en sus fases de desarrollo y MVP inicial con una base de datos local SQLite (`ludeka.db`).
Para su despliegue en producción sobre Docker y Google Cloud, el usuario ha determinado que la base de datos se alojará en **Supabase** (PostgreSQL gestionado), aprovechando su alta disponibilidad, copias de seguridad continuas y capacidades de escalabilidad.

Para dar este salto a producción con garantías de estabilidad:
1. No debemos romper el entorno de desarrollo local ni la suite de 866 pruebas unitarias (que ejecutan en segundos contra SQLite/memoria).
2. Debemos dotar a la arquitectura de una **estrategia de persistencia híbrida/conmutable**: SQLite en desarrollo/tests y PostgreSQL (Npgsql) en producción (Supabase).
3. Debemos proporcionar un mecanismo confiable para inicializar el esquema en Supabase, migrar los datos actuales de `ludeka.db` a Supabase y automatizar los backups periódicos.

---

## 2. Alcance Propuesto

### 2.1 Proveedor PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- Añadir el paquete oficial `Npgsql.EntityFrameworkCore.PostgreSQL` (versión 10.0.3 para .NET 10) a `Ludeka.Infrastructure`.
- Configurar `Program.cs` y la capa de infraestructura con conmutación inteligente de proveedor:
  - Si la variable de configuración `Database:Provider` es `PostgreSql` (o la cadena de conexión contiene parámetros típicos de PostgreSQL como `Host=`, `Server=`, `supabase.co`), se inicializa `options.UseNpgsql(...)`.
  - Si `Database:Provider` es `Sqlite` (predeterminado en desarrollo), se mantiene `options.UseSqlite(...)`.

### 2.2 Validación y Afinamiento del Modelo EF Core para PostgreSQL
- Revisión de `LudekaDbContext`:
  - Los mapeos JSON (`b.ToJson()`) para `Scalability`, `Sleeves`, `PurchaseLinks`, `AiSummary`, `PlayerCountRatings`, etc., son soportados de forma nativa como columnas `jsonb` en PostgreSQL.
  - Asegurar el manejo estricto de fechas UTC en `DateTimeOffset` (requerido por Npgsql para `timestamp with time zone`).
  - Compatibilidad de índices y búsquedas de texto (`EF.Functions.Like`).

### 2.3 Inicialización de Esquema en Supabase
- Generación de script SQL canónico inicial (`docs/database/supabase_schema.sql`) para crear todas las tablas, relaciones, índices y tipos en Supabase con 1 clic desde el SQL Editor de Supabase.
- Inicialización en código: si `db.Database.IsNpgsql()`, ejecutar `await db.Database.EnsureCreatedAsync()` de forma segura al arrancar la aplicación si las tablas no existen.
- Ejecución condicional de seeders: los seeders solo se ejecutan si las tablas correspondientes están completamente vacías.

### 2.4 Herramienta de Migración de Datos (SQLite → Supabase PostgreSQL)
- Crear un script o comando de migración (`scripts/migrate-sqlite-to-supabase.ps1`):
  - Lee los datos reales existentes en `ludeka.db`.
  - Inserta los registros en la base de datos de Supabase respetando el orden topológico de claves foráneas.
  - Genera reporte detallado de filas migradas por tabla y valida la integridad de los datos.

### 2.5 Script Automatizado de Backup para Producción
- Crear `scripts/supabase-backup.ps1` (y documentación asociada):
  - Utiliza `pg_dump` con compresión `.sql.gz` o volcado automatizado.
  - Soporta autenticación vía variables de entorno seguras (`SUPABASE_DB_URL` o `PGPASSWORD`).
  - Incluye política de rotación de copias de seguridad (ej. retención de últimos 7 días).

---

## 3. Impacto en la Arquitectura y Riesgos Mitigados

- **Cero regresiones en desarrollo local:** Los desarrolladores y los tests pueden seguir trabajando con `ludeka.db` sin obligar a tener una instancia de PostgreSQL corriendo localmente.
- **Transparencia en despliegue Docker:** En producción, simplemente inyectando la variable de entorno `ConnectionStrings__DefaultConnection` con la URL de Supabase y `Database__Provider=PostgreSql`, el contenedor arranca directamente en PostgreSQL.
- **Riesgo de divergencia de esquema:** Mitigado centralizando el modelo en `LudekaDbContext` y disponiendo del script SQL canónico auditable.

---

## 4. Pruebas y Criterios de Aceptación
1. Compilación limpia sin errores ni advertencias nuevas en .NET 10.
2. Los 866 tests unitarios existentes continúan pasando al 100%.
3. Nuevos tests de infraestructura que verifiquen:
   - La selección adecuada del proveedor de base de datos según la configuración.
   - El formateo y compatibilidad de cadenas de conexión de Supabase (modo Transaction Pooler / Session Pooler / Direct).
4. Script de migración SQLite → Supabase validado sintáctica y estructuralmente.
5. Script de backup validado y documentado en la guía de operaciones.
