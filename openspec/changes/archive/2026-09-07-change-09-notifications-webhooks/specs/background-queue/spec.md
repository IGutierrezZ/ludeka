# Especificación: background-queue (Cola Asíncrona en Memoria y Despachador)

## 1. Contexto y Propósito
Garantiza que la emisión de notificaciones comunitarias sea completamente asíncrona, no bloqueante y desacoplada del ciclo de vida de las peticiones HTTP del usuario, mediante `System.Threading.Channels.Channel<T>` y un `BackgroundService`.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Encolado no bloqueante de mensajes
**Dado** un flujo de usuario activo (ej: publicación de veredicto o aceptación de respuesta)  
**Cuando** el servicio emite una notificación comunitaria  
**Entonces** el mensaje se deposita en `ICommunityNotificationQueue` en memoria mediante `EnqueueAsync`  
**Y** la llamada retorna inmediatamente sin esperar el despacho a Discord o Telegram.

### Escenario 2: Despacho asíncrono en segundo plano
**Dado** uno o más mensajes encolados en la cola en memoria  
**Cuando** `CommunityNotificationDispatcherHostedService` procesa los elementos  
**Entonces** crea un ámbito de inyección de dependencias (`IServiceScopeFactory`)  
**Y** despacha cada mensaje a través de `ICommunityNotificationService`  
**Y** persiste el resultado en la base de datos SQLite como `CommunityNotificationLog`.

### Escenario 3: Manejo de backpressure
**Dado** una ráfaga masiva de notificaciones que excede la capacidad del canal (capacidad acotada de 1.000 elementos)  
**Cuando** se intenta encolar un elemento adicional  
**Entonces** el canal aplica la política de espera asíncrona (`BoundedChannelFullMode.Wait`) sin descartar silenciosamente mensajes.
