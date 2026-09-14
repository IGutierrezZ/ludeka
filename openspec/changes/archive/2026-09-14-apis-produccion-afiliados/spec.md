# Especificación: INC-37 — Modo Producción: APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados

## Requerimientos Funcionales y Criterios Gherkin

### REQ-1: Desacople de Datos Simulados en BoardGameGeek (BGG)
- **Descripción:** Cuando la aplicación opera con `SimulateApi=false`, no debe consultar bajo ninguna circunstancia el `BggSimulationDataset`. Si la llamada a BGG falla (por timeout, rate limiting HTTP 429 o fallo de red), el sistema debe reportar un error controlado con mensaje amigable sin inventar fichas.
- **Escenario 1.1:** Búsqueda en BGG cuando la API real está caída o devuelve HTTP 500
  - **Dado** que `Bgg:SimulateApi` es `false`
  - **Cuando** el usuario busca un juego en el modal de importación BGG y la API externa devuelve un error de conexión
  - **Entonces** el servicio retorna una lista vacía y un mensaje descriptivo indicando que los servidores de BGG no están disponibles temporalmente.
- **Escenario 1.2:** `ShouldSimulate` no debe forzar simulación solo por ausencia de token si la API pública responde
  - **Dado** que un desarrollador o entorno no tiene `ApiToken`
  - **Cuando** `SimulateApi` es explícitamente `false`
  - **Entonces** `ShouldSimulate` es `false` y se intenta la llamada HTTP con el User-Agent oficial.

---

### REQ-2: Manejo de Fallos en Síntesis de IA con Gemini
- **Descripción:** Si la llamada a Google Gemini falla o no hay `ApiKey`, el servicio `GeminiGameSummaryService` no debe generar textos heurísticos prefabricados ("Un apasionante juego publicado en..."). Debe registrar la ficha con estado pendiente/fallido y generar una incidencia en la bitácora de moderación.
- **Escenario 2.1:** Generación de síntesis sin API Key configurada
  - **Dado** que `Gemini:Simulate` es `false` y no existe `ApiKey`
  - **Cuando** se solicita generar la síntesis con IA para un juego
  - **Entonces** el servicio devuelve un resultado fallido indicando falta de credenciales sin generar texto ficticio.
- **Escenario 2.2:** Notificación de incidencia para moderadores
  - **Dado** un fallo de red o cuota al llamar a la API de Gemini
  - **Cuando** el servicio de síntesis detecta el error
  - **Entonces** se registra una alerta técnica para la moderación con el identificador del juego y el motivo del fallo.

---

### REQ-3: Búsqueda de YouTube en Vivo sin Videos Falsos
- **Descripción:** Cuando `YouTube:Simulate` es `false`, no debe recurrirse al dataset estático `YouTubeSimulationDataset` en caso de error o cuota agotada.
- **Escenario 3.1:** Consulta de vídeos comunitarios ante cuota agotada
  - **Dado** que `YouTube:Simulate` es `false` y la API devuelve HTTP 403 (quotaExceeded)
  - **Cuando** se consultan vídeos para la ficha de un juego
  - **Entonces** el servicio retorna una colección vacía y un estado de advertencia, sin inyectar vídeos de ejemplo.

---

### REQ-4: Insignia y Atribución Legal "Powered by BoardGameGeek" en Footer
- **Descripción:** El pie de página global (`MainLayout.razor`) debe incluir un bloque visible de atribución con el logo/texto "Powered by BoardGameGeek" con enlace a `https://boardgamegeek.com/`.
- **Escenario 4.1:** Visualización universal del pie de página
  - **Dado** que un usuario navega por cualquier página de Ludeka
  - **Cuando** se visualiza el footer global
  - **Entonces** se muestra la insignia de BoardGameGeek con enlace seguro (`target="_blank" rel="noopener noreferrer"`).

---

### REQ-5: Enlaces Comunitarios a Discord y Telegram en Footer
- **Descripción:** El pie de página debe incorporar botones interactivos con los iconos oficiales de Discord y Telegram vinculados a las URLs configuradas en el servidor.
- **Escenario 5.1:** Enlace de Discord configurado
  - **Dado** que `CommunityNotifications:DiscordInviteUrl` contiene `https://discord.gg/ludeka`
  - **Cuando** se renderiza el footer
  - **Entonces** se muestra el botón de Discord con dicho enlace y atributo `rel="noopener noreferrer"`.
- **Escenario 5.2:** Enlace de Telegram configurado
  - **Dado** que `CommunityNotifications:TelegramChannelUrl` contiene `https://t.me/ludeka`
  - **Cuando** se renderiza el footer
  - **Entonces** se muestra el botón de Telegram con dicho enlace y atributo `rel="noopener noreferrer"`.

---

### REQ-6: Motor Centralizado y Privado de Afiliación de Tiendas (`IAffiliateUrlResolver`)
- **Descripción:** Las URLs de compra mostradas en las ofertas de juegos (`StoreOffersCard`), expansiones y fundas (`SleeveGuideCard`) deben procesarse por un motor en el servidor que inyecta automáticamente el parámetro y tag de afiliado configurado para cada tienda colaboradora.
- **Escenario 6.1:** Resolución de enlace de tienda con tag configurado
  - **Dado** que la tienda "Zacatrus" tiene configurado `ParamName = "ref"` y `AffiliateTag = "ludeka"`
  - **Cuando** se resuelve la URL `https://zacatrus.es/juegos/catan.html`
  - **Entonces** la URL resultante es `https://zacatrus.es/juegos/catan.html?ref=ludeka`.
- **Escenario 6.2:** Resolución de URL que ya contiene parámetros query
  - **Dado** una URL como `https://tienda.es/buscar?q=fundas` con `ParamName = "aff"` y `AffiliateTag = "ludeka"`
  - **Cuando** se resuelve la URL
  - **Entonces** el parámetro de afiliado se anexa con `&` manteniendo los parámetros preexistentes (`https://tienda.es/buscar?q=fundas&aff=ludeka`).
- **Escenario 6.3:** Tienda sin afiliación configurada
  - **Dado** una tienda no registrada en el diccionario de afiliados
  - **Cuando** se procesa su URL
  - **Entonces** devuelve la URL original sin alteraciones.
- **Escenario 6.4:** Seguridad y SEO
  - **Dado** cualquier enlace de compra renderizado en la interfaz
  - **Cuando** se genera la etiqueta `<a>`
  - **Entonces** incluye estrictamente `target="_blank" rel="noopener noreferrer sponsored"`.

---

### REQ-7: Corrección de Cálculo de Inicio de Semana en `CommunityNotificationService`
- **Descripción:** El método `TriggerFridayReleasesBulletinAsync` debe calcular el lunes de la semana actual correctamente incluso si se ejecuta en domingo (`DayOfWeek.Sunday`).
- **Escenario 7.1:** Ejecución en domingo
  - **Dado** una fecha en domingo
  - **Cuando** se calcula el inicio de la semana lúdica
  - **Entonces** `startOfWeek` corresponde al lunes inmediatamente anterior (6 días antes) y no al lunes posterior.
