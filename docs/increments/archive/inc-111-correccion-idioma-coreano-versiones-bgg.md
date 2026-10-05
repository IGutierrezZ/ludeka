# INC-111: Corrección de Detección de Idioma (ID 2195 BGG), Filtrado de Descriptores de Edición y Saneamiento Automático de Títulos

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-10-05  
> **Fecha de Finalización:** 2026-10-05  
> **Tipo:** Corrección de Errores / Calidad de Datos / Resiliencia  
> **Rama:** `inc/fix-bgg-version-korean-titles`  
> **Worktree:** `F:\repos\ludeka-wt\fix-bgg-version-korean-titles`

---

## 1. Contexto y Diagnóstico del Problema

En producción (`ludeka.es`), múltiples títulos de catálogo (por ejemplo, *Ark Nova: Marine Worlds*) aparecían nombrados erróneamente como «Korean edition» o «Angry Lion Korean edition».

### Causa Raíz
1. **Confusión en Identificador de Idioma de BGG XMLAPI2:**
   En INC-105 se incluyó una comprobación rápida `idProp.GetString() == "2195"` asumiendo que `2195` correspondía a español. En el catálogo de BGG, `2195` es en realidad la etiqueta de **Korean** (coreano). Como consecuencia, cualquier versión en coreano presente en el nodo `<versions>` era clasificada como candidata válida en español.
2. **Suplantación de Título por Descriptores de Edición:**
   En BGG, los elementos `boardgameversion` casi siempre contienen en su campo `name` el descriptor físico de la edición comercial de la caja (ej. «Spanish edition», «Angry Lion Korean edition», «Edición en español») y no un título traducido. Al volcar la versión al catálogo, dicho descriptor sustituía al `SpanishTitle` del juego, destruyendo el nombre legible.
3. **Contaminación de Metadatos Adicionales:**
   Junto al título incorrecto, se heredaban editoriales coreanas y códigos de barras con prefijo GS1 de Corea del Sur (`880...`).

---

## 2. Solución Implementada

1. **Eliminación del ID numérico y Validación Semántica Estricta:**
   - En `BggRawSnapshotParser.IsSpanishLanguageLink`, se eliminó completamente la comprobación por ID `2195`.
   - Se valida de forma determinista y exclusiva por valor textual del enlace (`Spanish`, `Español`, `Castellano` y variantes flexivas).
2. **Filtrado y Limpieza de Descriptores de Edición (`IsGenericEditionTitle` / `CleanVersionTitle`):**
   - Nuevos métodos públicos y testeados que discriminan entre un título propio traducido (ej. «Alta Tensión») y una etiqueta descriptiva de caja (ej. «Spanish edition», «Edición española», «Maldito Games Spanish edition»).
   - `BggSpanishVersionInfoDto.Title` pasa a ser nullable (`string?`). Si la versión no aporta un título propio genuino, se extraen la editorial y el EAN sin alterar el título del juego.
3. **Fusión Inteligente de Candidatas en Español:**
   - Si un juego tiene múltiples versiones (por ejemplo una con título y otra con EAN), el parser combina ambas para obtener la mejor información posible.
4. **Saneador Automático de Base de Datos (`CatalogDataSanitizer`):**
   - Servicio en `Ludeka.Infrastructure.Seeding.CatalogDataSanitizer` que:
     - Detecta títulos de catálogo contaminados con «korean», «angry lion» o descriptores genéricos.
     - Restaura `SpanishTitle` con `OriginalTitle` (o el título en español oficial del snapshot satélite si existe).
     - Limpia editoriales coreanas y códigos de barras coreanos (`880...`).
   - Se ejecuta proactivamente al arrancar la aplicación (`Program.cs`) y al inicio de barridos de catálogo en `BggRawSnapshotSyncService`.

---

## 3. Verificación

- **Suite de Pruebas Unitarias:** 2.481 pruebas unitarias pasando al 100% en `Ludeka.UnitTests.dll`.
- **Pruebas Específicas de Regresión:**
  - `BggRawSnapshotParserVersionsTests`: validación de rechazo de ID 2195, detección de descriptores genéricos, limpieza de sufijos y combinación de versiones.
  - `CatalogDataSanitizerTests`: validación del saneador contra base de datos SQLite en memoria con casos sintéticos y reales.
  - `BggRawSnapshotSyncServiceTests`: validación de la restauración del título original al ejecutar barridos.
