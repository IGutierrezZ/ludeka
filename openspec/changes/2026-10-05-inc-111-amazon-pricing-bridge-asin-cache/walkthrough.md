# Walkthrough Técnico INC-111: Arquitectura de Precios de Amazon

## 1. Configuración del Proveedor en `appsettings.json`

En `src/Ludeka.Web/appsettings.json`, dispones de la sección `"Amazon"`:

```json
"Amazon": {
  "Provider": "None", // Cambiar a "Bridge" o "Official"
  "AssociateTag": "ludeka-21",
  "DomainMatch": "amazon.es",
  "Bridge": {
    "ApiKey": "", // Introducir API Key de Rainforest API
    "BaseUrl": "https://api.rainforestapi.com",
    "AmazonDomain": "amazon.es"
  },
  "PaApi": {
    "AccessKey": "", // Introducir AccessKey de Amazon PA-API
    "SecretKey": "", // Introducir SecretKey de Amazon PA-API
    "AssociateTag": "ludeka-21",
    "Region": "eu-west-1",
    "Host": "webservices.amazon.es"
  }
}
```

### Cómo operar hoy (Fase API Puente)
1. Regístrate en [Rainforest API](https://rainforestapi.com) para obtener una API Key gratuita de prueba.
2. En `appsettings.json` (o mediante variables de entorno):
   - Configura `"Provider": "Bridge"`.
   - Configura `"Bridge:ApiKey": "TU_API_KEY"`.
3. Al consultar cualquier juego que tenga código de barras EAN:
   - `AmazonPriceSyncService` invocará `RainforestAmazonProductProvider`.
   - Resolverá el EAN al ASIN de Amazon España (`amazon.es`).
   - Guardará el ASIN en `Game.Asin` en base de datos.
   - Extraerá el precio y disponibilidad de la BuyBox.
   - Guardará el precio en `GamePriceSnapshot` con la marca temporal UTC actual.
   - Si se vuelve a solicitar el precio del mismo juego antes de que pasen 24 horas, se devolverá el dato de BD instantáneamente sin consumir créditos de la API.

### Cómo operar mañana (Fase PA-API Oficial de Amazon)
1. Cuando alcances las 3 compras y Amazon apruebe tu cuenta de afiliados con acceso a PA-API 5.0:
2. Simplemente cambia en `appsettings.json`:
   - `"Provider": "Official"`.
   - Introduce tus credenciales en `"PaApi:AccessKey"` y `"PaApi:SecretKey"`.
3. El sistema comenzará a autenticarse automáticamente con la firma criptográfica AWS SigV4 (HMAC-SHA256) contra `webservices.amazon.es`. Cero cambios en código.

---

## 2. Archivos Modificados e Incorporados

- **Dominio:**
  - [`src/Ludeka.Core/Entities/Game.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Core/Entities/Game.cs): Propiedad `Asin` y método `SetAsin(string?)`.
- **Aplicación:**
  - [`src/Ludeka.Application/Contracts/IAmazonProductProvider.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Application/Contracts/IAmazonProductProvider.cs): Interfaz del proveedor.
  - [`src/Ludeka.Application/Contracts/IAmazonPriceSyncService.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Application/Contracts/IAmazonPriceSyncService.cs): Interfaz del orquestador de sincronización y caché.
  - [`src/Ludeka.Application/DTOs/AmazonProductPriceResult.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Application/DTOs/AmazonProductPriceResult.cs): DTO inmutable.
  - [`src/Ludeka.Application/Options/AmazonOptions.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Application/Options/AmazonOptions.cs): Opciones tipadas de configuración.
  - [`src/Ludeka.Application/Features/Affiliates/AmazonPriceSyncService.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Application/Features/Affiliates/AmazonPriceSyncService.cs): Servicio orquestador y de caché < 24h.
- **Infraestructura:**
  - [`src/Ludeka.Infrastructure/Affiliates/Amazon/RainforestAmazonProductProvider.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Affiliates/Amazon/RainforestAmazonProductProvider.cs): Cliente API puente.
  - [`src/Ludeka.Infrastructure/Affiliates/Amazon/OfficialAmazonPaApiProvider.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Affiliates/Amazon/OfficialAmazonPaApiProvider.cs): Cliente oficial PA-API 5.0 con firma AWS SigV4.
  - [`src/Ludeka.Infrastructure/Affiliates/Amazon/NullAmazonProductProvider.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Affiliates/Amazon/NullAmazonProductProvider.cs): Fallback seguro.
  - [`src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs): Migración DDL SQLite para `Asin`.
  - [`src/Ludeka.Infrastructure/Data/LudekaDbContext.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Data/LudekaDbContext.cs): Mapeo EF Core de `Asin`.
  - [`src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs): Persistencia de `Asin` en `UpdateAsync`.
  - [`src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs): Inyección de dependencias.
- **Web:**
  - [`src/Ludeka.Web/appsettings.json`](file:///F:/repos/ludeka-wt/amazon-pricing/src/Ludeka.Web/appsettings.json): Sección estructurada `Amazon`.
- **Pruebas (26 nuevas pruebas):**
  - [`tests/Ludeka.UnitTests/Domain/GameAsinTests.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/tests/Ludeka.UnitTests/Domain/GameAsinTests.cs)
  - [`tests/Ludeka.UnitTests/Infrastructure/RainforestAmazonProductProviderTests.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/tests/Ludeka.UnitTests/Infrastructure/RainforestAmazonProductProviderTests.cs)
  - [`tests/Ludeka.UnitTests/Infrastructure/OfficialAmazonPaApiProviderTests.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/tests/Ludeka.UnitTests/Infrastructure/OfficialAmazonPaApiProviderTests.cs)
  - [`tests/Ludeka.UnitTests/Application/AmazonPriceSyncServiceTests.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/tests/Ludeka.UnitTests/Application/AmazonPriceSyncServiceTests.cs)
  - [`tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs`](file:///F:/repos/ludeka-wt/amazon-pricing/tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs)
