# Exploración: change-09-notifications-webhooks (Incremento 9)

## 1. Estado Actual de la Solución

Ludeka cuenta con 8 incrementos completados y archivados, con una base sólida de Clean Architecture sobre .NET 10 y C# 13:
- **`Ludeka.Core`:** Entidades de dominio para juegos (`Game`), veredictos de la mesa fundadora (`FoundingVerdict`), sorteos comunitarios (`Giveaway`), lanzamientos de los viernes (`WeeklyRelease`), dudas de reglas comunitarias (`RuleQuestion`, `RuleAnswer`) y expansiones (`ExpansionSynergy`, `ExpansionRecipe`).
- **`Ludeka.Application`:** Servicios de aplicación especializados (`GiveawayService`, `WeeklyReleaseService`, `FoundingVerdictService`, `RuleQAService`, `ExpansionService`), DTOs y contratos desacoplados.
- **`Ludeka.Infrastructure`:** Persistencia con SQLite y EF Core (`LudekaDbContext`), clientes HTTP tipados para BGG XMLAPI2, y decoradores de caché L1/L2.
- **`Ludeka.Web`:** Blazor Web App interactiva con SSR y componentes de servidor, Tailwind CSS compilado en local y sistema dinámico de 4 temas visuales.
- **Pruebas:** 170 tests unitarios pasando al 100% en verde con xUnit y .NET 10.

---

## 2. Análisis del Problema y Oportunidad

Actualmente, las actividades comunitarias de Ludeka (sorteos del radar, boletines de lanzamientos semanales, veredictos de la mesa fundadora y soluciones a dudas complejas de reglamento) residen únicamente dentro de la web. Los usuarios solo se enteran de ellas si entran activamente a la plataforma.

Para dinamizar la comunidad y aumentar la retención orgánica sin coste publicitario ni dependencia de plataformas cerradas:
1. **Canales de Alta Retención:** Las comunidades de juegos de mesa en español están fuertemente concentradas en **Discord** (servidores de asociaciones, clubes y canales lúdicos) y **Telegram** (canales de chollos, novedades y quedadas).
2. **Desacoplamiento y Rendimiento:** La difusión a webhooks o APIs externas (Discord Webhooks, Telegram Bot API) jamás debe ralentizar la navegación web ni bloquear las peticiones HTTP del usuario.
3. **Resiliencia ante Fallos Externos:** Si la API de Discord o Telegram devuelve un rate limit (HTTP 429), un timeout o un error 5xx, el sistema debe registrar el fallo, permitir reintentos y operar en modo silencioso sin provocar excepciones no controladas.
4. **Modo Dry-Run / Simulación:** En entornos locales, staging o ejecución de pruebas unitarias automatizadas, el sistema debe poder operar en modo simulado registrando las salidas sin realizar llamadas HTTP reales.

---

## 3. Puntos de Integración en el Dominio Existente

- **Sorteos Próximos a Expirar (`Giveaway`):**
  - Entidad: `Giveaway.DeadlineAt` e `IsExpired`.
  - Evento: Notificación 24 horas antes del cierre del plazo para fomentar la participación de última hora.
- **Boletín de Lanzamientos de Viernes (`WeeklyRelease`):**
  - Entidad: `WeeklyRelease.ReleaseDate`, `Title`, `Publisher`, `EstimatedPvp`, `IsReprint`.
  - Evento: Resumen consolidado cada viernes con los títulos que llegan a tiendas.
- **Nuevo Veredicto Fundador (`FoundingVerdict`):**
  - Entidad: `FoundingVerdict.Recommendation`, `OverallVerdict`, `GameId`.
  - Evento: Publicación inmediata al emitirse un nuevo análisis oficial de la casa con sello lúdico (*Imprescindible* / *Recomendado*).
- **Duda de Reglas Resuelta (`RuleQuestion` + `RuleAnswer`):**
  - Entidad: `RuleQuestion.AcceptedAnswerId`, `Title`.
  - Evento: Al marcar una respuesta como aceptada por el autor o moderador, difundir la solución lúdica a la comunidad.

---

## 4. Estrategia Técnica Propuesta

1. **Patrón Productor-Consumidor en Memoria:** `System.Threading.Channels.Channel<CommunityNotificationMessage>` acotado (Bounded) para encolar notificaciones instantáneamente desde cualquier servicio sin latencia de red.
2. **Servicio en Segundo Plano (`BackgroundService`):** `CommunityNotificationDispatcherHostedService` que consume el canal asíncronamente y despacha a los clientes HTTP de Discord y Telegram.
3. **Persistencia e Historial en SQLite:** Entidad `CommunityNotificationLog` que audita cada mensaje enviado, canal, estado (Pendiente, Enviado, Fallido, Simulado), fecha y mensaje de error en caso de fallo.
4. **Clientes HTTP Tipados:** `IDiscordWebhookClient` y `ITelegramBotClient` configurados mediante `HttpClientFactory` con deserialización segura y formato enriquecido (Discord Embeds con color terracota de marca `#E05A38` y Telegram HTML con formato de negritas y enlaces limpios).
5. **Panel de Gestión y Diagnóstico (`AdminNotifications.razor`):** Pantalla en Blazor accesible para el equipo fundador con estado de conexión, botón de prueba en 1 clic ("Ping de prueba"), disparadores manuales del boletín y visor de historial con reintento de fallidos.
