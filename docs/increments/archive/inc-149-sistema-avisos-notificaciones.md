# Incremento 149: Sistema de Avisos, Campana de Notificaciones, Web Push (PWA/Navegador) y Contador Administrativo Real

## 📌 Identificador ODD / SDD
`inc-149-sistema-avisos-notificaciones`

## 🎯 Estado
⏳ En progreso

---

## 1. Contexto y Objetivos
1. **Campana e indicador de avisos en el menú de usuario (`AccountMenu.razor`)**:
   - Cuando el menú de usuario no está desplegado, el botón del icono de usuario muestra un círculo rojo con el número de notificaciones pendientes de leer (y para administradores/staff, indicador de tareas pendientes).
   - Al desplegar el menú, incluye el acceso **«Notificaciones»** con icono de campana (`bell`) y el contador de avisos sin leer, dirigiendo a la página dedicada `/cuenta/notificaciones`.
2. **Página dedicada de Notificaciones (`/cuenta/notificaciones`)**:
   - Las notificaciones no se quitan ni se marcan como leídas solas: el usuario debe pulsar en cada una para que pase a estado leído.
   - Permite consultar el historial de avisos, gestionar las alertas activas por juego (bajada de precio, reimpresión, reestock) y activar las notificaciones nativas del móvil (PWA) o navegador.
3. **Avisos de administración reales para Staff (`IAdminPendingSummaryService`)**:
   - Los administradores y moderadores ven el número real de asuntos pendientes de resolver en el área de gestión (suma de ingesta social pendiente, reportes comunitarios de catálogo abiertos, novedades editoriales pendientes de moderación y vídeos pendientes), sustituyendo el valor fijo de respaldo en `AccountMenu.razor`.
4. **Notificaciones nativas en móvil (PWA) y navegador (Web Push API + VAPID)**:
   - Soporte en `service-worker.js` (`push` y `notificationclick`) e interoperabilidad JS para registrar la suscripción Web Push del dispositivo y recibir avisos nativos en el móvil o navegador.
5. **Cuatro disparadores de avisos separados**:
   - **Bajada de precio ($\ge 15\%$ sobre PVP base):** Al marcar un juego como «Lo quiero» (`WantToBuy`), se pregunta al usuario si quiere activar el aviso de bajada de precio ($\ge 15\%$ respecto al PVP base), además de disponer de su botón propio en la ficha.
   - **Reimpresión:** Botón independiente en la ficha del juego para avisar cuando la web detecte una reimpresión (`WeeklyRelease` con `IsReprint = true`).
   - **Reestock:** Botón independiente en la sección de tiendas de la ficha cuando el juego no tenga stock en ninguna tienda, avisando cuando vuelva a detectarse disponibilidad.
   - **Sorteos:** Interruptor en el panel de cuenta (`/cuenta`) para apuntarse a avisos de nuevos sorteos dados de alta en la web.
