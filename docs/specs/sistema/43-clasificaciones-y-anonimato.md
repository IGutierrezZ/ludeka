# 43. Gamificación — Clasificaciones Públicas y Anonimato

> **Incremento de Origen:** INC-68  
> **Alcance:** Dominio (`Ludeka.Core`), Persistencia Dual SQLite/PostgreSQL (`Ludeka.Infrastructure`), Casos de Uso y Servicios (`Ludeka.Application`), Interfaz de Usuario Blazor (`Ludeka.Web`) y Pruebas Automatizadas.  
> **Estado:** ✅ Archivado y Verificado (1.876 pruebas unitarias superadas al 100%).

---

## 1. Visión y Propósito Funcional

El módulo de clasificaciones públicas de Ludeka ofrece reconocimiento comunitario a los jugadores más activos en mesa, respetando estrictamente el derecho al anonimato y la privacidad de cada participante. Resuelve la comparación entre jugadores (§7.4 de la visión de producto) garantizando un entorno sano, libre de toxicidad y sin patrones oscuros:

1. **Opt-in Explicito y Obligatorio (Default-Deny):** Ningún usuario aparece en ninguna clasificación comunitaria sin su consentimiento explícito previo (`LeaderboardOptIn == true`). Los perfiles no consentidos son excluidos en consulta, garantizando cumplimiento RGPD por diseño.
2. **Anonimato Estricto y Anti-Doxxing:** Los jugadores pueden participar en modo anónimo (`LeaderboardAnonymous == true`). En este modo, su nombre de cuenta, avatar, país y enlace a `/u/{userId}` son suprimidos por completo, mostrándose un seudónimo determinista no reversible o un alias personalizado seguro.
3. **Ventana Temporal Mensual:** La clasificación se evalúa por meses naturales (del primer al último día del mes en UTC), impidiendo la fosilización de tablas históricas acumulativas y premiando la actividad reciente.
4. **Métrica V1 Centrada en Mesa Real:** Ordenación por número de partidas registradas en el cuaderno de bitácora (`GamePlayLog`) durante el mes, condecoradas visualmente con el recuento de hitos desbloqueados de INC-67 (`UserMilestone`) y distintivo honorífico de jugador.
5. **Desempate Determinista e Idempotencia:** Ordenación estable e inalterable ante eventos repetidos: Partidas descendente $\rightarrow$ Fecha de primera partida ascendente $\rightarrow$ Identificador de usuario ascendente.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Preferencias de Clasificación en `UserPreference`
Ubicado en `src/Ludeka.Core/Entities/UserPreference.cs`:
- `LeaderboardOptIn` (`bool`, por defecto `false`): Determina la inclusión en clasificaciones públicas.
- `LeaderboardAnonymous` (`bool`, por defecto `false`): Activa la ofuscación de identidad pública.
- `LeaderboardPseudonym` (`string?`): Alias opcional elegido por el usuario (máximo 30 caracteres, anti-PII).
- Método de mutación de dominio `SetLeaderboardPreferences(bool optIn, bool anonymous, string? pseudonym = null)`.

### 2.2. Generador y Validador Anti-PII `PseudonymGenerator`
Ubicado en `src/Ludeka.Core/Helpers/PseudonymGenerator.cs`:
- **Máscara Determinista No Reversible:** Si el usuario es anónimo y no define un alias propio, genera `Mesa #XXXX`, donde `XXXX` son los primeros 4 dígitos hexadecimales de un hash criptográfico `SHA-256(userId)`.
- **Validación Anti-PII Estricta (`IsValidCustomPseudonym`):**
  - Rechaza correos electrónicos (detección de carácter `@`).
  - Rechaza enlaces y esquemas web (`http://`, `https://`, `www.`).
  - Rechaza extensiones de dominio comerciales o territoriales (`.com`, `.es`, `.net`, `.org`).
  - Límite de longitud estricto de 30 caracteres.

---

## 3. Contratos, DTOs y Casos de Uso (`Ludeka.Application`)

### 3.1. DTOs de Clasificación
Ubicado en `src/Ludeka.Application/DTOs/LeaderboardDtos.cs`:
- `LeaderboardEntryDto`: Representa una fila en la tabla de clasificación (`Rank`, `DisplayName`, `IsAnonymous`, `PublicProfileUrl`, `TotalPlaysThisMonth`, `UnlockedMilestonesCount`, `BadgeName`, `Country`).
- `MonthlyLeaderboardDto`: Resumen agregado del mes (`Year`, `Month`, `MonthLabel`, `PeriodStart`, `PeriodEnd`, `TotalParticipants`, `TotalPlaysLogged`, `Entries`, `CurrentUserHasOptedIn`, `CurrentUserRank`).
- `LeaderboardParticipationDto`: Consulta de estado de participación para ajustes de privacidad (`UserId`, `OptIn`, `Anonymous`, `CustomPseudonym`, `EffectivePseudonym`).

### 3.2. Contratos de Servicio y Repositorio
- `ILeaderboardService`:
  - `Task<MonthlyLeaderboardDto> GetMonthlyLeaderboardAsync(int? year = null, int? month = null, CancellationToken ct = default);`
  - `Task<LeaderboardParticipationDto> GetUserParticipationAsync(string userId, CancellationToken ct = default);`
  - `Task<LeaderboardParticipationAsync(string userId, bool optIn, bool anonymous, string? customPseudonym = null, CancellationToken ct = default);`
- `ILeaderboardRepository`:
  - `Task<List<MonthlyUserPlayAggregationDto>> GetMonthlyPlayAggregationsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);`

### 3.3. Lógica de Aplicación en `LeaderboardService`
Ubicado en `src/Ludeka.Application/Features/Gamification/LeaderboardService.cs`:
- Filtra participantes cruzando agregaciones de partidas con `UserPreference` donde `LeaderboardOptIn == true`.
- Si el usuario que consulta está autenticado y tiene opt-in, computa su posición exacta `CurrentUserRank`.
- Condecora cada entrada con los hitos conseguidos (`IMilestoneRepository`) y distintivo honorífico (`ComputePlayerBadge`).
- Si `IsAnonymous` es verdadero, anonimiza el nombre a través de `PseudonymGenerator.Generate(...)`, anula `PublicProfileUrl` y suprime `Country`.

---

## 4. Persistencia Dual (`Ludeka.Infrastructure`)

### 4.1. Migración EF Core y Snapshot
- Migración `20260925231052_AddLeaderboardPreferences.cs` incorporando las columnas `LeaderboardOptIn`, `LeaderboardAnonymous` y `LeaderboardPseudonym` en la tabla `UserPreferences`.
- Snapshot del modelo `LudekaDbContextModelSnapshot.cs` sincronizado.
- Canario de integración de esquema PostgreSQL actualizado a 13 migraciones en `tests/Ludeka.IntegrationTests/PostgresSchemaVerificationTests.cs`.

### 4.2. Reconciliador SQLite (`SqliteSchemaMigrator`)
- Incorpora las columnas en la definición canónica `CREATE TABLE IF NOT EXISTS UserPreferences`.
- Migración defensiva en caliente para bases SQLite existentes ejecutando `ALTER TABLE UserPreferences ADD COLUMN ...` condicionado a la inspección de `PRAGMA table_info(UserPreferences)`.

### 4.3. Repositorio de Clasificaciones (`SqliteLeaderboardRepository`)
- Ubicado en `src/Ludeka.Infrastructure/Data/SqliteLeaderboardRepository.cs`.
- Proyección ligera `{ p.UserId, p.PlayDate }` y agregación temporal en memoria para evitar errores de traducción LINQ a SQLite sobre el tipo `DateTimeOffset`.

---

## 5. Interfaz de Usuario y Accesibilidad (`Ludeka.Web`)

### 5.1. Ajustes de Privacidad y Anonimato (`AccountPrivacy.razor`)
- Ubicado en `/cuenta/privacidad`.
- Interruptores accesibles e interactivos para:
  - Participar en la clasificación mensual de partidas.
  - Modo anónimo (ocultar perfil y país bajo seudónimo).
  - Entrada de texto para seudónimo personalizado opcional con validación anti-PII y visualización previa de la máscara efectiva (`_effectivePseudonym`).

### 5.2. Página Pública de Clasificaciones (`Leaderboards.razor`)
- Ubicado en `/clasificaciones`.
- Selector accesible de mes/año con botones anterior/siguiente y acceso rápido al mes actual.
- Banner contextual según estado de sesión y participación del usuario.
- Podio de Honor con tarjetas destacadas para los 3 primeros puestos (corona y distintivos).
- Tabla completa accesible con encabezados semánticos (`scope="col"`), fila destacada para el usuario conectado y badges anti-doxxing para cuentas anónimas.
- Manifiesto explicativo de privacidad y reglas de mesa al pie de página.

### 5.3. Navegación Global (`MainLayout.razor`)
- Enlace directo a `/clasificaciones` en la barra de navegación de escritorio, en el menú móvil y en el pie de página.

---

## 6. Pruebas Automatizadas y Certificación

La funcionalidad cuenta con cobertura integral de pruebas unitarias y de contrato (1.876 pruebas unitarias pasando al 100%):
- **Dominio:** `PseudonymGeneratorTests.cs` (7 pruebas), `UserPreferenceTests.cs` (2 pruebas).
- **Servicios:** `LeaderboardServiceTests.cs` (7 pruebas), `SqliteUserPreferenceServiceTests.cs`.
- **Persistencia:** `SqliteLeaderboardRepositoryTests.cs` (3 pruebas), `SqliteSchemaMigratorTests.cs` (1 prueba).
- **Contratos Web:** `AccountPreferencesContractTests.cs` (5 pruebas), `LeaderboardsPageContractTests.cs` (5 pruebas).
