import fs from "fs";
import path from "path";

const specsRoot = path.resolve("openspec/changes/archive/2026-09-06-change-01-core-catalog/specs");

const translations = {
  "core-catalog/spec.md": `# Especificación: core-catalog

## Propósito
Definir la entidad raíz de agregado \`Game\`, la interfaz de repositorio para persistencia, el motor de búsqueda y filtrado multi-criterio, y el enrutamiento amigable para SEO basado en slug para el catálogo de juegos de mesa de Ludeca.

## Requerimientos

### Requerimiento: Entidad de Agregado de Juego de Mesa (\`Game\`)
El modelo de dominio DEBE representar un juego de mesa como un agregado raíz completo con identificadores únicos, metadatos editoriales, doble calificación y Value Objects encapsulados.

#### Escenario: Instanciar agregado Game válido
- DADO un identificador GUID único, un ID de BGG positivo \`266192\`, título original \`"Wingspan"\`, título en español \`"Wingspan"\` y año de publicación \`2019\`
- CUANDO se instancia la entidad \`Game\` con los parámetros editoriales requeridos
- ENTONCES la entidad DEBE tener sus campos \`Id\`, \`BggId\`, \`Slug\` establecido en \`"wingspan"\`, \`OriginalTitle\` y \`SpanishTitle\` correctamente asignados
- Y las calificaciones por defecto DEBEN inicializarse en \`0.0\` con rankings nulos.

#### Escenario: Normalización automática del slug
- DADO un juego con título \`"Los Castillos de Borgoña"\` o \`"7 Wonders: Duel!"\`
- CUANDO se ejecuta la lógica de generación del slug
- ENTONCES DEBE producir slugs en minúsculas seguros para URL, sin diacríticos, tildes ni caracteres especiales (ej. \`"los-castillos-de-borgona"\` y \`"7-wonders-duel"\`).

#### Escenario: Nombre en español de respaldo (fallback)
- DADO un juego importado de BGG donde no se ha definido un título alternativo oficial en español
- CUANDO se construye o actualiza la entidad \`Game\`
- ENTONCES \`SpanishTitle\` DEBE tomar como respaldo el valor de \`OriginalTitle\`
- Y \`SpanishTitle\` NO DEBE ser nulo ni vacío.

---

### Requerimiento: Contrato de Repositorio del Catálogo (\`IGameRepository\`)
La capa de aplicación DEBE definir una abstracción \`IGameRepository\` que provea métodos asíncronos para búsqueda, paginación, recuperación por slug y operaciones de persistencia.

#### Escenario: Recuperar juego por slug
- DADO un juego existente en el catálogo con slug \`"catan"\`
- CUANDO se invoca \`GetBySlugAsync("catan", cancellationToken)\`
- ENTONCES el repositorio DEBE devolver el agregado \`Game\` completo con todos sus Value Objects asociados cargados.

#### Escenario: Recuperar juego por slug inexistente
- DADO un slug \`"juego-inexistente-xyz"\` que no existe en el catálogo
- CUANDO se invoca \`GetBySlugAsync("juego-inexistente-xyz", cancellationToken)\`
- ENTONCES el repositorio DEBE devolver \`null\` sin lanzar excepciones no controladas.

#### Escenario: Recuperar juego por BGG ID
- DADO un juego existente con ID de BGG \`13\` (*Catán*)
- CUANDO se invoca \`GetByBggIdAsync(13, cancellationToken)\`
- ENTONCES el repositorio DEBE devolver la entidad \`Game\` correspondiente.

---

### Requerimiento: Búsqueda y Filtrado Multi-Criterio del Catálogo
El motor de consultas DEBE soportar búsqueda multi-criterio combinando coincidencias de texto sobre títulos en español y original, rangos de jugadores, estilos lúdicos, modos de confrontación, restricciones de duración y filtros rápidos predefinidos.

#### Escenario: Búsqueda de texto en doble título
- DADO juegos con \`SpanishTitle = "Los Castillos de Borgoña"\` (\`OriginalTitle = "The Castles of Burgundy"\`) y \`SpanishTitle = "Catán"\`
- CUANDO un usuario busca con el término \`"Burgundy"\` o \`"Borgoña"\`
- ENTONCES la consulta DEBE devolver \`"Los Castillos de Borgoña"\` en los resultados independientemente de si buscó en inglés o español.

#### Escenario: Filtrar por ajuste predefinido "Especial Parejas"
- DADO un criterio de filtrado donde \`EspecialParejas\` está marcado como \`true\`
- CUANDO se ejecuta \`SearchAsync(criteria, page, pageSize, cancellationToken)\`
- ENTONCES solo DEBEN devolverse juegos cuyo semáforo de escalabilidad para 2 jugadores sea \`MustPlay\` (Imprescindible 🟢).

#### Escenario: Filtrar por ajuste predefinido "Mesa Familiar"
- DADO un criterio de filtrado donde \`MesaFamiliar\` está marcado como \`true\`
- CUANDO se ejecuta la búsqueda
- ENTONCES solo DEBEN devolverse juegos con \`CommunityAge <= 8\` (o \`IsAccessibleEarlier == true\`) y dependencia del idioma \`None\` o \`Low\`.
`,

  "game-dna-badges/spec.md": `# Especificación: game-dna-badges

## Propósito
Definir los Value Objects de dominio y los badges de UI para Tipo de Confrontación, Estilo Lúdico y Modo Solitario, permitiendo una lectura visual compacta del ADN lúdico en 3 segundos tanto en tarjetas como en la ficha del juego.

## Requerimientos

### Requerimiento: Value Object de Tipo de Confrontación (\`ConfrontationType\`)
El modelo de dominio DEBE clasificar los juegos en paradigmas de confrontación claros para informar sobre la dinámica grupal y la tensión competitiva.

#### Escenario: Evaluar confrontación cooperativa
- DADO un juego que requiere colaboración total del equipo contra el juego (ej. *The Crew*, *Gloomhaven*)
- CUANDO se evalúa su tipo de confrontación
- ENTONCES DEBE representarse como \`Cooperative\`
- Y renderizarse con la etiqueta \`"Cooperativo"\` y el icono \`"🤝"\`.

#### Escenario: Evaluar confrontación competitiva
- DADO un juego donde los jugadores compiten individualmente por ganar (ej. *Catán*, *Wingspan*)
- CUANDO se evalúa su tipo de confrontación
- ENTONCES DEBE representarse como \`Competitive\`
- Y renderizarse con la etiqueta \`"Competitivo"\` y el icono \`"⚔️"\`.

#### Escenario: Evaluar roles ocultos o equipos
- DADO un juego donde los roles están ocultos o se compite por bandos (ej. *Secret Hitler*, *Código Secreto*)
- CUANDO se evalúa su tipo de confrontación
- ENTONCES DEBE representarse como \`HiddenRolesOrTeams\`
- Y renderizarse con la etiqueta \`"Roles Ocultos / Equipos"\` y el icono \`"👥"\`.

#### Escenario: Evaluar semi-cooperativo
- DADO un juego con metas compartidas de supervivencia pero con traidores o ganadores individuales (ej. *Nemesis*)
- CUANDO se evalúa su tipo de confrontación
- ENTONCES DEBE representarse como \`SemiCooperative\`
- Y renderizarse con la etiqueta \`"Semi-cooperativo"\` y el icono \`"🗡️"\`.

---

### Requerimiento: Value Object de Estilo Lúdico (\`GameStyle\`)
El modelo de dominio DEBE clasificar los juegos en arquetipos mecánicos y temáticos reconocibles por la comunidad.

#### Escenario: Clasificar estilo Eurogame
- DADO un juego enfocado en gestión de recursos, colocación de trabajadores y azar mínimo (ej. *Terraforming Mars*, *Agrícola*)
- CUANDO se evalúa su estilo
- ENTONCES DEBE asignarse \`Eurogame\`
- Y renderizarse con la etiqueta \`"Eurogame"\` y el icono \`"⚙️"\`.

#### Escenario: Clasificar estilo Ameritrash / Temático
- DADO un juego centrado en ambientación, combate y tensión narrativa (ej. *Zombicide*, *Las Mansiones de la Locura*)
- CUANDO se evalúa su estilo
- ENTONCES DEBE asignarse \`Ameritrash\`
- Y renderizarse con la etiqueta \`"Ameritrash / Temático"\` y el icono \`"🎲"\`.

#### Escenario: Clasificar estilo Party Game
- DADO un juego ligero pensado para grupos casuales, humor e interacción social (ej. *Dixit*, *Exploding Kittens*)
- CUANDO se evalúa su estilo
- ENTONCES DEBE asignarse \`PartyGame\`
- Y renderizarse con la etiqueta \`"Party Game"\` y el icono \`"🎉"\`.

#### Escenario: Clasificar estilo Filler / Abstracto
- DADO un juego de corta duración (<30 min) o abstracto sin temática densa (ej. *Azul*, *Hive*, *Cascadia*)
- CUANDO se evalúa su estilo
- ENTONCES DEBE asignarse \`FillerAbstract\`
- Y renderizarse con la etiqueta \`"Filler / Abstracto"\` y el icono \`"🧩"\`.

#### Escenario: Clasificar estilo Narrativo / Campaña
- DADO un juego guiado por historia, decisiones ramificadas o legado de partidas (ej. *Arkham Horror LCG*, *Pandemic Legacy*)
- CUANDO se evalúa su estilo
- ENTONCES DEBE asignarse \`NarrativeCampaign\`
- Y renderizarse con la etiqueta \`"Narrativo / Campaña"\` y el icono \`"📖"\`.

---

### Requerimiento: Indicador de Modo Solitario Oficial
El agregado \`Game\` DEBE distinguir de forma explícita si incluye reglas y componentes para juego en solitario oficial.

#### Escenario: Juego con modo solitario oficial
- DADO un juego cuyo rango oficial comienza en 1 jugador o incluye un modo automa oficial
- CUANDO se renderizan los badges de la ficha
- ENTONCES DEBE mostrarse el badge \`"👤 Modo Solitario Oficial"\`
- Y el filtro \`Top Solitario\` DEBE incluir el juego en sus resultados.
`,

  "scalability-traffic-light/spec.md": `# Especificación: scalability-traffic-light

## Propósito
Definir el modelo de semáforo dinámico de escalabilidad (1 a 7+ jugadores), el cálculo de estados por comensal (🟢 Imprescindible | 🟡 Bueno / Recomendado | 🔴 No recomendado) y la determinación textual de la configuración ideal ("Ideal a X jugadores").

## Requerimientos

### Requerimiento: Desglose Dinámico de Escalabilidad
El agregado \`Game\` DEBE contener una colección inmutable de recomendaciones de escalabilidad \`ScalabilityEntry\` para cada número de jugadores soportado (adaptable desde 1 hasta 7+ comensales).

#### Escenario: Estado Imprescindible (🟢)
- DADO un número de comensales donde el consenso de votos sitúa al juego en su mejor nivel (ej. 2 jugadores en *7 Wonders Duel* o 4 jugadores en *Blood Rage*)
- CUANDO se evalúa el estado para ese número de jugadores
- ENTONCES el estado DEBE ser \`MustPlay\` (🟢 Imprescindible / Brilla)
- Y el color visual en la UI DEBE ser verde esmeralda con alto contraste accesible.

#### Escenario: Estado Bueno / Recomendado (🟡)
- DADO un número de comensales donde el juego funciona bien y es disfrutable pero no es su pico máximo
- CUANDO se evalúa el estado para ese número de jugadores
- ENTONCES el estado DEBE ser \`Recommended\` (🟡 Bueno / Recomendado)
- Y el color visual en la UI DEBE ser ámbar dorado.

#### Escenario: Estado No Recomendado (🔴)
- DADO un número de comensales donde el entreturno es excesivo, el tablero queda desierto o el parche es artificial
- CUANDO se evalúa el estado para ese número de jugadores
- ENTONCES el estado DEBE ser \`NotRecommended\` (🔴 No recomendado)
- Y el color visual en la UI DEBE ser rojo suave de aviso.

---

### Requerimiento: Etiqueta "Ideal a X Jugadores"
El agregado \`Game\` DEBE calcular y exponer una propiedad \`IdealPlayerCountText\` que resuma en texto claro la mejor configuración de mesa.

#### Escenario: Único conteo ideal
- DADO un juego donde la configuración óptima indiscutible es a 2 personas
- CUANDO se genera la etiqueta
- ENTONCES el texto DEBE ser \`"Ideal: 2 jugadores"\`.

#### Escenario: Rango de conteos ideales
- DADO un juego que brilla a 3 y 4 jugadores
- CUANDO se genera la etiqueta
- ENTONCES el texto DEBE ser \`"Ideal: 3–4 jugadores"\`.
`,

  "game-accessibility-specs/spec.md": `# Especificación: game-accessibility-specs

## Propósito
Modelar la comparativa entre la edad legal de la caja y la edad real comunitaria infantil, el factor de dependencia del idioma, la estimación de duración por jugador y el espacio físico requerido en mesa (huella).

## Requerimientos

### Requerimiento: Edad de Caja vs. Edad Real Infantil
El modelo DEBE comparar la edad normativa marcada en la caja (\`BoxAge\`) con la edad consensuada por familias (\`CommunityAge\`) y destacar si es accesible antes de tiempo.

#### Escenario: Accesible antes de tiempo
- DADO un juego con \`BoxAge = 14\` (por normativa de piezas pequeñas) pero \`CommunityAge = 8\`
- CUANDO se evalúa la accesibilidad infantil
- ENTONCES \`IsAccessibleEarlier\` DEBE ser \`true\`
- Y la UI DEBE mostrar el distintivo verde: \`"🟢 Accesible antes: la comunidad dice que a partir de 8 años funciona genial"\`.

---

### Requerimiento: Dependencia del Idioma en Componentes
El modelo DEBE tipificar el nivel de lectura necesario para jugar sin barreras.

#### Escenario: Clasificar dependencia nula
- DADO un juego cuyos componentes solo contienen números o simbología visual (ej. *Carcassonne*, *Azul*)
- CUANDO se evalúa la dependencia del idioma
- ENTONCES DEBE ser \`None\` (\`"🟢 Nula (solo iconografía)"\`).

#### Escenario: Clasificar dependencia baja
- DADO un juego con frases cortas de texto público o cartas de referencia (ej. *Catán*)
- CUANDO se evalúa la dependencia del idioma
- ENTONCES DEBE ser \`Low\` (\`"🟡 Baja (frases cortas o texto visible)"\`).

#### Escenario: Clasificar dependencia alta
- DADO un juego con párrafos densos o cartas secretas con texto narrativo complejo (ej. *Arkham Horror LCG*)
- CUANDO se evalúa la dependencia del idioma
- ENTONCES DEBE ser \`High\` (\`"🔴 Alta (requiere lectura fluida)"\`).

---

### Requerimiento: Huella en Mesa (Espacio Requerido)
El modelo DEBE informar del tamaño de mesa necesario para desplegar el juego.

#### Escenario: Clasificar huella en mesa pequeña
- DADO un juego filler o de cartas sin despliegue extenso
- CUANDO se evalúa la huella
- ENTONCES DEBE ser \`SmallTable\` (\`"🪑 Mesa pequeña / Cafetería"\`).

#### Escenario: Clasificar huella estándar
- DADO un eurogame medio con tablero central y tableros personales moderados
- CUANDO se evalúa la huella
- ENTONCES DEBE ser \`StandardTable\` (\`"🍽️ Mesa de comedor estándar"\`).

#### Escenario: Clasificar monstruo de mesa
- DADO un juego masivo con mercados gigantes y tableros individuales extensos
- CUANDO se evalúa la huella
- ENTONCES DEBE ser \`TableMonster\` (\`"🏰 Monstruo de mesa (mesa grande)"\`).
`,

  "sleeve-guide/spec.md": `# Especificación: sleeve-guide

## Propósito
Definir la estructura de datos y componentes para la guía técnica de fundas de cartas (tamaños, recuentos exactos y enlaces de afiliados contextuales para compra).

## Requerimientos

### Requerimiento: Colección de Fundas Requeridas (\`SleeveItem\`)
El agregado \`Game\` DEBE mantener una colección de tipos de fundas necesarias para enfundar el juego completo.

#### Escenario: Juego con cartas de tamaño estándar
- DADO un juego como *Wingspan* con 212 cartas de formato estándar (63.5 x 88 mm)
- CUANDO se consulta la guía de fundas
- ENTONCES DEBE indicar el formato \`"Estándar (63.5 x 88 mm)"\`, cantidad \`212\` cartas
- Y generar enlaces de compra contextuales con código de afiliado (ej. \`"Comprar fundas compatibles en Zacatrus / Amazon"\`).

#### Escenario: Juego sin cartas
- DADO un juego como *Hive* o *Azul* que no contiene cartas
- CUANDO se consulta la guía de fundas
- ENTONCES DEBE indicar de forma limpia \`"Este juego no utiliza cartas"\` sin botones de compra erróneos.
`,

  "bgg-xmlapi-client/spec.md": `# Especificación: bgg-xmlapi-client

## Propósito
Implementar un cliente HTTP resiliente y parser LINQ-to-XML para consumir la API oficial BGG XMLAPI2 (\`https://boardgamegeek.com/xmlapi2/\`), extrayendo nombres alternativos en español, conteos de votos de escalabilidad, edad recomendada y enlaces de carátulas sin almacenamiento en disco propio.

## Requerimientos

### Requerimiento: Consulta Resiliente con Rate Limiting
El cliente \`BggXmlApiClient\` DEBE respetar un límite de llamadas seguro (máximo 2 peticiones por segundo) con política de reintentos exponenciales ante respuestas HTTP 202 (BGG queue) o 429.

#### Escenario: Manejo de respuesta HTTP 202 (En proceso en BGG)
- DADO que BGG responde con código HTTP 202 indicando que los datos se están compilando
- CUANDO el cliente procesa la petición
- ENTONCES DEBE esperar de forma exponencial (1s, 2s, 4s) antes de reintentar hasta un máximo de 3 veces.

---

### Requerimiento: Extracción Quirúrgica de Metadatos
El parser LINQ-to-XML DEBE extraer el nombre comercial en español prioritario si existe, las carátulas remotas oficiales, el rango de edad y la encuesta de jugadores recomendados.

#### Escenario: Parseo de encuesta de comensales recomendados
- DADO el XML devuelto por \`/xmlapi2/thing?id=13&stats=1\`
- CUANDO se parsea el elemento \`<poll name="suggested_numplayers">\`
- ENTONCES el parser DEBE tabular los votos de \`Best\`, \`Recommended\` y \`Not Recommended\` para cada número de jugadores
- Y asignar los estados correspondientes del semáforo.
`,

  "offline-catalog-seeder/spec.md": `# Especificación: offline-catalog-seeder

## Propósito
Proveer un seeder inicial con 25 a 50 juegos de mesa populares en español (ficheros JSON embebidos) y contexto SQLite EF Core 10, garantizando una experiencia de catálogo rica en modo offline sin depender de llamadas de red obligatorias.

## Requerimientos

### Requerimiento: Contexto de Base de Datos SQLite (\`LudecaDbContext\`)
La capa de infraestructura DEBE configurar EF Core 10 con SQLite y mapeo de Value Objects JSON (\`ToJson()\`) para almacenar el catálogo de juegos.

#### Escenario: Inicialización de base de datos y migraciones
- DADO que la aplicación arranca por primera vez
- CUANDO \`LudecaDbContext\` se inicializa
- ENTONCES DEBE crear la base de datos local SQLite \`ludeca.db\` si no existe
- Y aplicar automáticamente el esquema relacional.

---

### Requerimiento: Carga de Semillas JSON del Catálogo
El servicio \`CatalogSeeder\` DEBE poblar la base de datos con los 25 a 50 títulos top en español si la tabla \`Games\` está vacía.

#### Escenario: Carga inicial de catálogo curado
- DADO un fichero JSON local \`seed-games.json\` con juegos curados (*Catán*, *Wingspan*, *Los Castillos de Borgoña*, *Azul*, *7 Wonders Duel*, etc.)
- CUANDO la base de datos no contiene registros
- ENTONCES el seeder DEBE insertar los juegos con sus slugs, badges de ADN lúdico, semáforos de escalabilidad y datos de fundas
- Y dejar el catálogo listo para navegación inmediata.
`
};

for (const [relPath, content] of Object.entries(translations)) {
  const fullPath = path.join(specsRoot, relPath);
  const dir = path.dirname(fullPath);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(fullPath, content.trim(), "utf8");
  console.log(`✓ Traducido a español: ${relPath}`);
}
