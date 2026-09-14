# Incremento 38: Persistencia PostgreSQL en Supabase, Estrategia Dual y Herramientas de Migración y Backup

- **Identificador SDD:** `postgresql-supabase`
- **Rama Git:** `inc/postgresql-supabase`
- **Worktree:** `C:\repos\ludeka-wt\postgresql-supabase`
- **Estado:** ⏳ En progreso

---

## 1. Contexto y Objetivos

Para la salida a producción de Ludeka con contenedores Docker en Google Cloud, la persistencia se alojará en un cluster de **PostgreSQL gestionado en Supabase**. Este incremento dota al proyecto de:
1. Soporte oficial para PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`) manteniendo SQLite para desarrollo local y tests rápidos.
2. Conmutación inteligente de proveedor (`Sqlite` vs `PostgreSql`) por configuración o detección de cadena de conexión.
3. Script canónico de creación de esquema para Supabase (`docs/database/supabase_schema.sql`).
4. Script de migración de datos de SQLite (`ludeka.db`) hacia Supabase con respeto de orden topológico de claves foráneas.
5. Script automatizado de copias de seguridad (`scripts/supabase-backup.ps1`) con política de retención.

---

## 2. Requerimientos Técnicos

1. **Paquetes NuGet:** Incorporar `Npgsql.EntityFrameworkCore.PostgreSQL` a `Ludeka.Infrastructure`.
2. **Configuración en Program.cs:** Permitir conmutar proveedor mediante `Database:Provider` o análisis del connection string.
3. **Mapeo EF Core:** Validar que los tipos JSON (`b.ToJson()`), colecciones primitivas y `DateTimeOffset` UTC operen correctamente en PostgreSQL.
4. **Pruebas Automatizadas:** Mantener la suite al 100% (866+ tests) y agregar pruebas unitarias para la conmutación y compatibilidad de cadenas de conexión.
5. **Herramientas Operacionales:** Proporcionar scripts verificados para migración y backup periódico.
