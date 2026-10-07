# Tareas de Implementación: INC-122 — Reparación de Calidad y Completitud de Imágenes BGG

- [x] **Tarea 1 (Contrato e Implementación GeekDo):** Refactorizar `GeekDoImagesClient.cs` para deserializar el esquema real (`imageurl_lg`, `numrecommend`), realizar consulta dirigida de contraportada (`tag=BoxBack&sort=hot`) y de galería general (`sort=hot`), y garantizar que nunca se extraen miniaturas de 64px.
- [x] **Tarea 2 (Pruebas Unitarias GeekDo):** Actualizar `GeekDoImagesClientTests.cs` con payloads reales de GeekDo comprobando la extracción de `imageurl_lg` y `tag=BoxBack`.
- [x] **Tarea 3 (Jerarquía Defensiva de Portadas y Saneamiento Anti-Micro):** Modificar `BggImagesSyncService.cs` para priorizar la portada oficial de raíz (`rootCover`) ante la ausencia de portada ES específica, y detectar URLs degradadas con `__micro` en `IsCorruptedOrSimulatedCover` para forzar su reemplazo.
- [x] **Tarea 4 (Pruebas de BggImagesSyncService):** Actualizar `BggImagesSyncServiceTests.cs` validando la preservación de la portada de raíz y la asignación de las 3 imágenes en alta resolución.
- [x] **Tarea 5 (Alineación Editorial del Carrusel):** Refinar `GameImageCarousel.razor` adaptando el visor a `aspect-square sm:aspect-[4/3] max-h-[460px]`, preservando dimensiones anti-CLS y aplicando fondo oscuro editorial con sombras profundas.
- [x] **Tarea 6 (Verificación Global):** Ejecutar la suite completa de pruebas unitarias (`dotnet test`) asegurando que los 2.604 tests pasan al 100%.
