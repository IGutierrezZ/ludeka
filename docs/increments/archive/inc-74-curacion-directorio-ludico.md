# INC-74: Curación y Saneamiento Integral del Directorio Lúdico Español

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-27  
> **Fecha de Cierre:** 2026-09-27  
> **Rama de Trabajo:** `inc/curacion-directorio-ludico`  
> **Worktree:** `C:\repos\ludeka-wt\curacion-directorio-ludico`  
> **Dependencias:** INC-19 (Directorio de Editoriales, Creadores y Tiendas), INC-54 (Padrón Exhaustivo y Mecanismo de Carga)  
> **Especificación Viva:** [13. Directorio de Editoriales, Creadores y Tiendas con Redes y Foco Multimedia](file:///c:/repos/Ludeka/docs/specs/sistema/13-directorio-editoriales-creadores-tiendas.md)  

---

## 1. Contexto y Diagnóstico Empírico

El padrón canónico de INC-54 (`src/Ludeka.Infrastructure/Seeding/seed-directory.json`), compuesto por 46 editoriales, 37 tiendas y 35 creadores de contenido (118 entidades y 415 enlaces), fue poblado en origen con convenciones sintéticas y datos estáticos sin verificación HTTP activa.

La auditoría concurrente automatizada realizada revela **101 enlaces problemáticos (~24,3% de fallos)**:
1. **Dominios caídos o TLDs erróneos (53 fallos de red/DNS):** Por ejemplo, `doctormeeple.com` (cuando es `doctormeeple.es`), `clubdante.es` (cuando es `elclubdante.es`), `nostromocomic.com` (cuando es `nostromocomics.com`), o dominios extintos de microeditoriales y tiendas cerradas.
2. **Cuentas y perfiles 404 en redes sociales (22 fallos):** Handles inferidos mecánicamente en Twitter y YouTube que no corresponden con los canales reales o apuntan a cuentas inexistentes.
3. **Redundancia estructural en el esquema JSON:** Múltiples entidades duplican `websiteUrl` dentro del array `socialLinks` como `{ "platform": "Website", "url": "..." }`, generando entradas repetidas en la UI.
4. **Redirecciones 301 no canónicas:** Direcciones antiguas como `masqueoca.com` (hacia `www.masqueoca.com`) o `asmodee.es` (hacia `es.asmodee.com`).

---

## 2. Alcance Técnico del Incremento

1. **Curación y Verificación Rigurosa de `seed-directory.json`:**
   - Corrección de todas las URLs web canónicas comprobadas contra servidores activos.
   - Búsqueda y fijación de los identificadores reales de YouTube (`@handle`), Instagram y canales comunitarios activos.
   - Retirada limpia de enlaces a redes sociales abandonadas o ficticias.
   - Supresión de la duplicación redundante de `Website` dentro de `socialLinks`.
2. **Preservación de la Idempotencia y Contratos de Dominio:**
   - Respetar los slugs existentes para no alterar identificadores ya persistidos en bases de datos locales o de producción.
   - Garantizar que `DirectorySeeder` continúe sincronizando y enriqueciendo aditivamente sin duplicaciones ni regresiones.
3. **Validación y Pruebas Unitarias:**
   - Verificación sintáctica y de integridad referencial del archivo JSON.
   - Ejecución completa de la suite de pruebas unitarias (`DirectorySeederTests`, `WebMarkupContractTests`, etc.).
