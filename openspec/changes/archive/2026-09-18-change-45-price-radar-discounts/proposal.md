# Propuesta de Cambio: INC-45 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'

## 1. Contexto y Justificación
En el Manifiesto MVP de Ludeka, el estado **🔴 Quiero comprar** de la colección personal se diseñó como una lista de seguimiento comercial activa. Mientras que INC-11, INC-27 y INC-37 sentaron las bases para los enlaces de afiliación y la comprobación de stock y precios en tiempo real mediante microdatos Schema.org / OpenGraph, la plataforma carece de:
- Almacenamiento persistente de las lecturas de precios en el tiempo.
- Cálculo determinista del **Mínimo Histórico** absoluto y por tienda.
- Detección de bajadas de precio sustanciales (chollos o descuentos significativos).
- Experiencia de usuario en *Mi Ludoteca* que alerte proactivamente sobre ofertas en los juegos de la lista *Quiero comprar*.
- Sección pública o pestaña en el Radar para consultar los mayores descuentos y mínimos históricos vigentes en el catálogo.

## 2. Alcance Propuesto

### Dominio (`Ludeka.Core`)
- `GamePriceSnapshot`: Entidad inmutable que almacena la fotografía de precio tomada en un instante: `Id`, `GameId`, `StoreName`, `AffiliateUrl`, `Price`, `Currency`, `InStock`, `RecordedAtUtc`.
- `GamePriceMetrics`: Value object con métricas agregadas (`CurrentLowestPrice`, `CurrentLowestStore`, `AllTimeLowPrice`, `AllTimeLowStore`, `AllTimeLowDateUtc`, `AveragePrice`, `PriceDropPercentage`, `IsAllTimeLow`).
- `PriceDropAlert`: Representación de una alerta de oferta generada para un juego y tienda específica.

### Aplicación (`Ludeka.Application`)
- `IGamePriceRepository`: Interfaz para persistencia y consultas agregadas de precios y descuentos.
- `IPriceRadarService`: Orquestador de análisis de ofertas, cálculo de métricas y detección de bajadas de precio.
- DTOs correspondientes para la UI y la API interna (`PriceHistoryDto`, `GameDiscountDto`, `PriceDropAlertDto`).

### Infraestructura (`Ludeka.Infrastructure`)
- `SqliteGamePriceRepository`: Implementación en SQLite con soporte de transacciones y sentencias parametrizadas.
- Actualización de `LudekaDbContext` y `SqliteSchemaMigrator` para la tabla `GamePriceSnapshots` e índices correspondientes.
- Adaptador de muestreo de precios integrado con `IStoreStockService` para guardar instantáneas cuando se verifican ofertas.
- `PriceRadarHostedService`: Background worker liviano para refrescar periódicamente precios de juegos con alta demanda en listas *Quiero comprar* de forma respetuosa y espaciada.

### Interfaz de Usuario (`Ludeka.Web`)
1. **Ficha de Juego (`StoreOffersCard.razor`):**
   - Muestra el mínimo histórico en la cabecera ("Mínimo histórico: XX € en Tienda Y").
   - Etiqueta "¡Mínimo Histórico!" o badge de ahorro en ofertas que igualan o baten el récord.
2. **Mi Ludoteca (`MyLibrary.razor` -> Pestaña `Quiero comprar`):**
   - Enriquecimiento visual de los títulos seguidos con su mejor precio actual, ahorro respecto a la media o mínimo histórico.
   - Resumen superior de oportunidades activas en tu lista.
3. **Radar de Ofertas (`Radar.razor` o nueva vista dedicada):**
   - Pestaña "Ofertas & Chollos" en el Radar lúdico con selector y filtros por país, ordenación por mayor porcentaje de rebaja o novedad de la oferta.

## 3. Estrategia de Verificación y Testing
- Pruebas unitarias de dominio para `GamePriceSnapshot`, `GamePriceMetrics` y cálculo de descuentos.
- Pruebas de integración en memoria para `SqliteGamePriceRepository` (registro, consulta de histórico, cálculo de mínimos y top chollos).
- Pruebas de servicio para `IPriceRadarService` con escenarios de bajada, subida, stock out y repetición de precios.
- Pruebas de contratos de marcado y accesibilidad WCAG 2.2 AA para los nuevos componentes y badges.
- Verificación de no regresión sobre los 988 tests existentes.

## 4. Criterios de Transición SDD
Tras la aprobación de esta propuesta por parte del usuario, se procederá a:
1. `sdd-spec`: Redacción de especificación técnica y criterios de aceptación BDD (Gherkin).
2. `sdd-design`: Diseño detallado de contratos, esquemas de BD y componentes Razor.
3. `sdd-tasks`: Desglose granular de tareas ejecutables.
4. `sdd-apply`: Implementación guiada por pruebas (TDD estricto).
5. `sdd-verify`: Verificación de suite completa de pruebas.
6. `sdd-archive`: Integración, volcado a la especificación viva del sistema (`docs/specs/sistema/31-radar-precios-minimos-historicos.md`) y preparación del Pull Request a `main`.
