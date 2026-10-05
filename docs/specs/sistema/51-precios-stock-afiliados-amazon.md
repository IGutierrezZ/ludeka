# 51. Arquitectura de Proveedores de Precios de Amazon (API Puente / PA-API Oficial), Mapeo EAN-ASIN y Caché de Precios (< 24h)

> **Módulo:** `Ludeka.Application` / `Ludeka.Infrastructure` / `Ludeka.Core`  
> **Incremento:** INC-112  
> **Estado:** ✅ Verificado y Documentado  
> **Fecha de Especificación:** 2026-10-05  

---

## 1. Contexto y Objetivos del Módulo

Amazon España (`amazon.es`) es uno de los canales clave de compra y afiliación lúdica de Ludeka, operado bajo el identificador de afiliado `ludeka-21`. Sin embargo:
1. **Acceso Restringido a PA-API 5.0 Oficial:** Para obtener y mantener credenciales activas de la *Product Advertising API 5.0* de Amazon, el programa de afiliados exige que la cuenta genere al menos **3 compras cualificadas** en sus primeros 180 días. Hasta alcanzar dicho hito, las llamadas a la API oficial no están disponibles.
2. **Identificación por EAN vs. ASIN:** Las fichas de Ludeka disponen de códigos de barras comerciales **EAN-13** (incorporados en INC-86, INC-105 e INC-108). Sin embargo, el catálogo de Amazon y sus APIs operan primordialmente sobre **ASIN** (*Amazon Standard Identification Number*).
3. **Cumplimiento Normativo Estricto de Afiliados (< 24 horas):** La directiva operativa del programa de afiliados de Amazon prohíbe de forma taxativa cachear o mostrar precios o disponibilidad con más de 24 horas de antigüedad sin refrescarlos contra Amazon.

Este módulo provee la arquitectura integral desacoplada para solventar estas tres necesidades con soporte dual e intercambiable de proveedores, persistencia de ASIN en el dominio y base de datos, y observancia legal rigurosa de caché.

---

## 2. Dominio: Persistencia de ASIN en `Game`

Se incorpora el identificador ASIN en la entidad raíz de agregación [`Game`](file:///src/Ludeka.Core/Entities/Game.cs):

```csharp
public string? Asin { get; private set; }

public void SetAsin(string? asin)
{
    if (string.IsNullOrWhiteSpace(asin))
    {
        Asin = null;
        return;
    }

    var trimmed = asin.Trim().ToUpperInvariant();
    Asin = trimmed.Length > 20 ? trimmed[..20] : trimmed;
}
```

### Invariantes de Dominio
- **Normalización Invariable:** El ASIN se almacena en mayúsculas (`ToUpperInvariant()`) y sin espacios marginales.
- **Truncado Seguro:** Si excede 20 caracteres se trunca de forma defensiva para no romper esquemas de base de datos.
- **Nulabilidad:** Valores vacíos o en blanco se limpian a `null`.
- **Indexación:** Cuenta con índice dedicado en base de datos (`IX_Games_Asin`) para consultas ultra-rápidas directas.

---

## 3. Capa de Aplicación y Contratos

### DTOs y Contrato de Proveedor
Definidos en `Ludeka.Application`:
- [`AmazonProductPriceResult`](file:///src/Ludeka.Application/DTOs/AmazonProductPriceResult.cs):
  - `string Asin`: Identificador de Amazon.
  - `decimal? Price`: Precio extraído (null si no está disponible o agotado).
  - `string Currency`: Divisa ISO (por defecto `"EUR"`).
  - `bool InStock`: Indica disponibilidad de compra inmediata o reserva.
  - `string? Title`: Título del producto en Amazon.
  - `string? ProductUrl`: Enlace canónico del producto.
  - `DateTime FetchedAtUtc`: Marca temporal exacta de extracción.
- [`IAmazonProductProvider`](file:///src/Ludeka.Application/Contracts/IAmazonProductProvider.cs):
  - `Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default)`: Resuelve un EAN-13 al ASIN correspondiente.
  - `Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default)`: Consulta precio, stock y metadatos por ASIN.

### Configuración en `AmazonOptions`
Configurado en `appsettings.json` bajo la sección `"Amazon"`:
```json
{
  "Amazon": {
    "Provider": "Bridge", // Opciones: "None", "Bridge", "Official"
    "AssociateTag": "ludeka-21",
    "DomainMatch": "amazon.es",
    "Bridge": {
      "ApiKey": "...",
      "BaseUrl": "https://api.rainforestapi.com",
      "AmazonDomain": "amazon.es"
    },
    "PaApi": {
      "AccessKey": "...",
      "SecretKey": "...",
      "AssociateTag": "ludeka-21",
      "Region": "eu-west-1",
      "Host": "webservices.amazon.es"
    }
  }
}
```

### Servicio Orquestador y Caché (< 24h): `AmazonPriceSyncService`
El servicio [`AmazonPriceSyncService`](file:///src/Ludeka.Application/Features/Affiliates/AmazonPriceSyncService.cs) implementa la lógica de orquestación y cumplimiento normativo:
1. **Evaluación de Caché Existente:**
   - Comprueba si existe un `GamePriceSnapshot` con `Source == "Amazon"` cuya antigüedad sea menor a 24 horas (`SnapshotDateUtc >= DateTime.UtcNow.AddHours(-24)`).
   - En caso afirmativo, devuelve inmediatamente el snapshot en caché sin realizar ninguna petición HTTP saliente ni gastar cuota.
2. **Flujo en Dos Fases:**
   - Si `Game.Asin` es nulo pero existe `Game.Ean`, invoca `_provider.LookupAsinByEanAsync(game.Ean)` y almacena el ASIN resultante en `game.SetAsin(asin)`, guardando los cambios con `_gameRepository.UpdateAsync(game)`.
   - Con el ASIN disponible, invoca `_provider.GetPriceAndStockAsync(asin)`.
3. **Persistencia de Snapshot y Actualización de Enlace de Afiliado:**
   - Crea un nuevo `GamePriceSnapshot` con el precio, disponibilidad y procedencia.
   - Crea o actualiza el enlace en `GamePurchaseLink` asegurando la presencia del parámetro `tag=ludeka-21` y la URL canónica del producto.

---

## 4. Implementaciones de Infraestructura

### 1. Proveedor Puente (`RainforestAmazonProductProvider`)
- Permite la extracción inmediata de datos mediante [Rainforest API](https://www.rainforestapi.com/) utilizando una simple API Key.
- Resuelve búsquedas por EAN invocando `/request?api_key=...&type=search&search_term={ean}&amazon_domain={domain}`.
- Consulta detalles de producto e información de la BuyBox invocando `/request?api_key=...&type=product&asin={asin}&amazon_domain={domain}`.
- Si no hay API Key configurada o la respuesta es errónea, degrada elegantemente sin lanzar excepciones hacia la capa superior.

### 2. Proveedor Oficial PA-API 5.0 (`OfficialAmazonPaApiProvider`)
- Diseñado para activarse en cuanto la cuenta de Amazon disponga de `AccessKey` y `SecretKey`.
- **Firma Criptográfica Pura AWS SigV4 (HMAC-SHA256):** Implementada en C# nativo sin dependencias de SDKs pesados de AWS.
  - Firma en cascada: `Key` -> `DateKey` -> `DateRegionKey` -> `DateRegionServiceKey` -> `SigningKey`.
  - Construcción de peticiones canónicas y *String-to-Sign* con cabeceras requeridas (`content-encoding`, `content-type`, `host`, `x-amz-date`, `x-amz-target`).
- Implementa endpoints oficiales de PA-API 5.0:
  - `com.amazon.paapi5.v1.ProductAdvertisingAPIv1.SearchItems` (búsqueda de EAN en índice `All`).
  - `com.amazon.paapi5.v1.ProductAdvertisingAPIv1.GetItems` (extracción de `Offers.Listings.Price` y disponibilidad).
- Valida de antemano la presencia de claves; si no están configuradas, retorna `null` de forma segura.

### 3. Proveedor Nulo (`NullAmazonProductProvider`)
- Utilizado cuando `Provider` está establecido en `"None"` o no está configurado. Retorna `Task.FromResult<string?>(null)` y `Task.FromResult<AmazonProductPriceResult?>(null)` de forma no bloqueante.

---

## 5. Esquema de Base de Datos y Persistencia

### SQLite (`SqliteSchemaMigrator.cs` y `SqliteGameRepository.cs`)
```sql
ALTER TABLE Games ADD COLUMN Asin TEXT NULL;
CREATE INDEX IF NOT EXISTS IX_Games_Asin ON Games(Asin);
```

### PostgreSQL (`LudekaDbContext.cs`)
```csharp
game.Property(g => g.Asin)
    .HasMaxLength(20)
    .IsRequired(false);

game.HasIndex(g => g.Asin)
    .HasDatabaseName("IX_Games_Asin");
```

---

## 6. Pruebas y Validación Automática

El módulo cuenta con una suite dedicada de 26 pruebas automáticas:
1. `GameAsinTests` (8 pruebas): Invariantes de dominio, normalización a mayúsculas, truncado a 20 caracteres y manejo de nulos.
2. `RainforestAmazonProductProviderTests` (6 pruebas): Mapeo EAN-ASIN, extracción de buybox y stock, resiliencia ante errores HTTP y claves vacías.
3. `OfficialAmazonPaApiProviderTests` (6 pruebas): Firma AWS SigV4, cabeceras requeridas, validación de credenciales y parseo de respuestas JSON de PA-API.
4. `AmazonPriceSyncServiceTests` (5 pruebas): Política de caché < 24h, renovación tras expiración, flujo bifásico EAN->ASIN y actualización de enlaces de afiliado.
5. `SqliteGameRepositoryTests` (1 prueba): Persistencia y lectura de la columna `Asin` en base de datos SQLite real.
