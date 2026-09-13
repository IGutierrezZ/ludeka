# Propuesta: INC-37 — Modo Producción: APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados

## 1. Motivación y Contexto
Ludeka se prepara para su primer despliegue real en producción sobre infraestructura de contenedores (Docker en Google Cloud) con base de datos en PostgreSQL (Supabase).
Para salir del modo MVP/prototipo, es indispensable:
1. **Eliminar fallbacks silenciosos a datos simulados:** En producción no se deben inventar juegos, vídeos de YouTube ni textos con plantillas estáticas si las APIs externas fallan o no disponen de clave. Los fallos deben ser gestionados de forma transparente y controlada hacia el usuario y el equipo de moderación.
2. **Cumplimiento legal con BoardGameGeek (BGG):** Incorporar la insignia y atribución obligatoria *"Powered by BoardGameGeek"* en el pie de página global.
3. **Comunidad activa:** Conectar enlaces y botones directos de unión a los canales de Discord y Telegram en el footer global, operando con notificaciones reales (`DryRun=false`).
4. **Motor privado de enlaces de afiliado:** Mantener los parámetros y códigos de afiliado de tiendas lúdicas (Zacatrus, Mathom, Dungeon Marvels, etc.) centralizados en configuración privada del servidor. Al renderizar ofertas de compra para juegos, expansiones o fundas, inyectar automáticamente el parámetro correspondiente sin exponerlo en formularios públicos ni requerir que los moderadores introduzcan tags manualmente.
5. **Corrección de defecto de calendario:** Corregir el cálculo de inicio de semana en `CommunityNotificationService` cuando se ejecuta en domingo (`DayOfWeek.Sunday == 0`), garantizando la estabilidad de la suite de pruebas unitarias al 100%.

---

## 2. Alcance Funcional y Técnico

### A. Desacople de Mocks y Gestión de Fallos en APIs Externas
- **BGG (`BggXmlApiClient` & `BggOptions`):**
  - Desactivar la simulación automática por omisión de credenciales.
  - Cuando BGG falle (timeout, HTTP 429 rate limit o HTTP 5xx), retornar un resultado no exitoso con mensaje controlado: *"Los servidores de BoardGameGeek no están disponibles en este momento. Por favor, inténtalo de nuevo en unos minutos."*
- **Google Gemini (`GeminiGameSummaryService` & `GeminiOptions`):**
  - Si la llamada a la API falla o no existe `ApiKey`, no generar resúmenes con plantillas heurísticas inventadas.
  - Dejar el estado de la síntesis como no generado / pendiente y registrar una incidencia técnica en la bitácora/bandeja de moderación para que pueda ser reintentada desde `/moderacion/reportes`.
- **YouTube Data API v3 (`YouTubeSearchService` & `YouTubeOptions`):**
  - Si no hay `ApiKey` o se agota la cuota diaria (HTTP 403 quotaExceeded), no recurrir al dataset mock (`YouTubeSimulationDataset`).
  - Devolver lista vacía con mensaje informativo: *"No se han podido sincronizar vídeos comunitarios en este momento."*
  - Notificar el estado en el panel de moderación multimedia.

### B. Atribución Legal de BGG y Canales Comunitarios en Footer
- Incorporar en el pie de página global (`MainLayout.razor`):
  - Atribución oficial: logotipo / badge *"Powered by BoardGameGeek"* con enlace externo `https://boardgamegeek.com/`.
  - Botones directos con iconos oficiales de **Discord** y **Telegram** configurados vía variables de entorno (`Community__DiscordInviteUrl` y `Community__TelegramChannelUrl`).
  - Switch de producción para notificaciones reales (`DryRun: false`).

### C. Motor Privado de Afiliación de Tiendas (`IAffiliateUrlResolver`)
- Crear `AffiliateOptions` mapeable desde `Affiliates` en configuración/variables de entorno.
- Crear el servicio `IAffiliateUrlResolver` / `AffiliateUrlResolver`:
  - Recibe cualquier URL base de tienda (ej: `https://zacatrus.es/juegos-de-mesa/catan.html`).
  - Detecta el dominio de la tienda colaboradora y le añade el parámetro correspondiente (ej: `ref=ludeka`, `aff=xxx`).
  - Se integra en `StoreOffersCard.razor`, `SleeveStoreUrlResolver.cs` y los enlaces de compra de expansiones.
  - Añade atributos de seguridad y SEO obligatorios: `rel="noopener noreferrer sponsored"`.

### D. Corrección de Calendario en `CommunityNotificationService`
- Corregir el cálculo de `startOfWeek` para `DayOfWeek.Sunday`:
  `int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7; var startOfWeek = today.AddDays(-diff);`
- Elevar la suite de pruebas unitarias a 855/855 en verde (100%).

---

## 3. Criterios de Aceptación
1. Si BGG falla, no se inyectan datos de `BggSimulationDataset` y se notifica amigablemente al usuario.
2. Si Gemini falla, no se devuelven descripciones heurísticas inventadas; se marca la incidencia para moderación.
3. Si YouTube falla, la pestaña de vídeos no muestra el dataset simulado; devuelve colección vacía y estado de aviso.
4. El footer global muestra la atribución a BGG y los enlaces de Discord y Telegram configurables.
5. El motor de afiliados transforma las URLs de tiendas asociadas añadiendo el parámetro privado sin modificar la URL base almacenada.
6. La suite completa de pruebas unitarias pasa al 100% (855/855).
