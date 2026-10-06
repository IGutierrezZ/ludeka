# INC-115: Portada Editorial «Revista Lúdica» (Hero en Arco, Cintas de Avisos y Top Semanal)

## Estado
✅ Verificado (Fase 3 del Rediseño Integral «Revista Lúdica» — 2.543 pruebas unitarias y de contrato en verde al 100%)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/portada-editorial-revista-ludica`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
La portada actual (`HomeDashboard.razor`) se basa en carriles de scroll horizontal con tarjetas pequeñas. La dirección «Revista Lúdica» transforma la portada en un escaparate editorial rico, pausado y con objetos físicos de mesa:
1. **Hero terracota a todo el ancho:** titular gigante display con palabra final en cursiva mostaza (*¿A qué jugamos hoy?*), buscador píldora de 60 px y composición en arco (`220px 220px 24px 24px`) con borde blanco de 6 px y pegatina redonda inclinada a $-12^\circ$ (*«Bienvenido a tu mesa»*).
2. **Cinta de avisos inversa:** barra `--inverse` con texto mostaza de 14 px y puntos terracota, mostrando sorteos urgentes con cuenta atrás en vivo y novedades clave.
3. **El Top de la semana:** pestañas editoriales (*En tendencia / Mejor valorados / Para 2*) con números gigantes ciclálicos (`64-96px`, tracking `-0.06em`), carátulas cuadradas giradas a $\pm1.5^\circ$ y sombras profundas. En móvil, se adapta a una lista vertical compacta de alto rendimiento.
4. **Sorteos en marcha (Banda mostaza):** polaroids físicas con borde crema, rotaciones y botón a `/sorteos`.
5. **Novedades en tiendas (Banda verde):** formato editorial agrupado con calendario numérico grande y precio destacado.
6. **Ferias y grandes citas:** tarjetas pastel (`--p-blue`, `--p-pink`, `--p-peach`, `--p-mint`) de 24 px de radio con día gigante de 58 px.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Pages/HomeDashboard.razor`**:
   - Reemplazo completo del layout de portada por los 6 bloques editoriales.
   - Enlace directo del buscador del hero al catálogo con el término preaplicado.
   - Cuenta atrás reactiva para el sorteo más urgente.
2. **Componentes Editoriales de Portada (`Components/Home/`)**:
   - `HeroRevistaLudica.razor`: Arco fotográfico + titular clamp + buscador píldora.
   - `TickerBar.razor`: Cinta de avisos con temporizador reactivo.
   - `WeeklyTopSection.razor`: Grid desktop con numerales ciclálicos y lista móvil táctil.
   - `PolaroidGiveawayCard.razor`: Tarjeta física estilo polaroid.
   - `UpcomingEventsGrid.razor`: Tarjetas de eventos pastel.
3. **Optimización Móvil**:
   - Banner de instalación PWA cuando aplique (`ludeka-offline.js`).
   - Cero layouts rotos en anchos inferiores a 390 px.
4. **Pruebas de Contrato y Regresión**:
   - Actualización de `HomeDashboardContractTests.cs` y tests de renderizado SSR interactivo.

## Criterios de Aceptación
- La portada carga con CLS 0 (sin saltos de layout en imágenes y fuentes).
- La cuenta atrás de la cinta de avisos y las polaroids se actualiza deterministamente.
- En móvil, los listados de Top 5 y Sorteos son compactos y no superan el ancho de pantalla.
