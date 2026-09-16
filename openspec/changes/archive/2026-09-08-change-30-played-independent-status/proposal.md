# Propuesta de Cambio: change-30-played-independent-status

## 1. Resumen Ejecutivo
El **Incremento 30** transforma la experiencia de colección y seguimiento de Ludeka basándose en tres pilares fundamentales:
1. **Eliminación de «Deseado» y Consolidación en 3 Estados Claros:** Supresión del estado ambiguo `Wishlist` ("Deseado") y unificación de la barra de acciones en 3 botones de alto valor:
   - **`📚 En mi ludoteca`**: El usuario posee la copia física. Puede prestar el juego y valorarlo (si lo ha jugado).
   - **`🎲 Jugado`**: El usuario ha jugado al juego (en club, bar, evento o con amigos). No exige tenerlo ni querer comprarlo. Habilita la valoración y reseñas.
   - **`🛒 Comprar`** (*Deseo de compra / Seguimiento*): El usuario no lo tiene y desea adquirirlo. Puede haberlo jugado o no. Activa el radar de notificaciones de sorteos, descuentos y reimpresiones.
2. **Desacoplamiento Ortogonal de «Jugado»:**
   - La experiencia de juego (`IsPlayed = true`) es independiente de la posesión física o deseo de compra.
   - Un usuario puede marcar `Comprar` y además `Jugado` (`IsPlayed = true` + `Status = WantToBuy`).
   - Un usuario puede marcar `En mi ludoteca` y `Jugado`, o tenerlo sin jugar ("pila de la vergüenza").
   - Un usuario puede marcar únicamente `Jugado` sin tenerlo ni querer comprarlo.
3. **Regla de Negocio de Valoración Condicionada a Partida Jugada:**
   - **Solo se puede valorar un juego si ha sido jugado (`IsPlayed == true`).** Si no se ha jugado, la opción de valorar se bloquea con aviso explicativo o solicita marcarlo como jugado.
4. **Nuevo Subsistema de Registro de Partidas y Analíticas de Sesiones (`GamePlayLog`):**
   - Registro rápido de partidas: qué juego, qué día, dónde (casa, asociación/club, bar, online/BGA, etc.), cuántos jugadores y comentario/anécdotas.
   - Registrar una partida marca automáticamente el juego como **Jugado** (`IsPlayed = true`).
   - Diario y consulta de partidas en `Mi Ludoteca` (pestaña `🎲 Partidas`) y en la ficha de juego.
   - Analíticas y estadísticas lúdicas de partidas: total de partidas, juego más jugado, comensales habituales y lugar predilecto.

---

## 2. Decisiones de Producto y Arquitectura

### 2.1 Eliminación de «Deseado» y Fusión en «Comprar»
- `Wishlist` no aportaba funcionalidad diferencial real frente a `WantToBuy`. Al eliminarla:
  - Se simplifica la ergonomía de la barra de acciones a 3 botones grandes y directos.
  - Las filas históricas de la base de datos con `Status = 3` (`Wishlist`) se migran automáticamente a `Status = 4` (`WantToBuy`).
  - La lista de compra se vincula al radar de ofertas, sorteos locales e internacionales y avisos de reimpresión.

### 2.2 Regla de Integridad: No se Puede Valorar Sin Haber Jugado
- Para garantizar la calidad y autenticidad del consenso lúdico de la comunidad de Ludeka (Ludist Rating y desglose por comensales), solo los usuarios que declaren haber jugado al título pueden emitir puntuaciones de 1 a 10 y votos de número de comensales.

### 2.3 Entidad de Dominio: `GamePlayLog`
```csharp
public class GamePlayLog
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public Guid GameId { get; private set; }
    public DateTimeOffset PlayDate { get; private set; }
    public string Location { get; private set; }
    public int PlayerCount { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

## 3. Alcance de Componentes

1. **Dominio (`Ludeka.Core`):**
   - Actualización de `UserCollectionItem` con `IsPlayed` y `CollectionStatus? Status` (deprecando/eliminando `Wishlist`).
   - Nueva entidad `GamePlayLog` con validaciones de comensales (mínimo 1) y fecha.
2. **Aplicación (`Ludeka.Application`):**
   - Repositorio `IGamePlayLogRepository` y DTOs (`GamePlayLogDto`, `RecordPlayRequest`, `UserPlaysStatsDto`).
   - Servicio `GamePlayLogService` para registrar partidas y calcular analíticas.
   - `UserLibraryService` adaptado para 3 estados y regla de valoración condicionada a `IsPlayed`.
   - `BggImportService` adaptado para mapear wishlist de BGG a `WantToBuy` e `IsPlayed`.
3. **Infraestructura (`Ludeka.Infrastructure`):**
   - Nueva tabla `GamePlayLogs` e índices por `(UserId, PlayDate)` y `(GameId)`.
   - Migración SQLite en `SqliteSchemaMigrator`:
     - Columna `IsPlayed` en `UserCollectionItems`.
     - Migración de `Status = 2` (`Played`) a `IsPlayed = 1, Status = NULL`.
     - Migración de `Status = 3` (`Wishlist`) a `Status = 4` (`WantToBuy`).
     - Creación de tabla `GamePlayLogs`.
4. **Web UI (`Ludeka.Web`):**
   - `CollectionActionBar.razor`: Rediseñada a 3 botones (`En mi ludoteca`, `Jugado`, `Comprar`) con soporte de selección simultánea `Jugado + Comprar` o `Jugado + En mi ludoteca`.
   - `GameDetail.razor`: Botón `[ ➕ Registrar Partida ]`, historial de partidas propias del juego y bloqueo/desbloqueo de valoración según `IsPlayed`.
   - `RecordPlayModal.razor`: Modal accesible y táctil para registrar una partida en menos de 20 segundos.
   - `MyLibrary.razor`: Pestañas reestructuradas (`En mi ludoteca`, `Jugados`, `Comprar`, `Partidas`, `Préstamos`, `Apariencia & País`, `ADN y Estadísticas`).
