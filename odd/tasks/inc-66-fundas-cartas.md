# Documento Vivo ODD — INC-66: Fundas de Cartas — Calidad de Datos y Enlaces de Compra

> **Feature:** `fundas-cartas`  
> **Fichero:** `odd/tasks/inc-66-fundas-cartas.md` (fuente de verdad operativa)  
> **Incremento:** INC-66  
> **Rama:** `inc/fundas-cartas`  
> **Worktree:** `C:\repos\ludeka-wt\fundas-cartas`  
> **Creado:** 2026-09-25 · **Ruta:** rama `inc/fundas-cartas` → PR a `main`  
> **TDD Mode:** Strict TDD (RED ➔ GREEN ➔ REFACTOR)  
> **Línea Base:** 1.781 pruebas unitarias en verde (0 fallos)  

---

## 1. Objetivo

Acreditar y garantizar la calidad de los datos de fundas de cartas (*sleeves*) y sus enlaces de compra en Ludeka:
1. Validar en el dominio (`SleeveItem`) que las dimensiones sean plausibles y no se admitan datos aberrantes o negativos.
2. Eliminar la inferencia silenciosa de cantidades en el parser de BGG (`BggSleeveParser`), erradicando la invención de 50 cartas por defecto.
3. Incorporar Amazon como tienda de fundas con su tag de afiliado oficial (`ludeka-21`) y verificar las URLs de salida de todas las tiendas colaboradoras (Zacatrus, Dungeon Marvels, Cuarto de Juegos, Tablerum, Amazon).
4. Erradicar en `SleeveGuideCard.razor` el falso positivo que afirma "no contiene cartas" ante la simple ausencia de datos de fundas, mostrando un hueco visible y honesto ("Sin datos registrados de fundas") con invitación a editar.
5. Corregir las anomalías detectadas en los datos semilla (`seed-games.json`), en particular Catán (actualmente sin fundas registradas) y Dixit.

---

## 2. Problema y Diagnóstico Previo

1. **Ausencia de invariantes en el dominio (`SleeveItem`):**
   El record `SleeveItem` carece de validación en constructor; admite anchos/altos `<= 0`, números negativos o dimensiones absurdas sin lanzar excepción.
2. **Inferencia silenciosa de cantidad en `BggSleeveParser`:**
   Si el XML de BGG omite la cantidad de cartas, el parser asigna arbitrariamente `count = 50`, fabricando información falsa para el usuario y el radar de fundas.
3. **Hueco de Amazon y tiendas en `SleeveStoreUrlResolver`:**
   Amazon está configurado en `AffiliateOptions` (INC-56) pero no está implementado en `SleeveStoreUrlResolver`. Además, Cuarto de Juegos y Tablerum estaban omitidos de `partnerStores` en `ResolvePurchaseOptions`.
4. **Falsos positivos en UI (`SleeveGuideCard.razor`):**
   Cuando `Sleeves` está vacío, la tarjeta afirma categóricamente que el juego no contiene cartas, ocultando el hueco de información.
5. **Datos incompletos en seed:**
   Catán figura con `sleeves: []` a pesar de contener 120 cartas Mini Euro, y Dixit presenta dimensiones desalineadas con el formato Magnum.

---

## 3. Alcance

### Dentro de Alcance:
- **ODD-1 — Dominio (`Ludeka.Core`): Invariantes y Normalización de Formatos**
  - Constructor con validación en `SleeveItem`: ancho y alto en rango plausible `[30.0 mm, 250.0 mm]`, `CardCount >= 0`, `FormatName` no vacío.
  - Normalización de orientación en `StandardSleeveCatalog.Matches` y `SleeveItem` (`min(w,h) x max(w,h)`).
  - Pruebas unitarias en `CardSleeveDomainTests.cs` bajo Strict TDD.
- **ODD-2 — Ingesta e Infraestructura (`Ludeka.Infrastructure`): Parser BGG y Seed Data**
  - Corrección de `BggSleeveParser`: rechazo de medidas aberrantes/pulgadas sin convertir y eliminación de la invención de `count = 50`.
  - Corrección de `seed-games.json`: añadir fundas a Catán y ajustar formato Dixit.
  - Pruebas unitarias en `BggSleeveParserTests.cs`.
- **ODD-3 — Aplicación (`Ludeka.Application`): Tiendas, Resolver y Afiliación**
  - Soporte de Amazon en `SleeveStoreUrlResolver` con `tag=ludeka-21` e integración con `IAffiliateUrlResolver`.
  - Incorporar Cuarto de Juegos y Tablerum a las opciones de compra de España en `ResolvePurchaseOptions`.
  - Pruebas unitarias en `SleeveStoreUrlResolverTests.cs`.
- **ODD-4 — Presentación (`Ludeka.Web`): Hueco Visible y Honesto en UI**
  - Refactorizar `SleeveGuideCard.razor` para distinguir entre juegos sin cartas comprobados y juegos sin datos registrados ("Sin datos registrados de fundas" con llamada a la acción hacia el editor).
  - Pruebas unitarias de interfaz y contrato web.
- **ODD-5 — Verificación Final, Roadmap y Sincronización Documental**
  - Suite 100% verde (`dotnet test`).
  - Actualización de `ROADMAP.md`, `ROADMAP_MVP_SLICES.md`.
  - Volcado a especificación viva `docs/specs/sistema/19-especificacion-fundas-y-enlaces-tiendas.md` e índice `README.md`.
  - Traslado a `docs/increments/archive/inc-66-fundas-cartas.md`.

---

## 4. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Invariantes de Dominio y Normalización (`Ludeka.Core`)**
  - [x] 1.1 Tests en `CardSleeveDomainTests.cs` (RED): rechazo de medidas `<= 0`, medidas desproporcionadas (`> 250 mm`), `CardCount < 0` y normalización de orientación.
  - [x] 1.2 Validación en constructor de `SleeveItem` lanzando `ArgumentOutOfRangeException` / `ArgumentException`.
  - [x] 1.3 Normalización de dimensiones y coincidencia apaisada en `StandardSleeveCatalog.Matches`.
  - [x] 1.4 Verificación en verde (GREEN) y refactorización limpia (REFACTOR). Commit: `5c00c49` (1.803 pruebas unitarias verdes).
- [x] **ODD-2 — Parser BGG y Calidad de Semilla (`Ludeka.Infrastructure`)**
  - [x] 2.1 Tests en `BggSleeveParserTests.cs` (RED): sin cantidad inventada (`CardCount` nulo/cero o rechazo si no hay datos), rechazo de pulgadas/medidas fuera de rango.
  - [x] 2.2 Eliminar `count = 50` por defecto en `BggSleeveParser.cs` y validar plausibilidad de dimensiones.
  - [x] 2.3 Actualizar `seed-games.json`: añadir fundas de Catán (Mini Euro 44x68 mm) y corregir dimensiones de Dixit.
  - [x] 2.4 Verificación en verde (GREEN) y refactorización (REFACTOR). Commit: `344a78c` (1.810 pruebas unitarias verdes).
- [x] **ODD-3 — Tiendas, Amazon y Afiliación en Aplicación (`Ludeka.Application`)**
  - [x] 3.1 Tests en `SleeveStoreUrlResolverTests.cs` (RED): resolución de Amazon con `tag=ludeka-21`, presencia de Cuarto de Juegos y Tablerum con sus parámetros correctos (`partner` / `ref`).
  - [x] 3.2 Implementar soporte de Amazon en `SleeveStoreUrlResolver.cs`.
  - [x] 3.3 Incluir Cuarto de Juegos y Tablerum en `partnerStores` de `ResolvePurchaseOptions`.
  - [x] 3.4 Verificación en verde (GREEN) y refactorización (REFACTOR). Commit: `1edeeb2` (1.812 pruebas unitarias verdes).
- [x] **ODD-4 — UI Editorial y Estado Honesto sin Datos (`Ludeka.Web`)**
  - [x] 4.1 Tests en `Ludeka.UnitTests/Web/SleeveGuideCardTests.cs` (RED): verificar que un juego sin fundas registradas no renderiza el mensaje engañoso de "no contiene cartas" y manejo de recuento no especificado.
  - [x] 4.2 Actualizar `SleeveGuideCard.razor`: renderizar estado honesto ("Sin especificación registrada de fundas") con botón de acción al editor editorial y soporte para `HasNoCards`.
  - [x] 4.3 Verificación en verde (GREEN) y refactorización (REFACTOR). Commit de unidad de trabajo (1.817 pruebas unitarias verdes).
- [ ] **ODD-5 — Verificación Global, Documentación y Cierre**
  - [ ] 5.1 Ejecución completa de suite de pruebas unitarias (`dotnet test` 100% verde).
  - [ ] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` (INC-66 pasa a completado/archivado).
  - [ ] 5.3 Volcado a la especificación viva `docs/specs/sistema/19-especificacion-fundas-y-enlaces-tiendas.md` y actualización del índice `docs/specs/sistema/README.md`.
  - [ ] 5.4 Mover `docs/increments/inc-66-fundas-cartas.md` a `docs/increments/archive/inc-66-fundas-cartas.md`.
  - [ ] 5.5 Commit de cierre y resumen en Engram.
