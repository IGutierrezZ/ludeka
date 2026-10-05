# Propuesta INC-111: Arquitectura de Proveedores de Precios de Amazon (API Puente / PA-API Oficial), Mapeo EAN-ASIN y Caché de Precios

## 1. Motivación y Diagnóstico
Actualmente en Ludeka:
1. La integración con Amazon se limita a la resolución de enlaces de afiliados (`tag=ludeka-21`) y búsquedas por título (`/api/affiliates/{gameSlug}/amazon`).
2. No existe extracción automatizada de precios reales ni disponibilidad/stock de juegos en Amazon.
3. Para obtener las credenciales de la **PA-API 5.0 oficial** (*Product Advertising API*), Amazon exige 3 compras cualificadas previas en los primeros 180 días. Hasta entonces, no es posible invocar la API oficial directamente.
4. Muchos juegos ya disponen de código de barras **EAN** (incorporado en INC-86 e INC-105), pero no cuentan con el **ASIN** (*Amazon Standard Identification Number*), indispensable para consultas atómicas, directas y económicas.
5. El acuerdo de afiliados de Amazon prohíbe taxativamente almacenar o mostrar precios que tengan más de 24 horas sin actualizar o refrescar.

---

## 2. Alcance Propuesto

1. **Abstracción del Proveedor de Precios (`IAmazonProductProvider`)**:
   - Crear contrato en `Ludeka.Application` desacoplado del mecanismo de transporte:
     - `LookupAsinByEanAsync(string ean, CancellationToken ct)`: Resuelve el identificador ASIN a partir del EAN.
     - `GetPriceAndStockAsync(string asin, CancellationToken ct)`: Obtiene precio, moneda, disponibilidad y URL canónica.
   - En `Ludeka.Infrastructure`, proveer dos implementaciones bajo el patrón Strategy/Adapter:
     - `RainforestAmazonProductProvider` (o proveedor puente HTTP genérico mediante API key configurable).
     - `OfficialAmazonPaApiProvider` (esqueleto e implementación preparados para la firma AWS v4 / HMAC-SHA256 con `AccessKey`, `SecretKey` y `AssociateTag` para cuando Amazon conceda el acceso).
   - Selector en `appsettings.json`: `"Amazon:Provider": "Bridge" | "Official" | "None"`.

2. **Modelo de Dominio y Persistencia del ASIN en `Game`**:
   - Incorporar la propiedad `public string? Asin { get; private set; }` en la entidad [`Game`](file:///src/Ludeka.Core/Entities/Game.cs) y su método de mutación `SetAsin(string asin)`.
   - Soporte en repositorios SQLite y PostgreSQL (añadir columna `Asin` con migración segura y defensiva).

3. **Caché y Ciclo de Vida de Precios (`GamePriceSnapshot`)**:
   - Aprovechar la entidad [`GamePriceSnapshot`](file:///src/Ludeka.Core/Entities/GamePriceSnapshot.cs) existente.
   - Implementar `AmazonPriceSyncService` (o decorador de caché) que verifique la antigüedad de la última captura (`RecordedAtUtc`):
     - Si tiene menos de 24 horas (TTL < 24h), retorna el valor en caché sin realizar peticiones salientes (ahorro de cuota y cumplimiento legal de Amazon).
     - Si ha expirado o no existe, consulta al proveedor configurado, persiste el nuevo snapshot y actualiza la oferta en `GamePurchaseLink`.

4. **Flujo de Resolución en Dos Fases (EAN → ASIN → Precio)**:
   - Si el juego tiene `Ean` pero no `Asin`, el servicio primero resuelve y almacena el `Asin`.
   - Las consultas posteriores utilizan directamente el `Asin`, reduciendo el consumo de peticiones y optimizando la latencia.
