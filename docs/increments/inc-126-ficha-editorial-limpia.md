# INC-126: Ficha Editorial Limpia (Alineación Claude Design)

**Estado:** ⏳ En progreso  
**Rama:** `inc/ficha-editorial-limpia`  
**Worktree:** `F:\repos\ludeka-wt\ficha-editorial-limpia`

---

## 1. Contexto y Objetivos

Alineación fiel y definitiva de la ficha de juego (`GameDetail.razor`, `StoreOffersCard.razor`, `UserReviewCard.razor`) con la última versión del diseño editorial de Claude (`Ludeka Final.dc.html` y `Ludeka Final Movil.dc.html`), tanto en escritorio como en móvil.

### Decisiones de Diseño y Requisitos Clave:
1. **Hero y Cifras Clave:** Sustitución de píldoras amontonadas por 4 columnas de cifras editoriales gigantes (Ludeka `#FFC145`, BGG `#FFF8EE`, Jugadores ideales y Minutos) con divisores `border-l border-white/30`.
2. **Veredicto en Pestaña Limpia:** Eliminación del modal drawer slide-over conflictivo y la caja flotante pesada del hero. El veredicto se integra como pestaña principal (`El veredicto`) en el sticky nav y sección limpia en línea (`id="bloque-02-veredicto"`). En la pestaña `Resumen`, se muestra un callout superior con la cita y enlace directo a la pestaña de veredicto.
3. **Botonera de Hero y Acciones:** Split button negro/mostaza para ludoteca (`Añadir a mi ludoteca ▾`), botón de registro de partida contorneado en blanco y botón circular de compartir (`↗`), sin interferencias en móvil.
4. **Dónde Comprar (`StoreOffersCard.razor`):** Maquetación limpia sin tarjeta blanca pesada ni cajas anidadas. Lista directa sobre el fondo con indicadores circulares de stock (verde, mostaza, gris), precios grandes en `var(--accent)` (o `var(--muted)` sin stock) y leyenda de afiliados al pie.
5. **Tu Valoración (`UserReviewCard.razor`):** Reemplazo de estrellas por selector numérico 1 al 10 en rejilla de 5 columnas. En estado valorado, nota grande de 44px (`/10`) y botón «Modificar mi valoración», con fondo transparente y borde `border-2 border-[var(--rule)]`.
6. **Limpieza de Ruido y Solapamientos:**
   - Retirada de la cinta negra de mercado en el pliegue entre el hero y el sticky nav.
   - Retirada de la barra inferior móvil fija duplicada que colisionaba con `MobileBottomNav`.
   - Reubicación de herramientas administrativas (`Cartel para Redes`) en la barra de gestión de staff (`StaffBar`).
   - Ocultamiento de la pestaña de fundas al no haber datos cargados.
