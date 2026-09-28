# Especificación de Requisitos SDD — INC-61: Menú de Cuenta y Estado de Sesión en la Cabecera

## 1. Requisitos Funcionales (RF)

- **RF-01 (Control de Identidad para Usuario Autenticado):**  
  Cuando `ICurrentUserService.UserId` no sea vacío, la cabecera debe renderizar un botón interactivo que contenga:
  - Un avatar editorial compacto que muestre la inicial en mayúscula de `ICurrentUserService.UserName` (o icono `user` en su defecto).
  - El nombre de usuario visible en pantallas intermedias y grandes (`sr-only sm:not-sr-only truncate max-w-[8rem]`).
  - Un icono o glifo indicador de despliegue (`chevron-down` o indicador de apertura).
  - Un indicador sutil de aviso (punto ámbar) si la cuenta tiene correo no verificado.

- **RF-02 (Menú Desplegable Flotante y Destinos Canónicos):**  
  Al activar el control de identidad, debe desplegarse un panel con los siguientes elementos estructurados:
  1. **Cabecera de Identidad:** Nombre del usuario y rol del sistema si es Fundador o Moderador.
  2. **Perfil Público:** Enlace a `/u/{UserId}` con icono `user` y texto «Mi Perfil Público».
  3. **Área de Cuenta:** Enlace a `/cuenta` (hub de INC-50) con icono `settings` o `layout-dashboard` y texto «Área de Cuenta».
  4. **Mi Ludoteca:** Enlace a `/cuenta/ludoteca` con icono `library` y texto «Mi Ludoteca».
  5. **Preferencias:** Enlace a `/cuenta/ludoteca?seccion=apariencia` con icono `palette` y texto «Apariencia & Tema».
  6. **Conexiones:** Enlace a `/cuenta/conexiones` con icono `link` y texto «Cuentas Conectadas», acompañado de un distintivo si el correo requiere verificación.
  7. **Cierre de Sesión:** Enlace a `/logout` con icono `log-out` y texto «Cerrar Sesión».

- **RF-03 (Puerta Visible para Visitantes / Invitados):**  
  Cuando `ICurrentUserService.UserId` sea nulo o vacío, el componente debe renderizar un enlace directo a `/login` con el icono `user`, el texto accesible "Entrar" y estilo coherente con el resto de píldoras de navegación.

- **RF-04 (Cierre de Sesión Seguro y Limpio):**  
  La acción de cierre de sesión debe apuntar al endpoint HTTP `/logout` de ASP.NET Core, provocando la invalidación de la cookie de autenticación (`SignOutAsync`), la redirección a `/` y la regeneración del circuito anónimo sin residuos de credenciales.

- **RF-05 (Comprobación y Alerta de Correo sin Verificar):**  
  El componente debe consultar de forma asíncrona `IAccountConnectionsService.HasVerifiedProviderEmailAsync()`. Si el usuario no dispone de un correo de proveedor verificado, se emitirá una alerta visual discreta en el botón de cuenta y dentro del menú junto a «Conexiones».

- **RF-06 (Interacción de Cierre y Descarte):**  
  El menú desplegable debe cerrarse automáticamente ante:
  - Pulsación de la tecla `Escape`.
  - Clic en el telón de fondo semitransparente.
  - Evento de navegación entre páginas (`NavigationManager.LocationChanged`).
  - Activación de cualquiera de sus enlaces internos.

---

## 2. Requisitos No Funcionales (RNF)

- **RNF-01 (Accesibilidad WCAG 2.2 AA):**  
  - El botón disparador debe exponer `aria-haspopup="menu"`, `aria-expanded="true|false"` y `aria-label="Menú de cuenta de {UserName}"`.
  - El contenedor del menú debe usar el rol semántico `role="menu"` y sus elementos internos `role="menuitem"`.
  - Foco visible accesible con anillos de contraste (`focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]`).
  - Tamaño táctil mínimo de al menos 40-48px para facilitar el uso en dispositivos táctiles.

- **RNF-02 (Estética Editorial y Adaptabilidad de Temas):**  
  - Uso exclusivo de tokens semánticos del sistema (`--bg-card`, `--bg-surface-elevated`, `--border-subtle`, `--text-primary`, `--text-secondary`, `--brand-primary`).
  - Compatibilidad nativa con los 4 temas activos (`charcoal`, `editorial`, `tabletop`, `wood`, `midnight`).
  - Ausencia de librerías CSS externas o componentes sobredimensionados; CSS Tailwind limpio y Razor puro.

- **RNF-03 (Resiliencia y Prerenderizado):**  
  - El menú debe funcionar limpiamente tanto en prerenderizado SSR estático como en Blazor `InteractiveServer`, manejando `IDisposable` para evitar fugas de memoria en eventos de navegación e invalidación.
