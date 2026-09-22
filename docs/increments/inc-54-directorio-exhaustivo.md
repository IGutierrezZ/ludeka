# INC-54: Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico Español

> **Estado:** ⏳ En desarrollo  
> **Fecha de Inicio:** 2026-09-22 · **Fecha de Cierre:** Pendiente  
> **Rama de Trabajo:** `inc/directorio-exhaustivo`  
> **Worktree:** `C:\repos\ludeka-wt\directorio-exhaustivo`  
> **Dependencias:** INC-19 (Directorio de Editoriales, Creadores y Tiendas), INC-31 (Reorientación a Creadores de Contenido), INC-46 (Autenticación y Permisos de Moderación)  
> **Especificación Viva:** [19. Directorio de Editoriales, Creadores y Tiendas](file:///c:/repos/Ludeka/docs/specs/sistema/19-directorio-editoriales-creadores-tiendas.md)

---

## 1. Contexto y Motivación
El ecosistema lúdico en español requiere un directorio poblado de forma rigurosa y completa, eliminando la necesidad de dar de alta manualmente cada editorial, tienda o divulgador. Este incremento incorpora el padrón canónico exhaustivo de editoriales de España (>45), el top 35 de tiendas especializadas y el top 35 de creadores de contenido referentes, junto a mecanismos de carga automatizados vía CLI y panel administrativo web.

---

## 2. Alcance Técnico
1. **Asset Canónico (`seed-directory.json`):** Definición estructurada en formato JSON de todas las entidades, embebida en `Ludeka.Infrastructure`.
2. **Reconciliador Idempotente (`DirectorySeeder.cs`):** Carga y enriquece registros sin duplicar slugs ni alterar creaciones manuales de moderadores.
3. **Comando CLI (`Ludeka.Jobs`):** `dotnet run --project src/Ludeka.Jobs -- seed-directory` para siembras desatendidas.
4. **Acción Web:** Botón en el panel de administración para re-sincronizar el padrón con feedback interactivo.
5. **Pruebas Automatizadas:** Verificación exhaustiva de deserialización, idempotencia y purga de diseñadores de INC-31.
