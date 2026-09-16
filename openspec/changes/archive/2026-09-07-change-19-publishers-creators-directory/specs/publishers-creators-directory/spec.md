# Especificación de Requerimientos: change-19-publishers-creators-directory

## 1. Contexto y Objetivos
El objetivo de esta especificación es definir los requisitos funcionales, técnicos y de aceptación para el **Directorio de Editoriales, Creadores y Tiendas** en Ludeka, incorporando navegación cruzada con las fichas de catálogo y alimentación dinámica del motor de búsqueda de YouTube (INC-14).

---

## 2. Definición del Dominio y Entidades

### 2.1 Enumerados y Value Objects
- **`SocialPlatform`**:
  `Website`, `YouTube`, `Instagram`, `Twitter`, `Discord`, `Facebook`, `BoardGameGeek`, `Twitch`, `TikTok`, `Other`.
- **`StoreType`**:
  `PhysicalOnly`, `OnlineOnly`, `Hybrid`.
- **`SocialNetworkLink`**:
  - `Platform`: Plataforma de la red social.
  - `Url`: URL válida del perfil o canal.
  - `Handle`: Alias o handle opcional (ej. `@devirtv`, `@zacatrustv`).
  - `Title`: Etiqueta descriptiva opcional.

### 2.2 Entidad `Publisher` (Editorial)
- `Id`: Guid único.
- `Name`: Nombre comercial (ej. "Devir Iberia", "Maldito Games").
- `Slug`: Identificador amigable único para URL (ej. "devir", "maldito-games").
- `Country`: País de origen (ej. "España", "EEUU").
- `City`: Ciudad o sede opcional (ej. "Barcelona", "Sevilla").
- `Description`: Reseña histórica o línea editorial.
- `LogoUrl`: URL de imagen o avatar de la editorial.
- `WebsiteUrl`: Sitio web oficial.
- `SocialLinks`: Lista de `SocialNetworkLink`.
- `CreatedAt`: Fecha de creación UTC.
- `UpdatedAt`: Fecha de última edición UTC.

### 2.3 Entidad `Creator` (Autor / Diseñador / Ilustrador / Divulgador)
- `Id`: Guid único.
- `Name`: Nombre completo (ej. "Elizabeth Hargrave", "Klaus Teuber").
- `Slug`: Identificador amigable único para URL (ej. "elizabeth-hargrave", "klaus-teuber").
- `Nationality`: Nacionalidad opcional.
- `Bio`: Biografía profesional o trayectoria.
- `AvatarUrl`: Fotografía o avatar del autor.
- `BggPersonId`: Identificador numérico de BoardGameGeek opcional.
- `WebsiteUrl`: Sitio web o blog personal.
- `SocialLinks`: Lista de `SocialNetworkLink`.
- `CreatedAt`: Fecha de creación UTC.
- `UpdatedAt`: Fecha de última edición UTC.

### 2.4 Entidad `Store` (Tienda Lúdica)
- `Id`: Guid único.
- `Name`: Nombre comercial de la tienda (ej. "Zacatrus!", "Cuarto de Juegos").
- `Slug`: Identificador amigable único para URL (ej. "zacatrus", "cuarto-de-juegos").
- `Type`: `StoreType` (`PhysicalOnly`, `OnlineOnly`, `Hybrid`).
- `City`: Ciudad de la tienda física si aplica (ej. "Madrid", "Barcelona").
- `Address`: Dirección postal física opcional.
- `Description`: Descripción de servicios (almacén, mesas de juego, eventos).
- `LogoUrl`: Logotipo de la tienda.
- `WebsiteUrl`: Tienda en línea oficial.
- `AffiliateCode`: Código o parámetro de afiliado de Ludeka.
- `HasLoyaltyProgram`: Indica si ofrece puntos o programa de fidelidad.
- `SocialLinks`: Lista de `SocialNetworkLink`.
- `CreatedAt`: Fecha de creación UTC.
- `UpdatedAt`: Fecha de última edición UTC.

---

## 3. Comportamiento y Criterios de Aceptación (Gherkin)

```gherkin
Característica: Directorio de Editoriales, Creadores y Tiendas

  Escenario: Consulta del directorio de editoriales con filtrado
    Dado que existen editoriales registradas como "Devir", "Maldito Games" y "Tranjis Games"
    Cuando un visitante accede a "/editoriales" y busca "Devir"
    Entonces el listado muestra la tarjeta de "Devir" con su logo, país y recuento de títulos
    Y no muestra editoriales que no coincidan con el término

  Escenario: Visualización de la ficha de una editorial con sus juegos en catálogo
    Dado que existe la editorial "Devir" con slug "devir" y juegos asociados como "Catan"
    Cuando el visitante navega a "/editoriales/devir"
    Entonces se visualiza el logo, descripción, enlaces a su web y canales sociales (YouTube, Twitter)
    Y se visualiza el listado de juegos publicados en Ludeka pertenecientes a Devir

  Escenario: Visualización de la ficha de un creador con sus obras y perfil BGG
    Dado que existe el creador "Elizabeth Hargrave" con slug "elizabeth-hargrave" y juego "Wingspan"
    Cuando el usuario entra a "/creadores/elizabeth-hargrave"
    Entonces se muestra su biografía, enlace a su perfil BGG y sus obras diseñadas en Ludeka
    Y al hacer clic sobre "Wingspan" se navega a "/juegos/wingspan"

  Escenario: Ficha de tienda vinculada a ofertas de compra de juegos
    Dado que existe la tienda "Zacatrus!" con slug "zacatrus"
    Y existen juegos en el catálogo que tienen ofertas de compra con StoreName "Zacatrus!"
    Cuando el visitante accede a "/tiendas/zacatrus"
    Entonces se muestra su tipo "Híbrida", enlaces a su web y canal de YouTube
    Y se visualiza el catálogo de juegos con ofertas disponibles en Zacatrus!

  Escenario: Navegación cruzada desde la ficha de juego
    Dado que el usuario visita "/juegos/wingspan"
    Cuando observa la sección de créditos del juego
    Entonces el nombre "Elizabeth Hargrave" es un enlace activo a "/creadores/elizabeth-hargrave"
    Y el nombre de la editorial es un enlace activo a "/editoriales/{slug}"

  Escenario: Incorporación automática de canales al foco de YouTube (INC-14)
    Dado que se registra una editorial o tienda con un enlace de YouTube "@mitiendatv"
    Cuando el motor de búsqueda quirúrgica de YouTube evalúa un vídeo de "@mitiendatv"
    Entonces el proveedor de foco lo reconoce como canal de referencia oficial
    Y le otorga la bonificación algorítmica de relevancia prioritaria

  Escenario: Alta de nueva entidad por moderador
    Dado un usuario autenticado con rol "Moderator" o "FoundingTeam"
    Cuando accede al directorio y pulsa "➕ Nueva Tienda"
    Y rellena el nombre "Ludoteca Central", web y redes sociales
    Entonces el sistema genera automáticamente el slug "ludoteca-central"
    Y la tienda queda guardada y accesible de inmediato
```
