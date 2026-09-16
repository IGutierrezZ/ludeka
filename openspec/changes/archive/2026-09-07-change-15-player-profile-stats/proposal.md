# Propuesta: change-15-player-profile-stats (Incremento 15: Estadísticas Avanzadas de Colección y ADN del Jugador)

## 1. Resumen Ejecutivo y Motivación

En Ludeka ("El Letterboxd de los juegos de mesa en español"), la ludoteca personal no es únicamente un inventario pasivo de títulos, sino una expresión viva de la identidad lúdica del jugador.
Para cumplir con el **Punto 7.4 del MVP (Gamificación, Retención y Perfil Lúdico)** y dotar a la plataforma de engagement profundo y valor analítico:

1. **El "ADN de tu Estantería":**
   - Análisis inteligente de la colección física en propiedad (`InCollection`):
     - **Horas de juego en estantería:** Cálculo acumulado del tiempo mínimo y máximo de diversión disponible (`MinMinutes` y `MaxMinutes`).
     - **Distribución de estilos y ADN lúdico:** Desglose porcentual por géneros (Eurogame, Ameritrash/Temático, Party Game, Filler/Abstracto, Campaña/Narrativo) y dimensiones transversales (tasa de cooperativos y solitarios).
     - **Rango dulce de comensales (Sweet Spot de Escalabilidad):** Detección automática del número de jugadores con mayor densidad de recomendaciones (`MustPlay` y `Recommended`), orientando respuestas claras a preguntas del tipo *"¿Para cuántos jugadores está mejor equipada mi ludoteca?"*.
     - **Autores y Editoriales predilectos:** Identificación del top 5 de diseñadores (con parsing robusto de autores múltiples) y editoriales favoritas.
     - **Radar de Fundas y Protección:** Cómputo global de cartas en la colección y cálculo de paquetes de fundas requeridos (paquetes estándar de 50 fundas) con detalle de formatos clave.
2. **Insignia y Gamificación Respetuosa del Jugador:**
   - Asignación dinámica de rangos y rasgos sin artificios invasivos ni dinámicas tóxicas:
     - Nivel de Coleccionista: *Estantería en Blanco*, *Iniciado de Mesa*, *Explorador Lúdico*, *Veterano de Mesa*, *Mecenas Lúdico*.
     - Rasgo de Especialidad Lúdica: *Cerebro Eurogamer 🧠*, *Héroe Temático ⚔️*, *Alma de la Fiesta 🎉*, *Espíritu Cooperativo 🤝*, *Maestro del Filler ⚡*, *Lobo Solitario 🐺*, *Paladar Ecléctico 🌈*.
3. **Componente Dashboard Interactivo (`LibraryStatsDashboard.razor`):**
   - Visualización modular con barras de progreso y anillos de distribución estilizados con Tailwind CSS, respetando accesibilidad WCAG 2.2 AA y estética editorial moderna.
   - Pestaña dedicada `🧬 ADN y Estadísticas` en `/mi-ludoteca`.
4. **Página de Perfil Público Compartible (`PublicProfile.razor`):**
   - Rutas `/u/{userId}` y `/perfil/{userId}` para compartir la colección y métricas con grupos de juego y amigos sin requerir inicio de sesión.
   - Botón directo de "Copiar enlace de mi perfil" con notificación visual instantánea al visitar el perfil propio.

---

## 2. Decisiones de Dominio y Arquitectura

### 2.1 Capa de Dominio (`Ludeka.Core`)
- Las entidades `Game`, `UserCollectionItem`, `ScalabilityEntry` y `SleeveItem` ya poseen la riqueza requerida.
- Se crean Value Objects / Records de soporte analítico si se requiere, o DTOs en la capa de aplicación para estructurar las agregaciones sin acoplar el dominio a vistas específicas.

### 2.2 Capa de Aplicación (`Ludeka.Application`)
- **Contrato:** [`IUserLibraryStatsService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IUserLibraryStatsService.cs)
  ```csharp
  public interface IUserLibraryStatsService
  {
      Task<UserLibraryStatsDto> GetUserStatsAsync(string? userId = null, CancellationToken ct = default);
      Task<PublicUserProfileDto?> GetPublicProfileAsync(string userId, CancellationToken ct = default);
  }
  ```
- **Implementación:** `UserLibraryStatsService` en `Ludeka.Application.Features.Library`.
  - Inyecta `IUserCollectionRepository`, `IGameRepository` y `ICurrentUserService`.
  - Se apoya en consultas existentes con `.Include(c => c.Game)` para optimizar el rendimiento y evitar N+1 queries.
  - Implementa algoritmos de agregación en memoria para el conjunto de juegos del usuario.

### 2.3 Capa de Presentación Web (`Ludeka.Web`)
- **Componente:** `LibraryStatsDashboard.razor` en `src/Ludeka.Web/Components/Features/Library/`.
  - Parámetros: `UserLibraryStatsDto Stats`, `bool IsPublicView = false`.
- **Página de Perfil:** `PublicProfile.razor` en `src/Ludeka.Web/Components/Pages/PublicProfile.razor` con `@page "/u/{UserId}"` y `@page "/perfil/{UserId}"`.
- **Integración en Ludoteca:** Pestaña `🧬 ADN y Estadísticas` en `MyLibrary.razor`.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Cálculo de métricas avanzadas y horas de estantería
  Dado un usuario con 10 juegos físicos en estado "InCollection"
  Cuando se consultan sus estadísticas
  Entonces el sistema suma los minutos mínimos y máximos de duración de todos sus juegos
  Y devuelve el rango en horas con formato descriptivo (ej. "45 – 68 horas")

Escenario: Detección del estilo dominante y ADN lúdico
  Dado un usuario cuya colección contiene 6 Eurogames de un total de 10 juegos
  Cuando se calcula su ADN lúdico
  Entonces el porcentaje de Eurogames es del 60%
  Y el estilo dominante se clasifica como Eurogame
  Y se le otorga el rasgo "Cerebro Eurogamer 🧠"

Escenario: Rango dulce de comensales (Sweet Spot)
  Dado un usuario cuyos juegos tienen su mayor concentración de recomendaciones en 2 y 4 jugadores
  Cuando se calcula la escalabilidad de su ludoteca
  Entonces el sistema resalta que la colección es óptima para mesas de 2 y 4 jugadores

Escenario: Radar de fundas de la colección
  Dado un usuario con títulos con requerimientos de fundas cargados
  Cuando visualiza su radar de protección
  Entonces se totaliza el número de cartas y los paquetes de fundas de 50 unidades requeridos

Escenario: Consulta y compartición de perfil público
  Dado un enlace público "/u/usuario-fundador-ludeka"
  Cuando cualquier visitante o amigo accede a la URL
  Entonces visualiza el resumen del ADN lúdico del usuario y su vitrina de juegos
  Y si el usuario activo es el propietario, se muestra el botón interactivo para copiar su enlace
```
