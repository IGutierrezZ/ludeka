# Tareas INC-111: Arquitectura de Proveedores de Precios de Amazon, Mapeo EAN-ASIN y Caché (< 24h)

> **Metodología:** Strict TDD (Red -> Green -> Refactor)  
> **Worktree:** `F:\repos\ludeka-wt\amazon-pricing`  
> **Rama:** `inc/amazon-pricing`  

---

## 📋 Lista de Tareas

- [x] **Tarea 1: Dominio — Propiedad `Asin` y mutación en `Game`**
  - [x] Escribir pruebas unitarias en `tests/Ludeka.UnitTests/Domain/GameAsinTests.cs` (normalización de espacios, mayúsculas, truncado seguro a 20 chars, conversión de vacíos a null).
  - [x] Implementar `Asin` y `SetAsin` en `src/Ludeka.Core/Entities/Game.cs`.
  - [x] Ejecutar y pasar pruebas de dominio (8 superadas).

- [x] **Tarea 2: Contratos de Aplicación y DTOs**
  - [x] Crear DTO `AmazonProductPriceResult` en `src/Ludeka.Application/DTOs/AmazonProductPriceResult.cs`.
  - [x] Crear interfaz `IAmazonProductProvider` en `src/Ludeka.Application/Contracts/IAmazonProductProvider.cs`.
  - [x] Crear interfaz `IAmazonPriceSyncService` en `src/Ludeka.Application/Contracts/IAmazonPriceSyncService.cs`.

- [x] **Tarea 3: Persistencia de `Asin` en Repositorios (SQLite y PostgreSQL)**
  - [x] Escribir prueba de mapeo y persistencia de `Asin` en `SqliteGameRepositoryTests.cs`.
  - [x] Añadir migración defensiva `PRAGMA table_info` / `ALTER TABLE Games ADD COLUMN Asin TEXT NULL` e índice `IX_Games_Asin` en `SqliteSchemaMigrator.cs`.
  - [x] Actualizar `UpdateAsync` en `SqliteGameRepository.cs` para incluir `Asin`.
  - [x] Añadir mapeo de `Asin` e índice en `LudekaDbContext.cs`.
  - [x] Ejecutar y pasar pruebas de persistencia.

- [x] **Tarea 4: Proveedor Puente (`RainforestAmazonProductProvider`) y Configuración**
  - [x] Crear opciones de configuración `AmazonOptions`, `AmazonBridgeOptions`, `AmazonPaApiOptions` en `Ludeka.Application.Options`.
  - [x] Escribir pruebas unitarias `RainforestAmazonProductProviderTests.cs` simulando respuestas JSON para búsqueda por EAN, consulta por ASIN, y manejo de errores 4xx/5xx/timeout.
  - [x] Implementar `RainforestAmazonProductProvider` en `src/Ludeka.Infrastructure/Affiliates/Amazon/`.
  - [x] Ejecutar y pasar pruebas del proveedor puente (6 superadas).

- [x] **Tarea 5: Proveedor Oficial (`OfficialAmazonPaApiProvider`) y Null Provider**
  - [x] Escribir pruebas unitarias `OfficialAmazonPaApiProviderTests.cs` verificando validación de credenciales vacías y cálculo de cabeceras de firma AWS SigV4.
  - [x] Implementar `NullAmazonProductProvider` para entornos sin proveedor configurado.
  - [x] Implementar `OfficialAmazonPaApiProvider` con firma HMAC-SHA256 en `src/Ludeka.Infrastructure/Affiliates/Amazon/`.
  - [x] Ejecutar y pasar pruebas del proveedor oficial (6 superadas).

- [x] **Tarea 6: Servicio de Sincronización y Caché de Precios (< 24h)**
  - [x] Escribir pruebas unitarias `AmazonPriceSyncServiceTests.cs`:
    - Respeto del TTL < 24h: devuelve snapshot de BD sin llamar a la API externa.
    - Snapshot expirado (> 24h) o inexistente: llama al proveedor y guarda nuevo snapshot.
    - Flujo en dos fases: resuelve ASIN mediante EAN si no tiene ASIN, guarda el juego y pide el precio.
    - Actualización del enlace `GamePurchaseLink` de Amazon con tag `ludeka-21`.
  - [x] Implementar `AmazonPriceSyncService` en `src/Ludeka.Application/Features/Affiliates/AmazonPriceSyncService.cs`.
  - [x] Ejecutar y pasar pruebas de sincronización y caché (5 superadas).

- [x] **Tarea 7: Registro en DI, Configuración y Verificación Final**
  - [x] Registrar servicios y configuración en `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs`.
  - [x] Actualizar `appsettings.json` con la sección `Amazon` estructurada.
  - [x] Ejecutar la suite completa de pruebas (`dotnet test`) asegurando 100% verde y cero regresiones (2.477 superadas).
