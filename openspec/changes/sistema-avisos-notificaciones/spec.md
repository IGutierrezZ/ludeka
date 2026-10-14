# Especificación Funcional y Técnica: Sistema de Avisos, Campana de Notificaciones, Web Push y Contador Administrativo Real (INC-149)

## 1. Requisitos Funcionales y Escenarios (Gherkin)

### RF-01: Indicador en Menú de Usuario (`AccountMenu.razor`) y Campana de Notificaciones
- El botón colapsado del menú de usuario muestra un círculo rojo (`bg-rose-600 text-white`) con el número de notificaciones sin leer del usuario cuando dicho contador es mayor que `0`.
- Si el usuario tiene rol de administración o moderación (`Admin` o `Moderator`) y existen elementos pendientes de resolver en gestión, el indicador del botón colapsado refleja la suma de notificaciones sin leer más pendientes de administración (o prioriza mostrar que hay elementos pendientes de atención).
- Dentro del menú desplegado se incluye el enlace **«Notificaciones»** con icono de campana (`bell`), redirigiendo a `/cuenta/notificaciones` y mostrando la insignia roja con el contador de notificaciones sin leer.

```gherkin
Escenario: Usuario con notificaciones sin leer ve el círculo rojo en el icono de cuenta
  Dado un usuario autenticado con 3 notificaciones en estado IsRead = false
  Cuando se renderiza el componente AccountMenu en estado colapsado
  Entonces el botón de usuario muestra un círculo rojo con el número "3"
  Y al desplegar el menú aparece la opción "Notificaciones" con icono "bell" y el badge "3"
```

### RF-02: Contador Real de Tareas Pendientes de Administración
- Se elimina cualquier valor literal de respaldo (`"21"`) en `AccountMenu.razor`.
- El servicio `IAdminPendingSummaryService` calcula en tiempo real los elementos pendientes de resolución agregando:
  1. Ítems pendientes en la bandeja social (`SocialInboxItems` con `Status == SocialInboxStatus.Pending`).
  2. Reportes de incidencias de catálogo abiertos o en curso (`GameIssueReports` con `Status == Open || Status == InProgress`).
  3. Lanzamientos editoriales pendientes de moderación (`WeeklyReleases` con `Status == WeeklyReleaseStatus.PendingModeration`).
- En el menú desplegado de administradores/moderadores, el enlace **«Panel de gestión»** muestra el badge únicamente cuando `TotalPending > 0`, con el número exacto.

```gherkin
Escenario: Administrador visualiza el número real de tareas pendientes en el menú
  Dado un administrador autenticado
  Y existen 4 ítems pendientes en SocialInbox, 2 reportes de catálogo abiertos y 1 novedad pendiente de moderación
  Cuando despliega el menú de usuario
  Entonces el enlace "Panel de gestión" muestra el badge con el valor "7"
  Y si todos los contadores son 0 no se muestra ningún número ficticio
```

### RF-03: Centro de Notificaciones (`/cuenta/notificaciones`) y Lectura Individual
- Las notificaciones de usuario (`UserNotification`) permanecen con `IsRead = false` hasta que el usuario pulsa explícitamente sobre cada notificación (o utiliza el botón secundario de marcar todas como leídas).
- Al pulsar sobre una notificación individual sin leer:
  - Se invoca `MarkAsReadAsync(notificationId, userId)`.
  - Se actualiza inmediatamente el contador de la campana mediante un evento de estado en `IUserNotificationService`.
  - Si la notificación dispone de `TargetUrl`, el usuario puede navegar directamente al recurso (ficha del juego o página de sorteos).
- La página `/cuenta/notificaciones` incluye además tres bloques de gestión:
  1. **Notificaciones en este dispositivo (Web Push PWA / Navegador)**: estado de soporte, permiso actual y botón para activar o desactivar avisos nativos en el móvil o navegador.
  2. **Aviso de nuevos sorteos**: interruptor conectado a `UserPreference.NotifyNewGiveaways`.
  3. **Mis alertas activas en juegos**: listado de suscripciones activas (`PriceDrop`, `Reprint`, `Restock`) con opción de eliminarlas individualmente.

```gherkin
Escenario: Las notificaciones no se marcan como leídas automáticamente al abrir la página
  Dado un usuario con 2 notificaciones sin leer
  Cuando accede a "/cuenta/notificaciones"
  Entonces las 2 notificaciones siguen mostrándose como no leídas con indicador visual destacado
  Cuando el usuario pulsa sobre la primera notificación
  Entonces la primera notificación pasa a estado leído (IsRead = true y ReadAtUtc asignado)
  Y el contador de notificaciones sin leer en AccountMenu pasa de 2 a 1
```

### RF-04: Alerta de Bajada de Precio ($\ge 15\%$ sobre PVP Base) y Prompt en «Lo quiero»
- En la ficha de juego (`GameDetail.razor`), cuando el usuario pulsa **«Lo quiero»** (`CollectionStatus.WantToBuy`) para añadirlo a su colección y aún no tiene activa la alerta de bajada de precio para ese juego, se presenta un modal preguntando si desea activar el aviso de bajada de precio (mínimo un $15\%$ por debajo del PVP base).
- Adicionalmente, en la ficha de juego se muestra un botón independiente **«Avísame de oferta (-15%)»** / **«Alerta de oferta activa»** para activar o desactivar la suscripción `GameAlertType.PriceDrop` en cualquier momento.
- Cuando el radar de precios (`PriceRadarService`) registra una oferta en stock (`InStock == true`) cuyo precio cumple $\text{PrecioOferta} \le \text{PvpBase} \times 0.85$, se genera una notificación `UserNotificationType.PriceDrop` (y envío Web Push si el usuario tiene suscripción activa) para los usuarios suscritos a ese juego, evitando duplicados para un mismo o mayor precio ya notificado.

```gherkin
Escenario: Disparo de alerta de bajada de precio al detectar al menos un 15% de descuento sobre el PVP base
  Dado un juego con PVP base de 50,00 € y un usuario suscrito a GameAlertType.PriceDrop
  Cuando se registra una oferta en stock a 42,00 € (16% de descuento)
  Entonces se crea una notificación de tipo PriceDrop para el usuario indicando el nuevo precio y el porcentaje de ahorro
  Pero si la oferta registrada es de 45,00 € (10% de descuento), no se genera notificación
```

### RF-05: Alerta de Reimpresión (`GameAlertType.Reprint`)
- En la ficha de juego (`GameDetail.razor`), el usuario dispone de un botón independiente **«Avísame de reimpresión»** para activar o desactivar la suscripción `GameAlertType.Reprint`.
- Cuando se aprueba o publica un lanzamiento editorial (`WeeklyRelease`) vinculado al `GameId` con `IsReprint == true` (tanto desde moderación manual en `WeeklyReleaseService` como en sincronización editorial `EditorialReleasesSyncService`), se notifica a todos los usuarios suscritos a `GameAlertType.Reprint` de ese juego.

### RF-06: Alerta de Reestock (`GameAlertType.Restock`)
- Cuando un juego no tiene stock en ninguna tienda registrada (todas las ofertas tienen `InStock == false`) o el usuario desea vigilar su disponibilidad, `StoreOffersCard.razor` y `GameDetail.razor` permiten activar el botón independiente **«Avísame de reestock»** (`GameAlertType.Restock`).
- Cuando `PriceRadarService` registra una oferta con `InStock == true` para dicho juego, se genera una notificación `UserNotificationType.Restock` para los suscriptores activos.

### RF-07: Alerta de Nuevos Sorteos (`NotifyNewGiveaways`)
- Desde `/cuenta` (`Account.razor`) y `/cuenta/notificaciones` (`AccountNotifications.razor`), el usuario puede activar la preferencia **«Avisarme cuando se publique un nuevo sorteo»** (`UserPreference.NotifyNewGiveaways`).
- Cuando se da de alta un sorteo activo (`GiveawayService.CreateGiveawayAsync` o promoción desde `SocialIngestionService`), se genera una notificación `UserNotificationType.NewGiveaway` para todos los usuarios con `NotifyNewGiveaways == true`.

### RF-08: Notificaciones Web Push (PWA Móvil y Navegador)
- `wwwroot/service-worker.js` escucha los eventos `push` (mostrando `self.registration.showNotification(...)`) y `notificationclick` (enfocando o abriendo la ventana de la PWA en la URL destino).
- `wwwroot/js/ludeka-push.js` expone funciones de interoperabilidad para consultar soporte y permisos (`Notification.requestPermission()`), suscribir el navegador mediante `pushManager.subscribe` con clave pública VAPID y devolver `endpoint`, `p256dh` y `auth` para persistirlos en `UserPushSubscriptions`.
