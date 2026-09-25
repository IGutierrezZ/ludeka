# 19. Especificación de Fundas (Sleeves) por Juego y Enlaces de Compra Contextuales

> **Estado del Módulo:** ✅ Implementado y Verificado  
> **Incrementos Asociados:** [INC-26 (inc-26-card-sleeves-spec-stores.md)](file:///c:/repos/Ludeka/docs/increments/archive/inc-26-card-sleeves-spec-stores.md) · [INC-66 (inc-66-fundas-cartas.md)](file:///c:/repos/Ludeka/docs/increments/archive/inc-66-fundas-cartas.md)  
> **Pruebas Automatizadas:** 58 pruebas dedicadas (33 dominio + 13 parser + 7 aplicación + 5 web; 1.817 en total en la suite)  

---

## 1. Propósito y Filosofía

El módulo **"Protege tu juego: Guía Editorial de Fundas"** resuelve de forma didáctica, precisa, honesta y no invasiva una de las preguntas esenciales de todo aficionado al adquirir un nuevo juego de mesa:  
*¿Qué medidas exactas tienen sus cartas, cuántos paquetes de fundas necesito comprar y dónde puedo adquirirlas directamente sin búsquedas tediosas?*

El sistema proporciona:
1. **Dimensiones milimétricas exactas e invariantes de dominio:** Validación física rigurosa (`[30.0 mm, 250.0 mm]`) y recuento no negativo de cartas.
2. **Cálculo automático de paquetes:** Presentaciones universales de 50 y 100 fundas, con manejo pedagógico para recuentos no especificados (evitando inventar compras).
3. **Silueta gráfica proporcional:** Representación SVG/CSS con esquinas redondeadas y relación de aspecto proporcional de la carta.
4. **Píldora didáctica de micras:** Comparativa explicativa (Standard 50-60 µm para insertos originales vs. Premium 100 µm para máxima durabilidad).
5. **Enlaces de compra contextuales y quirúrgicos:** Integración de tiendas colaboradoras (Zacatrus, Dungeon Marvels, Cuarto de Juegos, Tablerum y Amazon) con inyección centralizada de parámetros de afiliado (`ref`, `partner`, `tag`) y filtrado territorial por país (`CountryCatalog` / `IUserLocationService` de INC-29).
6. **Ingesta robusta BGG XMLAPI2:** Extracción de enlaces `boardgamecardsleeve` sin inferencia inventada de 50 cartas, con soporte de conversión de unidades/pulgadas a milímetros.
7. **Presentación honesta y sin falsos positivos en UI:** Distinción explícita en `SleeveGuideCard.razor` entre juegos sin cartas comprobados (`HasNoCards = true`) y juegos sin datos registrados de fundas (`HasNoCards = false`), ofreciendo llamada a la acción hacia el editor de fichas.
8. **Edición y moderación editorial:** Pestaña interactiva en `GameEditorModal.razor` con presets estándar y trazabilidad en la bitácora universal de auditoría (`AuditLog`).

---

## 2. Componentes de Dominio (`Ludeka.Core`)

### 2.1 Value Object `SleeveItem`
Ubicación: [`src/Ludeka.Core/ValueObjects/SleeveItem.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/SleeveItem.cs)

Record inmutable con invariantes reforzadas en constructor compacto (INC-66):
- `FormatName`: No puede ser nulo ni estar vacío (`ArgumentException`).
- `WidthMm` y `HeightMm`: Rango de dimensiones físicas plausibles `[30.0 mm, 250.0 mm]` (`ArgumentOutOfRangeException`).
- `CardCount`: Debe ser `>= 0` (`ArgumentOutOfRangeException`).
- `CalculatePacksNeeded(int packSize = 50)`: Devuelve 0 si `CardCount <= 0`.
- `DimensionText`: Cadena formateada `{WidthMm} x {HeightMm} mm`.
- `ShipsTo(string? targetCountry)`: Evaluación de cobertura territorial según país de la tienda o países de envío.

### 2.2 Catálogo Maestro `StandardSleeveCatalog`
Ubicación: [`src/Ludeka.Core/ValueObjects/StandardSleeveCatalog.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/StandardSleeveCatalog.cs)

Define los 10 formatos canónicos del mercado y normaliza orientaciones:
- **Mini USA:** 41 x 63 mm (tolerancia 1.0 mm)
- **Mini Euro:** 44 x 68 mm (tolerancia 1.0 mm)
- **Estándar USA:** 56 x 87 mm (tolerancia 1.5 mm)
- **Chimera / USA:** 57 x 89 mm (tolerancia 1.5 mm)
- **Euro Standard:** 59 x 92 mm (tolerancia 1.5 mm)
- **Standard Card Game (CCG/LCG):** 63.5 x 88 mm (tolerancia 1.5 mm)
- **Tarot / 7 Wonders:** 65 x 100 mm (tolerancia 2.0 mm)
- **Tarot Grande:** 70 x 120 mm (tolerancia 2.0 mm)
- **Cuadrada:** 70 x 70 mm (tolerancia 2.0 mm)
- **Magnum / Dixit:** 80 x 120 mm (tolerancia 2.0 mm)

**Mejoras de INC-66:**
- `StandardSleeveFormat.Matches(width, height)`: Normaliza evaluando `Math.Min(w, h)` frente a `Math.Min(WidthMm, HeightMm)` y `Math.Max(w, h)` frente a `Math.Max(WidthMm, HeightMm)`, soportando cartas especificadas en apaisado sin falsos negativos.
- `StandardSleeveCatalog.FindByName(name)`: Soporte de alias comunes del mercado (`Mini European` ➔ `Mini Euro`, `Standard European` ➔ `Euro Standard`, `Chimera Standard` ➔ `Chimera / USA`).

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contrato e Implementación `ISleeveStoreUrlResolver`
Ubicaciones:
- [`src/Ludeka.Application/Contracts/ISleeveStoreUrlResolver.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/ISleeveStoreUrlResolver.cs)
- [`src/Ludeka.Application/Features/Sleeves/SleeveStoreUrlResolver.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Sleeves/SleeveStoreUrlResolver.cs)

Resuelve las opciones comerciales y enlaces directos quirúrgicos por medidas (`{width}x{height}`):
- **Zacatrus:** `https://zacatrus.es/catalogsearch/result/?q=fundas+{w}x{h}` (`&ref=ludeka`)
- **Dungeon Marvels:** `https://dungeonmarvels.com/buscar?controller=search&s=fundas+{w}x{h}` (`&ref=ludeka`)
- **Cuarto de Juegos:** `https://cuartodejuegos.es/buscar?q=fundas+{w}x{h}` (`&ref=ludeka`)
- **Tablerum:** `https://tablerum.es/buscar?q=fundas+{w}x{h}` (`&partner=ludeka`)
- **Amazon:** `https://www.amazon.es/s?k=fundas+{w}x{h}` (`&tag=ludeka-21`)

Inyecta centralizadamente las reglas de `IAffiliateUrlResolver` (`AffiliateOptions`) y mantiene un fallback seguro con los nombres de parámetro exactos (`ref`, `partner`, `tag`).

---

## 4. Ingesta e Infraestructura (`Ludeka.Infrastructure`)

### 4.1 Parser de Fundas BGG (`BggSleeveParser`)
Ubicación: [`src/Ludeka.Infrastructure/Bgg/BggSleeveParser.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/BggSleeveParser.cs)

- Extrae enlaces `<link type="boardgamecardsleeve" ...>` desde XML de BGG.
- **Sin inferencia inventada (INC-66):** Si no hay atributo `qty` ni patrón textual de cartas, asigna `CardCount = 0` (desconocido) en lugar de inventar 50 cartas.
- **Conversión de pulgadas:** Detecta comillas dobles `"` o el término `inches` en valores menores a 15 y los convierte a milímetros (`* 25.4`).
- **Validación dimensional:** Descarta automáticamente y retorna `null` ante medidas que violen el rango `[30.0, 250.0]` mm.

---

## 5. Componentes de Interfaz de Usuario Blazor (`Ludeka.Web`)

### 5.1 Componente `SleeveGuideCard.razor`
Ubicación: [`src/Ludeka.Web/Components/Shared/SleeveGuideCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/SleeveGuideCard.razor)

- **Parámetros:**
  - `Sleeves`: Lista de especificaciones `SleeveItem`.
  - `HasNoCards`: Indicador booleano explícito cuando el juego comprobado no contiene cartas (ej. losetas).
  - `OnOpenEditor`: `EventCallback` para invocar el modal editorial desde el estado sin datos.
- **Gestión pedagógica de recuento:** Si `CardCount == 0`, muestra "Recuento no especificado (consulta los componentes de tu edición)".
- **Estado honesto sin datos:** Si no hay fundas registradas y `HasNoCards == false`, muestra banner informativo neutro con acceso directo al botón "Añadir fundas" en lugar de afirmar falsamente que el juego no tiene cartas.
