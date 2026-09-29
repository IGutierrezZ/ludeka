# Incremento 84: Tema Madera Clara por Defecto y Selector Minimalista para Visitantes

> **ID:** INC-84  
> **Slug:** `tema-madera-clara-invitados`  
> **Rama:** `inc/tema-madera-clara-invitados`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 39 (`docs/specs/sistema/39-preferencias-de-usuario-privacidad-y-navegacion.md`), `src/Ludeka.Web/Components/App.razor`, `src/Ludeka.Web/Components/Layout/MainLayout.razor`, `src/Ludeka.Web/Components/Shared/GuestThemePicker.razor`  
> **Dependencias:** INC-62 (Preferencias de Usuario), INC-36 (Fundaciones Editoriales y Temas).

---

## 1. Contexto y Diagnóstico

1. **Tema por Defecto para Visitantes:**
   - La plataforma arrancaba históricamente con el tema Carbón Oscuro (`charcoal`) por defecto cuando no había sesión iniciada ni preferencia en `localStorage`.
   - La identidad editorial de Ludeka ("tu mesa", calidez hogareña y madera) se beneficia de un tema predeterminado en Madera Clara (`wood`), más luminoso, acogedor y cercano al mundo de los tableros de mesa nórdicos y fichas de madera.
2. **Ausencia de Selector de Tema para Usuarios No Autenticados:**
   - En la cabecera principal, los usuarios anónimos únicamente contaban con el botón «Entrar» (`/login`).
   - El selector de apariencia existía exclusivamente dentro del área autenticada (`/cuenta/apariencia`), imposibilitando a los visitantes explorar la web con su paleta preferida (blanco papel, madera clara, madera oscura, azul medianoche o carbón) sin antes crear una cuenta e iniciar sesión.
3. **Fricción por Redirección Involuntaria al Login:**
   - El método `SwitchTheme` en `MainLayout.razor` invocaba incondicionalmente a `PreferenceService.SetUserThemeAsync`, lanzando `UnauthorizedAccessException` ante identidades anónimas y redirigiendo forzosamente al usuario a `/login` al intentar cambiar de color localmente.

---

## 2. Objetivos y Solución Implementada

1. **Configuración de Madera Clara (`wood`) como Tema Predeterminado:**
   - Inyección directa del atributo `<html lang="es" data-theme="wood">` en `src/Ludeka.Web/Components/App.razor` para garantizar renderizado Zero-FOUC con el logotipo adaptativo `logo-light.png` en el primer byte servido por SSR.
   - Ajuste de los valores de repliegue (*fallback*) a `'wood'` tanto en el script de arranque en `<head>` como en las funciones JavaScript globales `getLudekaTheme` y en el estado inicial de Blazor en `MainLayout.razor`.
2. **Componente de Cabecera para Visitantes (`GuestThemePicker.razor`):**
   - Nuevo componente Blazor interactivo montado en la barra superior junto al botón «Entrar», visible únicamente si `!HasSession`.
   - Interfaz minimalista sin texto: botón disparador con icono de paleta (`Icon Name="palette"`) y punto indicador del color actual, que despliega un menú flotante con las 5 muestras circulares de color puro:
     - **Blanco** (`editorial`): fondo marfil `#F8F7F4` con acento rojo tinta `#B8432F`.
     - **Madera Clara** (`wood`): fondo arce `#F5EFEB` con acento ámbar tostado `#B85323`.
     - **Madera Oscura** (`tabletop`): fondo chocolate profundo `#241D1A` con acento caramelo `#D97736`.
     - **Azul Oscuro** (`midnight`): fondo noche `#10182D` con acento cian `#06B6D4`.
     - **Carbón** (`charcoal`): fondo pizarra `#1F262D` con acento terracota `#E05A38`.
   - Cumplimiento estricto **WCAG 2.2 AA**: roles `menu` y `menuitem`, `aria-label` descriptivos en cada muestra, indicador visual `ring-2` para el tema activo, soporte de teclado (`Escape`) y telón de descarte en clic exterior.
3. **Conmutación Local Segura y Fluida:**
   - En `MainLayout.razor`, `SwitchTheme` discrimina si existe sesión: para visitantes actualiza inmediatamente el DOM y `localStorage` sin llamar a la base de datos ni provocar redirecciones al login; para usuarios autenticados continúa sincronizando la preferencia en la base de datos SQLite/PostgreSQL.
4. **Verificación Automática:**
   - Creación de la suite de pruebas de contrato `GuestThemePickerContractTests.cs` (4 pruebas unitarias) que custodian el tema predeterminado en `App.razor`, el montaje en `MainLayout.razor` y la accesibilidad semántica de `GuestThemePicker.razor`.
   - Validación completa de la suite de pruebas unitarias: **2.113 pruebas superadas al 100%**.

---

## 3. Criterios de Aceptación y Resultados (TDD)

- **Criterio 1:** Un usuario nuevo o en modo incógnito visualiza la web con el tema Madera Clara (`wood`) de inmediato, sin parpadeos ni fondos desalineados con el logotipo. (Verificado).
- **Criterio 2:** En la cabecera aparece un selector de tema discreto junto a «Entrar» únicamente para usuarios sin sesión. (Verificado con `MainLayout_ShouldMountGuestThemePickerOnlyWhenNoSession`).
- **Criterio 3:** Al desplegar el selector se ofrecen los 5 temas canónicos como muestras de color sin texto, conmutando la estética de forma instantánea al pulsar cualquiera de ellos sin redirigir al login. (Verificado con `GuestThemePicker_ShouldDeclareAllFiveThemesWithoutTextLabels` y `MainLayout_SwitchTheme_ShouldNotCallDatabaseOrRedirectWhenGuest`).
- **Criterio 4:** Suite unitaria completa pasando al 100% (2.113 pruebas superadas, 0 fallos).
