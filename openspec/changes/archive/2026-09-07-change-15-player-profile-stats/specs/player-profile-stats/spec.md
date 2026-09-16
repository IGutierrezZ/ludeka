# Especificación: Estadísticas Avanzadas de Colección y ADN del Jugador

- **Módulo:** Ludoteca Personal, Gamificación y Perfil de Usuario
- **Incremento:** 15 (`change-15-player-profile-stats`)
- **Estado:** En Especificación (`sdd-spec`)

---

## 1. Requerimientos Funcionales

### RF-15.1: Cálculo de Horas de Juego Acumuladas en Estantería
- El sistema debe computar el tiempo total de juego acumulado para todos los títulos físicos en posesión (`InCollection`) que dispongan de ficha de catálogo (`Game != null`).
- Se calculará:
  - `TotalMinMinutes`: Sumatorio de `Game.Duration.MinMinutes`.
  - `TotalMaxMinutes`: Sumatorio de `Game.Duration.MaxMinutes`.
  - `TotalMinHours`: Minutos mínimos convertidos a horas con un decimal (`Math.Round(min / 60.0, 1)`).
  - `TotalMaxHours`: Minutos máximos convertidos a horas con un decimal (`Math.Round(max / 60.0, 1)`).
  - Si una colección no contiene juegos, devolverá 0 horas.

### RF-15.2: Desglose del ADN Lúdico y Estilo Dominante
- El sistema debe clasificar cada juego físico de la ludoteca según su `GameStyle`:
  - `Eurogame`: Gestión de recursos, optimización, colocación de trabajadores.
  - `Ameritrash`: Temáticos, dados, inmersión, combate.
  - `PartyGame`: Juegos sociales, dinámicos, grupos amplios.
  - `FillerAbstract`: Juegos rápidos, abstractos, fillers de cartas.
  - `NarrativeCampaign`: Juegos de campaña, legado, narrativa guiada.
- Debe calcular el porcentaje de presencia de cada estilo sobre el total de juegos en estantería (ej. 60% Eurogames, 20% Ameritrash, etc.).
- Debe calcular dimensiones transversales de la colección:
  - `CooperativePercentage`: Porcentaje de juegos con `Confrontation == Cooperative || Confrontation == SemiCooperative`.
  - `SoloReadyPercentage`: Porcentaje de juegos con `IsOfficialSolo == true`.
- Debe identificar el `DominantStyle`: estilo con mayor porcentaje relativo.

### RF-15.3: Detección del Rango Dulce de Comensales (Sweet Spot)
- El sistema debe analizar la escalabilidad de todos los juegos en la estantería:
  - Por cada número de comensales (1, 2, 3, 4, 5, 6, 7+), contar cuántos títulos tienen estado `ScalabilityStatus.MustPlay` o `ScalabilityStatus.Recommended`.
  - Identificar el número (o números) de jugadores con mayor cantidad de títulos optimizados.
  - Generar un texto de recomendación claro (ej. *"Tu ludoteca está especialmente optimizada para 2 y 4 jugadores"* o *"Especializada en mesas de 4 jugadores"*).
  - Proveer los datos de la curva de comensales para representarla gráficamente.

### RF-15.4: Top Diseñadores y Editoriales de la Colección
- El sistema debe extraer y contabilizar los diseñadores y editoriales presentes en la ludoteca del usuario.
- Debe contemplar autores múltiples separados por delimitadores comunes (`,`, `/`, `&`, ` y `), limpiando espacios en blanco y descartando valores vacíos o no informados.
- Debe generar el Top 5 de diseñadores y Top 5 de editoriales con su número de títulos y porcentaje representativo.

### RF-15.5: Radar de Fundas y Protección de Cartas
- El sistema debe consultar la información de fundas (`SleeveItem`) de cada juego en la estantería.
- Debe calcular:
  - Total acumulado de cartas en la ludoteca.
  - Total estimado de paquetes de fundas requeridos (considerando paquetes estándar de 50 fundas por formato, mediante `SleeveItem.CalculatePacksNeeded(50)`).
  - Número de juegos que contienen componentes de cartas que requieren protección.
  - Formatos de fundas más frecuentes en la colección (ej. *Estándar 63.5 x 88 mm*, *Mini Euro 45 x 68 mm*).

### RF-15.6: Asignación de Insignias y Rasgos de Jugador
- El sistema debe calcular de forma dinámica y no invasiva:
  - **Insignia de Coleccionista (según volumen en estantería):**
    - 0 títulos: *"Estantería en Blanco"* (Nivel 0)
    - 1–4 títulos: *"Iniciado de Mesa"* (Nivel 1)
    - 5–14 títulos: *"Explorador Lúdico"* (Nivel 2)
    - 15–29 títulos: *"Veterano de Mesa"* (Nivel 3)
    - 30+ títulos: *"Mecenas Lúdico"* (Nivel 4)
  - **Rasgo Lúdico Distintivo (según perfil de ADN):**
    - Si Eurogame ≥ 50%: *"Cerebro Eurogamer 🧠"*
    - Si Ameritrash ≥ 50%: *"Héroe Temático ⚔️"*
    - Si PartyGame ≥ 40%: *"Alma de la Fiesta 🎉"*
    - Si Cooperativo ≥ 40%: *"Espíritu Cooperativo 🤝"*
    - Si Filler/Abstracto ≥ 40%: *"Maestro del Filler ⚡"*
    - Si Solitario ≥ 40%: *"Lobo Solitario 🐺"*
    - De lo contrario: *"Paladar Ecléctico 🌈"*

### RF-15.7: Componente Dashboard Visual (`LibraryStatsDashboard.razor`)
- Debe renderizarse de forma reactiva y accesible (WCAG 2.2 AA).
- Integración directa en `/mi-ludoteca` bajo una pestaña dedicada `🧬 ADN y Estadísticas`.
- Presentación de métricas con microtextos lúdicos y diseño limpio con Tailwind CSS:
  - Tarjeta de Insignia de Perfil y Rasgo Distintivo.
  - Píldoras de métricas destacadas (Horas disponibles, títulos físicos, cartas totales, comensales óptimos).
  - Espectro visual del ADN Lúdico con desglose porcentual.
  - Gráfico de escalabilidad por número de comensales.
  - Módulo de fundas y protección.
  - Ranking de autores y editoriales favoritas.

### RF-15.8: Página Compartible de Perfil Público (`PublicProfile.razor`)
- Rutas públicas accesibles sin autenticación obligatoria: `/u/{userId}` y `/perfil/{userId}`.
- Permite compartir la colección personal con amigos, parejas o grupos lúdicos.
- Muestra el encabezado del perfil con insignia, el dashboard estadístico y la vitrina de juegos en estantería con opción de filtrado rápido.
- Si el usuario que navega es el dueño del perfil, incluye botón directo de *"📋 Copiar enlace de mi perfil"* con feedback visual interactivo ("¡Enlace copiado!").
- En caso de usuario inexistente o sin juegos, presenta estado vacío informativo y enlace al catálogo.

---

## 2. Requerimientos No Funcionales

- **Rendimiento:** El cálculo de métricas debe procesarse en memoria en < 20 ms para ludotecas de hasta 500 títulos.
- **Resiliencia:** Cero excepciones ante colecciones vacías, juegos con datos parciales (sin fundas, sin diseñador o duración 0).
- **Accesibilidad y Anti-Slop:** Contrastes nítidos acordes a los tokens de diseño de Ludeka, semántica ARIA para gráficos de progreso y compatibilidad completa con dispositivos móviles.
