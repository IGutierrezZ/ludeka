# Propuesta SDD — INC-44: Worker de Recolección Automática de Canales Sociales Monitorizados (YouTube RSS / Instagram)

## 1. Contexto y Justificación
En el Incremento 42 (INC-42) se consolidó el **Hub de Ingesta Social y Multimedia**, introduciendo:
1. Una Bandeja de Moderación editable (`/admin/ingesta-social`).
2. El flujo de Alta Exprés por URL y Alta Manual Avanzada con optimización de medios a Cloudflare R2 vía SkiaSharp.
3. El Directorio central de Cuentas Monitorizadas (`/admin/canales-monitorizados`) con sincronización desde editoriales, creadores y tiendas.

Sin embargo, en el estado actual la captura de publicaciones requiere que un moderador copie y pegue manualmente enlaces de YouTube o Instagram. Para que Ludeka sea un radar vivo y autónomo de la comunidad lúdica en español sin incurrir en costes de APIs de terceros (como Apify o servicios de scraping de pago), es necesario dotar a la plataforma de un **Worker Desatendido de Recolección Automática**.

## 2. Objetivos del Incremento

1. **Recolector Desatendido de Canales de YouTube (`IYouTubeFeedCollector`)**:
   - Consumo del feed nativo y público Atom/RSS de YouTube (`https://www.youtube.com/feeds/videos.xml?channel_id={channelId}`) sin cuotas de API de Google Cloud.
   - Resolución automática de handles (`@canal`) o nombres a Channel ID de YouTube mediante extracción de etiqueta canónica en el perfil.
   - Parseo XML seguro (`XDocument`) extrayendo: `videoId`, título, enlace canónico, fecha de publicación, descripción completa y miniatura en alta resolución.

2. **Recolector Resiliente de Instagram (`IInstagramFeedCollector`)**:
   - Extractor no invasivo y respetuoso con soporte para puentes RSS externos configurables (`RssBridgeUrlTemplate`).
   - Modo simulado y tolerante a fallos (`Simulate = true` por defecto en entornos de desarrollo y tests) que genera publicaciones sintéticas realistas de las cuentas monitorizadas para pruebas locales y CI.

3. **Orquestador Central de Recolección (`ISocialCollectorService` / `SocialCollectorService`)**:
   - Barrido periódico o bajo demanda de todas las cuentas habilitadas (`MonitoredSocialAccount.IsEnabled == true`).
   - Deduplicación estricta contra la base de datos de moderación (`ISocialInboxRepository.ExistsBySourceUrlAsync`) evitando reimportar vídeos o posts existentes.
   - Integración fluida con el pipeline de ingesta (`ISocialIngestionService`), enriqueciendo con análisis de IA (Gemini Flash / heurística) y optimización de carátulas a R2 vía SkiaSharp.
   - Actualización atómica de la marca de tiempo de sondeo de cada cuenta (`account.MarkChecked()`).
   - Trazabilidad y DTO de resultados consolidados: cuentas escaneadas, publicaciones descubiertas, ítems importados a moderación, duplicados omitidos y errores controlados.

4. **Worker en Segundo Plano (`SocialCollectorHostedService`)**:
   - Servicio en segundo plano (`BackgroundService`) con periodicidad configurable (`SocialCollector:IntervalMinutes`, por defecto 120 minutos).
   - Pausa de arranque inicial (`InitialDelaySeconds`) para permitir que la aplicación complete su inicio.
   - Manejo de excepciones granular por canal para garantizar que el fallo de una cuenta o red no interrumpa el ciclo del servicio ni afecte a las demás.

5. **Controles de Sondeo y Auditoría en la UI Editorial (`Ludeka.Web`)**:
   - `/admin/canales-monitorizados`: Botón *"Sondear Canales Ahora"* con indicador de progreso y resumen en toast; botón contextual *"Sondear"* por canal con visualización de `LastCheckedAt`.
   - `/admin/ingesta-social`: Botón *"Sondear Canales"* y badge identificativo para publicaciones auto-recolectadas.
   - Respeto estricto al contrato de maquetación editorial (cero emojis, uso exclusivo de Lucide Icons).

6. **Cobertura de Pruebas Unitarias al 100%**:
   - Tests unitarios con Fakes puros (sin librerías externas de mocks):
     - Parseo XML de feeds de YouTube con fixtures locales.
     - Extracción y manejo de errores de canal.
     - Colector de Instagram en modo simulado y modo feed puente.
     - Orquestador de recolección y deduplicación.
     - Worker en segundo plano (arranque y cancelación limpia).
     - Verificación de contratos de maquetación web (`WebMarkupContractTests`).

## 3. Criterios de Aceptación
- [ ] Los canales de YouTube monitorizados se sondean extrayendo sus últimos vídeos vía feed Atom público.
- [ ] Las publicaciones ya existentes en la bandeja de moderación se omiten sin generar duplicados.
- [ ] Las nuevas publicaciones se registran en `SocialInboxItem` en estado `PendingReview` con metadatos y análisis de IA listos para revisión.
- [ ] Cada cuenta examinada actualiza su fecha `LastCheckedAt`.
- [ ] El servicio en segundo plano `SocialCollectorHostedService` ejecuta el ciclo respetando el intervalo configurado de forma resiliente.
- [ ] Los administradores pueden forzar el sondeo de todos los canales o de un canal individual desde `/admin/canales-monitorizados`.
- [ ] Toda la suite de pruebas unitarias pasa al 100% sin regresiones y sin emojis en la UI.
