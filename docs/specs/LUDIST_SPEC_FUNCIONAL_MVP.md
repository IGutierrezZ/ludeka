# 🎲 PROYECTO: LUDIST / LUDEKA — ESPECIFICACIÓN FUNCIONAL MAESTRA (MVP)

> **Documento de Definición Funcional, Reglas de Negocio, UX y Alcance de Producto**  
> **Versión:** 1.2 (MVP Maestro con Automatización Omnicanal)  
> **Estado:** Aprobado y cerrado para desarrollo guiado por producto  
> **Propósito:** Especificación funcional definitiva y exhaustiva para el desarrollo sin ambigüedades.

---

## 1. Visión del Producto, Identidad y Posicionamiento

### 1.1 Misión y Propuesta de Valor
Convertirse en la plataforma web de referencia ágil, contemporánea y visual para la comunidad hispanohablante de juegos de mesa (*"Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa."*).

* **Problemas reales que resuelve:**
  * **Dispersión:** Tutoriales, partidas y reseñas en español están fragmentados en YouTube e Instagram sin indexar por juego ni formato.
  * **Barrera de BGG:** BoardGameGeek es el estándar técnico indiscutible pero resulta anticuado, denso, en inglés y poco práctico para consultas ágiles.
  * **Fricción previa a la compra:** Dificultad para saber en 10 segundos cómo escala realmente un juego a 2 jugadores, si un niño de 8 años puede jugarlo aunque la caja marque +14, el espacio real que ocupa en mesa o qué expansión merece la pena.
  * **Oportunidades desaprovechadas:** Sorteos en redes y bajadas de precio pasan desapercibidos por falta de un radar centralizado.
* **Tono y Personalidad:**
  * Cercano, de jugón a jugón, transparente y comunitario.
  * Cero aspecto de base de datos corporativa o enciclopedia de los años 2000.
  * Microtextos con identidad (ej. *"Aún nadie ha probado este juego a 2 jugadores, ¿te animas a ser el primero?"* en lugar de *"Sin registros"*).

### 1.2 Nombres de Trabajo y Evaluación de Marca
* **Opción A: LUDEKA** (Ludo + Fonoteca/Biblioteca: evoca catálogo ordenado, repositorio de consulta ágil).
* **Opción B: LUDIST** (Término anglosajón que define al aficionado/jugador: club, perfil jugón, red social de mesa).
* *(Descartes acordados: Ludex descartado por colisión directa con marcas y federaciones existentes).*

---

## 2. Principios de Diseño y Experiencia de Usuario (UX/UI)

### 2.1 Enfoque "Mobile-First Radical"
Diseñado para su consumo principal desde pantallas móviles (enlaces desde redes, Telegram, ferias, quedadas o en tiendas físicas).
* **Zonas táctiles para el pulgar:** Botones principales de acción (*Tengo*, *Jugado*, *Comprar*, *Prestar*) accesibles en la barra inferior de interacción.
* **Fichas sin scroll infinito:** Navegación por pestañas horizontales limpias (*Resumen & Veredicto / Vídeos & Redes / Reglas & Q&A / Expansiones*).
* **Carga instantánea:** Estructura visual ligera, sin bloqueos de renderizado ni elementos cosméticos pesados.

### 2.2 Política de Accesos: Abierto vs. Registro
* **Acceso público y libre:** Todo el catálogo, rankings, vídeos, fotos, semáforos, dudas resueltas y sorteos son visibles sin registrarse.
* **Registro ligero en 1 clic (OAuth):** Inicio de sesión exclusivo mediante **Google o Discord** (sin formularios tediosos ni verificación de correo por tokens) requerido únicamente para:
  * Gestionar la ludoteca personal y el cuaderno de préstamos.
  * Emitir micro-opiniones, notas, fotos y votos de edad/jugadores.
  * Publicar preguntas o respuestas en el consultorio de reglas.
  * Recibir alertas de bajadas de precio y sorteos de juegos en seguimiento.
  * Importar colecciones desde BGG.

---

## 3. Catálogo y Ficha Inteligente de Juego

### 3.1 Cabecera Visual y "ADN Lúdico"
La zona superior de la ficha muestra un bloque compacto de lectura en 3 segundos:

* **Identidad:** Título original y de forma prominente el **Nombre comercial en español** según las editoriales que lo publican o distribuyen en España/Latinoamérica (Devir, Maldito Games, Asmodee, Zacatrus, Arrakis, Tranjis, etc.).
* **Imagen oficial:** Uso exclusivo de la URL remota de la portada oficial servida por la CDN de BGG (`CoverImageUrl`), sin almacenamiento en disco propio para mantener el coste a 0 €.
* **Píldoras de Dinámica y Estilo (Badges):**
  * *Confrontación:* 🤝 Cooperativo | ⚔️ Competitivo | 👥 Roles Ocultos / Equipos | 🗡️ Semi-cooperativo.
  * *Estilo Lúdico:* ⚙️ Eurogame | 🎲 Ameritrash / Temático | 🎉 Party Game | 🧩 Filler / Abstracto | 📖 Narrativo / Campaña.
  * *Modo Solitario:* 👤 Modo solitario oficial incluido (si las reglas oficiales traen sistema o bot sin apaños caseros).

### 3.2 Doble Rating y Doble Ranking (Global vs. Local)
Visualización comparativa directa entre el criterio internacional y el consenso hispanohablante:
* **Ratings:**
  * ⭐️ **BGG Rating:** Media global oficial sobre decenas de miles de votos internacionales.
  * ⭐️ **Ludist Rating:** Media ponderada de los votos de usuarios registrados en la plataforma.
* **Rankings:**
  * Posición general: `#X Global BGG` vs `#Y Global Ludist`.
  * Posición por categoría: `#X en Estrategia BGG` vs `#Y en Euros Ludist`.

### 3.3 Semáforo Dinámico de Escalabilidad (1 a 7+ Jugadores)
La escala se adapta al rango real del título, evitando el corte restrictivo de 4 jugadores:
* **Rango adaptativo:** Desglose individual de `1J`, `2J`, `3J`, `4J`, `5J`, `6J`, `7J+` (o rangos adaptativos superiores para juegos masivos de roles ocultos).
* **Semáforo visual:**
  * 🟢 **Imprescindible / Brilla:** La configuración óptima donde el juego despliega su mejor nivel.
  * 🟡 **Bueno / Recomendado:** Totalmente jugable y disfrutable.
  * 🔴 **No recomendado:** Escala mal, tablero desierto, entreturno excesivo o parche artificial.
* **Etiqueta "Ideal a":** Destaca de forma textual la configuración perfecta (ej. *"Ideal: 2 jugadores"*).

### 3.4 Edad de la Caja vs. Edad Real Comunitaria
* **Doble dato visible:**
  * *Edad de caja (Oficial):* Etiqueta legal/normativa (típicamente `+14` por directivas de seguridad de componentes pequeños).
  * *Edad real comunitaria:* Consenso votado por familias y jugadores (ej. `8+`).
* **Distintivo de accesibilidad temprana:** 🟢 *"Accesible antes: la comunidad dice que a partir de 8 años funciona genial"*.
* **Dependencia del idioma en componentes:**
  * 🟢 *Nula (solo iconografía/simbología)*
  * 🟡 *Baja (frases cortas o texto visible)*
  * 🔴 *Alta (párrafos densos, requiere lectura fluida)*

### 3.5 Duración Real por Jugador y Huella en Mesa
* **Duración calculada:** Muestra los minutos reales estimados por persona sentada a la mesa (ej. ⏱️ *~30–40 min por jugador: a 2 son ~1h, a 4 se va a ~2h30*).
* **Huella en mesa (Espacio requerido):**
  * 🪑 *Mesa pequeña / Cafetería* (fillers, cartas sin tablero desplegado).
  * 🍽️ *Mesa de comedor estándar* (euros medios, mapas fijos).
  * 🏰 *Monstruo de mesa* (requiere mesa grande para tableros individuales gigantes, mercados y reservas).

### 3.6 Guía de Fundas (Sleeves) & Afiliación Directa
* Información técnica: Medidas exactas y número de cartas (ej. *120 cartas formato Estándar 63.5 x 88 mm*).
* Botón de compra contextual con código de afiliado: *«Comprar fundas compatibles en Zacatrus / Amazon»*.

### 3.7 Pestaña de Expansiones
* Listado de expansiones oficiales asociadas.
* Etiquetas funcionales: *¿Aumenta jugadores?*, *¿Añade modo solitario?*, *¿Es imprescindible para corregir el base o solo para completistas?*.

---

## 4. Veredictos, Reseñas y el Rol de la Mesa Fundadora

### 4.1 Ciclo de Vida del Veredicto en Tres Fases
1. **Fase Inicial (Arranque con IA):** Bloque claramente identificado como `🤖 Resumen generado por IA` con síntesis de escalabilidad, edad real y huella en mesa basada en el consenso del hobby para evitar fichas vacías.
2. **Fase Editorial Fundadora (Vuestra aportación directa):** Sustitución prioritaria por el distintivo `👤 Veredicto de la mesa fundadora`.
3. **Fase Comunitaria (Relevo Orgánico):** Con 3 a 5 micro-reseñas de usuarios, los algoritmos recalculan el semáforo y la nota local, pasando el resumen de IA a segundo plano o retirándose la marca automática.

### 4.2 Módulo y Panel de la Mesa Fundadora y Moderación
Panel interno para usuarios con rol `FoundingTeam` o `Moderator` con acceso directo desde la propia ficha (`[ 🛡️ Gestionar Veredicto Fundador ]`):
* **Análisis oficial de la casa:** Reseña estructurada enfocada en la experiencia real en pareja (a 2 jugadores) y en familia con niños (adaptación de reglas, duración, ritmo).
* **Galería de Fotos Reales de Partida:** Posibilidad de subir de 1 a 3 fotografías reales tomadas en mesa propia (despliegue real en salón, componentes en juego, niños jugando), aportando una credibilidad visual absoluta frente a renders comerciales.
* **Sello de Recomendación:** Selector de insignia (*"Imprescindible de la Mesa"*, *"Recomendado con adaptaciones"*, *"Prescindible"*).
* **Tratamiento en UI:** Esta tarjeta se muestra fija en la parte superior de la ficha (antes que la IA y antes que las opiniones abiertas) y posee un peso ponderado mayor en el algoritmo del ranking local.

---

## 5. Hub Multimedia y Pipeline de Ingesta

### 5.1 Estructura Segregada por Pestañas (Evitar Mezcla de Formatos)
La sección multimedia organiza el contenido en pestañas horizontales limpias con diseño de tarjeta adaptativo:

* **Pestaña 1: 🎬 Tutoriales (YouTube):**
  * Miniaturas en formato horizontal 16:9 con duración y canal.
  * Enfoque: Guías explicativas de "cómo se juega" en 10–20 minutos.
* **Pestaña 2: 🎲 Partidas Completas (YouTube):**
  * Miniaturas 16:9 con badge identificativo obligatorio del número de jugadores (ej. *"Partida a 2 jugadores"*).
* **Pestaña 3: 💬 Opiniones y Redes (Instagram + Shorts/Reels):**
  * **Posts de Instagram (Foto + Texto):** Tarjetas cuadradas limpias que muestran la fotografía principal del post, autor (`@canal`), contador de likes y las primeras líneas de la reseña. Al tocarlo abre un modal limpio o redirige al post oficial.
  * **Vídeos Verticales (Reels / Shorts):** Tarjetas en formato 9:16 con icono de reproducción rápida y duración (ej. `0:55`).

### 5.2 Estrategia de Ingesta: Cold Start vs. Modo Crucero
* **Carga Inicial (Cold Start):**
  * Catálogo base: **Top 1.000 de BGG + Colección de la mesa fundadora**.
  * YouTube: Búsqueda quirúrgica dirigida a los juegos del catálogo (máximo los 2 mejores vídeos/partidas por juego).
  * Instagram: Barrido acotado a los últimos 10–15 posts por cuenta registrada (últimas 2–3 semanas), descartando el histórico caducado.
  * Ingesta escalonada durante 2–3 días en segundo plano para respetar las cuotas gratuitas.
* **Modo Crucero Diario:**
  * Comprobación nocturna de subidas de las últimas 24–48 horas en canales y perfiles monitorizados.

### 5.3 Panel de Moderación Rápido
* Interfaz móvil para aprobar o descartar contenido en 1 clic.
* **Bandeja de vídeos huérfanos:** Cola para asignar manualmente vídeos o posts de novedades/campañas de mecenazgo que no hicieron coincidencia automática con el catálogo.
* **Detector de enlaces rotos:** Proceso semanal ligero para despublicar vídeos eliminados o privados (HTTP 404).

---

## 6. Motor de Automatización Omnicanal (Panel Moderador -> Instagram + Web)

Para alimentar la cuenta oficial de Instagram de la plataforma (`@ludist.app`) y la web simultáneamente sin duplicar tareas manuales:

### 6.1 Formulario Rápido de Sorteos y Novedades
Desde el panel móvil de moderación, los administradores cuentan con la herramienta de creación rápida:
* Selector de tipo: `[ 🎁 Sorteo ]` | `[ 🚀 Novedad de Tiendas ]`.
* Autocompletado del juego contra el catálogo de la web (o búsqueda asistida de BGG).
* Enlace de destino (post del organizador, tienda o editorial).
* Cuenta organizadora (`@editorial` o `@divulgador`).
* Fecha límite de participación (para sorteos).
* Imagen base (subida de foto personalizada o uso automático de la portada oficial).

### 6.2 Motor de Composición Automática de Imágenes (Plantillas de Marca)
El sistema genera dinámicamente un activo gráfico cuadrado (1:1 / 4:5 JPEG estándar) optimizado para el feed de Instagram:
* Plantilla corporativa con degradado oscuro de fondo y tipografía nítida.
* Portada/foto del juego enmarcada en el centro.
* Píldora identificativa destacada: `🎁 SORTEO ACTIVO` o `🚀 NOVEDAD SEMANAL`.
* Datos clave legibles en miniatura: Nombre de la editorial y fecha límite.
* Pie de marca sutil de la plataforma (`Ludist.app`).

### 6.3 Publicación Simultánea en 1 Clic
Al presionar `[ Aprobar y Publicar Todo ]`:
1. **Publicación en Instagram:** Publicación desatendida en el feed de la cuenta oficial vía la API oficial de Meta (`Instagram Content Publishing API` sobre cuenta profesional vinculada).
2. **Publicación en la Web:** Inserción inmediata en el **Radar de Sorteos Activos** de la web con expiración programada.
3. **Inclusión en Carruseles Semanales:** Entrada directa en el carrusel de la Home (*"Sorteos activos de esta semana"* o *"Llegadas a tiendas este viernes"*).
4. **Alerta de Deseos:** Notificación automática a los usuarios que tengan ese juego en *Quiero comprar*.
5. **Difusión en Discord:** Publicación automática en los canales `#radar-sorteos` o `#novedades-viernes`.

---

## 7. Ludoteca Personal, Préstamos y Valoraciones de Usuario

### 7.1 Colección en 4 Estados y Flujo Interactivo
Cada ficha dispone de una barra de acción inferior para el usuario registrado:
* 🟢 **En mi ludoteca:** Copias físicas en propiedad.
* 🔵 **Jugado:** Títulos jugados en asociaciones, bares lúdicos o casas de amigos.
* 🟡 **Deseado:** Títulos en el radar de interés para probar.
* 🔴 **Quiero comprar:** Lista de seguimiento comercial activa (disparador de alertas).

**Reglas de interacción y edición:**
* **Cambio de estado libre:** El usuario puede alternar de estado en cualquier momento.
* **Disparador de valoración (Bottom Sheet):** Al pulsar *Jugado* o *En mi ludoteca* por primera vez, se sugiere: *"¡Registrado! ¿Te apetece dejar tu nota y veredicto rápido en 30 segundos?"*.
* **Tarjeta personalizada y edición:** Si el usuario ya valoró el juego, ve su propia tarjeta fija encima de las opiniones públicas (`⭐️ Tu valoración: 8.5/10`) con un botón `[ ✏️ Editar mi valoración ]` para modificar datos tras nuevas partidas.

### 7.2 Modo Préstamo Integrado ("¿A quién se lo dejé?")
* En los juegos marcados como *En mi ludoteca*, acción directa: `[ Prestar ]`.
* Campos mínimos: Nombre de la persona o asociación y fecha del préstamo.
* Pestaña privada en el perfil del usuario: **«Mis juegos prestados (X)»** con opción de marcar como devuelto en 1 toque.

### 7.3 Formulario de Valoración Modular Unificado
Una única pantalla interactiva que se completa en menos de 45 segundos:
1. **Datos Básicos:** Puntuación (1 al 10) y Micro-reseña (máximo **280 caracteres**).
2. **Experiencia por Jugadores (Chips interactivos de 1 clic):**
   * El usuario enciende solo los que ha jugado: `[ 1J ] [ 2J ] [ 3J ] [ 4J ] [ 5J ] [ 6J ] [ 7J+ ]`.
   * Para cada uno encendido, selecciona su semáforo: `🟢 Imprescindible | 🟡 Recomendado | 🔴 No recomendado`.
3. **Experiencia Familiar / Infantil (Opcional):**
   * Checkbox: ☑️ *«Lo he jugado con niños / en familia»*.
   * Selectores: Edad mínima real recomendada (`4+ | 6+ | 8+ | 10+ | 12+ | 14+`) y si se usaron reglas oficiales o adaptaciones.
4. **Contexto de Partida (Proof of Play):** Selector voluntario: *Propiedad / Club o Asociación / Amigos / Bar lúdico / BGA*.

### 7.4 Gamificación y Retención
* Contador en perfil: *"Tienes [X] juegos jugados pendientes de opinar"*.
* Rango de perfil evolutivo según la actividad (reseñas aportadas, dudas resueltas y antigüedad).

---

## 8. Importador BGG y Crecimiento Orgánico del Catálogo

### 8.1 Importador en 1 Clic desde BGG
* El usuario introduce su nombre de usuario de BoardGameGeek en su perfil.
* La plataforma extrae sus listas (`Owned`, `Wishlist`).
* **Cruce con el catálogo local:**
  * Títulos existentes: Se asocian inmediatamente a su ludoteca personal.
  * Títulos no existentes en Ludist: Se añaden a su perfil bajo la etiqueta `⏳ En cola de catalogación` (permitiendo ya gestionar notas privadas o préstamos).

### 8.2 Cola Nocturna de Auto-Catalogación
* Los juegos solicitados que no estaban en Ludist entran en la tabla `PendingBggImports` y se ordenan automáticamente por popularidad (número de usuarios que los tienen).
* Cada noche, un worker automático extrae los **20 a 50 juegos más demandados**, genera su ficha oficial desde BGG, ejecuta la síntesis inicial de IA y los publica en el catálogo general.

### 8.3 Añadir Título a Mano Asistido por BGG
* Si un usuario quiere registrar un juego que aún no está en la web, el buscador consulta en vivo la API de búsqueda de BGG (`/xmlapi2/search`).
* Al seleccionarlo, se vincula con su `BggId` oficial exacto, evitando duplicados y garantizando su catalogación limpia.

---

## 9. Motor de Filtros y Exploración del Catálogo

### 9.1 Tops por Categoría y Estilo
Listados con alternancia de orden (*Según BGG* vs *Según Comunidad Hispana*):
* 🏆 **Top Global**
* ⚙️ **Top Eurogames**
* 🎲 **Top Temáticos / Ameritrash**
* 🎉 **Top Party Games**
* 🧩 **Top Fillers / Abstractos**
* 🤝 **Top Cooperativos**
* 👤 **Top Solitario**

### 9.2 Filtros Prácticos de Situación Real
* ⚔️ **Especial Parejas:** Juegos con semáforo 🟢 (*Imprescindible*) a 2 jugadores.
* 👨‍👩‍👧 **Mesa Familiar:** Juegos con edad real comunitaria $\le 8	ext{--}10$ años y baja dependencia de lectura.
* 👥 **"Somos 5 en mesa" / "Grupos grandes (7+)":** Juegos donde la escalabilidad brille para ese número exacto de comensales.
* ⏱️ **Partidas Rápidas:** Duración total menor a 45 minutos y complejidad ligera ($\le 2.2/5$).

---

## 10. Radar de Sorteos, Novedades y Comunidad

### 10.1 Radar de Sorteos Externos
* Extracción y publicación de sorteos vigentes con fecha límite (`DeadlineAt`).
* Expiración y archivo automático al superar la fecha fin.
* **Fusión de colaboraciones:** Sorteos conjuntos (editorial + influencer) unidos en una única tarjeta para evitar duplicados.

### 10.2 Radar de Lanzamientos Semanales
* Sección destacada en la Home cada jueves/viernes con el carrusel de novedades que entran en tiendas ese fin de semana.

### 10.3 Sorteos Exclusivos de la Comunidad (Internos)
* Sorteos periódicos de juegos top adquiridos con los fondos del proyecto.
* Requisito de participación: Haber estado activo en la plataforma ese mes (al menos 1 micro-reseña o 1 voto de reglas aportado).

### 10.4 Q&A de Reglas (Estilo StackOverflow)
* Consultorio por juego para resolver dudas de reglamento a mitad de partida.
* Formato técnico: Pregunta, respuestas de la comunidad, votos y marca de *"Respuesta válida/aceptada"*.

### 10.5 Comunidad en Discord
* Servidor oficial para coordinar partidas locales y debates.
* Bot sincronizado que emite alertas automáticas de nuevos sorteos aprobados y novedades semanales.

---

## 11. Modelo de Negocio y Manifiesto de Transparencia

### 11.1 Vías de Ingreso
1. **Afiliación contextual:** Enlaces de compra en tiendas especializadas y Amazon sin banners invasivos.
2. **Radar de bajada de precios:** Alertas internas a usuarios con el juego en *Quiero comprar* cuando el precio caiga.
3. **Mecenazgo en Ko-fi / Buy Me a Coffee:** Donaciones voluntarias que otorgan la insignia visual *"Mecenas"* en el perfil.

### 11.2 Manifiesto de Transparencia de Fondos
Visible en la página de Contacto/Apoyo y en el pie de página de la web:
> *"Ludist es un proyecto independiente nacido de jugadores para jugadores. Todos los ingresos generados mediante enlaces de afiliación y donaciones voluntarias se destinan íntegramente a tres fines transparentes:*  
> 1. *Cubrir los costes mensuales de servidores, bases de datos y herramientas de desarrollo.*  
> 2. *Adquirir nuevos títulos y expansiones para que la mesa fundadora y moderadores puedan probarlos a fondo, ampliando los análisis a 2 jugadores y en familia con fotografías reales de mesa.*  
> 3. *Financiar sorteos periódicos de juegos top exclusivos para los miembros activos de la comunidad."*

---

## 12. Plan de Marketing y Lanzamiento (Coste 0 €)

### 12.1 Alianzas con Creadores de Contenido
* Contacto directo con canales medianos y pequeños avisándoles de que sus tutoriales y posts están indexados en las fichas oficiales.
* Mención semanal a editoriales con el resumen de sorteos activos y novedades en tiendas.

### 12.2 Sorteo Inaugural de Tracción
* Sorteo de lanzamiento de un juego de máxima demanda (ej. *Brass: Birmingham* o *Ark Nova*).
* **Requisitos de participación orientados a poblar la plataforma:**
  1. Registrarse con 1 clic vía Google o Discord.
  2. Añadir al menos 3 juegos a su ludoteca personal (*Tengo* o *Jugado*).
  3. Dejar al menos 1 micro-reseña o resolver una duda de reglas.

---

## 13. Checklist Final de Entrega del MVP

- [x] Catálogo curado del **Top 1.000 de BGG + Colección de la Mesa Fundadora**.
- [x] Fichas con nombre comercial en español, ADN lúdico, carátula BGG y guía de fundas con enlaces de compra.
- [x] Doble Rating y Ranking (BGG vs. Comunidad Hispana).
- [x] Semáforo dinámico de escalabilidad adaptativo hasta **7+ jugadores** con etiqueta "Ideal a".
- [x] Comparativa de edad de caja vs. edad real infantil + factor de dependencia del idioma.
- [x] Duración real estimada por jugador y nivel de huella en mesa.
- [x] Panel y Veredicto de la Mesa Fundadora con galería de fotos reales de mesa.
- [x] Hub multimedia segregado por pestañas: Tutoriales (YT), Partidas (YT) y Opiniones & Redes (Instagram/Shorts/Reels).
- [x] Motor de automatización omnicanal: generación de plantilla gráfica de marca y publicación en 1 clic en Instagram, web, carruseles de Home y Discord.
- [x] Ludoteca en 4 estados con módulo privado de **Gestión de Préstamos**.
- [x] Botones de colección dinámicos con cambio de estado libre y edición de valoración existente.
- [x] Formulario de valoración modular de 1 clic (280 caracteres, desglose por comensales y experiencia infantil).
- [x] Importador BGG en 1 clic con cola nocturna automática de catalogación para juegos pendientes.
- [x] Buscador asistido por API de BGG para registrar juegos a mano sin duplicados.
- [x] Radar de sorteos externos (con fusión de colaboraciones) y Sorteos internos de la comunidad.
- [x] Radar de lanzamientos de los viernes en tiendas españolas.
- [x] Consultorio de reglas Q&A estilo StackOverflow y servidor sincronizado de Discord.
- [x] Monetización por afiliación, alertas de bajadas de precio y Manifiesto de Transparencia de Fondos.
- [x] Estrategia de arranque en frío (seed escalonado) y plan de captación orgánica a coste 0 €.
