# INC-62: Preferencias de Usuario — Tema y País

> **Estado:** ⏳ En progreso
> **Fecha de Inicio:** 2026-09-25
> **Rama de Trabajo:** `inc/preferencias-usuario`
> **Worktree:** `C:\repos\ludeka-wt\preferencias-usuario`
> **Dependencias:** INC-50 (Área de Cuenta), INC-61 (menú de cuenta)
> **Especificación Viva:** [31. Persistencia de Preferencias](file:///c:/repos/Ludeka/docs/specs/sistema/31-preferencias-de-usuario.md) · [29. Localización por País y Filtrado Territorial](file:///c:/repos/Ludeka/docs/specs/sistema/29-localizacion-pais-y-filtrado-territorial.md)

---

## 1. Cómo se descubrió

El modelo de preferencias ya persiste tema y país, pero el usuario no tiene una pantalla donde verlos ni cambiarlos: el país se capta por un modal y el tema ni siquiera se expone.

## 2. El agujero, verificado

Lo que SÍ existe:

- `UserPreference(userId, preferredTheme, country)` con `NormalizeTheme` (`src/Ludeka.Core/Entities/UserPreference.cs`) — temas `wood`, `charcoal`, `emerald`, `editorial`, etc.
- `IUserPreferenceService` / `SqliteUserPreferenceService` (persistencia operativa).
- `IUserLocationService` + `CountryCatalog` (catálogo de países, INC-29).
- `LocationSelectorModal.razor` guarda el país vía `SetUserCountryAsync` (modal, no pantalla).

Lo que NO existe (hueco de evidencia):

- **Ninguna pantalla de preferencias de usuario** verificable en UI (barrido de vistas sin resultados).
- El selector de tema no está expuesto como ajuste persistente del usuario.
- El país solo se capta por el modal de ubicación; no se consulta ni se edita desde una vista estable.

## 3. Lo que pide el maintainer

Que cada usuario gobierne su experiencia: tema visual y país de forma persistente, visibles y editables en su área de cuenta, sin modales intrusivos.

## 4. Alcance y decisiones que hay que tomar

1. Vista de preferencias dentro del área de cuenta (colgando de INC-50/61).
2. Selector de tema persistente (taxonomía existente en `NormalizeTheme`) aplicado en el layout.
3. Selector de país persistente reutilizando `CountryCatalog` y conviviendo con `LocationSelectorModal`.
4. **Decisión abierta:** primera visita (¿modal actual vs alta diferida dentro de preferencias?) y default de tema por defecto para invitados.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Preferencias de notificación, idioma de la interfaz y privacidad granular (van en INC-68).

## 6. Criterios de aceptación

1. El usuario cambia tema y país desde una vista estable y los cambios sobreviven a recarga y a nuevo login.
2. `NormalizeTheme` sigue siendo la única autoridad de taxonomía de temas.
3. `LocationSelectorModal` y la vista de preferencias no se pisan (un solo origen de verdad del país).
4. Tema aplicado sin parpadeo (FOUC) en la primera carga.
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- FOUC/parpadeo al aplicar el tema en SSR antes de la hidratación.
- Duplicación de estado entre el modal de ubicación y la nueva vista.
- Regresión en filtrado territorial (INC-29) si el default de país cambia de semántica.
