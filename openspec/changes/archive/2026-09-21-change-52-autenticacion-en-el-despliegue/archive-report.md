# Informe de Archivado — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-archive` · **Fecha:** 2026-09-21  
> **Cambio:** `change-52-autenticacion-en-el-despliegue`  
> **Worktree de origen:** `inc/autenticacion-despliegue-9-archivado`, rama `inc/autenticacion-despliegue-9-archivado`  
> **Base:** `main` en `189ef27` (al inicio del archivado)

---

## Resumen ejecutivo

**El cambio está completamente archivado.** INC-52 comprendió ocho incrementos de código (PRs #77–#84) que cerraron los cinco defectos de bloqueador de la puerta de producción: reconstrucción del esquema HTTPS detrás de proxy inverso (B1), inyección de credenciales en el pipeline (B2), guarda irreversible del correo del fundador (B3), aviso de arranque sin proveedores (B4), y corrección de afirmación falsa en el ROADMAP (B5). Las especificaciones delta se han fusionado en `openspec/specs/` y la carpeta del cambio se ha archivado.

---

## Entrega y PRs

| # | Rama | Asunto | Estado |
|---|------|--------|--------|
| **#77** | `inc/autenticacion-despliegue-1-contrato` | test: contrato de pipeline de CI/CD (Y3) | Merged |
| **#78** | `inc/autenticacion-despliegue-2-puerta` | feat: puerta de decisión de primer despliegue (A1–A2) | Merged |
| **#79** | `inc/autenticacion-despliegue-3-middleware` | feat: middleware de cabeceras reenviadas (A1, N1–N6) | Merged |
| **#80** | `inc/autenticacion-despliegue-4-pruebas` | test: arnés con proxy simulado (N1–N6, G2, Y3) | Merged |
| **#81** | `inc/autenticacion-despliegue-5-guarda-aviso` | feat: guarda de fundador + aviso agregado (G1–G6, A1–A3) | Merged |
| **#82** | `inc/autenticacion-despliegue-6-cableado-pipeline` | ci: cableado de credenciales en flujo CI/CD (B2, B3) | Merged |
| **#83** | `inc/autenticacion-despliegue-7-documentacion` | docs: primera sección del despliegue a producción | Merged |
| **#84** | `inc/autenticacion-despliegue-8-verificacion` | docs(sdd): registrar verificación de INC-52 y corregir módulo 34 | Merged |

**Total: 8 PRs, todas merged.** Véase la rama `main` en `189ef27` con toda la cadena integrada.

---

## Especificaciones: síntesis de cambios

### Nuevas (creadas)

| Dominio | Fichero | Requisitos | Escenarios | Estado |
|---------|---------|-----------|-----------|--------|
| **production-auth-bootstrap** | `openspec/specs/production-auth-bootstrap/spec.md` | 2 | 7 | ✓ Creada |
| **reverse-proxy-forwarded-headers** | `openspec/specs/reverse-proxy-forwarded-headers/spec.md` | 3 | 7 | ✓ Creada |

Ambas copiadas mecánicamente desde `openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue/specs/`. Verificadas con `diff -r` (resultado vacío).

### Existentes (fusionadas)

| Dominio | Fichero | Delta | Estado |
|---------|---------|-------|--------|
| **social-login-authentication** | `openspec/specs/social-login-authentication/spec.md` | +1 requisito (aviso de arranque sin proveedores), +3 escenarios | ✓ Fusionada |

- **Requisitos previos conservados:** 7 requisitos existentes intactos.  
- **Nuevo requisito:** "Aviso de arranque cuando ningún proveedor social resulta utilizable" con sus 3 escenarios (A1–A3), insertado al final de la especificación.  
- **Verificación:** los requisitos existentes (R1–R7) permanecen byte-idénticos; el nuevo requisito se añadió limpiamente sin truncación ni alteración de secciones previas.

---

## Conformidad verificada

**Veredicto final:** `pass_with_warnings` (per `verify-report.md` §8).

| Métrica | Cantidad | Detalles |
|---------|----------|---------|
| **Requisitos** | 6/6 ✓ | 2 en `production-auth-bootstrap`, 3 en `reverse-proxy-forwarded-headers`, 1 nuevo en `social-login-authentication` |
| **Escenarios** | 17/17 ✓ | 7 en bootstrap, 7 en forwarded-headers, 3 en aviso agregado |
| **Tareas** | 36/36 ✓ | Comprobadas línea por línea contra el código |
| **Suite (pruebas)** | 1.603 ✓ | 1.593 unitarias + 10 de integración |
| **Compilación** | EXIT 0 | 0 errores, 13 advertencias |
| **Hallazgos** | 0 CRITICAL, 3 WARNING, 1 SUGGESTION | Ninguno bloquea el archivado |

---

## Cinco defectos cerrados

| Defecto | Problema | Solución implementada | Acreditado en |
|---------|----------|----------------------|---------------|
| **B1** | `redirect_uri` de OAuth en `http` detrás de proxy | `UseForwardedHeaders` + tests N1–N6 | PR #79, verify-report §3.2 |
| **B2** | Cero credenciales en el pipeline | Cableado en `ci-cd.yml` (env + secrets) | PR #82, verify-report §3.1.6–7 |
| **B3** | Correo del fundador irreversible | Guarda en `Production` + vaciar valor por defecto | PR #81, verify-report §3.1.2–7 |
| **B4** | Silencio cuando ningún proveedor operativo | Aviso agregado de error/aviso por entorno | PR #81, verify-report §3.3 |
| **B5** | ROADMAP mentía sobre PR #60 abierto | Corrección documental en PR #83 + módulo 34 en #84 | verify-report §5.3 |

---

## Artefactos del cambio archivado

| Artefacto | Ubicación | Presencia | Integridad |
|-----------|-----------|-----------|-----------|
| `proposal.md` | `archive/…/proposal.md` | ✓ Presente | Bytes íntegros |
| `explore.md` | `archive/…/explore.md` | ✓ Presente | Bytes íntegros |
| `design.md` | `archive/…/design.md` | ✓ Presente | Bytes íntegros |
| `specs/` (3 dirs) | `archive/…/specs/{domain}/spec.md` | ✓ Presente (3) | Bytes íntegros |
| `tasks.md` | `archive/…/tasks.md` | ✓ Presente | 36/36 tareas completadas |
| `verify-report.md` | `archive/…/verify-report.md` | ✓ Presente | Bytes íntegros |
| `research-dominio-cloud-run.md` | `archive/…/research-dominio-cloud-run.md` | ✓ Presente | Bytes íntegros |

**Ruta archivada:** `openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue/`

---

## Huecos de evidencia y riesgos residuales

Per `verify-report.md` §6:

1. **No existe entorno de producción real.** Ni proyecto GCP, ni base Supabase, ni dominio mapeado. Todo está acreditado contra código y pruebas, no contra un despliegue vivo.

2. **Cloud Run no documenta `X-Forwarded-Proto`** en su propia documentación oficial. El incremento lo convirtió en prueba unitaria en lugar de en cita, pero la emisión real por Cloud Run sigue sin observarse (verificado empiricamente en Nginx).

3. **Domain mapping de Cloud Run en fase *preview*.** Google lo desaconseja para producción. Decisión consciente del maintainer, documentada en `deployment/google-cloud-run.md`.

4. **No se ha observado un viaje OAuth real** contra ninguno de los tres proveedores ni un sembrado real del fundador en un entorno vivo.

5. **Alcanzabilidad del contenedor sin front-end de Cloud Run:** no verificada. La mitigación de diseño no descansa en ello.

6. **Hallazgos 1 y 2 del verify-report** (pruebas más débiles de lo ideal) son riesgos de mantenibilidad: hoy el comportamiento es correcto, pero una regresión futura podría no detectarse. No son incumplimientos.

---

## Trabajo diferido

Per `proposal.md` §3.2 (Fuera):

- **Cascada 2a/2b de INC-49.** Funciona; documentación de uso pendiente (no es cambio de lógica).
- **Autenticación por contraseña, cuentas locales o correo de recuperación.** No incluidas en INC-52 ni en el alcance.
- **Revisión de aplicaciones de Meta para Facebook.** Trámite externo.
- **Quinto Cloud Run Job del boletín semanal.** Decisión pendiente, no relacionada.
- **Claves en los cuatro Cloud Run Jobs.** Deliberadamente no cableadas: los Jobs no atienden HTTP, no pueden recibir retorno OAuth.
- **Aprovisionar el entorno real (GCP, Supabase, DNS, apps OAuth).** Guion entregado en `deployment/google-cloud-run.md`; ejecución es trabajo manual del maintainer.

---

## Transiciones a la especificación viva

**Obligación per `AGENTS.md` §1.6:** Este archivado requiere volcado a `docs/specs/sistema/`. 

- **Nuevo módulo:** `36-autenticacion-en-produccion-y-despliegue.md` creará la capacidad en el catálogo vivo, enlazando con módulos 32 (autenticación social), 34 (Cloud Run Jobs) y 35 (persistencia).
- **Índice actualizado:** entrada de módulo 36 en `docs/specs/sistema/README.md` con la cifra desglosada: 1.593 pruebas unitarias + 10 de integración verificadas.
- **Documento de incremento:** trasladado de `docs/increments/inc-52-autenticacion-en-el-despliegue.md` a `docs/increments/archive/inc-52-autenticacion-en-el-despliegue.md` con estado `✅ Archivado (2026-09-21)`.
- **ROADMAP:** Fila de INC-52 actualizada de `⏳ En progreso` a `✅ Archivado` con enlace a la carpeta archivada.

---

## Evidencia de operación mecánica de archivado

### Paso 2: Fusión de specs delta

**production-auth-bootstrap:**
```
cp openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue/specs/production-auth-bootstrap/spec.md \
   openspec/specs/production-auth-bootstrap/spec.md
diff -r ... : VACÍO ✓
```

**reverse-proxy-forwarded-headers:**
```
cp openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue/specs/reverse-proxy-forwarded-headers/spec.md \
   openspec/specs/reverse-proxy-forwarded-headers/spec.md
diff -r ... : VACÍO ✓
```

**social-login-authentication:**
```
Edit: insertado nuevo requisito al final (línea ~168)
Requisitos previos (líneas 1–167): intactos byte-idénticamente ✓
```

### Paso 3: Movimiento a archive

```
source = openspec/changes/change-52-autenticacion-en-el-despliegue
destination = openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue
snapshot_root = ${TMPDIR}/sdd-archive.XXXXXX

cp -R $source $snapshot_root/source
mkdir -p openspec/changes/archive
mv $source $destination
test ! -e $source  ✓
diff -r $snapshot_root/source $destination : VACÍO ✓
```

---

## Cierre

**Fecha de archivado:** 2026-09-21 (UTC)  
**Ejecutor:** `sdd-archive` (Haiku 4.5)  
**Comando usado:** operaciones mecánicas únicamente (cp, mv); ningún axiom sdd archive-compose (prohibido por restricción del proyecto)  
**Modo de almacén:** hybrid (ficheros + Engram)

El cambio está completamente archivado. Las tres especificaciones delta se han integrado en el almacén canónico. La carpeta del cambio reposa en `openspec/changes/archive/2026-09-21-change-52-autenticacion-en-el-despliegue/`. Las transiciones a la documentación viva proceden en los pasos siguientes.
