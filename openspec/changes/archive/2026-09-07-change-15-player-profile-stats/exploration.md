# Exploración: change-15-player-profile-stats (Incremento 15: Estadísticas Avanzadas de Colección y ADN del Jugador)

## 1. Estado Actual de la Solución y Análisis de Brecha

### 1.1 Modelo de Datos de la Ludoteca Personal
- **Entidad `UserCollectionItem` ([`UserCollectionItem.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/UserCollectionItem.cs)):**
  - Registra la pertenencia de un juego a la ludoteca de un usuario bajo 4 estados: `InCollection`, `Played`, `Wishlist`, `WantToBuy`.
  - Vinculado a `Game` (o `BggId` / título pendiente si proviene de importación en cola).
  - Incluye `AddedAt` para temporalidad.
- **Entidad `Game` ([`Game.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs)):**
  - Contiene metadatos ricos necesarios para análisis estadístico:
    - `Duration`: `MinMinutes`, `MaxMinutes`, `EstimatedPerPlayerMinutes`.
    - `Style`: `Eurogame`, `Ameritrash`, `PartyGame`, `FillerAbstract`, `NarrativeCampaign`.
    - `Confrontation`: `Cooperative`, `Competitive`, `HiddenRolesOrTeams`, `SemiCooperative`.
    - `IsOfficialSolo`: Booleano.
    - `Designer`: Cadena de texto (puede contener múltiples autores separados por coma, barra o `&`).
    - `Publisher`: Cadena de texto de la editorial.
    - `Scalability`: Lista de `ScalabilityEntry` con comensales recomendados y `MustPlay`.
    - `Sleeves`: Lista de `SleeveItem` con formatos, dimensiones y `CardCount`.
- **Servicio `UserLibraryService` ([`UserLibraryService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Library/UserLibraryService.cs)):**
  - Provee resúmenes básicos de conteo (`UserLibrarySummaryDto`): total en colección, jugados, deseados, por comprar y préstamos activos.
  - **Brecha detectada:** No calcula ninguna métrica analítica agregada (horas de estantería, ADN de estilos, diseñadores top, dulce de comensales, necesidad de fundas ni insignias de perfil).

### 1.2 Interfaz de Usuario Actual ([`MyLibrary.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MyLibrary.razor))
- Dispone de pestañas para `En mi ludoteca`, `Jugados`, `Deseados`, `Comprar`, `Préstamos`, `Cola comunitaria` y `Apariencia y Tema`.
- **Brecha detectada:**
  1. Carece de una pestaña o panel dedicado de estadísticas visuales ("El ADN de tu estantería").
  2. No existe un componente reutilizable de dashboard estadístico.
  3. No existe una página o URL pública para compartir el perfil lúdico (`/u/{userId}` o `/perfil/{userId}`) con amigos o grupos de juego.

---

## 2. Decisiones Técnicas y Opciones de Diseño

### 2.1 Desacoplamiento de Servicios: `IUserLibraryStatsService`
En lugar de sobrecargar `UserLibraryService`, se crea una abstracción limpia y especializada en `Ludeka.Application.Contracts.IUserLibraryStatsService`:
```csharp
public interface IUserLibraryStatsService
{
    Task<UserLibraryStatsDto> GetUserStatsAsync(string? userId = null, CancellationToken ct = default);
    Task<PublicUserProfileDto?> GetPublicProfileAsync(string userId, CancellationToken ct = default);
}
```
Esto respeta el principio de Responsabilidad Única (SRP) y permite cachear o evolucionar las analíticas sin alterar la gestión transaccional de la ludoteca (añadir/quitar/prestar).

### 2.2 Algoritmos de Cálculo Estadístico
1. **Horas en Estantería (`ShelfTimeStatsDto`):**
   - Considera exclusivamente los títulos físicos en propiedad (`Status == CollectionStatus.InCollection` y `Game != null`).
   - `TotalMinMinutes = sum(Game.Duration.MinMinutes)`
   - `TotalMaxMinutes = sum(Game.Duration.MaxMinutes)`
   - Conversión a horas: `TotalMinHours = Math.Round(TotalMinMinutes / 60.0, 1)`, `TotalMaxHours = Math.Round(TotalMaxMinutes / 60.0, 1)`.
   - Formato descriptivo: *"XX – YY horas acumuladas en tu estantería"*.

2. **Distribución del ADN Lúdico (`PlayerDnaDistributionDto`):**
   - Agrupa por `GameStyle` (`Eurogame`, `Ameritrash`, `PartyGame`, `FillerAbstract`, `NarrativeCampaign`).
   - Dimensión cooperativa: calcula el porcentaje de juegos con `Confrontation == Cooperative || Confrontation == SemiCooperative`.
   - Dimensión en solitario: calcula el porcentaje con `IsOfficialSolo == true`.
   - Identifica el estilo dominante y genera una etiqueta de especialidad lúdica.

3. **Top Autores y Editoriales (`TopEntityDto`):**
   - Parser defensivo para `Game.Designer`: divide por delimitadores comunes (`,`, `/`, `&`, ` y `), normaliza espacios y descarta vacíos o valores genéricos.
   - Conteo de frecuencias y cálculo de porcentajes sobre la colección.
   - Top 5 de diseñadores y top 5 de editoriales.

4. **Escalabilidad y Rango Óptimo de Comensales (`ScalabilitySweetSpotDto`):**
   - Para cada número de jugadores (1 a 7+):
     - Cuenta cuántos juegos tienen calificación óptima (`MustPlay` o `Recommended`).
   - Detecta los números con mayor cantidad de juegos disponibles.
   - Genera una síntesis contextual: *"Tu ludoteca brilla especialmente a 2 y 4 jugadores"*.

5. **Radar de Fundas y Protección (`SleeveProtectionRadarDto`):**
   - Suma `CardCount` de todos los `SleeveItem` de los juegos en la colección.
   - Calcula paquetes de fundas estimados mediante `SleeveItem.CalculatePacksNeeded(50)`.
   - Desglosa por formato de funda (ej. *Estándar 63.5 x 88 mm*, *Mini Euro 45 x 68 mm*).

6. **Insignia y Gamificación Respetuosa (`PlayerBadgeDto`):**
   - **Nivel por volumen:**
     - 0 juegos: *"Estantería en Blanco"*
     - 1 a 4 juegos: *"Iniciado de Mesa"*
     - 5 a 14 juegos: *"Explorador Lúdico"*
     - 15 a 29 juegos: *"Veterano de Mesa"*
     - 30+ juegos: *"Mecenas Lúdico"*
   - **Rasgo distintivo lúdico:**
     - Si Eurogame ≥ 50%: *"Cerebro Eurogamer 🧠"*
     - Si Ameritrash ≥ 50%: *"Héroe Temático ⚔️"*
     - Si PartyGame ≥ 40%: *"Alma de la Fiesta 🎉"*
     - Si Cooperativo ≥ 40%: *"Espíritu Cooperativo 🤝"*
     - Si Fillers ≥ 40%: *"Maestro del Filler ⚡"*
     - Si Solitario ≥ 40%: *"Lobo Solitario 🐺"*
     - De lo contrario: *"Paladar Ecléctico 🌈"*

### 2.3 Componente Visual Blazor: `LibraryStatsDashboard.razor`
- Gráficos con barras de progreso estilizadas con Tailwind CSS y contrastes accesibles WCAG 2.2 AA.
- Tarjetas modulares:
  - Cabecera con Insignia + Rasgo lúdico.
  - Píldoras de métricas clave (Horas, Juegos, Cartas, Mesa Ideal).
  - Espectro de ADN Lúdico (barras visuales y porcentajes).
  - Distribución de comensales (histograma visual para 1, 2, 3, 4, 5, 6, 7+).
  - Radar de fundas y protección.
  - Podio de Diseñadores y Editoriales.
- Diseñado para integrarse tanto en la pestaña `🧬 ADN y Estadísticas` de `MyLibrary.razor` como en la página pública del perfil.

### 2.4 Perfil Público Compartible: `PublicProfile.razor`
- Rutas: `@page "/u/{userId}"` y `@page "/perfil/{userId}"`.
- Acceso sin requerir autenticación para compartir la ludoteca con amigos o grupos de juego.
- Incluye botón de copiar enlace al portapapeles con confirmación visual cuando el usuario activo visita su propio perfil.
- Vista pública de la colección con filtrado básico.

---

## 3. Plan de Verificación Automatizada
- Tests unitarios de `UserLibraryStatsService`:
  - Cálculo de horas con colección vacía, un solo juego y múltiples juegos.
  - Distribución porcentual y asignación de estilo dominante.
  - Parser y normalización de diseñadores compartidos.
  - Cálculo exacto de paquetes de fundas requeridos.
  - Determinación de rangos ideales de comensales.
  - Asignación de insignias y rasgos.
  - Obtención de perfil público.
