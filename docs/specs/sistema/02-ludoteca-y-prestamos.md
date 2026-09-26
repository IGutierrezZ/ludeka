# 02. Ludoteca Personal, Colección, Préstamos y Diario de Partidas

## 1. Visión General y Propósito
Este módulo gestiona la colección lúdica individual de cada usuario con estados de pertenencia física y seguimiento de compra independientes del estado de juego (`IsPlayed`), el registro privado de préstamos a amigos o asociaciones ("¿A quién se lo dejé?"), el subsistema de Diario de Partidas (registro de sesiones y estadísticas lúdicas) y el sistema de valoración rápida de 45 segundos con desglose por número de comensales.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1 Entidad `UserCollectionItem`
Ubicación: [`src/Ludeka.Core/Entities/UserCollectionItem.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserCollectionItem.cs)

- `Id` (Guid), `UserId` (string), `GameId` (Guid?).
- `Status`: Enum `CollectionStatus?` (nullable):
  - `InCollection` (🟢 En mi ludoteca - copia física en propiedad).
  - `WantToBuy` (🛒 Comprar - radar activo de ofertas, sorteos y reimpresiones).
  - `null` (Cuando un título solo está marcado como "Jugado" sin pertenencia física ni intención de compra).
- `IsPlayed` (bool): Estado ortogonal e independiente que indica si el usuario ha jugado al juego (permite combinaciones `En mi ludoteca + Jugado`, `Comprar + Jugado`, etc.).
- `IsPendingCatalog`: booleano que indica si el título proviene de una importación de BGG aún en cola.
- `PendingBggId`: identificador BGG en espera de promoción.
- `AddedAt`, `UpdatedAt`.
- **Métodos de dominio:** `ChangeStatus(CollectionStatus? newStatus)`, `TogglePlayed()`, `SetPlayed(bool isPlayed)`, `PromoteToCataloged(Guid gameId)`.

### 2.2 Entidad `GamePlayLog` (Diario de Partidas)
Ubicación: [`src/Ludeka.Core/Entities/GamePlayLog.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/GamePlayLog.cs)

- `Id` (Guid), `UserId` (string), `GameId` (Guid).
- `PlayDate` (DateTimeOffset): Fecha de la sesión (no puede ser futura).
- `Location` (string): Espacio o lugar del juego (ej. "En casa", "Club / Asociación", "Bar lúdico").
- `PlayerCount` (int): Número de comensales (mínimo 1).
- `DurationMinutes` (int?): Duración opcional de la partida en minutos.
- `Comment` (string?): Crónica, notas o anécdotas de la sesión (máx. 500 caracteres).
- `CreatedAt` (DateTimeOffset).

### 2.3 Entidad `GameLoan` ("¿A quién se lo dejé?")
Ubicación: [`src/Ludeka.Core/Entities/GameLoan.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/GameLoan.cs)

- `Id` (Guid), `UserId` (string), `GameId` (Guid).
- `BorrowerName`: Nombre de la persona o asociación.
- `LoanDate`: Fecha de salida.
- `IsReturned`, `ReturnedDate`: Estado de devolución (`null` mientras esté activo).
- `Notes`: Anotaciones sobre componentes o fecha pactada.

### 2.4 Entidades de Reseña y Valoración
Ubicación: [`src/Ludeka.Core/Entities/UserGameReview.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserGameReview.cs) y [`UserPlayerCountVote.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserPlayerCountVote.cs)

- `Score`: Valor numérico entre 1.0 y 10.0 (con incrementos de 0.5).
- `MicroReview`: Micro-reseña con límite de 280 caracteres.
- `PlayerCountRatings`: Colección de votos por número de jugadores (`PlayerCount`, `Status` MustPlay/Recommended/NotRecommended).
- `FamilyExperience`: Indicadores opcionales si se jugó con niños (edad mínima real y adaptación de reglas).
- `PlayContext`: Selector de entorno (Propiedad, Asociación, Bar, Amigos, BGA).
- **Regla de Integridad de Negocio:** No se puede valorar un título que esté en la lista de compra (`WantToBuy`) sin haberlo jugado (`IsPlayed == true`). Al registrar una reseña sobre un título sin estado, se marca automáticamente como `IsPlayed = true`.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

- **Servicios:**
  - [`UserLibraryService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Library/UserLibraryService.cs): Gestión de estados, toggle de `IsPlayed`, préstamos y validación de reseñas.
  - [`GamePlayLogService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Plays/GamePlayLogService.cs): Registro de partidas (asegura automáticamente `IsPlayed = true` en la colección) y cálculo de analíticas de juego (`UserPlaysStatsDto`: total, comensales habituales, lugar favorito, partidas en el mes).
  - [`BggImportService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggImportService.cs): Importación retrocompatible de colecciones BGG, mapeando listas de deseos a `WantToBuy` e importando partidas históricas (`numPlays > 0`) directamente como `IsPlayed = true`.
- **Contratos:** [`IUserCollectionRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IUserCollectionRepository.cs), [`IGamePlayLogRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IGamePlayLogRepository.cs), [`IGameLoanRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IGameLoanRepository.cs), [`IUserReviewRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IUserReviewRepository.cs).

---

## 4. Componentes UI (`Ludeka.Web`)

- [`CollectionActionBar.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/CollectionActionBar.razor): Barra ergonómica con 3 acciones principales: `En mi ludoteca`, `Jugado` (independiente y coexistente), y `Comprar` (con aviso dinámico de radar de ofertas/sorteos). Incorpora badge de partidas jugadas y botón de acceso rápido `[ ➕ Registrar partida ]`.
- [`RecordPlayModal.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/RecordPlayModal.razor): Modal accesible para registro táctil rápido de sesiones con selector de fecha, contador de comensales, chips rápidos de lugar, duración y notas.
- [`MyLibrary.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MyLibrary.razor): Vista de ludoteca reestructurada con pestañas: `📚 En mi ludoteca`, `🎲 Jugados` (filtrado por `IsPlayed`), `🛒 Comprar` (con banner de radar de ofertas), `📝 Diario de Partidas` (métricas y listado cronológico de sesiones), `📦 Préstamos`, `⏳ Cola comunitaria`, `🎨 Apariencia` y `🧬 ADN y Estadísticas`.
- [`UserReviewCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/UserReviewCard.razor) y [`ReviewBottomSheet.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ReviewBottomSheet.razor): Bloqueo informativo si el usuario tiene el juego en lista de compra sin haberlo jugado.
- [`GameDetail.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor):
  - **Flujo Guiado de "En mi ludoteca" (INC-69):** Al hacer clic en "Tengo" sin haber marcado previamente "Jugado", se presenta un modal accesible: *«¿Has jugado al juego [Nombre]?»*.
    - **Sí:** Marca automáticamente `IsPlayed = true`, persiste la colección y abre el modal de valoración.
    - **No:** Guarda en la colección y muestra un aviso amigable recordando valorar y marcar como jugado cuando se estrene en mesa.
  - **Gestión Robusta de Excepciones:** Todas las operaciones interactivas (colección, jugado, reseña, préstamo) están protegidas en bloques `try/catch` con banner de feedback descartable (`_actionFeedbackMessage`), previniendo errores de UI no controlados.

---

## 5. Infraestructura y Persistencia Limpia (`Ludeka.Infrastructure` - INC-69)

- **Aislamiento de Grafo en EF Core:** En `SqliteUserCollectionRepository`, `SqliteUserReviewRepository` y `SqliteGameLoanRepository`, los métodos de mutación (`UpdateAsync` / `UpsertAsync`) aíslan la actualización a nivel de propiedades escalares de la entidad obtenida por `Id` en el contexto. Se evita invocar `Context.Update(item)` sobre instancias desasociadas con la propiedad de navegación `.Game` cargada, impidiendo que el agregador `Game` y sus entidades complejas JSON propiedad (`Scalability`, `Sleeves`, `AiSummary`) se marquen erróneamente como modificados en EF Core.
- **Suite de Pruebas Automatizadas:** Verificación contractual en `SqliteLibraryPersistenceTests.cs` garantizando actualizaciones libres de conflictos de seguimiento y concurrencia.

