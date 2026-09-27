# Propuesta SDD: INC-73 — Curación y Saneamiento Integral del Directorio Lúdico Español

## 1. Motivación y Diagnóstico Empírico

El padrón de entidades lúdicas en España incorporado en INC-54 (`seed-directory.json`) reúne 118 entidades (46 editoriales, 37 tiendas y 35 creadores de contenido) y 415 enlaces asociados.

Tras una auditoría concurrente contra la red en tiempo real, se han constatado **101 enlaces con error o anomalías (un 24,3% del total)**:
- **53 fallos de red / DNS:** Dominios con TLD equivocado (`.com` en lugar de `.es` o viceversa, omisión de `www.`), nombres de dominio desactualizados o proyectos inactivos.
- **22 errores HTTP 404 en redes sociales:** Canales de YouTube y perfiles de Twitter/X generados por extrapolación del slug que no existen o pertenecen a terceros.
- **Duplicidad sistemática de Website:** Inclusión del sitio web principal en la propiedad `websiteUrl` y simultáneamente como elemento del array `socialLinks`.
- **Redirecciones no consolidadas:** Enlaces que sufren múltiples saltos HTTP (301/302).

## 2. Propuesta de Solución

1. **Auditoría y Corrección de `seed-directory.json` Asistida por Búsqueda e IA:**
   - Contrastar una a una las entidades afectadas mediante búsqueda web verificada.
   - Establecer las URLs oficiales canónicas para cada editorial, tienda y divulgador.
   - Vincular los handles vigentes de YouTube (`@canal`), Instagram y Twitter/X.
   - Purgar enlaces rotos sin sustituto válido en lugar de conservar enlaces 404.
   - Eliminar los objetos `{"platform": "Website", ...}` del array `socialLinks` cuando `websiteUrl` ya contenga la dirección web oficial.

2. **Garantías de Arquitectura e Idempotencia:**
   - Mantener invariantes los `slug` de todas las entidades para asegurar retrocompatibilidad con registros ya existentes en bases de datos.
   - Garantizar que `DirectorySeeder` siga operando de manera aditiva e idempotente sin generar registros duplicados ni desajustes en claves foráneas o relaciones.

3. **Verificación Automatizada:**
   - Ejecutar la suite completa de pruebas unitarias existentes (1.945 pruebas en verde).
   - Validar que ninguna URL de la suite de pruebas quede rota.

## 3. Criterios de Aceptación
- Cero dominios con error de host desconocido en entidades activas.
- Todas las cuentas de YouTube e Instagram asociadas corresponden a los perfiles oficiales reales.
- El JSON embebido se deserializa e inserta limpiamente mediante `DirectorySeeder`.
- El 100% de la suite de pruebas unitarias se mantiene en verde.
