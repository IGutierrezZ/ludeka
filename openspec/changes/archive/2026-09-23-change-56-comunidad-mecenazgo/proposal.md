# Propuesta de Cambio: INC-56 — Comunidad, Mecenazgo y Enlaces de Apoyo

> **Cambio:** `change-56-comunidad-mecenazgo` · **Fase:** `sdd-propose` · **Fecha:** 2026-09-23  
> **Rama:** `inc/comunidad-mecenazgo` · **Worktree:** `C:\repos\ludeka-wt\comunidad-mecenazgo`  
> **Línea base de pruebas:** 1.639 unitarias + 10 de integración en verde

---

## 1. Motivación y Contexto

Ludeka promueve una política editorial independiente de honestidad y transparencia radical respecto a sus vías de financiación (manifiesto en `/transparencia`). Sin embargo, existía un desfase entre lo documentado y lo efectivamente desplegado:
- No existía ningún enlace público hacia el mecenazgo en Ko-fi en el pie de página común de la web.
- La página de transparencia enlazaba a páginas de inicio genéricas de Ko-fi y Discord mediante cadenas literales no configurables, ignorando los canales oficiales del proyecto y omitiendo el canal de Telegram.
- La tienda Amazon se mencionaba en el texto de transparencia y en una prueba unitaria, pero el motor de afiliados `AffiliateOptions` no disponía de regla configurada para Amazon por defecto.

## 2. Cambios Propuestos

1. **Configuración Tipada:** Incorporar `KofiUrl` en `CommunityNotificationOptions` y registrar la regla de `Amazon` en `AffiliateOptions` (parámetro `tag=ludeka-21`, dominio `amazon.es`).
2. **Interfaz de Usuario:**
   - Añadir botón de Ko-fi accesible en el footer de `MainLayout.razor` junto a Discord y Telegram.
   - En `Transparency.razor`, inyectar `CommunityOptions` y usar las rutas configuradas para Ko-fi, Discord y Telegram.
3. **Pruebas y Verificación:**
   - Pruebas unitarias para `AffiliateUrlResolver` verificando la inyección del tag en URLs de Amazon.
   - Pruebas de contrato de fuente para certificar la presencia de los botones accesibles y atributos `rel="noopener noreferrer"`.
4. **Especificación Viva:** Actualización de los módulos 08 y 25 de la especificación viva del sistema.
