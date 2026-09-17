# INC-49: Vinculación de Cuentas entre Proveedores, Recuperación de Acceso y Política de Correo Ausente

> **Estado:** ✅ Archivado (2026-09-18) — verificado con `pass_with_warnings`, 35/35 escenarios conformes, 0 hallazgos CRITICAL, 1 WARNING, 2 SUGGESTION; suite final 1.417/1.417 (línea base 1.345). Smoke test manual de navegador pendiente (tarea 4.5, exige credenciales OAuth reales no disponibles en este entorno).
> **Fecha de Inicio:** 2026-09-16
> **Rama de Trabajo:** `inc/vinculacion-cuentas` (cadena de 7 PRs encadenados, #23-#29, todos mergeados a `main`)
> **Worktree:** `C:\repos\ludeka-wt\vinculacion-cuentas`
> **Dependencias:** INC-46 (Autenticación Real, archivado)
> **Especificación Viva del Sistema:** [32. Autenticación Social, Autorización por Permisos y Política de Anonimia](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas entre Proveedores y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md)

---

## 1. Motivación y Visión

INC-46 resolvió el acceso: hoy se entra con Google, Discord o Facebook en un clic, sin cuentas con correo y contraseña. Pero dejó abierta una consecuencia que este incremento debe cerrar.

### El problema, paso a paso

El acceso funciona así: el proveedor nos dice quién es el usuario y, **si quiere**, nos da su correo. La vinculación con una cuenta de Ludeka ocurre en cascada:

1. Por el par `(Provider, ProviderKey)` — la identidad única en ese proveedor. Es la vía normal.
2. Si no existe, por el **correo verificado** que entregue el proveedor.
3. Si tampoco, se crea una cuenta nueva como `CommunityUser`.

El caso 3 tiene una particularidad: `AppUser.Email` es un campo **obligatorio** — la entidad de dominio lanza excepción si se intenta crear un usuario sin correo (`Core/Entities/AppUser.cs:40-41`). Es correcto: el correo es la forma natural de identificar a una persona.

Pero si el proveedor **no entrega correo** —Facebook lo omite si el usuario no autoriza el permiso, o si la aplicación no ha pasado la revisión de Meta— no hay correo que guardar. La solución actual, deliberadamente conservadora, es un correo sintético en el dominio reservado `.invalid` (`ExternalLoginService.PlaceholderEmailDomain = "ludeka.invalid"`).

**La consecuencia:** esa cuenta queda archivada bajo `discord_12345@ludeka.invalid`, no bajo el correo real de la persona. Si más adelante esa misma persona entra con Google y Google sí entrega su correo verificado, el paso 2 **no encontrará nada**, porque su cuenta está bajo la dirección sintética. Resultado: **una segunda cuenta**, y con ella la ludoteca, los préstamos y el diario de partidas **partidos en dos**.

### Por qué no se fusiona automáticamente

Lo tentador sería "si el correo coincide, fusiono". Es exactamente el vector de robo de cuenta: si alguien puede registrarse en un proveedor con `ana@gmail.com` sin que ese correo esté verificado, accedería a la cuenta de Ana en Ludeka. La regla "solo se fusiona con correo verificado" no es pedantería: es la frontera de seguridad.

### Qué falta y por qué importa

Falta **la pantalla de vinculación explícita**, que es el estándar de la industria. Y no es una comodidad: es **el mecanismo de recuperación**. Sin correo, alguien que pierda su cuenta de Discord pierde su cuenta de Ludeka para siempre. Vincular un segundo proveedor es la única red de seguridad posible cuando se ha decidido no usar correo.

---

## 2. Objetivos y Alcance Técnico

### 2.1. Pantalla de conexiones (`/cuenta/conexiones`)

Página para usuarios con sesión iniciada donde se ve qué proveedores están vinculados y se pueden vincular o desvincular.

- Listado de los proveedores habilitados por configuración, indicando cuáles están ya vinculados a la cuenta.
- Botón de vincular para los no vinculados. La operación es un desafío OAuth normal; al volver, el proveedor se asocia a la cuenta **en sesión**.
- Botón de desvincular para los vinculados.
- La vinculación desde esta pantalla **no crea cuenta nueva jamás**: asocia la identidad externa a la cuenta autenticada.

### 2.2. Guarda del último método de acceso

- **No se puede desvincular el último método de acceso.** Si es el único, el botón se deshabilita y se explica por qué.
- Tampoco se puede desvincular si la cuenta no tiene ninguna otra vía de recuperación.
- Es la regla que evita que alguien se deje fuera de su propia cuenta para siempre.

### 2.3. Aviso visible en cuentas sin correo verificado

- La cuenta debe poder distinguir si tiene algún correo verificado o solo el sintético.
- En `/cuenta/conexiones` y en el perfil, aviso explícito: *"Tu cuenta no tiene un correo verificado. Vincula otro proveedor de acceso para no perderla."*
- El objetivo es que la persona entienda el riesgo **antes** de necesitar la recuperación, no después.

### 2.4. Flujo de colisión: avisar, nunca fusionar en silencio

Cuando alguien inicia sesión con un proveedor y ocurre alguna de estas situaciones:

- El correo verificado del proveedor ya pertenece a **otra** cuenta de Ludeka, o
- La cuenta existente ya tiene otros proveedores vinculados y la identidad entrante es nueva,

**no se fusiona automáticamente**. Se muestra un mensaje que explica la situación y ofrece la salida correcta:

> *"Ya existe una cuenta con este correo. Inicia sesión con el método que ya usas y vincula este proveedor desde Ajustes → Conexiones."*

Se sigue el patrón de Slack y Notion: la fusión siempre la inicia el usuario desde una sesión ya establecida.

**Excepción segura y ya implementada:** si el proveedor entrega un correo **verificado** que coincide con una cuenta que aún no tiene proveedores vinculados, la vinculación automática es correcta y se conserva.

### 2.5. Modelo de datos

Hay que poder responder "¿esta cuenta tiene algún correo verificado?" sin ambigüedad. Opciones a resolver en diseño:

- Una bandera explícita en `AppUser`, o
- Derivarlo de `ExternalLogin`: una cuenta está "verificada" si tiene al menos una fila con `ProviderEmail` no nulo y verificada.

La segunda opción no requiere migración destructiva y **es la preferida**. Debe decidirse en la fase de diseño, junto con el tratamiento del correo sintético en el momento en que la cuenta sí recibe un correo verificado (¿se reemplaza? ¿se conserva el sintético como histórico?).

### 2.6. Recuperación de acceso

- La pantalla de conexiones es la vía principal de recuperación.
- Si una persona ya quedó con dos cuentas por este motivo, este incremento debe dejar resuelto al menos el camino: iniciar sesión con una y vincular la otra **no es posible** (los proveedores ya están repartidos). Se debe decidir en diseño si se ofrece una **herramienta de fusión asistida por un administrador** (con auditoría) o si se documenta el procedimiento manual.

### 2.7. Configuración

Reutiliza `Authentication__Providers__*` de INC-46. No introduce credenciales nuevas.

### 2.8. Pruebas (Strict TDD activo)

Runner contractual: `dotnet test Ludeka.sln`. Cobertura mínima:

- No se puede desvincular el último método de acceso.
- Vincular desde una sesión activa asocia la identidad a esa cuenta y **no crea una nueva**.
- Un intento de inicio de sesión cuya identidad ya pertenece a otra cuenta **no fusiona** y muestra el aviso correcto.
- Un correo verificado que coincide con una cuenta sin proveedores vinculados sí vincula automáticamente.
- Una cuenta sin correo verificado muestra el aviso correspondiente.

---

## 3. Decisiones pendientes para el maintainer

1. **Fusión asistida de cuentas ya duplicadas.** ¿Se construye una herramienta de administrador con auditoría, o se documenta el procedimiento manual? Afecta al alcance: la herramienta es trabajo real, el procedimiento manual no.
2. **Reemplazo del correo sintético.** Cuando una cuenta creada con correo sintético recibe después un correo verificado, ¿se reemplaza el sintético o se conserva? Reemplazarlo es más limpio; conservarlo preserva el rastro de cómo nació la cuenta.
3. **Visibilidad del aviso.** ¿Solo en `/cuenta/conexiones` o también en el perfil público y en la cabecera?

---

## 4. Criterios de Aceptación y Verificación

1. Existe una pantalla de conexiones donde el usuario ve y gestiona sus proveedores vinculados.
2. Es imposible desvincular el último método de acceso.
3. Vincular desde la sesión nunca crea una cuenta nueva.
4. Un inicio de sesión en colisión **no fusiona en silencio** y explica al usuario qué hacer.
5. Una cuenta sin correo verificado lo advierte de forma visible.
6. Suite completa en verde con `dotnet test Ludeka.sln`.
7. Smoke test con navegador real del ciclo vincular → desvincular → intento de desvincular el último.

---

## 5. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Un error en la guarda del último método deja cuentas inaccesibles | Alto | Prueba dedicada obligatoria y comprobación en servidor, no solo en la interfaz |
| La vinculación desde la sesión abre una vía de apropiación si no valida bien la identidad entrante | Alto | El desafío OAuth lo emite el proveedor; se asocia siempre a la cuenta autenticada y se audita |
| Detectar "correo verificado" de forma ambigua | Medio | Derivarlo de `ExternalLogin`, con prueba que fije el criterio |
| El aviso de cuenta sin correo puede percibirse como alarma innecesaria | Bajo | Redacción clara y accionable, sin lenguaje técnico |
| Cambios en `ExternalLogin` afectan a INC-46 ya archivado | Medio | La tabla ya existe; este incremento no debe requerir migración destructiva |
