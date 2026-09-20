# INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Estado:** ⏳ En progreso (contrato SDD cerrado el 2026-09-20)
> **Fecha de Inicio:** 2026-09-20
> **Rama de Trabajo:** `inc/autenticacion-en-el-despliegue`
> **Worktree:** `C:\repos\ludeka-wt\autenticacion-en-el-despliegue`
> **Dependencias:** INC-46 (Autenticación Real, archivado), INC-47 (Trabajos en Cloud Run, archivado), INC-48 (Persistencia de Producción, archivado), INC-49 (Vinculación de Cuentas, archivado)
> **Especificación Viva:** [32. Autenticación y Autorización](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md) · [35. Persistencia de Producción y Medios](file:///c:/repos/Ludeka/docs/specs/sistema/35-persistencia-produccion-y-medios-con-fallback.md)

---

## 1. Cómo se descubrió

El maintainer, tras archivar INC-48, pidió la lista completa de variables que debía informar para desplegar en producción por primera vez. Al elaborarla contra el código —no contra la documentación— apareció un hueco que no figuraba en ningún documento del repositorio:

**El pipeline de despliegue no inyecta ninguna credencial de autenticación.**

Tirando de ese hilo, la exploración destapó **tres defectos bloqueantes independientes**. Ninguno provoca un fallo de arranque, ninguno aparece en `/healthz` ni en `/ready`, y ninguno estaba registrado. El primer despliegue habría producido una web que levanta, se ve bien y en la que **nadie puede entrar, incluido el propio maintainer**.

---

## 2. Los cinco defectos

| Id | Severidad | Defecto | Consecuencia real |
|---|---|---|---|
| **B1** | 🔴 Bloqueante | `UseForwardedHeaders` no existe en ningún `.cs`. Cloud Run termina TLS y reenvía HTTP interno; sin procesar `X-Forwarded-Proto`, `Request.Scheme` vale siempre `"http"` | El `redirect_uri` de OAuth sale en `http://` y no coincide con el `https://` registrado. **Los tres proveedores rechazan el desafío, siempre** |
| **B2** | 🔴 Bloqueante | `ci-cd.yml` no define ninguna clave `Authentication__*` ni `AdminUser__*`; `appsettings.json` trae los tres proveedores en `Enabled: false` | Cero proveedores registrados. `Login.razor` no pinta ningún botón. **No existe acceso por contraseña**: los esquemas sociales son el único mecanismo |
| **B3** | 🔴 Bloqueante | `AdminUserSeeder.cs:29-36` retorna en cuanto existe un `FoundingTeam`; la lógica que lee `AdminUser:Email` queda por debajo de ese `return` | El correo del fundador se fija **de forma irreversible** en el primer arranque. Sin la clave en el pipeline, se sembraría `admin@ludeka.es` para siempre |
| **B4** | 🟠 Importante | `GetConfigurationWarnings` hace `continue` si el proveedor no está habilitado | Con los tres deshabilitados, **cero líneas** en los registros advierten de que el acceso está inoperativo |
| **B5** | 🟠 Importante | `ROADMAP.md:75` afirma que el PR #60 «está abierto y retenido» | Es falso: se fusionó el 2026-09-19. Haría creer al maintainer que dispone de una red de seguridad que ya no existe |

**Solo B2 era previsible desde la pregunta original.** B1, B3, B4 y B5 los destapó la auditoría.

---

## 3. El bootstrap del fundador, que no estaba escrito en ninguna parte

Es la pregunta central del incremento: **cómo obtiene el maintainer su primer acceso administrativo**. El código la responde sin ambigüedad, pero ningún documento lo recogía. Es conducta emergente entre INC-46 e INC-49:

1. `AdminUserSeeder` crea, en el primer arranque contra base vacía, un `AppUser` con rol `FoundingTeam` y permisos completos, **con cero filas en `ExternalLogins`**. En esa fila no se puede iniciar sesión.
2. La activa la rama **2a** de `ExternalLoginService.ResolveAsync`: un inicio de sesión social con correo **verificado** que coincide con `AdminUser:Email`; el servicio localiza la cuenta, comprueba que no tiene proveedores vinculados, crea el vínculo y devuelve **esa misma cuenta** con sus permisos intactos.

**El primer inicio de sesión social cuyo correo coincida con `AdminUser:Email` es la puerta de entrada del fundador.** Por eso B3 es bloqueante y no cosmético.

---

## 4. Decisiones del maintainer

| Decisión | Elección | Motivo |
|---|---|---|
| Estrategia de dominio | **Dominio propio desde el principio** | Permite registrar las apps OAuth de una sola vez, sin doble pasada |
| Vía de servicio del dominio | **Mapeo directo**, asumiendo la advertencia de Google | Coste cero frente a ~18 USD/mes fijos del balanceador global, que cobra exista o no tráfico y rompería el escalado a cero. Sin tráfico, el riesgo de latencia es bajo y reversible |
| Guarda de identidad | **Que aborte en `Production`**, sin validar formato | Convierte un fallo silencioso e irreversible en uno ruidoso y reversible. Mismo patrón que la guarda de PostgreSQL de INC-48 |
| Dependencia de pruebas | **No se añade `Microsoft.AspNetCore.TestHost`** | El repositorio ya tiene el patrón: `MinimalMediaHostHarness`, de INC-48 |
| Entrega de la documentación | **Fases 7 y 8 fusionadas** | Ambas son prosa y no tocan código ejecutable |
| Contrato SDD | **Un solo PR con `size:exception`** | Precedente del repositorio: los artefactos de planificación de INC-47 e INC-48 se fusionaron con 2.712 y 848 líneas |

---

## 5. Alcance

**Dentro:** `UseForwardedHeaders` con sus pruebas, incluida la negativa; guarda de arranque de identidad; aviso agregado cuando no hay proveedor operativo; cableado de `Authentication__Providers__*` y `AdminUser__*` en el pipeline; documentación del mapeo de dominio y del bootstrap del fundador; corrección de `ROADMAP.md:75` y de `google-cloud-run.md:115`.

**Fuera:** la cascada 2a/2b de INC-49, que funciona y solo carecía de documentación; la revisión de aplicaciones de Meta para Facebook; el quinto Cloud Run Job del boletín semanal.

---

## 6. Huecos de evidencia declarados

1. **No existe entorno de producción.** Nada puede acreditarse contra un despliegue real.
2. **Cloud Run no documenta `X-Forwarded-Proto` para su propio producto.** La página *Container runtime contract* no lo menciona; la única confirmación literal procede de Cloud Run functions, producto hermano. El diseño lo convierte en prueba, no en cita.
3. **El mapeo de dominio está en fase *preview*** y Google lo desaconseja para producción. Decisión consciente, documentada en §4.
4. **Alcanzabilidad del contenedor saltándose el front-end de Google**: no verificada. La mitigación del diseño no descansa en ella.

---

## 7. La puerta de decisión

La **Fase 2** aísla la prueba negativa N2 —que sin vaciar las listas de confianza el esquema no cambia— **antes de escribir una línea de código de producción**. N2 no depende de ese código.

**Si N2 sale roja, vaciar las listas de confianza no era la corrección de B1.** La regla del diseño es entonces parar y volver a `sdd-design`. **No se reescribe la prueba para que pase.**

Un detalle sin el cual N2 no probaría nada: un `HttpClient` contra `127.0.0.1` produce una dirección remota de bucle invertido, que es justo la que se sospecha que las listas por defecto aceptan. El arnés fuerza `RemoteIpAddress` a `203.0.113.10`, de TEST-NET-3.

---

## 8. Entrega

Cadena `stacked-to-main` de **7 PRs**: contrato, y seis rebanadas de implementación. **36 tareas.** Estimación de ~780-1.490 líneas, ninguna rebanada por encima de 400 en su estimación alta.

**Los siete deben estar fusionados antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`**: ese es el gate real del incremento, porque configurar esos dos secretos arma el despliegue automático.
