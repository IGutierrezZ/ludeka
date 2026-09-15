# Especificación de Requerimientos: INC-45 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'

## 1. Resumen Ejecutivo
El presente incremento formaliza la infraestructura de captura histórica de precios, detección de ofertas destacadas (chollos) y cálculo de mínimos históricos en Ludeka. Dota de interactividad e inteligencia comercial a la lista de seguimiento **🔴 Quiero comprar** de *Mi Ludoteca*, añade información contextual de valor en la ficha de cada juego y despliega una nueva vista comunitaria en el Radar lúdico para consultar ofertas verificadas.

---

## 2. Requerimientos Funcionales (FR)

### FR-01: Registro Histórico Inmutable de Precios
- El sistema debe persistir cada lectura de precio obtenida para una tienda y juego en una entidad `GamePriceSnapshot`.
- Campos mínimos: `Id` (Guid), `GameId` (Guid), `StoreName` (string), `AffiliateUrl` (string), `Price` (decimal), `Currency` (string), `InStock` (bool), `RecordedAtUtc` (DateTimeOffset).
- **Regla anti-saturación:** Si en las últimas 6 horas ya existe una lectura para el mismo `GameId` y `StoreName` con exactamente el mismo `Price` y estado `InStock`, el sistema no inserta una fila redundante, preservando espacio y rendimiento.

### FR-02: Cálculo Determinista de Métricas y Mínimos Históricos
- Para cualquier juego con historial registrado, el sistema debe calcular un objeto `GamePriceMetrics`:
  - `CurrentLowestPrice`: El menor precio actual en stock entre todas las tiendas asociadas al juego.
  - `CurrentLowestStore`: Tienda que ofrece el precio actual más bajo.
  - `AllTimeLowPrice`: El precio más bajo jamás registrado para este juego (en stock o global).
  - `AllTimeLowStore`: Nombre de la tienda donde se produjo el mínimo histórico.
  - `AllTimeLowDateUtc`: Fecha exacta del mínimo histórico.
  - `AveragePrice`: Precio medio histórico en tiendas.
  - `PriceDropPercentage`: Porcentaje de descuento del precio actual respecto al precio anterior más reciente o al PVP orientativo:
    $$\text{Descuento} = \left(1 - \frac{\text{Precio Actual}}{\text{Precio Referencia}}\right) \times 100$$
  - `IsAllTimeLow`: `true` si la mejor oferta actual en stock es menor o igual al `AllTimeLowPrice`.

### FR-03: Detección de Chollos y Bajadas Destacadas
- Un juego se califica como **Oferta Destacada (Chollo)** si:
  1. Su precio actual en stock representa una bajada $\ge 10\%$ respecto a su precio medio o de referencia anterior.
  2. O la oferta actual alcanza un nuevo **Mínimo Histórico** absoluto.
- El servicio `IPriceRadarService.GetTopDiscountsAsync` debe devolver las mejores ofertas activas ordenadas por porcentaje de descuento o proximidad al mínimo histórico, con soporte de filtrado territorial por país.

### FR-04: Alertas Personalizadas para la Lista 'Quiero comprar'
- Para un usuario autenticado, `IPriceRadarService.GetUserWantToBuyAlertsAsync(userId)` debe contrastar los juegos marcados con `CollectionStatus.WantToBuy` en su ludoteca contra las ofertas actuales.
- Si un juego seguido tiene una rebaja $\ge 10\%$ o está en mínimo histórico, se emite un `PriceDropAlertDto`.
- La pestaña `Comprar` en `/mi-ludoteca` muestra un banner superior resumen ("X ofertas activas para tu lista de compra") y badges de bajada en las tarjetas correspondientes.

### FR-05: Ficha del Juego (`StoreOffersCard.razor`)
- La tarjeta "Dónde Comprar" de la ficha del juego debe exhibir en la cabecera:
  - Insignia de mínimo histórico: *"Mínimo histórico: XX,XX € en [Tienda]"* (si existe histórico).
- En cada tarjeta de oferta individual:
  - Badge `"Mínimo Histórico"` si la oferta iguala o mejora el récord histórico.
  - Badge `"Bajada -XX%"` si la oferta tiene un descuento significativo respecto al PVP orientativo.

### FR-06: Pestaña Comunitaria "Ofertas & Chollos" en `/radar`
- La página `/radar` (y `/sorteos`) se amplía con pestañas editoriales:
  - **Sorteos Activos:** Mantiene el radar de sorteos existente.
  - **Ofertas & Chollos:** Muestra el catálogo de juegos con bajada de precio activa, indicando tienda, precio actual, precio habitual, porcentaje de descuento y botón directo a la tienda.
  - Soporte de filtro por país de envío y ordenación (Mayor descuento, Menor precio, Recientes).

### FR-07: Background Worker de Muestreo de Precios (`PriceRadarHostedService`)
- Tarea en segundo plano con ejecución configurable (intervalo por defecto: 6 horas).
- Recorre los títulos en seguimiento (`WantToBuy`) con mayor demanda comunitaria y muestrea precios mediante `IStoreStockService`.
- Opera de forma respetuosa con backoff y modo simulado para desarrollo y suites de prueba.

---

## 3. Criterios de Aceptación BDD (Gherkin)

### Escenario 1: Cálculo exacto de mínimo histórico y porcentaje de bajada
```gherkin
Dado que un juego "Terraforming Mars" tiene un histórico con precios de 45.00 €, 42.00 € y 38.00 €
Cuando una tienda ofrece "Terraforming Mars" a 34.00 € en stock
Entonces el sistema determina que IsAllTimeLow es verdadero
Y el AllTimeLowPrice es 34.00 €
Y el porcentaje de rebaja respecto a los 45.00 € originales es de 24.44%
```

### Escenario 2: Generación de alertas para títulos en 'Quiero comprar'
```gherkin
Dado un usuario que tiene "Ark Nova" en su colección con estado WantToBuy
Cuando el sistema detecta que "Ark Nova" baja de 55.00 € a 44.00 € en una tienda asociada
Entonces IPriceRadarService.GetUserWantToBuyAlertsAsync incluye una alerta para "Ark Nova"
Y en la pestaña 'Comprar' de Mi Ludoteca se muestra el badge de bajada de precio con la tienda y el nuevo importe
```

### Escenario 3: Prevención de duplicados en el historial
```gherkin
Dado que se registró una lectura de 39.99 € para "Catan" en la tienda "Zacatrus" hace 2 horas
Cuando el servicio de stock vuelve a consultar "Catan" en "Zacatrus" con el mismo precio de 39.99 € y stock en existencia
Entonces no se crea un nuevo registro duplicado en la base de datos
```

### Escenario 4: Filtrado territorial en el Radar de Ofertas
```gherkin
Dado un listado de 5 ofertas activas donde 3 envían a "España" y 2 solo a "México"
Cuando un usuario con ubicación en "España" consulta el Radar de Ofertas
Entonces solo se visualizan las 3 ofertas con envío disponible a "España"
```

---

## 4. Requerimientos No Funcionales (NFR)
- **NFR-01 (Dual Persistence):** Soporte completo para SQLite local y PostgreSQL (Supabase) sin dependencias exclusivas de motor.
- **NFR-02 (Estética & A11y):** Iconos SVG vectoriales Lucide, cero emojis decorativos en producción, contraste WCAG 2.2 AA.
- **NFR-03 (Performance):** Consultas indexadas por `(GameId, RecordedAtUtc)` para asegurar tiempos de respuesta $< 15\text{ ms}$ en SQLite.
- **NFR-04 (Testing):** Cobertura unitaria estricta de todos los casos de uso, agregados de dominio y componentes web (+20 pruebas nuevas).
