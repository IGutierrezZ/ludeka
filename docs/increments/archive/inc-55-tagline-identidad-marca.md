# INC-55: Retirada del Tagline de Marca y Unificación de la Identidad

> **Estado:** ✅ Archivado (2026-09-23, PR #102)  
> **Fecha de Inicio:** 2026-09-23  
> **Rama de Trabajo:** `inc/tagline-identidad-marca`
> **Worktree:** `C:\repos\ludeka-wt\tagline-identidad-marca`
> **Dependencias:** —
> **Especificación Viva:** [22. Portada y Directorio de Creadores](file:///c:/repos/Ludeka/docs/specs/sistema/22-portada-y-directorio-creadores.md) · [15. Dashboard de Inicio Editorial](file:///c:/repos/Ludeka/docs/specs/sistema/15-dashboard-inicio-editorial.md)

---

## 1. Cómo se descubrió

Al preparar el backlog del 2026-09-22 se hizo un barrido de la identidad de marca declarada en el proyecto y el claim «Letterboxd de los juegos de mesa en español» apareció replicado en decenas de sitios sin una fuente única ni una decisión formal sobre su uso.

## 2. El agujero, verificado

44 matches de `Letterboxd` en el repositorio, de los cuales los vivos (no históricos) están dispersos en:

- `axiom.yaml:39`, `AGENTS.md:3`, `GEMINI.md:3`, `openspec/config.yaml:8` (declaraciones de proyecto).
- `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md:13`, `docs/specs/sistema/22-portada-y-directorio-creadores.md:12,27`, `docs/specs/sistema/15-dashboard-inicio-editorial.md:8` (especificación).
- `src/Ludeka.Web/wwwroot/manifest.webmanifest:11` (PWA), `src/Ludeka.Web/Styles/input.css:7` (comentario «Estilo Letterboxd»).
- `src/Ludeka.Web/Components/Pages/MyLibrary.razor:382` (UI).
- `src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs:~366` (el claim viaja dentro del prompt de Gemini).
- Forma truncada «El Letterboxd…» en al menos un punto de la UI.
- `openspec/specs/social-card-generator/spec.md` también lo menciona (fuera de `archive/`, por tanto vigente).

Problemas concretos: (a) no hay fuente única del claim, (b) la forma exacta varía (truncada vs completa), (c) el claim se cuela en prompts de IA donde puede inducir respuestas no deseadas, (d) la mención a una marca ajena (Letterboxd) no tiene decisión documentada de riesgo legal.

## 3. Lo que pide el maintainer

> «quiero quitar la frase "El Letterboxd de los juegos de mesa en español." de todos los sitios no me gusta y esta en muchos sitios. O ponemos otra frase o directamente se quita.»

Quitar el tagline de todos los sitios porque no gusta. La única alternativa abierta es sustituirlo por otra frase (aún sin decidir) o dejar la zona sin tagline.

## 4. Alcance y decisiones que hay que tomar

1. **Decisión abierta:** retirada seca (opción por defecto) o sustitución por otra frase que fijará el maintainer. Conservar el claim actual está **descartado** por el maintainer.
2. Si se decide frase alternativa: crear una única fuente (constante o configuración) que UI, manifest, social cards y prompt de Gemini consuman. Si se retira sin más: eliminar las referencias sin dejar constantes muertas.
3. Actualizar las menciones vivas listadas arriba y dejar constancia en la especificación viva.
4. Revisar el prompt de `GeminiGameSummaryService` para que la identidad no filtre instrucciones espurias.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Rediseño de portada, cambio de dominio o naming del producto, y reescritura de históricos en `docs/increments/archive/` o `openspec/changes/archive/`.

## 6. Criterios de aceptación

1. `grep -i letterboxd` sobre fuentes vivas devuelve cero matches si se retira; si se sustituye, solo la fuente única de la frase nueva.
2. El tagline renderizado en UI, `manifest.webmanifest` y social cards coincide literalmente.
3. El prompt de Gemini ya no arrastra el claim como texto libre.
4. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Dependencia de una marca ajena si se conserva el claim sin revisión legal.
- Coste de reemplazo amplio (docs, UI, prompts, PWA) si se decide retirar tarde.
- Regresión en social cards (módulo `social-card-generator`) si el tagline cambia de longitud.
