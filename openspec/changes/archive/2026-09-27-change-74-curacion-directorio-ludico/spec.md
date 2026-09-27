# Especificación Técnica: INC-73 — Curación y Saneamiento Integral del Directorio Lúdico Español

## 1. Resumen Ejecutivo
Este incremento sanea de raíz el padrón canónico de entidades de la industria y la comunidad lúdica en España (`src/Ludeka.Infrastructure/Seeding/seed-directory.json`), solventando los 101 enlaces con anomalías (DNS inexistentes, TLDs equivocados, canales 404 y redundancias estructurales) detectados en la auditoría empírica, garantizando la calidad y fidelidad de los datos que nutren la aplicación, el motor multimedia de YouTube y los directorios públicos.

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Saneamiento y Validación de Editoriales (46 entidades)
- Cada editorial con presencia activa en la industria debe contar con su `websiteUrl` oficial canónico (ej. `https://www.masqueoca.com` en lugar de la redirección rota, `https://es.asmodee.com`, etc.).
- Los canales didácticos de YouTube vinculados deben apuntar a canales reales y verificados (ej. `@devirtv`, `@MalditoGames`, `@TranjisGames`, `@tcgfactory`, etc.).
- Las redes sociales secundarias (Instagram, Twitter/X) deben apuntar a perfiles activos. Se purgan handles sintéticos que arrojen HTTP 404 o no existan.
- Para microeditoriales o sellos discontinuados o con dominios expirados, se asigna su canal social o de contacto oficial vigente, o se deja `websiteUrl = null` documentado con honestidad editorial.

### REQ-2: Saneamiento y Validación de Tiendas Especializadas (37 entidades)
- Corrección de dominios y subdominios canónicos de tiendas (ej. `https://nostromocomics.com` en lugar del dominio inexistente `nostromocomic.com`).
- En tiendas con cierre comercial definitivo o dominios dados de baja por sus antiguos dueños, se sanean los enlaces o se actualizan a sus redes/canales activos.
- Los atributos de negocio (`affiliateCode`, `hasLoyaltyProgram`, `shippingCountries`) se mantienen rigurosamente intactos.

### REQ-3: Saneamiento y Validación de Creadores de Contenido (35 divulgadores)
- **Foco YouTube Crítico:** Al ser el eje vertebrador del motor audiovisual de Ludeka (búsqueda de tutoriales, partidas y unboxings), el canal de YouTube de cada creador debe ser verificado con exactitud milimétrica (`@handle` canónico).
- **Webs de Divulgadores:** Corregir TLDs y dominios de referencia (ej. Doctor Meeple debe ser `https://doctormeeple.es`, El Club Dante debe ser `https://www.elclubdante.es`).
- Retirar perfiles de Twitter/X inexistentes (404) o cuentas abandonadas que generen frustración al usuario.

### REQ-4: Normalización Estructural del Esquema JSON
- Se elimina la duplicidad sistemática por la cual la web oficial figuraba tanto en `websiteUrl` como en un elemento `{ "platform": "Website", "url": "..." }` dentro de `socialLinks`.
- El array `socialLinks` contendrá exclusivamente plataformas de redes y divulgación (`YouTube`, `Instagram`, `Twitter`, `Twitch`, `TikTok`, `Podcast`/`iVoox`), reservando `websiteUrl` para el portal corporativo/comercial/blog.

### REQ-5: Preservación de Slugs e Idempotencia
- **Invariante de Identidad:** Ningún `slug` existente puede ser modificado (ej. `devir-iberia`, `maldito-games`, `analisis-paralisis`, etc.). Esto garantiza que cualquier registro ya creado en bases de datos SQLite o PostgreSQL mantenga su relación unívoca sin duplicar filas.
- La ejecución de `DirectorySeeder.SeedDirectoryAsync` debe actualizar las propiedades de enlaces y descripciones enriquecidas sin romper claves primarias ni foráneas.

### REQ-6: Cobertura de Pruebas y Cero Regresiones
- Mantener en verde las 1.945 pruebas unitarias actuales.
- Incorporar pruebas unitarias en `DirectorySeederTests` que auditen la integridad sintáctica del JSON, la ausencia de URLs vacías en `socialLinks` y la eliminación de duplicados de `Website`.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Deserialización e integridad estructural de seed-directory.json
  Dado el archivo embebido seed-directory.json saneado
  Cuando se deserializa mediante DirectorySeeder
  Entonces se obtienen exactamente 46 editoriales, 37 tiendas y 35 creadores
  Y ninguna entidad contiene un enlace social con platform "Website" duplicado de websiteUrl
  Y todos los enlaces sociales poseen url no vacía y esquema https

Escenario: Precisión en webs de divulgadores clave
  Dado el censo de creadores de contenido
  Cuando se consultan las entidades "Doctor Meeple" y "Club Dante"
  Entonces "Doctor Meeple" apunta a "https://doctormeeple.es"
  Y "Club Dante" apunta a "https://www.elclubdante.es"

Escenario: Idempotencia de siembra y preservación de slugs
  Dado un contexto de base de datos poblado previamente
  Cuando se ejecuta DirectorySeeder.SeedDirectoryAsync
  Entonces no se crean entidades duplicadas
  Y los slugs existentes coinciden al 100% con los identificadores canónicos
```
