# Incremento 90: Tabla Satélite de Snapshots Crudos BGG, Ingesta Automática y Poblado Retroactivo con Respeto de Límites

> **ID:** INC-90  
> **Slug:** `bgg-raw-snapshots`  
> **Rama:** `inc/bgg-raw-snapshots`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-base.md`), Módulo 05 (`docs/specs/sistema/05-importador-bgg.md`), Módulo 37 (`docs/specs/sistema/37-ingesta-bgg-catalogo.md`)  
> **Dependencias:** INC-89.  

---

## 1. Contexto y Diagnóstico

Actualmente, las respuestas de la API XML2 de BGG (`/xmlapi2/thing`) se procesan en memoria y se descartan tras extraer los campos de `Game`. Todo dato no modelado hoy (artistas, descripciones originales completas, mecánicas secundarias, votos pormenorizados de encuestas) se pierde irreversiblemente.

Para permitir el recálculo futuro de categorías, heurísticas deterministas (ADN lúdico, huella en mesa) o la incorporación de nuevos atributos sin volver a saturar la API externa de BGG ni inflar la tabla caliente `Games`, se requiere una tabla satélite desacoplada que almacene el snapshot completo en formato JSON estructurado.

---

## 2. Objetivos Técnicos

1. **Entidad y Persistencia Satélite `BggRawSnapshot`:**
   - Tabla `BggRawSnapshots` con clave primaria `BggId`, `RawJson`, `ApiVersion` y marcas de tiempo UTC.
   - Repositorio `IBggRawSnapshotRepository` con soporte dual SQLite y PostgreSQL.
2. **Auto-Captura Transparente:**
   - Integración en `BggXmlApiClient` y orquestadores de catálogo para persistir el snapshot en cada consulta exitosa a BGG.
3. **Servicio y Panel de Poblado Retroactivo (Backfill):**
   - Servicio `IBggRawSnapshotSyncService` con *rate-limiting* estricto y cancelación cooperativa para rellenar los snapshots de juegos existentes en catálogo.
   - Tarjeta y botón interactivo en `/admin/cola-catalogacion` con métricas en tiempo real y autorización por permisos `CanEditGames`.
   - Comando de runner en `Ludeka.Jobs` para ejecución desatendida en Cloud Run.
4. **Verificación Automatizada Completa:**
   - Cobertura de pruebas unitarias para entidad, repositorio, servicio de sincronización y componentes UI.
