# Propuesta de Cambio — INC-55: Retirada del Tagline de Marca y Unificación de la Identidad

## 1. Motivación y Contexto
Durante las fases iniciales del proyecto se adoptó de forma informal el claim *"El Letterboxd de los juegos de mesa en español"* para describir la propuesta de valor de Ludeka. Con el tiempo, esta frase se replicó en más de 40 puntos del repositorio sin una fuente de verdad única, introduciendo tres problemas relevantes:
1. **Riesgo y dependencia de marca ajena:** El uso de una marca registrada ajena ("Letterboxd") como reclamo público en el `<PageTitle>`, pie de página, PWA y llamadas a la acción genera confusión sobre la autoría y posibles conflictos legales o de posicionamiento.
2. **Inconsistencias en UI y metadatos:** En ciertos puntos de la interfaz aparece truncado (*"El Letterboxd de los juegos de mesa"*), en otros completo (*"El Letterboxd de los juegos de mesa en español"*) y en otros dentro de comentarios de CSS o descripciones de temas de color.
3. **Contaminación de prompts de IA:** El prompt de sistema en `GeminiGameSummaryService` inyecta textualmente esta frase, condicionando al modelo de lenguaje a imitar estilos cinematográficos o de plataformas externas en lugar del tono crítico, cálido y riguroso propio de Ludeka.

El mantenedor ha solicitado expresamente la retirada completa de esta frase de todos los lugares activos del proyecto.

## 2. Alcance Propuesto
- **Eliminación y reemplazo en Frontend (Blazor Web / PWA):**
  - Actualizar el `<PageTitle>` de `HomeDashboard.razor` a una propuesta editorial propia y concisa (ej. `Ludeka — Juegos de mesa en español`).
  - Retirar la frase del pie de página en `MainLayout.razor`.
  - Normalizar el texto de bienvenida en `Login.razor`.
  - Limpiar la vista previa de publicaciones en `InstagramModeration.razor`.
  - Ajustar la descripción del tema Terracota en `MyLibrary.razor` y en `input.css`.
  - Actualizar la descripción de la PWA en `manifest.webmanifest`.
- **Saneamiento en Servicios Backend:**
  - Actualizar el prompt del sistema de `GeminiGameSummaryService` para enfocarlo en crítica editorial de juegos de mesa sin referencias a terceros.
  - Ajustar el encabezado `User-Agent` de `GeekDoImagesClient` hacia un identificador formal de comunidad lúdica.
- **Armonización de Documentación Viva:**
  - Actualizar `AGENTS.md`, `GEMINI.md`, `axiom.yaml`, `openspec/config.yaml` y los módulos correspondientes en `docs/specs/sistema/` y `docs/specs/`.
  - Conservar intactos los registros históricos en `archive/`.

## 3. Criterios de Aceptación
1. `grep -i letterboxd` sobre archivos vivos de código (`src/`) devuelve cero coincidencias.
2. `grep -i letterboxd` sobre documentación viva y configuraciones devuelve cero coincidencias no intencionadas.
3. El prompt de Gemini mantiene su estructura y efectividad analítica sin mencionar marcas de terceros.
4. `dotnet test` pasa al 100% (1.622 pruebas unitarias en verde).
