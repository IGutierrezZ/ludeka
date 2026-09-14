# INC-37: Modo Producción — APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados

- **Identificador:** `apis-produccion-afiliados`
- **Estado:** ⏳ **En progreso**
- **Rama:** `inc/apis-produccion-afiliados` (worktree en `C:\repos\ludeka-wt\apis-produccion-afiliados`)
- **Especificación SDD:** `openspec/changes/2026-09-14-apis-produccion-afiliados/`

---

## 🎯 Objetivo del Incremento
Preparar el núcleo de la aplicación web de Ludeka para su despliegue en producción sobre Docker/Google Cloud, desacoplando los fallbacks automáticos a datos simulados, asegurando el cumplimiento legal con BoardGameGeek, reforzando los canales comunitarios en el footer y creando un motor de afiliación privado y centralizado en el backend.

---

## 📋 Entregables Principales
1. **Desacople de mocks en BGG, Gemini y YouTube:** Errores limpios y transparentes para el usuario ante caídas de APIs externas sin datos ficticios.
2. **Atribución oficial a BGG:** Insignia *"Powered by BoardGameGeek"* en el footer de toda la aplicación.
3. **Enlaces de Comunidad en el Footer:** Botones directos con enlaces a los canales oficiales de Discord y Telegram configurados por entorno.
4. **Motor privado de afiliados:** `IAffiliateUrlResolver` para inyectar tags y parámetros de tiendas lúdicas colaboradoras (Zacatrus, Mathom, Dungeon Marvels, etc.) sin exponerlos en la base de datos ni requerir edición manual en cada juego.
5. **Corrección de defecto de calendario:** Estabilidad total de la suite de tests unitarios (855/855 en verde).
