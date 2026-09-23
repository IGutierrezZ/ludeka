# INC-50: Área de Cuenta — Puerta de Acceso en la Cabecera y Hub del Usuario

> **Estado:** ✅ Archivado (completado y verificado el 2026-09-23)
> **Fecha de Finalización:** 2026-09-23
> **Rama de Trabajo:** `inc/area-de-cuenta`
> **Worktree:** `C:\repos\ludeka-wt\area-de-cuenta`
> **Dependencias:** INC-46 (Autenticación Real, archivado), INC-49 (Vinculación de Cuentas, archivado)
> **Especificación Viva:** [32. Autenticación y Autorización](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md)

---

## 1. Cómo se descubrió

Durante el smoke test manual de INC-49 (tarea 4.5), el maintainer entró con Google correctamente y entonces se topó con lo siguiente: **no había forma de llegar a su cuenta**. Ni a `/cuenta/conexiones`, ni a ninguna vista de usuario. Sus palabras:

> «me he podido loguear con google, pero luego no tengo acceso a mi cuenta y menos a cuenta/conexiones, no veo donde entrar, falta el acceso»

No es una percepción: es un agujero verificado en el código.

## 2. El agujero, verificado

### 2.1 La pantalla de conexiones es inalcanzable para quien tiene correo verificado

El **único** enlace a `/cuenta/conexiones` en toda la interfaz vive en `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor:21`, dentro del aviso de cabecera que INC-49 construyó.

Y ese aviso, **por diseño**, solo se renderiza cuando la cuenta **no** tiene correo verificado (decisión 3 de INC-49). Google y Discord entregan correo verificado, así que para la inmensa mayoría de las cuentas el aviso nunca aparece — y con él desaparece la única puerta.

Resultado: **`/cuenta/conexiones` solo es alcanzable escribiendo la URL a mano.** La verificación de INC-49 no lo detectó porque sus 35 escenarios comprueban que el aviso aparece cuando debe y que la página responde, nunca que exista un camino hasta ella.

### 2.2 No hay ningún enlace a `/login` en la interfaz

Búsqueda de `href="/login"` en todo `src/Ludeka.Web`: **cero resultados**. La página de acceso que entregó INC-46 tampoco tiene puerta. Se llega por redirección (cuando una página con `[Authorize]` rechaza a un anónimo) o escribiendo la URL.

### 2.3 La cabecera no sabe si hay sesión

`src/Ludeka.Web/Components/Layout/MainLayout.razor` no consulta `HasSession` ni `IsAuthenticated` en **ningún** punto (0 coincidencias). Renderiza bloques condicionales por **rol y permiso** para las herramientas de moderación, pero no distingue entre «visitante» y «persona con sesión». Su único enlace de usuario es la píldora `/mi-ludoteca`, idéntica para todo el mundo.

### 2.4 Lectura de fondo

INC-46 construyó el **mecanismo** de acceso. INC-49 construyó la **pantalla** de conexiones. Ninguno de los dos construyó la **puerta**. Cada incremento cumplió su alcance y el hueco cayó justo entre ambos: es un fallo de costura, no de ejecución.

## 3. Lo que pide el maintainer

Textualmente:

> «entiendo que ese botón será el típico de persona donde si estoy logueado me va a mi cuenta, y si no lo estoy me lleva al login o crear cuenta.
> dentro de mi cuenta tendré mi ludoteca y demás cosas que se configuran a nivel de usuario, cuando doy a mi ludoteca simplemente será ir a la cuenta y a la sección de mi ludoteca directamente.»

De ahí salen tres piezas.

### 3.1 Botón de persona en la cabecera

Un único punto de entrada, con dos comportamientos según el estado de sesión:

- **Con sesión**: lleva al área de cuenta. Muestra identidad reconocible (nombre o avatar del proveedor).
- **Sin sesión**: lleva a `/login`.

Es el patrón universal y es lo que cierra a la vez 2.1, 2.2 y 2.3.

### 3.2 Área de cuenta como hub del usuario

Una zona bajo `/cuenta` que agrupa **todo lo que se configura a nivel de usuario**, con secciones. Como mínimo:

- **Mi ludoteca** (hoy `/mi-ludoteca`, página suelta).
- **Conexiones de acceso** (`/cuenta/conexiones`, ya entregada por INC-49 — aquí solo se integra).
- El resto de ajustes de usuario que hoy estén dispersos: hay que inventariarlos en la exploración, no suponerlos.

### 3.3 «Mi ludoteca» pasa a ser una sección del área

El maintainer lo dice explícitamente: pulsar «Mi ludoteca» debe ser **entrar en la cuenta y caer directo en esa sección**, no navegar a una página independiente.

**Cuidado aquí:** `/mi-ludoteca` es una ruta viva, referenciada desde la cabecera y muy probablemente desde otros sitios. La exploración debe inventariar quién la enlaza, y el diseño decidir si se conserva como alias que redirige o si se reescriben las referencias. **Ninguna URL que hoy funcione debe romperse en silencio.**

## 4. Alcance y decisiones que hay que tomar

Este documento **no** decide el diseño. Son decisiones para las fases de exploración y propuesta:

1. **Topología de la navegación.** ¿Pestañas dentro de una única página de cuenta, o rutas hijas (`/cuenta/ludoteca`, `/cuenta/conexiones`) con una cabecera de sección compartida? Afecta al patrón de página con `[Authorize]` que INC-49 estrenó.
2. **Destino del anónimo.** El botón sin sesión lleva a `/login`; ¿y si alguien escribe `/cuenta` sin sesión? Ya existe el mecanismo `AuthorizeRouteView` → `RedirectToLogin`, pero conviene fijar si se conserva la intención de destino para devolverlo donde iba.
3. **Compatibilidad de `/mi-ludoteca`.** Alias con redirección permanente o reescritura de referencias. Hay que inventariar primero.
4. **Identidad visible en la cabecera.** ¿Solo icono, icono con nombre, o avatar del proveedor? El avatar implica decidir si se guarda la URL de la foto del proveedor, lo que es alcance nuevo de datos y de privacidad, no solo de interfaz.
5. **Inventario de ajustes de usuario existentes.** Qué hay hoy disperso que deba entrar en el hub (tema visual, país, preferencias). `UserPreference` existe en el dominio; hay que ver qué expone ya la interfaz y desde dónde.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

- **Perfil público.** `PublicProfile.razor` es lo que ven los demás; esto es el área privada. No se mezclan.
- **Herramientas de moderación y administración.** Ya tienen su sitio en la cabecera, gobernado por permisos.
- **Cualquier cambio en el mecanismo de acceso de INC-46 o en la lógica de conexiones de INC-49.** Este incremento construye la puerta, no toca lo que hay detrás.

## 6. Criterios de aceptación

1. Existe un punto de entrada de cuenta **visible en la cabecera** en todas las páginas.
2. Con sesión iniciada, ese punto lleva al área de cuenta y muestra identidad reconocible.
3. Sin sesión, lleva a `/login`. **Hoy no hay ningún enlace a `/login` en la interfaz; tras este incremento sí.**
4. `/cuenta/conexiones` es alcanzable navegando, **sin escribir la URL y sin depender de que aparezca el aviso de correo no verificado**.
5. «Mi ludoteca» se alcanza desde el área de cuenta como sección.
6. **Ninguna URL que hoy funcione deja de funcionar**, `/mi-ludoteca` incluida.
7. WCAG 2.2 AA: el punto de entrada es accesible por teclado con foco visible, y su estado (con sesión / sin sesión) no se comunica solo por color o solo por icono.
8. Suite completa en verde con `dotnet test Ludeka.sln`. Línea base al abrir este incremento: **1.622 pruebas unitarias + 10 de integración**.

## 7. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Reubicar `/mi-ludoteca` rompe enlaces vivos dentro y fuera de la aplicación | Alto | Inventariar referencias en exploración; conservar alias con redirección salvo decisión explícita en contra; prueba de que la ruta antigua sigue resolviendo |
| Tocar `MainLayout.razor` afecta a todas las páginas | Medio | Es el fichero con más alcance transversal del proyecto; cambio mínimo y prueba de contrato de fuente, como hizo INC-49 con `AccountEmailNotice` |
| La cabecera tendría que consultar la sesión, y `ICurrentUserService` tiene la superficie **congelada** por `CurrentUserContractTests.cs:41-59` con `Assert.Equal` exacto, implementada por 16 clases | Medio | `ICurrentUserService` ya expone `UserId` y `UserName`: **debería bastar sin ampliar el contrato**. Si el diseño cree que hace falta más, seguir el precedente de INC-49 y crear un contrato aparte, nunca ampliar el congelado |
| Añadir avatar del proveedor arrastra alcance de datos y privacidad | Medio | Decisión 4 de la sección 4: si se elige avatar, se trata como alcance propio con su propia justificación |

## 8. Pendiente heredado de INC-49

El **smoke test manual de INC-49 (tarea 4.5) sigue sin completar**, y fue precisamente lo que destapó este incremento. Quedó en el paso 2 de 7: la sesión con Google funcionó, pero no había forma de navegar a `/cuenta/conexiones` para continuar.

Se terminará en la sesión de este incremento. Las credenciales OAuth de Google y Discord **ya están configuradas** en el almacén de user-secrets (`ludeka-web-user-secrets-2026`), verificado el 2026-09-18: `/login` ofrece los dos botones correctamente.

Para arrancar la aplicación con HTTPS en el puerto que esperan las URIs de redirección registradas:

```
cd C:\repos\Ludeka
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="https://localhost:7291;http://localhost:5081" dotnet run --project src/Ludeka.Web --no-launch-profile
```

**No** uses la vista previa con `.claude/launch.json`: impone su propio `--urls http://0.0.0.0:<port>` después de los argumentos propios y sirve HTTP plano donde se espera TLS. El síntoma engaña —el puerto escucha, el certificado es válido y confiado— y la línea que lo delata es `Now listening on: http://…` en el log.

Recordatorio: el arranque tarda minutos porque `PriceRadarHostedService` rastrea tiendas antes de atender, y hay que **parar el servidor antes de ejecutar `dotnet test`** o bloqueará `bin/Debug` con `MSB3027`.
