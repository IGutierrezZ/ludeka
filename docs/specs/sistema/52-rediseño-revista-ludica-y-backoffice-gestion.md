# 52. Rediseño «Revista Lúdica» y Backoffice Editorial «Ludeka Gestión»

> **Estado:** Implementado y verificado (INC-113 a INC-119)  
> **Pruebas asociadas:** 2.583 unitarias al 100% (incluyendo suites contractuales y funcionales)  
> **Tecnología:** .NET 10 (C# 13), Blazor Web App (SSR + InteractiveServer), Tailwind CSS, Lucide Icons

---

## 1. Visión y Propósito Arquitectónico

La épica **«Rediseño Revista Lúdica & Ludeka Gestión»** unifica y eleva la identidad visual y operativa de Ludeka a un estándar editorial de revista moderna especializada en juegos de mesa, erradicando cualquier aspecto genérico de software administrativo en el frontend público e introduciendo un entorno de control operacional sobrio, denso y productivo para el equipo editorial y de moderación (*staff*).

El trabajo abarca siete incrementos vertebrales desarrollados sobre una misma arquitectura de componentes desacoplados:

1. **INC-113 — Tokens y Tipografía:** Adopción de la fuente *Plus Jakarta Sans*, paleta cromática editorial (terracota `#C9481C`, salvia `#4E7A68`, arena cálido `#FFF1E2`, carbón `#17120F`) y modo claro/oscuro determinista.
2. **INC-114 — Shell Global «Revista Lúdica»:** Cabecera con identidad de revista, barra de navegación móvil ergonómica con panel emergente «Más» y barra contextual de staff (`StaffBar.razor`) visible solo para moderadores y administradores.
3. **INC-115 — Portada Editorial:** Hero narrativo con diseño de arco (*Arch Hero*), cintas horizontales de descubrimiento y bloque destacado de recomendaciones semanales de la mesa.
4. **INC-116 — Catálogo Híbrido:** Exploración por frase interactiva conversacional («Quiero jugar a algo para *N* personas, de duración *media* y dificultad *accesible*»), combinada con panel lateral de facetas y píldoras activas de filtrado.
5. **INC-117 — Ficha de Juego Editorial en 5 Bloques:** Estructura modular estandarizada:
   - *Bloque 01: Identidad y Mesa* (portada, ficha técnica, semáforo de comensales).
   - *Bloque 02: Veredicto y Experiencia* (análisis de la mesa, opinión estructurada).
   - *Bloque 03: Multimedia y Comunidad* (vídeos curados de YouTube y redes).
   - *Bloque 04: Expansiones y Fundas* (ecosistema del juego y catálogo de fundas exactas).
   - *Bloque 05: Dónde Comprar* (radar de precios en tiendas asociadas con enlaces afiliados).
   - Herramientas integradas de staff para edición directa y resolución de incidencias.
6. **INC-118 — Ludoteca Integral en 7 Pestañas:** Colección personal organizada en *Tengo*, *Jugado*, *Deseado*, *Prestar*, *Diario de Partidas*, *Radar de Precios* y *Modo Consulta Offline (PWA)*, con modales interactivos para registrar partidas y préstamos de juegos.
7. **INC-119 — Backoffice Editorial «Ludeka Gestión»:** Consola administrativa unificada bajo `AdminLayout.razor` que cubre todas las rutas `/admin/*` y `/moderacion/*`.

---

## 2. Especificación Técnica de «Ludeka Gestión» (AdminLayout)

### 2.1. Arquitectura de Layout y Paleta de Backoffice
A diferencia del frontend público, el backoffice adopta una **paleta oscura de alta densidad** inspirada en herramientas profesionales de edición (tokens `#17120F` para el fondo base, `#241C17` para superficies y tarjetas, terracota oscuro `#B8401A` para acentos principales, mostaza `#FFC145` para insignias operativas y arena `#FFF1E2` para tipografía).

- **Encabezado superior (56 px de altura):**
  - Logotipo editorial con insignia distintiva «Gestión» en mostaza.
  - Indicador de estado de la mesa fundadora con contador de los 11 módulos activos.
  - Buscador global interactivo mediante atajo `Cmd/Ctrl + K`.
  - Tarjeta de usuario staff con avatar, nombre, rol y botón de escape directo hacia la web pública (`Ir a la web`).
- **Barra lateral fija de 248 px en escritorio:**
  - Ocultable y colapsable mediante interruptor de panel.
  - Acceso a las 11 bandejas operativas con badges de estado o pendientes y teclas de acceso rápido `1-9`.
  - Segmentada en 5 agrupaciones de trabajo:
    1. *General:* Resumen (`/admin`).
    2. *Catálogo & Cola:* Bandeja de Ingesta (`/admin/bandeja`), Reportes de Fichas (`/admin/reportes`), Cola de Catalogación (`/admin/cola`) y Multimedia (`/admin/multimedia`).
    3. *Comunidad & Sorteos:* Eventos (`/admin/eventos`) y Cuentas Monitorizadas (`/admin/cuentas`).
    4. *Comercial & Tiendas:* Feeds de Afiliados (`/admin/feeds`) e Instagram (`/admin/instagram`).
    5. *Administración:* Usuarios & Permisos (`/admin/usuarios`), Auditoría (`/admin/auditoria`) y Notificaciones (`/admin/notificaciones`).
- **Navegación móvil ergonómica:**
  - Barra inferior con accesos rápidos a *Resumen*, *Bandeja*, *Reportes* y un cajón emergente (*Drawer*) accesible que expone los 11 módulos completos en pantallas táctiles.
- **Guardia de seguridad integrada:**
  - Si un usuario no autenticado o sin roles de staff (`CanReviewSocialMedia`, `CanModerateReports`, `CanEditGames`, `IsFounder`, etc.) accede a una ruta administrativa, `AdminLayout` intercepta la carga y presenta la pantalla *Acceso Restringido* sin renderizar componentes internos ni exponer datos operativos.

### 2.2. Atajos de Teclado Globales
El componente registra escuchadores nativos de teclado en el ciclo de vida del cliente Blazor:
- `Cmd + K` / `Ctrl + K`: Apertura inmediata del buscador global interactivo con enfoque automático en el campo de texto.
- `Escape`: Cierre de modales, buscador rápido o cajón móvil.
- Teclas numéricas `1` a `9` (cuando el foco no está en un campo de texto editable): Navegación directa a los módulos más frecuentados:
  - `1`: Resumen general (`/admin`).
  - `2`: Bandeja de ingesta social (`/admin/bandeja`).
  - `3`: Reportes de fichas (`/admin/reportes`).
  - `4`: Cola de catalogación BGG (`/admin/cola`).
  - `5`: Moderación multimedia (`/admin/multimedia`).
  - `6`: Gestión de eventos (`/admin/eventos`).
  - `7`: Cuentas monitorizadas (`/admin/cuentas`).
  - `8`: Feeds y afiliados (`/admin/feeds`).
  - `9`: Directorio y permisos de usuarios (`/admin/usuarios`).

### 2.3. Consola Central de Operaciones (`AdminDashboard.razor`)
Ubicada en `/admin` y `/admin/resumen`, la consola agrega telemetría en tiempo real de toda la plataforma:
- **Tarjetas de métricas operativas:**
  - Publicaciones pendientes en la bandeja de ingesta social.
  - Reportes de catálogo sin resolver.
  - Vídeos y enlaces multimedia pendientes de validación.
  - Juegos pendientes de enriquecimiento en la cola BGG.
  - Discrepancias EAN detectadas en feeds de afiliados.
  - Enlaces caídos o con incidencias en tiendas.
- **Panel de Estado de Sistemas:**
  - Salud del pipeline BGG XMLAPI2.
  - Estado de feeds comerciales de tiendas (Shopify JSON / Google Shopping XML).
  - Estado de los webhooks de Discord y Telegram.
  - Estado del publicador de Instagram y cola de renderizado SkiaSharp.
  - Estado del radar social de noticias y sorteos.
- **Bitácora de Auditoría en Tiempo Real:**
  - Últimas 8 operaciones registradas por `IAuditService`, con timestamp formateado, usuario ejecutor, tipo de entidad, clave y distintivo visual según la acción realizada (`Created`, `Updated`, `Deleted`, `StatusChanged`, `RoleChanged`, etc.).

---

## 3. Cobertura de Pruebas y Blindaje de Contrato

La implementación se respalda en la suite `AdminLayoutContractTests.cs` (15 pruebas unitarias) y las suites contractuales de cada incremento:
- Verificación estricta de altura de 56 px en la cabecera.
- Presencia y accesibilidad de las 11 bandejas operativas.
- Integración de atajos de teclado `Cmd/Ctrl+K` y numéricos `1-9`.
- Vinculación obligatoria de `@layout AdminLayout` en todas las páginas administrativas.
- Verificación de la pantalla de bloqueo de seguridad ante usuarios anónimos o no autorizados.
- Total verificado de la suite de pruebas unitarias: **2.583 pruebas en verde (0 errores, 0 omitidos)**.
