# Exploración: change-08-game-expansions (Incremento 8: Fichas de Expansión, Ecosistema de Juego y Compatibilidad Lúdica)

## 1. Estado Actual del Monorepo y la Solución

### 1.1 Arquitectura y Proyectos (.NET 10 y C# 13)
- **Solución `Ludeka.slnx`:** Estructura Clean Architecture con 4 capas:
  - `src/Ludeka.Core`: Entidades de dominio puras (`Game`, `UserCollectionItem`, `UserGameReview`, `MediaItem`, `Giveaway`, `WeeklyRelease`, `RuleQuestion`, etc.).
  - `src/Ludeka.Application`: Casos de uso, DTOs (`GameDetailDto`, `GameSummaryDto`), contratos de repositorio e interfaces de servicio (`ICatalogService`, `IUserLibraryService`, `IMediaService`, etc.).
  - `src/Ludeka.Infrastructure`: EF Core 10 con SQLite (`LudekaDbContext`), cliente e ingesta BGG XMLAPI2, repositorios e importador comunitario.
  - `src/Ludeka.Web`: Frontend Blazor Web App (SSR interactivo con Streaming Rendering y Tailwind CSS compilado).
  - `tests/Ludeka.UnitTests`: 157 pruebas unitarias y de integración pasando al 100% en verde con .NET 10 SDK.

### 1.2 Estado Actual del Modelo `Game` y Fichas de Juego
- Actualmente, la entidad `Game` modela todos los títulos de forma homogénea, asumiendo que cada registro es un juego independiente (`BaseGame`).
- La vista de detalle `GameDetail.razor` (accesible en `/juegos/{Slug}`) despliega:
  1. Cabecera hero con carátula oficial BGG, títulos (español y original), diseñador, editorial, año y doble rating (BGG vs Ludist).
  2. Píldoras de ADN Lúdico (`QuickBadges`).
  3. Barra de colección interactiva en 4 estados (`CollectionActionBar`) y préstamos (`LoanModal`).
  4. Veredicto oficial de la Mesa Fundadora (`FoundingVerdictCard`) o resumen IA.
  5. Tarjeta de valoración propia (`UserReviewCard`) y modal de review en 45s (`ReviewBottomSheet`).
  6. Semáforo dinámico de escalabilidad (`ScalabilityTrafficLight`).
  7. Guía de fundas para cartas (`SleeveGuideCard`).
  8. Hub multimedia segregado (`MultimediaHub`: tutoriales YouTube, partidas completas YouTube y posts/reels Instagram).
  9. Consultorio de reglas Q&A (`RuleQuestionsSection`).
  10. Generador de tarjetas para redes sociales (`SocialCardModal`).

### 1.3 Carencias Actuales frente al Requerimiento de Expansiones
1. **Diferenciación de Tipo de Juego y Relación Jerárquica:**
   - No existe diferenciación entre un *Juego Base* (`BaseGame`), una *Expansión* (`Expansion`) o una *Expansión Autojugable* (`StandaloneExpansion`).
   - No existe un campo `BaseGameId` ni relación reflexiva que vincule una expansión con su juego base nodriza.
2. **Atributos de Aporte y Veredicto de Expansión:**
   - No existe modelado para saber **qué aporta la expansión al juego base** ni cómo altera la experiencia de juego.
   - Faltan métricas de impacto lúdico:
     - Veredicto de necesidad de compra (ej: *"Imprescindible, mejora el juego base"*, *"Muy recomendada"*, *"Solo para completistas"*, *"Prescindible"*).
     - Píldoras de impacto lúdico (ej: *"Agrega más jugadores"*, *"Imprescindible para 2 jugadores"*, *"Corrige balance"*, *"Introduce modo solitario"*, *"Añade asimetría"*, *"Nuevos módulos"*).
     - Resumen editorial de qué aporta a la mesa.
     - Alteración de métricas base (+jugadores, +tiempo, alteración de huella de mesa).
3. **Compatibilidad y Sinergia entre Expansiones:**
   - En el mundo de los juegos de mesa (ej. *Wingspan*, *Terraforming Mars*, *Carcassonne*, *Root*, *Catan*), uno de los mayores problemas y dudas de los jugadores es saber qué expansiones combinan bien entre sí y cuáles provocan sobrecarga de reglas, duración excesiva o incompatibilidad de componentes.
   - Actualmente no existe ninguna estructura en el sistema para modelar la relación par-a-par de compatibilidad/sinergia entre expansiones de un mismo juego base, ni herramientas interactivas de comprobación de mesa.
4. **Navegación Transversal e Integración en la UI:**
   - La ficha del juego base no muestra sus expansiones asociadas ni permite explorar su ecosistema.
   - La ficha de una expansión no destaca el juego base que requiere ni permite saltar a él ni a las demás expansiones hermanas.
   - En el catálogo principal no hay filtros ni badges para distinguir expansiones de juegos base.

---

## 2. Requerimientos Funcionales del Incremento

### 2.1 Ficha Propia, Multimedia Propio y Valoraciones Propias para Expansiones
- Cada expansión debe ser una entidad completa de primer nivel (`Game` con `Type = Expansion`), lo que le confiere de forma natural:
  - Ficha propia e individual en `/juegos/{slug-expansion}` con su carátula, diseñador, editorial, año y fundas de cartas requeridas (`Sleeves`).
  - Multimedia propio (`MediaItems` asociados a su `GameId`): tutoriales de cómo se juega la expansión, unboxings, partidas específicas con la expansión.
  - Valoraciones comunitarias propias (`UserGameReviews` asociadas a su `GameId`): puntuación de 1 a 10 de la expansión, micro-reseñas sobre si merece la pena y voto de recomendación.
  - Gestión en ludoteca personal (`UserCollectionItems`): el usuario puede marcar la expansión como "En mi ludoteca", "Jugada" o "Deseada" de manera independiente al juego base.

### 2.2 Relación Base-Expansión y "Qué aporta al juego base"
- Una expansión pertenece a un juego base nodriza (`BaseGameId`).
- Cada expansión contiene metadatos editoriales de impacto:
  - **Veredicto de Necesidad (`ExpansionNecessity`):**
    - `MustHave`: "Imprescindible, mejora el juego base"
    - `HighlyRecommended`: "Muy recomendada"
    - `Situational`: "Recomendada según grupo / jugadores"
    - `OnlyForFans`: "Solo para muy cafeteros / completistas"
    - `Dispensable`: "Prescindible / Aporta poco"
  - **Píldoras de Aporte (`ExpansionImpactTag`):**
    - `AddsPlayers`: "Agrega más jugadores" (ej: amplía de 4 a 5 o 6 jugadores).
    - `ImprovesTwoPlayers`: "Imprescindible para 2 jugadores" (mejora o crea el modo duelo).
    - `FixesBalance`: "Corrige balance / ritmo del juego base" (ej: *Preludio* en Terraforming Mars u *Oceanía* en Wingspan).
    - `AddsSoloMode`: "Introduce modo solitario oficial".
    - `AddsAsymmetry`: "Añade asimetría y facciones únicas".
    - `ModularContent`: "Módulos independientes combinables".
    - `NewMapOrFactions`: "Nuevos mapas, tableros o facciones".
    - `VariableGameLength`: "Acelera o acorta el tiempo de partida".
  - **Resumen Editorial ("Qué aporta a la mesa"):** Texto conciso redactado por la mesa fundadora o editores explicando el impacto exacto en partida.
  - **Impacto en Métricas:** Modificadores sobre el juego base (+X jugadores, +Y minutos, cambio en huella de mesa).

### 2.3 Matriz de Compatibilidad y Sinergia entre Expansiones ("Mezclador de Mesa")
- ¿Cómo saber qué expansiones funcionan bien entre sí?
- Modelo par-a-par `ExpansionSynergy`:
  - `BaseGameId`, `ExpansionAId`, `ExpansionBId`.
  - `SynergyLevel`:
    - 🟢 `PerfectCombo` ("Sinergia Excelente / Combo Ideal"): Se integran de forma limpia y enriquecen la partida sin fricción.
    - 🟡 `CompatibleWithCaution` ("Compatible pero Exigente / Sobrecarga"): Se pueden jugar juntas, pero alargan la partida (+45m), sobrecargan la mesa o aumentan el análisis-parálisis.
    - 🔴 `Incompatible` ("Incompatible / Conflicto"): Reglas contradictorias o sustitución de los mismos componentes físicos.
  - `Notes`: Explicación lúdica de por qué combinan bien o qué conflicto presentan.
- **Herramienta Interactiva en la UI ("Mezclador de Expansiones y Diagnóstico de Mesa"):**
  - Selector en la ficha del juego base donde el usuario marca las expansiones que quiere mezclar (mediante checkboxes o botones táctiles).
  - El sistema calcula y muestra un diagnóstico en vivo:
    - Estado global de la combinación (🟢 Mesa equilibrada / 🟡 Mesa exigente / 🔴 Incompatibilidad).
    - Métricas resultantes calculadas (Rango de jugadores final, tiempo estimado de partida, huella de mesa).
    - Alertas específicas de sinergia entre los pares seleccionados.
- **Recetas de Mesa Recomendadas (`ExpansionRecipe`):**
  - Preajustes lúdicos oficiales (ej: *"Setup de Torneo"*, *"Setup Parejas 45 min"*, *"La Experiencia Completa"*).

### 2.4 Navegación Transversal Fluida
- **En la ficha del Juego Base:**
  - Pestaña/Sección dedicada de "Expansiones y Ecosistema": listado de expansiones con carátula, qué aporta, badge de veredicto, nota y enlace directo.
  - Mezclador de expansiones interactivo y recetas recomendadas.
- **En la ficha de la Expansión:**
  - Banner superior: `🧩 Expansión oficial de [Título del Juego Base]` con tarjeta interactiva y enlace para volver al juego base con 1 clic.
  - Tarjeta de "¿Qué aporta al Juego Base?" (veredicto, badges de aporte, cambios de métricas y resumen editorial).
  - Sección de "Otras Expansiones del Ecosistema", mostrando la compatibilidad directa con cada una respecto a la que se está visualizando.
- **En el Catálogo Principal:**
  - Badge visual `🧩 Expansión` en las tarjetas de juego.
  - Filtro por tipo: `Todos`, `Juegos Base`, `Expansiones`.

---

## 3. Próximos Pasos SDD
- Elaborar `proposal.md` con la propuesta de arquitectura formal, modelo de datos, DTOs, interfaces de servicio, componentes UI y estrategia de verificación.
- Presentar la propuesta al usuario en español y esperar su aprobación explícita antes de pasar a especificaciones y código.
