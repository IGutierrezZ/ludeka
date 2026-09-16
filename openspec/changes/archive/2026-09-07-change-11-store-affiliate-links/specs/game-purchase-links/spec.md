# Especificación: game-purchase-links

## Propósito
Definir la estructura de datos, reglas de validación, persistencia y visualización para los enlaces de compra de tiendas para juegos de mesa y expansiones, soportando enlaces de afiliados, precios orientativos y disponibilidad.

---

## Requerimientos

### Requerimiento: Value Object `GamePurchaseLink`
El sistema DEBE modelar de forma inmutable cada enlace u oferta de compra de una tienda especializada.

#### Escenario: Creación válida de un enlace de tienda
- DADO un nombre de tienda `"Zacatrus"`, una URL `"https://zacatrus.es/wingspan.html?ref=ludeka"`, un precio opcional de `49.95m`, estado de stock `true` y distintivo `"Envío 24h"`
- CUANDO se instancia un `GamePurchaseLink`
- ENTONCES los atributos quedan asignados correctamente y la URL es accesible.

#### Escenario: Validación de campos obligatorios
- DADO un intento de crear un `GamePurchaseLink` con nombre de tienda vacío o URL vacía/inválida
- CUANDO se instancia el objeto
- ENTONCES DEBE lanzar una excepción de validación descriptiva (`ArgumentException`).

---

### Requerimiento: Colección de Ofertas en la Entidad `Game`
El agregado raíz `Game` DEBE contener una colección de enlaces de compra (`PurchaseLinks`) tanto para juegos base como para expansiones.

#### Escenario: Juego con múltiples ofertas de tiendas asociadas
- DADO un juego con ofertas en Zacatrus, Amazon y Cuarto de Juegos
- CUANDO se consulta el juego
- ENTONCES `PurchaseLinks` retorna la lista ordenada de tiendas con sus enlaces de afiliados.

#### Escenario: Juego sin ofertas configuradas
- DADO un juego descatalogado o de importación sin ofertas registradas
- CUANDO se consulta el juego
- ENTONCES `PurchaseLinks` retorna una lista vacía sin provocar errores nulos.

---

### Requerimiento: Persistencia JSON en EF Core y Reconciliación SQLite
El mapeo relacional DEBE persistir la colección `PurchaseLinks` como colección propia JSON (`OwnsMany(..., b => b.ToJson())`).

#### Escenario: Persistencia y carga en base de datos SQLite
- DADO un juego con enlaces de compra guardado en `LudekaDbContext`
- CUANDO se recupera desde la base de datos
- ENTONCES todos los elementos de `PurchaseLinks` son deserializados fielmente con sus propiedades.

#### Escenario: Reconciliación con base de datos existente
- DADO un entorno SQLite donde la tabla `Games` existía sin la columna `PurchaseLinks`
- CUANDO arranca la aplicación y se ejecuta `SqliteSchemaMigrator`
- ENTONCES añade la columna `PurchaseLinks` como `TEXT NOT NULL DEFAULT '[]'` sin pérdida de datos.

---

### Requerimiento: Presentación UI en `GameDetail.razor`
La ficha del juego DEBE renderizar el componente `StoreOffersCard.razor` con estética sobria y enlaces seguros `rel="noopener noreferrer sponsored"`.

#### Escenario: Visualización de ofertas disponibles
- DADO un juego con enlaces de compra
- CUANDO el usuario navega a la ficha del juego
- ENTONCES se muestra la tarjeta de tiendas con nombre, distintivo, precio formateado, enlace directo y aviso ético de transparencia.

#### Escenario: Estado sin ofertas
- DADO un juego con `PurchaseLinks` vacío
- CUANDO el usuario navega a la ficha del juego
- ENTONCES se muestra un mensaje informativo sutil indicando que no hay enlaces de compra directos disponibles en este momento.
