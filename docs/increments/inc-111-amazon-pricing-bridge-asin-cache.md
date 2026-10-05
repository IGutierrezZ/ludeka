# Incremento 111: Arquitectura de Proveedores de Precios de Amazon (API Puente / PA-API Oficial), Mapeo EAN-ASIN y Caché de Precios (< 24h)

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-10-05  
> **Rama de Trabajo:** `inc/amazon-pricing`  
> **Worktree:** `F:\repos\ludeka-wt\amazon-pricing`  
> **Tipo:** Feature / Arquitectura e Integración  

---

## 🎯 Objetivo y Alcance

Diseñar e implementar la infraestructura desacoplada para la obtención de precios y stock de juegos en Amazon:
1. **Contrato de Integración (`IAmazonProductProvider`)**: Abstracción limpia para resolver EAN a ASIN y consultar precios/disponibilidad.
2. **Implementación Dual**:
   - Proveedor de API puente (ej. Rainforest API / HTTP) con API key para uso inmediato previo a la aprobación de Amazon.
   - Proveedor oficial preparado para la **PA-API 5.0** (*Product Advertising API*) con firma AWS v4 (`AccessKey`, `SecretKey`, `AssociateTag`).
3. **Persistencia de ASIN en `Game`**: Incorporar la columna `Asin` en la entidad de dominio y base de datos (SQLite y PostgreSQL) para evitar re-consultas por EAN.
4. **Caché con Cumplimiento de Políticas (< 24h)**: Validación temporal estricta sobre `GamePriceSnapshot` para no superar las 24 horas estipuladas por Amazon, minimizando consumo de cuota y evitando peticiones redundantes.

---

## 🏗️ Fases SDD

- [x] **sdd-explore:** Análisis de requisitos, políticas de Amazon y estado actual en `Ludeka.Core` y `Ludeka.Infrastructure`.
- [x] **sdd-propose:** Redacción y aprobación de `proposal.md`.
- [ ] **sdd-spec:** Especificación formal de requerimientos y casos de prueba.
- [ ] **sdd-design:** Diseño detallado de interfaces, DTOs, persistencia y configuración.
- [ ] **sdd-tasks:** Desglose en unidades de trabajo atómicas bajo Strict TDD.
- [ ] **sdd-apply:** Implementación guiada por pruebas.
- [ ] **sdd-verify:** Verificación de la suite completa en verde.
- [ ] **sdd-archive:** Apertura de PR, custodia de CI/CD, merge a `main` y volcado a especificación viva.
