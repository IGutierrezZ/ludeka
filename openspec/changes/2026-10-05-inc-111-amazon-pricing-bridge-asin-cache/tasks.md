# Tareas INC-111: Arquitectura de Proveedores de Precios de Amazon, Mapeo EAN-ASIN y Caché (< 24h)

> **Metodología:** Strict TDD (Red -> Green -> Refactor)  
> **Worktree:** `F:\repos\ludeka-wt\amazon-pricing`  
> **Rama:** `inc/amazon-pricing`  

---

## 📋 Lista de Tareas

- [ ] **Tarea 1: Dominio — Propiedad `Asin` y mutación en `Game`**
  - [ ] Escribir pruebas unitarias en `tests/Ludeka.UnitTests/Domain/GameAsinTests.cs` (normalización de espacios, mayúsculas, truncado seguro a 20 chars, conversión de vacíos a null).
  - [ ] Implementar `Asin` y `SetAsin` en `src/Ludeka.Core/Entities/Game.cs`.
  - [ ] Ejecutar y pasar pruebas de dominio.

- [ ] **Tarea 2: Contratos de Aplicación y DTOs**
  - [ ] Crear DTO `AmazonProductPriceResult` en `src/Ludeka.Application/DTOs/AmazonProductPriceResult.cs`.
  - [ ] Crear interfaz `IAmazonProductProvider` en `src/Ludeka.Application/Interfaces/IAmazonProductProvider.cs`.
  - [ ] Crear interfaz `IAmazonPriceSyncService` en `src/Ludeka.Application/Services/IAmazonPriceSyncService.cs`.

- [ ] **Tarea 3: Persistencia de `Asin` en Repositorios (SQLite y PostgreSQL)**
  - [ ] Escribir prueba de mapeo y persistencia de `Asin` en `SqliteGameRepositoryTests.cs`.
  - [ ] Añadir migración defensiva `PRAGMA table_info` / `ALTER TABLE Games ADD COLUMN Asin TEXT NULL` en `SqliteGameRepository.cs`.
  - [ ] Actualizar lecturas, inserciones y actualizaciones en `SqliteGameRepository.cs` para incluir `Asin`.
  - [ ] Añadir mapeo de `Asin` en `LudekaDbContext.cs`.
  - [ ] Ejecutar y pasar pruebas de persistencia.

- [ ] **Tarea 4: Proveedor Puente (`RainforestAmazonProductProvider`) y Configuración**
  - [ ] Crear opciones de configuración `AmazonOptions`, `AmazonBridgeOptions`, `AmazonPaApiOptions` en `Ludeka.Infrastructure`.
  - [ ] Escribir pruebas unitarias `RainforestAmazonProductProviderTests.cs` simulando respuestas JSON para búsqueda por EAN, consulta por ASIN, y manejo de errores 4xx/5xx/timeout.
  - [ ] Implementar `RainforestAmazonProductProvider` en `src/Ludeka.Infrastructure/Affiliates/Amazon/`.
  - [ ] Ejecutar y pasar pruebas del proveedor puente.

- [ ] **Tarea 5: Proveedor Oficial (`OfficialAmazonPaApiProvider`) y Null Provider**
  - [ ] Escribir pruebas unitarias `OfficialAmazonPaApiProviderTests.cs` verificando validación de credenciales vacías y cálculo de cabeceras de firma AWS SigV4.
  - [ ] Implementar `NullAmazonProductProvider` para entornos sin proveedor configurado.
  - [ ] Implementar `OfficialAmazonPaApiProvider` con firma HMAC-SHA256 en `src/Ludeka.Infrastructure/Affiliates/Amazon/`.
  - [ ] Ejecutar y pasar pruebas del proveedor oficial.

- [ ] **Tarea 6: Servicio de Sincronización y Caché de Precios (< 24h)**
  - [ ] Escribir pruebas unitarias `AmazonPriceSyncServiceTests.cs`:
    - Respeto del TTL < 24h: devuelve snapshot de BD sin llamar a la API externa.
    - Snapshot expirado (> 24h) o inexistente: llama al proveedor y guarda nuevo snapshot.
    - Flujo en dos fases: resuelve ASIN mediante EAN si no tiene ASIN, guarda el juego y pide el precio.
    - Actualización del enlace `GamePurchaseLink` de Amazon con tag `ludeka-21`.
  - [ ] Implementar `AmazonPriceSyncService` en `src/Ludeka.Application/Services/AmazonPriceSyncService.cs`.
  - [ ] Ejecutar y pasar pruebas de sincronización y caché.

- [ ] **Tarea 7: Registro en DI, Configuración y Verificación Final**
  - [ ] Registrar servicios y configuración en `src/Ludeka.Infrastructure/DependencyInjection.cs` o `ServiceCollectionExtensions.cs`.
  - [ ] Actualizar `appsettings.json` con la sección `Amazon` estructurada.
  - [ ] Ejecutar la suite completa de pruebas (`dotnet test`) asegurando 100% verde y cero regresiones.
