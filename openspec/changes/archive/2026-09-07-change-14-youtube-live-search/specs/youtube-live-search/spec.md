# Especificación: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real

- **Módulo:** Hub Multimedia y Pipeline de Ingesta Audiovisual
- **Incremento:** 14 (`change-14-youtube-live-search`)
- **Estado:** En Especificación (`sdd-spec`)

---

## 1. Requerimientos Funcionales

### RF-14.1: Clasificación de Formatos Audiovisuales y Tipo `QuickOverview`
- El sistema debe soportar un nuevo tipo de medio: `MediaType.QuickOverview` (*"⚡ Cómo funciona en 2 minutos"*).
- Este tipo está destinado a vídeos breves (≤ 180 segundos / 3 min) que transmiten el objetivo y mecánicas esenciales del juego para evaluar si gusta antes de consultar un tutorial exhaustivo.
- El Hub Multimedia (`MultimediaHub.razor`) debe presentar una pestaña selectoras o sección dedicada para `⚡ Cómo funciona`.

### RF-14.2: Foco Prioritario en Canales de Editoriales, Creadores y Tiendas
- El sistema debe mantener un padrón oficial de canales hispanos de referencia vinculados a los tres pilares del hobby:
  - **Editoriales:** *Devir TV, Tranjis Games, Maldito Games, Asmodee Ibérica, 2Tomatoes, TCG Factory, Arrakis Games, Mercurio*, etc.
  - **Creadores / Divulgadores:** *Análisis Parálisis, Meepletopía, El Agujero de Hobbit, Mesa de Guerra, Pareja de Ases, Sentido Antihorario, Jugador Inicial, La Mazmorra de Pacheco, Consola y Tablero, Océano de Juegos*, etc.
  - **Tiendas Especializadas:** *Zacatrus!, Jugamos Otra, Cuarto de Juegos*, etc.
- Este padrón servirá de foco prioritario algorítmico al ponderar resultados de YouTube (máxima relevancia `RelevanceScore`) y se conectará en el Incremento 19 con la base de datos de Editoriales, Creadores y Tiendas.

### RF-14.3: Patrones Quirúrgicos de Búsqueda
El servicio `IYouTubeSearchService` debe ejecutar tres búsquedas especializadas para cualquier juego:
1. **Cómo funciona / Vistazo Rápido:** Consulta `"{Título} cómo funciona mecánicas en 2 minutos"` o `"{Título} cómo se juega rápido"` con filtro de duración corta (≤ 3 min).
2. **Tutoriales:** Consulta `"{Título} cómo jugar tutorial español"` con filtro de duración media (8 a 25 min).
3. **Partidas Completas:** Consulta `"{Título} partida completa español"` con extracción automática del badge de jugadores.

### RF-14.4: Invariante Estricta de Jugadores en Partidas (`PlayerCountBadge`)
- La creación de cualquier `MediaItem` de tipo `Playthrough` requiere obligatoriamente `PlayerCountBadge`.
- Se debe implementar `PlayerCountExtractor` que analice por expresiones regulares títulos y descripciones (detectando `"a 2"`, `"a 3"`, `"en solitario"`).
- En caso de no detectarse en el texto del vídeo, se aplicará como fallback la escalabilidad ideal del juego (ej. `"Partida a 2"` o `"Partida a {Min}-{Max}"`).

### RF-14.5: Conexión en Vivo con YouTube Data API v3
- Si se configura `YouTube:ApiKey` en `appsettings.json`, el sistema realizará llamadas HTTP en tiempo real a la API oficial de Google (`https://www.googleapis.com/youtube/v3/search` y `videos`).
- Se incluye un dataset mock estático como salvaguardas para pruebas automáticas (`dotnet test`) en entornos sin conexión a internet ni clave API configurada.

### RF-14.6: Panel de Moderación Rápida
- En el panel de moderación (`/admin/moderacion-medios` y `/moderacion-media`), los moderadores dispondrán de una acción *"🔍 Buscar vídeos en YouTube"*.
- El moderador selecciona un juego y visualiza los resultados divididos en pestañas: `⚡ Cómo funciona`, `🎬 Tutoriales`, `🎲 Partidas`.
- Cada resultado permite previsualización del reproductor y botones en 1 clic para:
  - `📥 Guardar en Pendientes` (estado `PendingApproval`)
  - `✅ Aprobar Directo` (estado `Approved`)
- Se valida la existencia previa por URL para evitar vídeos duplicados.

---

## 2. Requerimientos No Funcionales

- **Rendimiento:** Las llamadas a YouTube limitan `maxResults` a 5 por categoría para optimizar el consumo de cuota diaria (10.000 unidades/día).
- **Seguridad:** Los iframes se incrustan utilizando el dominio con privacidad mejorada `https://www.youtube-nocookie.com/embed/{videoId}`.
- **Resiliencia:** Cero caídas (Zero-Crash Fallback): si la API externa de YouTube experimenta caídas, timeouts o cuota agotada, el servicio conmuta limpiamente a los datos curados sin arrojar excepciones no controladas a la interfaz.
