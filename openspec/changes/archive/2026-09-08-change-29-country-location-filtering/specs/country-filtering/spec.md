# Especificación Funcional: change-29-country-location-filtering

## Requerimientos y Criterios de Aceptación (Gherkin)

### Requerimiento 1: Catálogo y Normalización de Países
El sistema debe disponer de un catálogo estandarizado de países hispanohablantes principales y la opción especial Internacional, permitiendo resolver nombres, códigos ISO y emojis de banderas.

```gherkin
Escenario: Obtención de información de un país válido
  Dado el catálogo de países de Ludeka
  Cuando se consulta el país "México" o su código "MX"
  Entonces se obtiene el nombre canónico "México", código "MX" y la bandera "🇲🇽"

Escenario: Normalización de nombres de país con acentos y mayúsculas
  Dado el catálogo de países de Ludeka
  Cuando se consulta "espana", "ESPAÑA" o "ES"
  Entonces se normaliza a "España" con bandera "🇪🇸"

Escenario: Soporte para ámbito Internacional
  Dado el catálogo de países de Ludeka
  Cuando se consulta "Internacional" o "Global"
  Entonces se resuelve con código "INT" y bandera "🌎"
```

### Requerimiento 2: Configuración de País en Perfil y Advertencia Explícita
El usuario puede configurar opcionalmente su país de residencia. Si decide seleccionar un país, el sistema debe presentar una advertencia clara indicando que dicha elección filtrará sorteos, eventos y tiendas.

```gherkin
Escenario: Visualización de la advertencia al seleccionar país en el perfil
  Dado un usuario en la pantalla de su perfil o preferencias
  Cuando selecciona un país específico como "Chile" en el selector
  Entonces se muestra una advertencia visual destacada indicando que los sorteos, eventos y tiendas se filtrarán a su territorio
  Y se le informa de que si prefiere ver la información completa sin filtrar no debe elegir ningún país

Escenario: Guardado y persistencia de país en el perfil
  Dado un usuario autenticado que confirma la selección de "Chile"
  Cuando se guardan las preferencias
  Entonces se almacena "Chile" en su registro de preferencias en base de datos
  Y el estado de la sesión activa refleja el país seleccionado
```

### Requerimiento 3: Filtrado Territorial Estricto en Fichas de Compra (Juegos y Fundas)
En la sección "Dónde Comprar" de una ficha de juego (tanto para ofertas del juego base/expansiones como para fundas protectoras), solo deben mostrarse las ofertas de tiendas que tengan sede o envíen al país del usuario.

```gherkin
Escenario: Filtrado de tiendas de compra para usuario con país seleccionado
  Dado un juego con ofertas de tiendas de "España" (Zacatrus) y tiendas de "México" (JugandoAndo)
  Y un usuario con país seleccionado "México"
  Cuando el usuario visualiza la sección "Dónde Comprar"
  Entonces solo se muestran las ofertas de "JugandoAndo" (México)
  Y no se muestran las ofertas de "Zacatrus" (España)

Escenario: Tienda con cobertura internacional o envíos multi-país
  Dado un juego con una oferta de una tienda española que realiza envíos a "México"
  Y un usuario con país seleccionado "México"
  Cuando consulta las ofertas de compra
  Entonces la tienda española con envíos a México sí se muestra en los resultados

Escenario: Estado vacío cuando no existen tiendas para el juego o fundas en el país del usuario
  Dado un juego que únicamente dispone de ofertas de compra en "España"
  Y un usuario con país seleccionado "Argentina"
  Cuando consulta la sección "Dónde Comprar"
  Entonces no se muestra ninguna tienda de España
  Y se visualiza un mensaje indicando claramente que actualmente no hay tiendas colaboradoras con envíos a Argentina para este título o sus fundas
```

### Requerimiento 4: Marcado Visual con Badges en Sorteos, Tiendas y Eventos
Las tarjetas de sorteos, tiendas físicas/online, eventos de agenda y ofertas de compra deben indicar visiblemente el país asociado mediante un badge con bandera y nombre legible.

```gherkin
Escenario: Badge de país en tarjetas de sorteos
  Dado un sorteo registrado con país "Colombia"
  Cuando se visualiza su tarjeta en "/sorteos"
  Entonces luce un badge visible con "🇨🇴 Colombia"

Escenario: Sorteos internacionales visibles para todos los países
  Dado un usuario con país seleccionado "Perú"
  Y un sorteo registrado con ámbito "Internacional"
  Cuando el usuario consulta "/sorteos"
  Entonces el sorteo internacional se muestra en el listado luciendo el badge "🌎 Internacional"
```

### Requerimiento 5: Detección de Ubicación y Priorización Inteligente
El sistema permite detectar la ubicación (mediante navegador / zona horaria) para ordenar los contenidos mostrando en primer lugar los del país detectado.

```gherkin
Escenario: Ordenación prioritaria de eventos sin filtro estricto
  Dado un usuario sin filtro excluyente pero con ubicación detectada en "España"
  Y un listado con eventos celebrados en "Alemania", "España" y "México"
  Cuando se listan los eventos priorizando su país
  Entonces los eventos de "España" se ordenan en las primeras posiciones
  Seguidos por los de otros países
```
