# INC-45: Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-15  
> **Rama de Trabajo:** `inc/price-radar-discounts`  
> **Worktree:** `C:\repos\ludeka-wt\price-radar-discounts`  
> **Dependencias:** INC-11 (Enlaces de Compra y Afiliados), INC-27 (Verificación de Stock en Vivo), INC-30 (Estado 'Quiero comprar' en Colección), INC-37 (APIs Reales y Afiliados Contextuales)  
> **Especificación Viva del Sistema:** [31. Radar de Bajadas de Precios y Mínimos Históricos](file:///c:/repos/Ludeka/docs/specs/sistema/31-radar-precios-minimos-historicos.md) (en redacción)

---

## 1. Motivación y Visión

En el Manifiesto y Especificación Funcional Maestra de Ludeka (`LUDIST_SPEC_FUNCIONAL_MVP.md`), el estado **🔴 Quiero comprar** de la ludoteca personal se concibió específicamente como una **lista de seguimiento comercial activa** (secciones 7.1 y 11.1):
> *"🔴 Quiero comprar: Lista de seguimiento comercial activa (disparador de alertas)."*  
> *"Radar de bajada de precios: Alertas internas a usuarios con el juego en Quiero comprar cuando el precio caiga."*

Hasta ahora, la plataforma cuenta con:
1. Enlaces de afiliados y tiendas en las fichas (`GamePurchaseLink`).
2. Comprobación en vivo de stock y precio orientativo (`IStoreStockService` / `HtmlSchemaStoreStockClient`).
3. Colección personal con filtro `CollectionStatus.WantToBuy` en *Mi Ludoteca*.

Sin embargo:
- **No existía persistencia histórica de precios:** cada consulta web era efímera en memoria caché L1 sin registrar la evolución en el tiempo.
- **No se calculaba el Mínimo Histórico:** el usuario no sabe si 39,99 € es una oferta real, el precio habitual o el más bajo registrado.
- **No existían alertas proactivas ni badges de descuento:** el usuario con juegos en *Quiero comprar* debía entrar manualmente tienda por tienda.
- **No existía un Radar de Ofertas / Chollos público:** la comunidad no disponía de un lugar donde descubrir de un vistazo las mejores oportunidades y bajadas de precio del catálogo.

Con **INC-45**, Ludeka cierra este pilar fundamental de valor lúdico y comercial independiente, respetando estrictamente la accesibilidad, estética editorial y ausencia de publicidad invasiva.

---

## 2. Objetivos y Alcance Técnico

### 2.1. Modelo de Dominio y Registro Histórico de Precios
- **`GamePriceSnapshot`**: Registro inmutable de precio con `Id`, `GameId`, `StoreName`, `AffiliateUrl`, `Price`, `Currency`, `InStock`, `RecordedAtUtc`.
- **`GamePriceMetrics`**: DTO/Value Object con estadísticas agregadas por juego:
  - `CurrentLowestPrice`: Mejor precio actual disponible en stock.
  - `CurrentLowestStore`: Nombre de la tienda con la mejor oferta actual.
  - `AllTimeLowPrice`: Mínimo histórico absoluto registrado.
  - `AllTimeLowStore`: Tienda donde se alcanzó el mínimo histórico.
  - `AllTimeLowDateUtc`: Fecha en la que se registró el mínimo histórico.
  - `AveragePrice`: Precio medio registrado.
  - `PriceDropPercentage`: Porcentaje de rebaja respecto al precio anterior o medio (ej. -22%).
  - `IsAllTimeLow`: Bandera booleana si la oferta actual iguala o bate el mínimo histórico.

### 2.2. Repositorio e Infraestructura de Precios (`IGamePriceRepository`)
- Persistencia dual en SQLite (`SqliteGamePriceRepository`) y PostgreSQL (`LudekaDbContext` / `SqliteSchemaMigrator`).
- Consultas optimizadas por índice `(GameId, RecordedAtUtc)` y `(StoreName, GameId)`.
- Métodos de inserción masiva (`RecordSnapshotsAsync`), consulta histórica (`GetHistoryAsync`), métricas consolidadas (`GetMetricsAsync`) y obtención de las mayores bajadas recientes (`GetTopDiscountsAsync`).

### 2.3. Motor de Detección de Bajadas y Chollos (`IPriceRadarService`)
- Orquesta la captura periódica o bajo demanda de precios desde `IStoreStockService`.
- Evalúa los umbrales de oferta:
  - **Mínimo Histórico:** Cuando `CurrentPrice <= AllTimeLowPrice`.
  - **Bajada Significativa / Chollo:** Rebaja >= 10% respecto al histórico reciente.
  - **Oportunidad en Stock:** Reaparición en stock con precio competitivo de un juego previamente agotado.
- Generación de alertas para usuarios con el juego en `WantToBuy`.

### 2.4. Alertas y Experiencia en "Mi Ludoteca" -> "Quiero comprar"
- Enriquecimiento de la pestaña `TabType.WantToBuy` en `/mi-ludoteca`:
  - Cada tarjeta de juego muestra la mejor oferta actual en vivo, badge de mínimo histórico o porcentaje de bajada ("-15% en Zacatrus").
  - Resumen superior con contador de ofertas activas para tu lista ("3 de tus juegos seguidos tienen bajada de precio hoy").
  - Enlace directo a la tienda con parámetro de afiliado contextual.

### 2.5. Ficha del Juego: Sección de Precios y Mínimo Histórico (`StoreOffersCard.razor`)
- Insignia destacada en la cabecera de "Dónde Comprar":
  - *"Mínimo histórico: 34,95 € en Jugamos Otra"*
- Indicador visual en cada oferta si está en mínimo histórico o en su precio medio habitual.

### 2.6. Radar de Ofertas Comunitario (`/radar` / `/ofertas`)
- Pestaña o sección integrada en el Radar lúdico con selector:
  - Pestaña 1: 🎁 **Sorteos Activos** (comunitarios e instagram).
  - Pestaña 2: 🏷️ **Radar de Ofertas & Bajadas** (chollos filtrables por país, porcentaje de rebaja y disponibilidad inmediata).

### 2.7. Worker Desatendido de Precios (`PriceRadarHostedService`)
- Sondeo en segundo plano con intervalos de cortesía para monitorizar los juegos más deseados en listas `WantToBuy` sin saturar servidores externos.
- Modo simulado y respetuoso con límites de peticiones HTTP.

---

## 3. Criterios de Aceptación y Verificación
1. **Historial inmutable:** Registro determinista de cambios de precio sin duplicar lecturas idénticas en ventanas cortas.
2. **Cálculo de Mínimo Histórico:** Cálculo matemático preciso de mínimos y porcentajes de descuento.
3. **Privacidad y Rendimiento:** Cero impacto en el render inicial de fichas (streaming / carga asíncrona no bloqueante).
4. **Respeto a las Directrices Editorial y WCAG:** Iconos Lucide sin emojis en UI de producción, soporte de temas visuales y filtrado territorial estricto por país del usuario.
5. **Cobertura de Tests:** Suite 100% en verde con pruebas unitarias exhaustivas para repositorios, motor de métricas, alertas y componentes Blazor.
