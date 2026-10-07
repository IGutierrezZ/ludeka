# INC-127: Rediseño Editorial de Cuenta, Shell Móvil y Panel de Gestión (Alineación Claude Design)

**Estado:** ✅ Verificado (Listo para PR)  
**Rama:** `inc/cuenta-movil-gestion`  
**Worktree:** `F:\repos\ludeka-wt\cuenta-movil-gestion`  
**Pruebas superadas:** 2.627 unitarias (100% en verde)  

---

## 1. Contexto y Objetivos

Alineación integral con la versión final del prototipo editorial de Claude (`Ludeka Final.dc.html`, `Ludeka Final Movil.dc.html`, `Ludeka Gestion.dc.html`, `github.md` y capturas de diseño) para redefinir las áreas personales de usuario, simplificar el shell móvil y modernizar el panel de operaciones «Ludeka Gestión».

---

## 2. Pantallas y Componentes Implementados

### 1. Shell y Experiencia Móvil (`MainLayout.razor`, `HomeDashboard.razor`, `Radar.razor`)
- **Retirada del menú superior móvil:** Eliminado el desplegable `<details id="mobile-nav-details">` en la cabecera superior móvil, delegando la navegación íntegramente en la barra inferior táctil `MobileBottomNav` al alcance del pulgar, tal y como especifica el diseño móvil de Claude.
- **Sorteos compactos en Portada Móvil:** Sustitución de las tarjetas Polaroid sobredimensionadas en vista móvil por una lista editorial compacta (miniaturas de 58×58 con rotación sutil, título, plataforma, ámbito y temporizador compacto con `<Icon Name="timer" />`).
- **Sorteos en Catálogo y Radar:** Ajuste de proporción y padding a 60×60 para el listado móvil con alineación métrica precisa.

### 2. Menú de Cuenta Desplegable (`AccountMenu.razor`)
- **4 Destinos Canónicos con Contadores:**
  1. `Mi perfil público` (`/perfil/{username}`, icono `user`)
  2. `Mi ludoteca` (`/cuenta/ludoteca`, icono `library` con contador numérico de colección)
  3. `Ajustes de cuenta` (`/cuenta`, icono `settings` hacia la vista unificada)
  4. `Panel de gestión` (`/admin`, icono `shield` con badge de moderación para staff)
- **Pie de Cierre de Sesión:** Botón accesible de `Cerrar sesión` con icono `log-out`.
- **Accesibilidad y Atajos:** Soporte para tecla `Escape`, `aria-expanded`, `aria-haspopup` y área táctil WCAG 2.2 AA.

### 3. «Tu cuenta» Unificada (`Account.razor`)
- **Centralización en `/cuenta`:** Unificación de las 4 secciones interactivas sin fragmentación:
  - `01 Apariencia y tema`: Selector interactivo de tarjetas Claro y Oscuro con muestras cromáticas activas.
  - `02 Ubicación y país`: Selector de píldoras territoriales (Global, España, México, Argentina, Chile) con persistencia en `IUserLocationService`.
  - `03 Privacidad`: Interruptores reactivos para visibilidad de perfil, colección, partidas y clasificación con persistencia en `IUserPreferenceService`.
  - `04 Conexiones`: Lista de proveedores OAuth con vinculación/desvinculación y estado verificado en `IAccountConnectionsService`.
- **Accesos directos:** Barra de navegación secundaria accesible para saltar a `/cuenta/ludoteca`, `/cuenta/apariencia`, `/cuenta/pais`, etc.

### 4. Mi Ludoteca (`MyLibrary.razor`)
- **Métricas monumentales:** Indicadores numéricos planos de colección (En mi ludoteca, Partidas, Este mes, Prestados) con borde superior `border-t-[3px] border-[var(--rule)]`.
- **Acceso rápido a estantería:** Botón de cabecera `Ver como estantería` en mostaza y barra de filtros limpios.

### 5. Perfil Público (`PublicProfile.razor`)
- **Hero Oliva Editorial:** Fondo `#3A4D39` con avatar circular rotado -6º y medalla de rango rotada +6º.
- **Métricas en Línea:** 5 indicadores planos (En ludoteca, Partidas, Este mes, Prestados, Deseados).
- **5 Secciones de Contenido:** 01 ADN lúdico, 02 Mesa ideal con histograma de curva de jugadores, 03 Horas en estantería con autores y editoriales favoritas, 04 Hitos con medallas y barra de progreso porcentual, y 05 Estantería física con filtro en vivo y tarjetas con rotación sutil.

### 6. Panel de Gestión («Ludeka Gestión» — `AdminLayout.razor` y `AffiliatesAdmin.razor`)
- **Shell de Operaciones:** Cabecera fija de 56px, barra lateral con 6 grupos desaturados (`General`, `Moderación`, `Catálogo`, `Comercial`, `Comunicación`, `Administración`) y atajos globales.
- **Feeds y Afiliados (`AffiliatesAdmin.razor`):** Cabecera limpia con categoría `Comercial`, botones redondeados `+ Registrar feed` y `Sincronizar Feeds Activos`, pestañas tipo píldora (`Fuentes de Catálogo` y `Discrepancias EAN`), puntos de estado (`●` verde, rojo o gris), switches compactos y resolución en 1 clic de discrepancias EAN.

---

## 3. Verificación de Pruebas

- **Total de pruebas unitarias ejecutadas:** 2.627
- **Resultado:** 2.627 superadas, 0 con error, 0 omitidas.
- **Contratos específicos verificados:**
  - `AffiliatesAdminWebTests`: 5/5 superadas.
  - `AccountMenuContractTests`: 5/5 superadas.
  - `AccountAreaContractTests`: 8/8 superadas.
  - `WebMarkupContractTests`: 100% superadas.
  - `MobileNavigationContractTests`: 100% superadas.
  - `CatalogFilterContractTests`: 100% superadas.
  - `AccountPreferencesContractTests`: 100% superadas.
