# Especificación de Requisitos SDD — INC-60: Navegación Móvil — Barra Inferior y Safe-Area

## 1. Requisitos Funcionales (RF)

- **RF-01 (Barra Inferior Móvil Fija):**  
  La aplicación debe renderizar un componente de navegación inferior fijo en la parte inferior del viewport en dispositivos móviles y tabletas (`fixed bottom-0 inset-x-0 z-40 lg:hidden`).

- **RF-02 (Cinco Destinos de Primer Nivel):**  
  La barra debe albergar 5 destinos fijos de primer nivel representados por icono Lucide oficial y etiqueta de texto:
  1. **Inicio:** Ruta `/`, icono `house`, etiqueta `Inicio`.
  2. **Catálogo:** Ruta `/catalogo`, icono `dices`, etiqueta `Catálogo`.
  3. **Ludoteca:** Ruta `/cuenta/ludoteca` (compatible con alias `/mi-ludoteca`), icono `library`, etiqueta `Ludoteca`.
  4. **Sorteos:** Ruta `/sorteos`, icono `gift`, etiqueta `Sorteos`.
  5. **Cuenta / Acceso:** Si el usuario tiene sesión activa, ruta `/cuenta`, icono `user`, etiqueta `Cuenta`. Si es visitante (sin sesión), ruta `/login`, icono `user`, etiqueta `Entrar`.

- **RF-03 (Detección Reactiva de Ruta Activa y Marcado ARIA):**  
  El destino coincidente con la URL actual debe marcarse visualmente como activo (`text-[var(--brand-primary)]` y fondo sutil) y portar el atributo `aria-current="page"`. Los destinos inactivos portarán `aria-current="false"` o valor nulo. La reactividad debe responder automáticamente ante eventos `NavigationManager.LocationChanged`.
  - La ruta `/` solo se considera activa si la ruta relativa es exactamente vacía o `"/"`.
  - `/catalogo` está activa si la URL comienza por `/catalogo` o `/juegos`.
  - `/cuenta/ludoteca` está activa si la URL comienza por `/cuenta/ludoteca` o `/mi-ludoteca`.
  - `/sorteos` está activa si la URL comienza por `/sorteos`.
  - `/cuenta` está activa si la URL comienza por `/cuenta` (excluyendo `/cuenta/ludoteca`) o `/login`.

- **RF-04 (Soporte de Áreas Seguras `viewport-fit=cover` y `safe-area-inset`):**  
  - En `App.razor`, la etiqueta `<meta name="viewport">` debe incluir `viewport-fit=cover`.
  - El contenedor de la barra debe incorporar padding inferior dinámico: `padding-bottom: env(safe-area-inset-bottom, 0px)`.
  - La maquetación de `MainLayout.razor` debe incorporar en móviles una reserva de margen inferior de al menos `calc(4rem + env(safe-area-inset-bottom, 0px))` para que el contenido deslizable y el pie de página (`footer`) no queden cubiertos por la barra fija.

- **RF-05 (Simetría Responsiva Estricta):**  
  En pantallas de escritorio (`>= 1024px`, breakpoint `lg:`), la barra inferior debe ocultarse completamente (`lg:hidden`) y el padding de reserva del layout debe restablecerse a cero (`lg:pb-0`), preservando la limpieza de la interfaz de escritorio.

- **RF-06 (Ergonomía de Acciones de Colección al Alcance del Pulgar):**  
  En la ficha de juego (`GameDetail.razor`), los botones de estado de colección (`CollectionActionBar.razor`) deben presentar una disposición táctil optimizada en móvil para ser operables con una sola mano, respetando el espaciado de seguridad y garantizando que el usuario pueda registrar su estado (`Tengo`, `Jugado`, `Comprar`) con feedback visual inmediato.

---

## 2. Requisitos No Funcionales (RNF)

- **RNF-01 (Accesibilidad WCAG 2.2 AA):**  
  - Elemento landmark semántico `<nav aria-label="Navegación principal móvil">`.
  - Controles con tamaño táctil mínimo de 48x48px (altura total de barra de al menos 56-64px más safe-area).
  - Anillos de foco visibles (`focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:outline-none`).
  - Prohibición expresa de emojis; empleo estricto de iconos SVG Lucide registrados en `IconCatalog.cs`.

- **RNF-02 (Estética Editorial Anti-Slop y Multi-Tema):**  
  - Acabado sobrio con fondo traslúcido `backdrop-blur-md bg-[var(--bg-nav)]` y borde superior `border-t border-[var(--border-subtle)]`.
  - Integración automática con las variables semánticas de los 4 temas soportados (`charcoal`, `editorial`, `tabletop`, `wood`, `midnight`).

- **RNF-03 (Estabilidad y Rendimiento):**  
  - Cero saltos visuales de maquetación (CLS = 0).
  - Implementación ligera en Blazor (`InteractiveServer`), con suscripción y desuscripción limpia de `LocationChanged` en `IDisposable`.
