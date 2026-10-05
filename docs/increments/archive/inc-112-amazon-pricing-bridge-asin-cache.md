# Incremento 112: Arquitectura de Proveedores de Precios de Amazon (API Puente / PA-API Oficial), Mapeo EAN-ASIN y Caché de Precios (< 24h)

> **Estado:** ✅ Archivado y Desplegado en Producción  
> **Fecha de Inicio:** 2026-10-05  
> **Rama de Trabajo:** `inc/amazon-pricing`  
> **Worktree:** `F:\repos\ludeka-wt\amazon-pricing`  
> **Tipo:** Feature / Arquitectura e Integración  
> **Pruebas Verificadas:** 2.507 unitarias (100% éxito, 0 fallos, 26 nuevas pruebas añadidas, 2.517 totales con integración)  

---

## 🎯 Objetivo y Alcance

Diseñar e implementar la infraestructura desacoplada para la obtención de precios y stock de juegos en Amazon:
1. **Contrato de Integración (`IAmazonProductProvider`)**: Abstracción limpia para resolver EAN a ASIN y consultar precios/disponibilidad.
2. **Implementación Dual**:
   - Proveedor de API puente (`RainforestAmazonProductProvider`) con API key para uso inmediato previo a la aprobación de Amazon.
   - Proveedor oficial (`OfficialAmazonPaApiProvider`) preparado para la **PA-API 5.0** (*Product Advertising API*) con firma AWS SigV4 (`AccessKey`, `SecretKey`, `AssociateTag`).
   - Proveedor nulo (`NullAmazonProductProvider`) para entornos sin proveedor activo.
3. **Persistencia de ASIN en `Game`**: Incorporar la columna `Asin` en la entidad de dominio y base de datos (SQLite y PostgreSQL) para evitar re-consultas por EAN.
4. **Caché con Cumplimiento de Políticas (< 24h)**: Validación temporal estricta sobre `GamePriceSnapshot` en `AmazonPriceSyncService` para no superar las 24 horas estipuladas por Amazon, minimizando consumo de cuota y evitando peticiones redundantes.

---

## 🏗️ Fases SDD

- [x] **sdd-explore:** Análisis de requisitos, políticas de Amazon y estado actual en `Ludeka.Core` y `Ludeka.Infrastructure`.
- [x] **sdd-propose:** Redacción y aprobación de `proposal.md`.
- [x] **sdd-spec:** Especificación formal de requerimientos y casos de prueba en `spec.md`.
- [x] **sdd-design:** Diseño detallado de interfaces, DTOs, persistencia y configuración en `design.md`.
- [x] **sdd-tasks:** Desglose en 7 unidades de trabajo atómicas bajo Strict TDD en `tasks.md`.
- [x] **sdd-apply:** Implementación guiada por pruebas (Red -> Green -> Refactor).
- [x] **sdd-verify:** Verificación de la suite completa en verde (2.507 superadas, 0 fallos, 2.517 totales con integración).
- [x] **sdd-archive:** Apertura de PR #203, custodia de CI/CD, merge a `main`, despliegue automático verificado en Google Cloud Run, volcado a especificación viva (módulo 51) y cleanup.
