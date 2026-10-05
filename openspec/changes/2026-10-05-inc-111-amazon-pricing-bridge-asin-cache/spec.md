# Especificación INC-111: Proveedores de Precios de Amazon, Mapeo EAN-ASIN y Caché (< 24h)

## 1. Contexto y Objetivos del Sistema
Ludeka integra enlaces de compra contextuales con afiliados de Amazon España (`ludeka-21`). Para enriquecer las fichas de catálogo con precios y disponibilidad real sin violar las políticas de Amazon ni depender inmediatamente de la aprobación de la PA-API 5.0 oficial, se requiere:
1. Una capa de abstracción desacoplada para consultar productos de Amazon.
2. Soporte para un proveedor puente basado en API Key (Rainforest API) y preparación completa del proveedor oficial PA-API 5.0 con firma AWS v4.
3. Persistencia del identificador de producto `Asin` en la entidad [`Game`](file:///src/Ludeka.Core/Entities/Game.cs) y en base de datos.
4. Servicio de sincronización y caché que imponga el límite estricto de 24 horas (TTL < 24h) establecido en el acuerdo operativo de afiliados de Amazon.

---

## 2. Requerimientos de Dominio y Entidades

### REQ-DOM-01: Atributo `Asin` en `Game`
- La entidad `Game` debe incorporar la propiedad `public string? Asin { get; private set; }`.
- Debe existir el método de mutación `public void SetAsin(string? asin)`.
- **Invariantes:**
  - El ASIN debe normalizarse eliminando espacios en blanco en los extremos.
  - Si es una cadena vacía o espacios en blanco, se almacena como `null`.
  - Si no es nulo, su longitud máxima estándar no debe exceder los 20 caracteres (normalmente 10 caracteres alfanuméricos en mayúsculas).

### REQ-DOM-02: Modelo de Resultado `AmazonProductPriceResult`
- DTO inmutable (record) en `Ludeka.Application.DTOs`:
  - `string Asin`: Identificador único de Amazon.
  - `decimal Price`: Precio extraído.
  - `string Currency`: Símbolo de moneda (por defecto `"€"`).
  - `bool InStock`: Disponibilidad inmediata para compra.
  - `string? Title`: Título devuelto por el marketplace.
  - `string? ProductUrl`: Enlace canónico al producto.
  - `DateTimeOffset FetchedAtUtc`: Marca temporal de la consulta.

---

## 3. Requerimientos de Integración y Servicios

### REQ-INT-01: Interfaz `IAmazonProductProvider`
Definida en `Ludeka.Application.Interfaces`:
```csharp
public interface IAmazonProductProvider
{
    Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default);
    Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default);
}
```

### REQ-INT-02: Proveedor Puente `RainforestAmazonProductProvider`
- Ubicado en `Ludeka.Infrastructure.Affiliates.Amazon`.
- Consume la API REST de Rainforest:
  - Búsqueda por EAN: `type=search&search_term={ean}&amazon_domain=amazon.es` (o parámetro `gtin={ean}`).
  - Consulta de producto por ASIN: `type=product&asin={asin}&amazon_domain=amazon.es`.
- Maneja de forma defensiva:
  - Respuestas HTTP no exitosas (4xx, 5xx) o timeouts: retorna `null` y registra advertencia sin lanzar excepciones no controladas.
  - Parseo de precios (ej. campo `buybox_winner.price.value` o `prices[0].value`).

### REQ-INT-03: Proveedor Oficial `OfficialAmazonPaApiProvider`
- Ubicado en `Ludeka.Infrastructure.Affiliates.Amazon`.
- Diseñado para la especificación PA-API 5.0:
  - Operaciones `SearchItems` (búsqueda por EAN) y `GetItems` (consulta por ASIN).
  - Estructura preparada para firma criptográfica AWS SigV4 (HMAC-SHA256 con `AccessKey`, `SecretKey`, `AssociateTag`, región `eu-west-1` y host `webservices.amazon.es`).
  - Validación defensiva: si las credenciales no están configuradas, retorna `null` con log explicativo en lugar de colapsar la aplicación.

### REQ-INT-04: Orquestador y Caché `IAmazonPriceSyncService`
- Ubicado en `Ludeka.Application.Services`:
```csharp
public interface IAmazonPriceSyncService
{
    Task<GamePriceSnapshot?> SyncGamePriceAsync(Guid gameId, CancellationToken ct = default);
}
```
- **Lógica de negocio y caché de 24h:**
  1. Recupera el juego por su `gameId`. Si no existe, retorna `null`.
  2. Consulta en `IGamePriceSnapshotRepository` el último snapshot registrado para la tienda `"Amazon"`.
  3. Si existe un snapshot y `DateTimeOffset.UtcNow - snapshot.RecordedAtUtc < TimeSpan.FromHours(24)`:
     - El precio se considera vigente. Se devuelve el snapshot existente sin invocar la API externa.
  4. Si no existe o ha expirado:
     - Si el juego no tiene `Asin`, pero tiene `Ean`:
       - Invoca `provider.LookupAsinByEanAsync(game.Ean)`.
       - Si encuentra un ASIN, lo asigna mediante `game.SetAsin(asin)` y persiste el cambio en el repositorio de juegos.
     - Si el juego tiene `Asin` (original o recién resuelto):
       - Invoca `provider.GetPriceAndStockAsync(game.Asin)`.
       - Si se obtiene precio, crea un nuevo `GamePriceSnapshot(gameId, "Amazon", affiliateUrl, price, inStock, currency, now)` y lo guarda en el repositorio de snapshots.
       - Actualiza o añade el `GamePurchaseLink` correspondiente a Amazon con la etiqueta de afiliado configurada (`ludeka-21`).
       - Guarda el juego con el enlace de compra actualizado.

---

## 4. Requerimientos de Persistencia y Migración

### REQ-PER-01: Migración Defensiva de Esquema (SQLite y PostgreSQL)
- En `SqliteGameRepository`:
  - Verificar en la inicialización si la columna `Asin` existe en la tabla `Games`. Si no existe, ejecutar `ALTER TABLE Games ADD COLUMN Asin TEXT NULL;`.
  - Mapear la lectura y escritura de `Asin` en las consultas de `GetByIdAsync`, `GetBySlugAsync`, `SearchAsync`, `InsertAsync` y `UpdateAsync`.
- En esquema PostgreSQL (`LudekaDbContext` / Migración):
  - Configurar propiedad `Asin` en el entity type de `Game` con longitud máxima 20 y nulabilidad.

---

## 5. Criterios de Aceptación y Pruebas Automáticas

1. **Pruebas de Dominio (`GameAsinTests.cs`)**:
   - `Game.SetAsin` normaliza espacios en blanco, asigna valores válidos y convierte cadenas vacías en `null`.
2. **Pruebas de Proveedor Puente (`RainforestAmazonProductProviderTests.cs`)**:
   - Mapeo correcto de EAN a ASIN con simulación de respuesta HTTP JSON válida.
   - Extracción de precio, moneda, disponibilidad y URL de producto a partir de payload JSON de Rainforest.
   - Manejo resiliente ante errores 404/500/timeout sin lanzar excepciones hacia la capa superior.
3. **Pruebas de Proveedor Oficial (`OfficialAmazonPaApiProviderTests.cs`)**:
   - Validación de configuración ausente o incompleta (retorna null con log sin fallar).
   - Generación de estructura de cabeceras de firma AWS v4.
4. **Pruebas de Servicio de Caché (`AmazonPriceSyncServiceTests.cs`)**:
   - Si existe un snapshot de Amazon de hace menos de 24 horas, **no** llama a `IAmazonProductProvider` y devuelve el snapshot en caché.
   - Si no existe snapshot o tiene más de 24 horas, llama al proveedor, crea nuevo `GamePriceSnapshot` y actualiza `PurchaseLinks`.
   - Si el juego carece de `Asin` pero tiene `Ean`, realiza el flujo en dos fases: resuelve el ASIN, lo persiste en el juego y a continuación pide el precio.
