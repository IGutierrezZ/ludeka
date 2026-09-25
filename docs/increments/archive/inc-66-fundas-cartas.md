# INC-66: Fundas de Cartas — Calidad de Datos y Enlaces de Compra

> **Estado:** ✅ Archivado (entregado el 2026-09-25)  
> **Fecha de Inicio:** 2026-09-25 · **Fecha de Cierre:** 2026-09-25  
> **Rama de Trabajo:** `inc/fundas-cartas`  
> **Worktree:** `C:\repos\ludeka-wt\fundas-cartas`  
> **Pruebas Automatizadas:** 58 pruebas dedicadas de fundas (1.817 pruebas unitarias en verde en total)  
> **Dependencias:** INC-26 (Especificación de Fundas, archivado), INC-56 (afiliación/transparencia)  
> **Especificación Viva:** [19. Especificación de Fundas (Sleeves) y Enlaces de Compra](file:///c:/repos/Ludeka/docs/specs/sistema/19-especificacion-fundas-y-enlaces-tiendas.md)  

---

## 1. Cómo se descubrió

Al cerrar la Fase E (compra y comunidad) se revisó la funcionalidad de fundas heredada de INC-26: el motor existe y resuelve tiendas, pero la calidad del dato que lo alimenta nunca se acreditó.

## 2. El agujero, verificado

Lo que SÍ existe (evidencia de código):

- `SleeveItem` y `StandardSleeveCatalog` (`src/Ludeka.Core/ValueObjects/`).
- `BggSleeveParser`: parsea links `boardgamecardsleeve` que BGG publica por juego.
- `SleeveStoreUrlResolver` / `ISleeveStoreUrlResolver` (`src/Ludeka.Application/Features/Sleeves/`): resuelve a Zacatrus, Dungeon Marvels, Cuarto de Juegos y Tablerum, con `?ref=ludeka` / `partner=ludeka-tab`.
- `SleeveGuideCard.razor` (UI de guía de fundas) y `SleevesRadar` dentro de `UserLibraryStatsService`.

Huecos verificados (hueco de evidencia):

- **No está acreditada la calidad/cobertura del dato BGG** de `boardgamecardsleeve`: se desconoce qué % de los ~8.000 títulos tiene medida de funda fiable.
- **No están verificados los enlaces de Amazon** para fundas (a diferencia de tiendas ES con parámetro de afiliado ya resuelto).
- No hay test documentado de `SleeveStoreUrlResolver` por tienda con URLs de salida reales.
- Riesgo de silueta medida mal parseada (mm vs pulgadas) sin validación de dominio conocida.

## 3. Lo que pide el maintainer

Que la guía de fundas sea de fiar: decir la medida justa de cada juego, con enlaces de compra que funcionen y con la misma transparencia de afiliados que el resto del producto.

## 4. Alcance y decisiones que hay que tomar

1. Auditoría de cobertura y exactitud del dato `boardgamecardsleeve` sobre el catálogo ingestado (informe con %).
2. Validación de dominio en `SleeveItem` (medidas plausibles, unidades, tolerancias de silueta).
3. Tests de `SleeveStoreUrlResolver` por tienda (URL de salida exacta, con parámetro de afiliado).
4. **Decisión abierta:** alta de Amazon como tienda de fundas con tag de afiliado (pendiente de lo decidido en INC-56) y política cuando no hay medida de funda (¿ocultar la guía vs «sin dato» explícito?).

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Base de datos propia de medidas de fundas independiente de BGG, compra asistida y stock en tiempo real de fundas.

## 6. Criterios de aceptación

1. Informe de cobertura del dato de fundas sobre el catálogo (con % y muestra de errores).
2. Medidas inválidas o implausibles rechazadas por el dominio (test).
3. Cada tienda del resolver tiene test con su URL de salida y parámetro de afiliado.
4. Cuando no hay dato fiable, la UI no inventa medida (hueco visible, no fabricado).
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- **Fabricar medidas** por inferencia silenciosa (el defecto sistémico que INC-51 ya detectó en otros módulos): preferir «sin dato».
- Enlaces rotos o sin afiliado si las tiendas cambian su esquema de URLs.
- Colisión de alcance con INC-56 (tag de afiliado): ejecutar o alinear decisiones para no duplicar configuración.
