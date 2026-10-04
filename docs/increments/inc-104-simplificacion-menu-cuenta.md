# INC-104: Simplificación del Menú de Usuario en Cabecera y Retirada de Submenú Redundante de Cuenta

## 1. Contexto y Motivación
El menú desplegable de usuario en la cabecera superior (`AccountMenu.razor`) ya centraliza y expone directamente todos los destinos canónicos de cuenta (Perfil Público, Mi Ludoteca, Apariencia & Tema, Ubicación & País, Privacidad, Conexiones OAuth y Cerrar Sesión).

A raíz de ello:
1. El botón disparador mostraba el avatar con inicial, nombre del usuario y flecha desplegable chevron, ocupando espacio visual innecesario en la barra de navegación cuando el usuario prefiere un acceso discreto con el típico icono de usuario (`user`), manteniendo la información de usuario y opciones dentro del desplegable al abrirse.
2. Al navegar a cualquier sección de cuenta (`/cuenta/ludoteca`, `/cuenta/apariencia`, `/cuenta/pais`, `/cuenta/privacidad`, `/cuenta/conexiones`), se mostraba una barra horizontal secundaria de píldoras (`AccountSectionNav`) con todas las opciones que resultaba redundante con el propio menú superior.

## 2. Alcance de los Cambios
1. **`AccountMenu.razor`**:
   - Sustitución del botón disparador por un botón circular compacto accesible (`w-8 h-8 rounded-full`) con el icono de usuario típico (`<Icon Name="user" Size="16" />`), atributos ARIA (`aria-label`, `aria-haspopup="menu"`, `aria-expanded`), `title` con el nombre del usuario y preservación del punto indicador si hay correo pendiente de verificación.
   - Retirada de la propiedad no utilizada `UserInitial`.
   - Se mantiene íntegro el menú desplegable al hacer clic con el nombre de usuario, rol y todos los enlaces directos.
2. **Retirada de submenú en pantallas de cuenta**:
   - Eliminación del montaje `<AccountSectionNav>` en `MyLibrary.razor`, `AccountAppearance.razor`, `AccountCountry.razor`, `AccountPrivacy.razor` y `AccountConnections.razor`.
   - Eliminación del fichero obsoleto `src/Ludeka.Web/Components/Shared/AccountSectionNav.razor`.
3. **Pruebas de Contrato y Regresión**:
   - Actualización de `AccountMenuContractTests.cs`, `AccountAreaContractTests.cs` y `AccountPreferencesContractTests.cs` para validar el nuevo disparador compacto y la ausencia de submenú redundante.

## 3. Verificación
- 2.340 pruebas unitarias superadas al 100% en `tests/Ludeka.UnitTests/`.
