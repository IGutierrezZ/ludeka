# Informe de Verificación SDD: Sistema de Avisos, Campana de Notificaciones, Web Push (PWA/Navegador) y Contador Administrativo Real (INC-149)

## 1. Resumen Ejecutivo
- **Incremento:** `INC-149` (`sistema-avisos-notificaciones`)
- **Estado de Verificación:** ✅ **VERIFICADO AL 100%**
- **Cobertura de Requerimientos:** 100% de los criterios funcionales, de arquitectura y de interfaz confirmados por el usuario.

---

## 2. Matriz de Verificación de Requerimientos (`spec.md`)

| Requerimiento | Estado | Evidencia de Implementación y Pruebas |
| :--- | :---: | :--- |
| **R1. Campana en Menú de Usuario (`AccountMenu.razor`) y Círculo Rojo Colapsado** | ✅ | `AccountMenu.razor` muestra un badge circular rojo (`bg-rose-600 text-white`) en el botón de usuario cuando el menú no está desplegado (`!_isOpen && HeaderBadgeCount > 0`) y añade la entrada **«Notificaciones»** con icono `bell` hacia `/cuenta/notificaciones`. |
| **R2. Contador Administrativo Real en Panel de Gestión** | ✅ | `IAdminPendingSummaryService` y `AdminPendingSummaryService` calculan en tiempo real la suma de pendientes en moderación social, reportes de catálogo y lanzamientos semanales (`TotalPendingCount`), eliminando el literal estático `"21"` en `AccountMenu.razor` y mostrando el desglose en `/cuenta/notificaciones`. |
| **R3. Página Dedicada `/cuenta/notificaciones` con Lectura Individual Explícita** | ✅ | `AccountNotifications.razor` lista las notificaciones recibidas y mantiene cada aviso como `Sin leer` (`IsRead = false`) hasta que el usuario pulsa explícitamente sobre la tarjeta o su botón `Marcar leído` (`MarkAsReadAsync`), actualizando reactivamente el contador de `AccountMenu.razor` mediante `OnNotificationsChanged`. |
| **R4. Notificaciones Nativas en Móvil (PWA) y Navegador (Web Push VAPID)** | ✅ | `wwwroot/service-worker.js` incorpora los eventos `push` y `notificationclick`; `wwwroot/js/ludeka-push.js` gestiona la suscripción PushManager con clave pública VAPID; `VapidWebPushNotificationSender` cifra y envía los avisos conforme a RFC 8291 (`aes128gcm`) y RFC 8292 (`ES256`). |
| **R5. Aviso de Bajada de Precio ($\ge 15\%$ sobre PVP Base) y Sugerencia al Pulsar «Lo quiero»** | ✅ | `GameDetail.razor` incluye el botón independiente **«Avísame de oferta (-15% PVP)»** y, al marcar el juego como **«Lo quiero»** (`CollectionStatus.WantToBuy`), abre el modal `_isPriceDropPromptModalOpen` preguntando si se desea activar la alerta. `UserNotificationService.EvaluatePriceAndStockAlertsAsync` exige un descuento mínimo del 15% (`MinimumPriceDropRatio = 0.15m`) sobre el PVP base. |
| **R6. Aviso de Reimpresión Separado** | ✅ | `GameDetail.razor` expone el botón independiente **«Avísame de reimpresión»** (`GameAlertType.Reprint`), disparado desde `WeeklyReleaseService` y `SocialIngestionService` al confirmar una reimpresión. |
| **R7. Aviso de Reestock Separado** | ✅ | `StoreOffersCard.razor` (cuando no hay stock confirmado en tiendas) y `GameDetail.razor` exponen el botón independiente **«Avísame de reestock»** (`GameAlertType.Restock`), disparado al detectar stock positivo en `PriceRadarService`. |
| **R8. Aviso de Nuevos Sorteos desde el Panel de Usuario** | ✅ | `Account.razor` y `AccountNotifications.razor` permiten activar el interruptor `NotifyNewGiveaways`, disparando avisos automáticos al publicar o aprobar un sorteo en `GiveawayService` y `SocialIngestionService`. |

---

## 3. Pruebas Automatizadas Ejecutadas
- **`UserNotificationServiceTests`:**
  - `EvaluatePriceAndStockAlertsAsync_WhenDiscountIsBelow15Percent_DoesNotNotify` (verifica que un 10% de descuento no genera aviso).
  - `EvaluatePriceAndStockAlertsAsync_WhenDiscountIsAtLeast15Percent_CreatesUnreadNotificationAndSendsPush` (verifica que un $\ge 15\%$ genera notificación no leída, envía Web Push y evita duplicados ante el mismo precio).
  - `DispatchReprintAndRestockAndGiveawayNotifications_CreateUnreadNotificationsUntilExplicitClick` (verifica avisos de reimpresión, reestock y sorteos, y que permanecen sin leer hasta pulsar individualmente `MarkAsReadAsync`).
- **`UserNotificationsUiContractTests`:**
  - Verifica contratos de marcado en `AccountMenu.razor`, `AccountNotifications.razor`, `GameDetail.razor`, `StoreOffersCard.razor` y `service-worker.js`.
- **Suite Global (`Ludeka.UnitTests`):** 100% en verde, 0 errores.
