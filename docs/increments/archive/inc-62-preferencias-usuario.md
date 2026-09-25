# INC-62: Preferencias de Usuario — Apariencia, País y Privacidad

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-25  
> **Fecha de Cierre:** 2026-09-25  
> **Rama de Trabajo:** `inc/preferencias-usuario`  
> **Worktree:** `C:\repos\ludeka-wt\preferencias-usuario`  
> **Dependencias:** INC-50 (Área de Cuenta), INC-61 (menú de cuenta)  
> **Especificación Viva:** [39. Preferencias de Usuario, Privacidad de Perfil y Navegación de Cuenta](file:///c:/repos/Ludeka/docs/specs/sistema/39-preferencias-de-usuario-privacidad-y-navegacion.md)  
> **Total Verificado:** 1.703 pruebas unitarias en verde  

---

## 1. Contexto y Descubrimiento

El modelo de preferencias persistía tema y país, pero el usuario no disponía de una pantalla dedicada para configurarlos: el país se captaba únicamente por un modal emergente y el tema visual no estaba expuesto en la interfaz de cuenta. Adicionalmente, el mantenedor solicitó:
1. Retirar el enlace «Mi Ludoteca» de la barra superior de cabecera (accesible desde el menú de cuenta).
2. Retirar el selector de país de la cabecera (accesible desde el menú de cuenta).
3. Eliminar la opción intermedia «Área de Cuenta» (`/cuenta`) del desplegable de cuenta por redundante.
4. Separar las vistas de Apariencia y País en ventanas independientes (`/cuenta/apariencia` y `/cuenta/pais`) junto a la ludoteca (`/cuenta/ludoteca`) y privacidad (`/cuenta/privacidad`).
5. Añadir opción persistente para ocultar el perfil público de usuario ante la comunidad.

---

## 2. Unidades de Trabajo Implementadas

### Unidad 1: Modelo y Persistencia de Visibilidad de Perfil (`875206a`)
- Entidad de dominio `UserPreference` con propiedad `HidePublicProfile` y método `SetProfileVisibility(bool)`.
- DTOs y contratos `IUserPreferenceService` (`IsPublicProfileHiddenAsync`, `SetPublicProfileHiddenAsync`).
- Implementación `SqliteUserPreferenceService` y migración DDL en `SqliteSchemaMigrator`.
- Pruebas unitarias de dominio y servicio en verde.

### Unidad 2: Pantallas Dedicadas y Guarda de Perfil Privado (`13c3b65`)
- Creación de `/cuenta/apariencia` (`AccountAppearance.razor`): selector de los 5 temas con `NormalizeTheme`.
- Creación de `/cuenta/pais` (`AccountCountry.razor`): selector de territorio con `CountryCatalog` y detección.
- Creación de `/cuenta/privacidad` (`AccountPrivacy.razor`): toggle reactivo para ocultar perfil y previsualización.
- Guarda en `PublicProfile.razor`: aviso sobrio para visitantes y banner con enlace de gestión para el dueño.
- Pruebas de contrato en `AccountPreferencesContractTests.cs`.

### Unidad 3: Limpieza de Cabecera y Topología de Navegación (`cc5fd7d`)
- `MainLayout.razor`: Retirados los botones directos de país y ludoteca de la cabecera.
- `AccountMenu.razor`: Retirada la opción intermedia `/cuenta`; incorporados 7 destinos directos accesibles.
- `AccountSectionNav.razor`: Enriquecida la barra compartida con las 5 secciones de cuenta.
- `Account.razor`: Actualizadas las tarjetas del hub hacia las rutas dedicadas.
- `AccountMenuContractTests.cs`, `AccountAreaContractTests.cs` y `WebMarkupContractTests.cs` actualizados y en verde.

---

## 3. Criterios de Aceptación y Verificación

1. El usuario cambia tema, país y visibilidad de perfil desde vistas estables y dedicadas; los cambios sobreviven a recarga y nuevo login.
2. `NormalizeTheme` y `CountryCatalog` se mantienen como fuentes únicas de verdad.
3. Se eliminan las redundancias en cabecera y en el menú de cuenta.
4. Suite completa de 1.703 pruebas unitarias al 100% en verde.
