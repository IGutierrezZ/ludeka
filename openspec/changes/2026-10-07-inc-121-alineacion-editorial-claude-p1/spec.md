# Especificación de Requisitos: INC-121 — Alineación Editorial de Claude (Parte 1)

## Requisitos Funcionales

### RF-01: Cabecera Global y Retirada de Campana
- **RF-01.1:** Retirar el enlace a `/admin/notificaciones` con el icono `bell` de la barra superior pública en `MainLayout.razor`.
- **RF-01.2:** En `MainLayout.razor`, cuando la ruta activa sea `tendencias` o `tendencias/*`, el método `GetHeaderColors()` debe retornar fondo `var(--brand)`, texto `var(--on-brand)` y acento `var(--mustard)`.
- **RF-01.3:** En `MainLayout.razor`, el pie de página debe retirar el lema `Juegos, sorteos, eventos y opiniones de verdad.` y mantener únicamente `Ludeka © 2026` con espaciado vertical homogéneo.

### RF-02: Barra de Gestión Contextual (`StaffBar`)
- **RF-02.1:** `StaffBar.razor` detectará la ruta actual (`CurrentRelativePath`) y renderizará acciones específicas:
  - **Portada (`""` o `"/"`):** Botón `Editar destacados de portada` + `Panel de gestión →`.
  - **Catálogo (`"catalogo"`):** Botón `+ Añadir juego desde BGG` + `Panel de gestión →`.
  - **Sorteos (`"sorteos"` o `"radar"`):** Botón `+ Alta exprés` + `Panel de gestión →`.
  - **Ficha de sorteo (`"sorteos/{id}"` o `"sorteo/{id}"`):** Botones `Editar sorteo` y `Eliminar sorteo` + `Panel de gestión →`.
  - **Otras pantallas:** Badge `Gestión · [Rol]` + `Panel de gestión →`.
- **RF-02.2:** La barra contextual mantendrá los márgenes y alineación `max-w-[1280px]` consistentes con el diseño de Claude.

### RF-03: Destacados y Cuentas Atrás de Portada
- **RF-03.1:** En `HomeDashboard.razor`, la sección de destacados mostrará 3 tarjetas predeterminadas del día:
  1. Sorteo a punto de expirar.
  2. Última novedad confirmada.
  3. Próximo gran evento lúdico.
- **RF-03.2:** Los moderadores contarán con un modal accesible para gestionar los 3 destacados activos, permitiendo intercambiar cualquiera de ellos por títulos en tendencia del día.
- **RF-03.3:** En `GiveawayService.CalculateRemainingTime`, se suprime el prefijo "Queda / Quedan", devolviendo `1 día`, `{days} días`, `Finaliza hoy` o `Finalizado`.

### RF-04: Numerales del Catálogo de Juegos
- **RF-04.1:** En `GameCard.razor`, el contenedor relativo que alberga el numeral no aplicará `overflow-hidden`, permitiendo que el número flote en la esquina superior izquierda (`-top-3.5 -left-1.5` o `-top-4 -left-2`) con sombra `text-shadow: 0 2px 0 var(--paper)`.
- **RF-04.2:** El número de posición se formateará siempre con dos dígitos (`01`, `02`, `03`...) mediante `.ToString("00")`.

### RF-05: Catálogo de Sorteos y Ofertas
- **RF-05.1:** En `Radar.razor`, retirar el botón "Mis Alertas" y el botón "Alta Exprés" de la cabecera hero.
- **RF-05.2:** En las tarjetas de sorteos, el botón con icono de lápiz (✎) abrirá el modal de edición de datos del sorteo, en lugar de alternar el estado de promoción.
- **RF-05.3:** Añadir paginación por bloques (ej. 10 elementos por página) tanto en la pestaña de Sorteos como en la pestaña de Ofertas.

### RF-06: Ficha de Sorteo y Modal de Edición
- **RF-06.1:** En `GiveawayDetail.razor`, cambiar el enlace de retorno a `← Todos los sorteos`.
- **RF-06.2:** Mover las acciones "Editar" y "Eliminar" de la cabecera del sorteo a la `StaffBar`.
- **RF-06.3:** Añadir el enlace `Reportar sorteo` en el subheader, con modal de reporte (motivo + comentario).
- **RF-06.4:** Actualizar el modal de edición de sorteo para utilizar exclusivamente los tokens editoriales (`var(--paper)`, `var(--rule)`, `var(--card)`, `var(--ink)`).
