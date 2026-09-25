# 42. Sistema de Hitos y Logros del Jugador (Gamificación No Invasiva)

> **Incremento de Origen:** INC-67  
> **Alcance:** Dominio (`Ludeka.Core`), Persistencia Dual SQLite/PostgreSQL (`Ludeka.Infrastructure`), Lógica de Aplicación (`Ludeka.Application`), Interfaz Web Blazor (`Ludeka.Web`) y Pruebas Automatizadas.  
> **Estado:** ✅ Archivado y Verificado (1.843 pruebas unitarias superadas al 100%).

---

## 1. Visión y Propósito Funcional

El sistema de hitos y logros de Ludeka celebra la trayectoria personal, la curiosidad y la dedicación de cada jugador en la mesa, reconociendo sus progresos en tres pilares lúdicos esenciales:

1. **Colección:** El cuidado, volumen y generosidad de la estantería física (primer título registrado, estantería en crecimiento, títulos jugados y préstamos a amigos).
2. **Partidas y Diario de Mesa:** La experiencia activa de juego (estreno del cuaderno de bitácora, constancia en registrar partidas y mesas llenas con grupos numerosos).
3. **Comunidad:** La participación activa y constructiva en el ecosistema (micro-reseñas honestas aportadas a la comunidad y valoraciones positivas emitidas).

### Invariantes y Principios de Diseño
- **Gamificación Cálida y No Invasiva:** Cero patrones oscuros o coercitivos. No existen rachas diarias que penalicen la desconexión, no hay moneda virtual ni clasificaciones jerárquicas competitivas.
- **Idempotencia y Preservación Temporal:** Un hito solo se desbloquea una vez por usuario. Si se vuelve a evaluar el progreso tras nuevas partidas o adquisiciones, la fecha original de desbloqueo (`UnlockedAt`) se preserva inmutable.
- **Convivencia con el Rango/Rasgo de Jugador (`ComputePlayerBadge` de INC-15):** El badge global de ADN lúdico sigue computando el estilo global y rango de estantería; los hitos constituyen medallas individuales que complementan y enriquecen la vitrina personal sin colisionar con aquel.
- **Frontera Estricta con INC-68:** Este incremento no implementa clasificaciones entre jugadores, comparaciones públicas competitivas ni *leaderboards*, reservadas para el consentimiento explícito y anonimato de INC-68.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Categorías de Hitos (`MilestoneCategory`)
Ubicado en `Ludeka.Core.Enums.MilestoneCategory`:
```csharp
public enum MilestoneCategory
{
    Collection = 1,
    Plays = 2,
    Community = 3
}
```

### 2.2. Tipos de Hitos Canónicos (`MilestoneType`)
Ubicado en `Ludeka.Core.Enums.MilestoneType`:
1. `FirstGameInCollection`: «Primera Piedra» (primer título en ludoteca).
2. `TenGamesInCollection`: «Estantería Viva» (alcanzar 10 títulos en colección).
3. `FirstGamePlayed`: «Estrenando Tableros» (primer juego marcado como jugado).
4. `TenGamesPlayed`: «Curtido en Mesa» (10 títulos jugados).
5. `FirstGameLoaned`: «Biblioteca Amiga» (primer préstamo registrado a un compañero).
6. `FirstPlayLogged`: «Cuaderno de Bitácora» (primera partida registrada en el diario).
7. `FivePlaysLogged`: «Mesa Frecuente» (5 partidas registradas en el diario).
8. `TableOfFivePlayers`: «Mesa Llena» (partida registrada con 5 o más comensales).
9. `FirstReviewWritten`: «Voz en la Mesa» (primera micro-reseña publicada).
10. `FirstLikeGiven`: «Buen Ojo» (primer «me gusta» emitido a editoriales, tiendas, creadores o medios).

### 2.3. Entidad de Dominio `UserMilestone`
Ubicada en `Ludeka.Core.Entities.UserMilestone`:
- Clave natural `(UserId, Type)`.
- Validaciones en constructor inmutable:
  - `UserId` obligatorio y no vacío (`ArgumentException.ThrowIfNullOrWhiteSpace`).
  - `MilestoneType` definido en el enumerado (`ArgumentOutOfRangeException`).
  - `UnlockedAt` establecido por defecto a `DateTimeOffset.UtcNow`.
  - Cero dependencias de infraestructura ni frameworks.

### 2.4. Catálogo Canónico de Hitos (`MilestoneCatalog`)
Ubicado en `Ludeka.Core.ValueObjects.MilestoneCatalog`:
Proporciona la definición inmutable de cada hito (`MilestoneDefinition`) con título evocador, descripción explicativa, emoji de medalla, categoría y orden secuencial.

---

## 3. Persistencia e Infraestructura (`Ludeka.Infrastructure`)

### 3.1. Contrato `IUserMilestoneRepository`
Ubicado en `Ludeka.Application.Contracts.IUserMilestoneRepository`:
- `GetByUserIdAsync(string userId, CancellationToken ct)`: recupera los hitos desbloqueados del usuario.
- `HasMilestoneAsync(string userId, MilestoneType type, CancellationToken ct)`: consulta booleana rápida de desbloqueo.
- `AddAsync(UserMilestone milestone, CancellationToken ct)`: inserción atómica e idempotente.
- `AddBatchAsync(IEnumerable<UserMilestone> milestones, CancellationToken ct)`: inserción múltiple para inicializaciones o sincronizaciones en lote.

### 3.2. Implementación SQLite (`SqliteUserMilestoneRepository`)
- Integrado en `LudekaDbContext` con `DbSet<UserMilestone>`.
- Mapeo relacional en `OnModelCreating`: clave primaria compuesta `(UserId, Type)` e índice secundario por `(UserId, UnlockedAt)`.
- Ordenación en memoria tras recuperación asíncrona para sortear la limitación de SQLite con tipos `DateTimeOffset`.

### 3.3. Migración Defensiva de Esquema
- **SQLite (`SqliteSchemaMigrator`):** Paso de migración 31 creando la tabla `UserMilestones` y sus índices si no existen.
- **PostgreSQL Supabase (`supabase_schema.sql`):** Bloque DDL `CREATE TABLE IF NOT EXISTS "UserMilestones"` cumpliendo las pruebas arquitectónicas de frescura (`SupabaseSchemaFreshnessTests`).

---

## 4. Lógica de Aplicación (`Ludeka.Application`)

### 4.1. Servicio `IMilestoneService` / `MilestoneService`
Ubicado en `Ludeka.Application.Features.Milestones.MilestoneService`:
- Evalúa el estado del jugador cruzando:
  - `IUserCollectionRepository`: recuentos de títulos en posesión y jugados.
  - `IGameLoanRepository`: préstamos activos o históricos registrados.
  - `IGamePlayLogRepository`: recuento de partidas y detección de mesas de 5+ jugadores.
  - `IUserReviewRepository`: recuento de micro-reseñas aportadas por el usuario.
  - `IUserLikeRepository`: recuento de votos «me gusta» emitidos.
- Preserva de forma determinista la fecha del primer desbloqueo: los hitos existentes no se reescriben ni alteran su marca temporal.
- Devuelve `UserMilestoneProgressDto` conteniendo el recuento total de desbloqueados, porcentaje de completitud y listado ordenado de hitos con su estado.

---

## 5. Interfaz de Usuario y Presentación Editorial (`Ludeka.Web`)

### 5.1. Componente `MilestonesCard.razor`
Ubicado en `src/Ludeka.Web/Components/Shared/MilestonesCard.razor`:
- Diseño cálido y editorial alineado con el sistema de diseño de Ludeka.
- Barra de progreso accesible con `role="progressbar"`, `aria-valuenow`, `aria-valuemin="0"`, `aria-valuemax="100"` y etiqueta legible.
- Badges de estado distinguidos:
  - **Desbloqueado:** fondo sutilmente realzado, medalla a todo color, insignia esmeralda con fecha formateada en estándar internacional/español.
  - **Por desbloquear:** transparencia moderada, icono desaturado/atenuado e insignia sutil «Por desbloquear».
- Total compatibilidad con modo oscuro y contraste WCAG 2.2 AA.

### 5.2. Integración en `PublicProfile.razor`
- Inyección asíncrona de `IMilestoneService` en la página de perfil público (`/u/{UserId}` y `/perfil/{UserId}`).
- Carga de los hitos del jugador junto a las estadísticas de colección, mostrándolos en una vitrina destacada inmediatamente bajo el panel de ADN Lúdico.

---

## 6. Pruebas Automatizadas y Verificación

El incremento INC-67 incorpora una batería completa de pruebas unitarias bajo Strict TDD:
- **Dominio:** `UserMilestoneTests.cs` (10 pruebas unitarias ejercitando validaciones de constructor, inmutabilidad y coherencia del catálogo).
- **Persistencia:** `SqliteUserMilestoneRepositoryTests.cs` (6 pruebas unitarias verificando inserción, consulta, idempotencia ante claves duplicadas y recuperación en lote).
- **Aplicación:** `MilestoneServiceTests.cs` (6 pruebas unitarias comprobando el desbloqueo según colección, partidas, préstamos, reseñas, likes e idempotencia fechada).
- **Presentación Web:** `MilestonesCardTests.cs` (4 pruebas unitarias headless verificando renderizado de progreso, badges de desbloqueo, estados pendientes y accesibilidad).

Total acumulado verificado en la solución tras INC-67: **1.843 pruebas unitarias superadas al 100% (0 fallos, 0 omitidos)**.
