```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:5a79021df56f2f682e4219269d74e44ec9b6baa2694c695acb7ea255954dcc13
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 6/6
scenarios: 17/17
test_command: dotnet test Ludeka.sln
test_exit_code: 0
test_output_hash: sha256:59b3f4a0aae572ef0051e9651cc00309c70d2f1060b153967826ed23e5943531
build_command: dotnet build Ludeka.sln
build_exit_code: 0
build_output_hash: sha256:fbb69171a0bd7bc6479891e0f3f58b925d456963f219f0568239b814c67da0a0
```

# Informe de Verificación — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-verify` · **Fecha:** 2026-09-21
> **Estado verificado:** `main` en `4ec84f5`, con los siete PRs de la cadena fusionados (#77–#83)
> **Worktree:** `inc/autenticacion-despliegue-8-verificacion`

---

## 1. Resumen ejecutivo

**Veredicto: CONFORME con reservas.** Los **6 requisitos y 17 escenarios** de las tres especificaciones delta están acreditados contra el código real. Las **36 tareas** tienen correspondencia verificada. La suite pasa con **1.593 unitarias + 10 de integración, exit 0**.

- **0 hallazgos CRITICAL.**
- **3 WARNING**, dos de calidad de prueba y uno de verdad documental descubierto por el orquestador.
- **1 SUGGESTION.**

Los cinco defectos que motivaron el incremento están cerrados: **B1** (el `redirect_uri` de OAuth salía en `http`), **B2** (cero credenciales en el pipeline), **B3** (correo del fundador irreversible), **B4** (silencio cuando nadie puede entrar) y **B5** (afirmación falsa del ROADMAP).

---

## 2. Evidencia de pruebas

### 2.1. Medición del verificador

En su propio worktree, con redirección a fichero y lectura separada del código de salida:

```
dotnet test Ludeka.sln    -> EXIT 0
Ludeka.UnitTests.dll        1593/1593, 0 errores, 0 omitidas
Ludeka.IntegrationTests.dll   10/10,   0 errores, 0 omitidas
```

### 2.2. Medición independiente del orquestador

Ejecutada **en paralelo y en otro directorio** (`C:\repos\Ludeka`, mismo commit), para no firmar la cifra del subagente sin contraste:

```
dotnet build Ludeka.sln   -> EXIT 0, 0 errores, 13 advertencias
dotnet test  Ludeka.sln   -> EXIT 0, 1593 unitarias + 10 de integración
```

**Las dos coinciden exactamente.** Los resúmenes `sha256` de ambas salidas están en la cabecera legible por máquina de este informe.

### 2.3. Validación adicional

```
docker compose -f docker-compose.prod.yml config   -> EXIT 0
```

El fichero sigue siendo válido tras retirar el valor por defecto de `AdminUser__Email`.

---

## 3. Conformidad requisito a requisito

### 3.1. `production-auth-bootstrap` — 2 requisitos, 7 escenarios

| Escenario | Veredicto | Evidencia |
|---|---|---|
| Production con correo explícito arranca | CONFORME | `WebStartupGuardsTests.cs:116-124` (G1) |
| Production sin correo **aborta** | CONFORME | `WebStartupGuardsTests.cs:126-135` (G2) y `:173-184` (G6, contra el `appsettings.json` real); `WebStartupGuards.cs:60-81`; `Program.cs:129-135` lanza |
| Entornos distintos no activan la guarda | CONFORME | `WebStartupGuardsTests.cs:147-157` (G4) |
| El compose de producción no reintroduce el valor por defecto | CONFORME | `docker-compose.prod.yml:19`; `docker compose config` exit 0 |
| El pipeline suministra el correo | CONFORME | `ci-cd.yml:136`; `CiCdWorkflowContractTests.cs:78-88` (Y3) |
| El despliegue declara al menos un proveedor utilizable | CONFORME | `ci-cd.yml:119-120`, `:132-135`; Y1 e Y2 |
| Ningún secreto viaja en texto plano | CONFORME | `ci-cd.yml:108-120` sin `ClientSecret`/`AppSecret`/`AdminUser__Email`; Y4 |

### 3.2. `reverse-proxy-forwarded-headers` — 3 requisitos, 7 escenarios

| Escenario | Veredicto | Evidencia |
|---|---|---|
| Proxy no loopback corrige el esquema a `https` | CONFORME | `ForwardedHeadersPipelineTests.cs:83-101` (N1, parametrizada en tres entornos) |
| **Sin vaciar las listas de confianza el esquema NO cambia** | CONFORME — pieza central | `:58-76` (N2), con `RemoteIpAddress` forzada a `203.0.113.10` |
| Mismo comportamiento fuera de Production | CONFORME | Cubierto por N1 |
| Sin proxy, el comportamiento local no cambia | CONFORME | `:107-119` (N3) |
| `X-Forwarded-Host` no altera el host | CONFORME | `:148-163` (N5); `ForwardedHeadersConfiguration.cs:28` |
| `redirect_uri` en `https` detrás del proxy | CONFORME | `:169-182` (N6) |
| `redirect_uri` en `http` en local sin proxy | CONFORME | `:188-200` |

### 3.3. `social-login-authentication` (delta) — 1 requisito, 3 escenarios

| Escenario | Veredicto | Evidencia |
|---|---|---|
| Production sin proveedores utilizables registra **error** | CONFORME | `WebAuthenticationRegistrationTests.cs:171-180` (A1) |
| Otro entorno registra **aviso** | CONFORME | `:182-191` (A2) |
| Con un proveedor utilizable no se emite el aviso agregado | CONFORME con matiz | `:193-202` (A3) — ver hallazgo 4 |

**17/17 escenarios conformes. Cero no conformidades.**

---

## 4. Cobertura de tareas (36/36)

```
grep -c '^- \[x\]' tasks.md  -> 36
grep -c '^- \[ \]' tasks.md  -> 0
```

Todas con correspondencia real comprobada contra el código, por fase: arnés con dirección remota forzada fuera del bucle invertido, middleware y función pura, las nueve pruebas de tubería, guarda y aviso con sus configuraciones vaciadas, contrato del pipeline, y documentación.

Cero referencias a `Microsoft.AspNetCore.TestHost`: la decisión de no añadir dependencia se respetó.

---

## 5. Hallazgos

| # | Severidad | Hallazgo |
|---|---|---|
| **1** | WARNING | **`ForwardedHeadersPipelineTests.cs:138-141`** — N4 solo excluye 307 y 308 con `Assert.NotEqual`, en vez de fijar el `200 OK` que se observó empíricamente. Un futuro 500 o 404 seguiría pasando la prueba. Es más débil que el hallazgo que documenta. |
| **2** | WARNING | **`Program.cs:129-150`** — la guarda de identidad y el aviso agregado están bien probados como funciones puras (G1-G6, A1-A3), pero **ningún contrato de fuente fija que estén cableados**, a diferencia de `UseForwardedHeaders`, que sí lo tiene en `AuthorizationPipelineContractTests.cs:37`. Retirar la llamada de `Program.cs` no rompería ninguna prueba. Afecta a la pieza más crítica e irreversible del incremento. El patrón se hereda de la guarda de INC-48. |
| **3** | WARNING | **Verdad documental: la corrección de B5 quedó incompleta.** El orquestador amplió la búsqueda de la afirmación falsa a `docs/` entero y la encontró **viva en la especificación viva**: `docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md:3` decía «25/26 PR mergeados… PR #60 (R7) **abierto y retenido a propósito**», y `:151` decía que dos trabajos «pierden su disparador **en cuanto mergee** el PR #60». Ambas falsas desde el 2026-09-19. La segunda es operativamente relevante: **el boletín semanal y el escaneo de sorteos ya no tienen disparador automático**, y el módulo lo presentaba como un riesgo futuro. **Corregido en esta rebanada.** |
| **4** | SUGGESTION | **`WebAuthenticationRegistrationTests.cs:193-202`** (A3) — no combina un proveedor utilizable con otro sin credenciales en el mismo caso; esa cláusula queda cubierta indirectamente por una prueba preexistente. |

El hallazgo 3 no lo detectó el verificador porque su búsqueda se limitó al ROADMAP y a la guía de despliegue.

---

## 6. Huecos de evidencia y riesgos residuales

1. **No existe entorno de producción.** Ni proyecto de GCP, ni base de Supabase, ni dominio mapeado. **Nada está acreditado contra un despliegue real:** lo verificado es coherencia de código, pruebas y configuración.
2. **Cloud Run no documenta `X-Forwarded-Proto` para su propio producto** (la única confirmación literal es de Cloud Run functions, producto hermano). Por eso el incremento lo convirtió en prueba en lugar de en cita, pero la emisión real de la cabecera por la plataforma sigue sin observarse.
3. **El mapeo de dominio está en fase *preview*** y Google lo desaconseja para producción. Decisión consciente y documentada del maintainer, no un descuido.
4. **No se ha observado un viaje OAuth real** contra Google ni un sembrado real del fundador.
5. **Alcanzabilidad del contenedor saltándose el front-end de Cloud Run:** no verificada. La mitigación del diseño no descansa en ella.
6. Los hallazgos 1 y 2 son riesgos de mantenibilidad: hoy el comportamiento es correcto, pero una regresión futura podría no detectarse.

---

## 7. Auditoría del orquestador sobre este informe

| Afirmación comprobada | Resultado |
|---|---|
| Suite y código de salida | **Coincide**: 1.593 + 10, exit 0, medido en directorio distinto |
| N4 solo excluye 307/308 | **Correcta**, leído `:138-141` |
| No existe contrato de posición para la guarda | **Correcta**: cero coincidencias en `tests/` |
| Sí existe para `UseForwardedHeaders` | **Correcta**, `AuthorizationPipelineContractTests.cs:37` |
| «Abierto y retenido» erradicado de `docs/` | **INCOMPLETA** — ver hallazgo 3 |

Este fichero lo escribió el orquestador: el agente `sdd-verify` no dispone de herramienta de escritura.

---

## 8. Veredicto

**CONFORME con reservas.** INC-52 puede archivarse.

Los tres WARNING no son incumplimientos: dos son pruebas más débiles de lo que podrían ser, y el tercero era una falsedad documental que esta misma rebanada corrige.

**Lo que el incremento acredita:** que detrás de un proxy inverso el esquema se reconstruye y el `redirect_uri` sale en `https`; que el arranque aborta en producción si falta el correo del fundador; que el pipeline inyecta las credenciales de autenticación y ninguna llega a los Cloud Run Jobs.
**Lo que no acredita:** que nada de esto funcione en un entorno de producción que todavía no existe.
