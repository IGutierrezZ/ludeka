# Especificación: INC-38 — Persistencia PostgreSQL en Supabase, Estrategia Dual y Usuario Admin Inicial

## 1. Resumen Ejecutivo
Para el lanzamiento a producción sobre Google Cloud con base de datos en **Supabase**, Ludeka adoptará una estrategia de doble proveedor en Entity Framework Core:
- **SQLite:** Permanece como proveedor local y en tests unitarios para garantizar velocidad instantánea (cero dependencias externas en local).
- **PostgreSQL (Npgsql):** Proveedor para entornos de staging y producción en Supabase.

Dado que los datos actuales de la web en desarrollo son meramente de prueba y no confirmados, **en producción la base de datos arrancará limpia sin catálogo ficticio**. No obstante, el sistema garantizará de forma automática y obligatoria la existencia de un **Usuario Administrador Fundador** (`FoundingTeam`) con permisos completos de moderación para que los administradores puedan autenticarse y gestionar la plataforma desde el primer minuto.

---

## 2. Requerimientos Funcionales y Técnicos

### REQ-1: Doble Proveedor EF Core (`Database:Provider`)
- El sistema soportará dos proveedores de persistencia: `Sqlite` y `PostgreSql`.
- Se añadirá el paquete oficial `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3) compatible con .NET 10.
- La selección de proveedor se determinará automáticamente mediante:
  1. La clave `Database:Provider` (`"PostgreSql"` o `"Sqlite"`).
  2. Detección automática por cadena de conexión (`Host=`, `Server=`, `supabase.co` o `postgres://` activan PostgreSQL; `Data Source=*.db` activa SQLite).

### REQ-2: Cero Datos de Prueba en Producción y Semillado Controlado
- Se introducirá la opción de configuración `Database:SeedDemoData` (por defecto `true` en Development, `false` en Production).
- Si `SeedDemoData` es `false`:
  - No se ejecutarán los sembradores de catálogo de prueba (`CatalogSeeder`, `DirectorySeeder`, `BoardGameEventSeeder`, etc.).
  - El catálogo, directorios y eventos arrancarán vacíos, listos para ingesta real y confirmada.

### REQ-3: Usuario Administrador Inicial Permanente
- Independientemente del valor de `SeedDemoData`, se creará un sembrador exclusivo `AdminUserSeeder`:
  - Comprobará si existe al menos un usuario con rol `UserRole.FoundingTeam`.
  - Si no existe ninguno, creará el usuario administrador fundador inicial.
  - Los datos del administrador podrán personalizarse vía variables de entorno:
    - `AdminUser:Id` (predeterminado: `admin-fundador`)
    - `AdminUser:UserName` (predeterminado: `Administrador Ludeka`)
    - `AdminUser:Email` (predeterminado: `admin@ludeka.es`)
  - El usuario tendrá rol `UserRole.FoundingTeam` y `ModeratorPermission.All`.

### REQ-4: Script SQL Canónico para Supabase (`docs/database/supabase_schema.sql`)
- Proporcionar un script SQL completo, determinista e idempotente para crear todas las tablas, columnas, restricciones de clave foránea, índices y tablas intermedias en Supabase con 1 solo clic desde el SQL Editor de Supabase.

### REQ-5: Script de Copias de Seguridad para Producción (`scripts/supabase-backup.ps1`)
- Script automatizado en PowerShell para realizar copias de seguridad de la base de datos de Supabase utilizando `pg_dump`.
- Empaquetado en formato comprimido `.sql.gz` con fecha/hora en el nombre.
- Política de retención configurable (por defecto conserva los últimos 7 días y purga copias más antiguas).
- Credenciales inyectadas de forma segura sin exponer contraseñas en código.

---

## 3. Criterios de Aceptación Gherkin

### Escenario 1: Conmutación a PostgreSQL por configuración
```gherkin
Dado que la configuración contiene Database:Provider con valor "PostgreSql"
Y una cadena de conexión válida de PostgreSQL
Cuando la aplicación inicializa los servicios de Entity Framework Core
Entonces el proveedor configurado en LudekaDbContext es Npgsql
```

### Escenario 2: Creación garantizada del usuario administrador en base de datos vacía
```gherkin
Dado que la base de datos no contiene ningún usuario registrado
Cuando la aplicación arranca en modo producción con SeedDemoData=false
Entonces se crea automáticamente un usuario con rol FoundingTeam
Y el usuario posee todos los permisos de moderación
Y no se crean juegos ni eventos de prueba ficticios
```

### Escenario 3: Ejecución de la suite completa de pruebas
```gherkin
Dado el entorno de pruebas unitarias con SQLite
Cuando se ejecuta la suite completa de pruebas de Ludeka.sln
Entonces los 866+ tests se ejecutan y superan al 100% sin requerir un servidor PostgreSQL
```
