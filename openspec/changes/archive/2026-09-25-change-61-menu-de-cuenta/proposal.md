# Propuesta SDD — INC-61: Menú de Cuenta y Estado de Sesión en la Cabecera

## 1. Motivación y Contexto

En el estado actual de Ludeka, el usuario que inicia sesión carece de una experiencia de cabecera que refleje su identidad y le permita gestionar sus áreas personales de forma fluida y autónoma.
Tras auditar la cabecera en `MainLayout.razor` y los servicios de identidad:
1. **Falta de menú desplegable de cuenta:** Cuando el usuario está autenticado, la cabecera solo ofrece un botón plano con su nombre que navega directamente al área `/cuenta`. No existe desplegable para saltar de inmediato a opciones clave como su perfil público, su ludoteca, sus preferencias o sus conexiones OAuth.
2. **Ausencia de control de cierre de sesión en la interfaz:** Aunque existe el endpoint HTTP `/logout` en el servidor (`Program.cs:336`), no hay ningún botón o enlace en la cabecera que permita al usuario cerrar sesión.
3. **Desconexión con el perfil público:** El perfil de jugador (`/u/{UserId}`) no está enlazado directamente desde la identidad del usuario en la cabecera.
4. **Tratamiento de avisos de verificación:** `AccountEmailNotice` advierte si la cuenta carece de correo verificado mediante un banner superior; el menú de cuenta debe respaldar este estado con un indicador discreto sin sobrecargar la pantalla.

Este incremento aborda la creación de un componente interactivo y accesible de menú de cuenta (`AccountMenu.razor`), que respeta el principio editorial de la plataforma, cumple las pautas WCAG 2.2 AA y articula de forma coherente las secciones introducidas en INC-50.

---

## 2. Alcance Propuesto

1. **Componente de Menú de Cuenta (`AccountMenu.razor`):**
   - **Estado autenticado:**
     - Botón de cabecera con avatar estilizado (inicial destacada o icono de usuario), nombre de usuario y chevron indicador de menú.
     - Indicador discreto (punto de atención) si el correo no está verificado.
     - Panel desplegable flotante con:
       - Resumen de identidad y rol (Fundador / Moderador si corresponde).
       - Acceso a Perfil Público (`/u/{UserId}`).
       - Acceso al Hub del Área de Cuenta (`/cuenta`).
       - Acceso a Mi Ludoteca (`/cuenta/ludoteca`).
       - Acceso a Preferencias de Apariencia (`/cuenta/ludoteca?seccion=apariencia`).
       - Acceso a Conexiones (`/cuenta/conexiones`), indicando el estado del correo.
       - Botón de Cierre de Sesión (`/logout`).
   - **Estado invitado (sin sesión):**
     - Botón visible y accesible de acceso directo a `/login` con etiqueta "Entrar".

2. **Integración en la Maquetación Global (`MainLayout.razor`):**
   - Sustitución del bloque condicional anterior por `<AccountMenu />`.
   - Compatibilidad total con la barra inferior móvil de INC-60 (`MobileBottomNav`) y los temas del sistema.

3. **Accesibilidad Integral (WCAG 2.2 AA):**
   - Atributos ARIA (`aria-haspopup="menu"`, `aria-expanded`, `role="menu"`, `role="menuitem"`).
   - Manejo de teclado (tecla `Escape` para cerrar, navegación con tabulador).
   - Telón de fondo (*backdrop*) transparente para captura de clics exteriores.

4. **Batería de Pruebas Automatizadas:**
   - Pruebas unitarias y de marcado semántico en `AccountMenuContractTests.cs`.
   - Garantía de mantenimiento del 100% de la suite en verde.

---

## 3. Criterios de Aceptación

1. Con sesión activa, la cabecera muestra el control de cuenta interactivo con el nombre y avatar/inicial del usuario.
2. Al pulsar el control de cuenta, se despliega el menú con los 6 destinos canónicos sin rutas huérfanas:
   - `/u/{UserId}` (Perfil público)
   - `/cuenta` (Área de cuenta / hub)
   - `/cuenta/ludoteca` (Ludoteca)
   - `/cuenta/ludoteca?seccion=apariencia` (Preferencias de tema y visualización)
   - `/cuenta/conexiones` (Gestión de proveedores)
   - `/logout` (Cerrar sesión)
3. Sin sesión activa (invitado), la cabecera expone un enlace directo y visible a `/login` con el texto "Entrar".
4. El menú se cierra de forma predecible al pulsar la tecla `Escape`, al hacer clic fuera del panel o al cambiar de ruta.
5. El cierre de sesión mediante `/logout` destruye la cookie de sesión y restablece el estado anónimo de la aplicación de forma segura.
6. La suite completa de pruebas de Ludeka supera el 100% de las pruebas con cero fallos (`dotnet test Ludeka.sln`).
