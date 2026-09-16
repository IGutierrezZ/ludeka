# Informe de Verificación y Validación: change-22-draws-news-events-split

## 1. Resumen Ejecutivo
El **Incremento 22** ha sido implementado y verificado con éxito en todas las capas de la arquitectura Clean Architecture / Vertical Slices (.NET 10 y Blazor Web App). La suite completa de pruebas automatizadas xUnit ha alcanzado **472 pruebas pasando al 100% sin ningún error ni regresión** (19 pruebas nuevas añadidas específicamente para este incremento).

---

## 2. Resultados de las Pruebas Automatizadas

```text
Serie de pruebas para C:\repos\Ludeka\tests\Ludeka.UnitTests\bin\Debug\net10.0\Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error: 0, Superado: 472, Omitido: 0, Total: 472, Duración: 3 s - Ludeka.UnitTests.dll (net10.0)
```

### Nuevas Baterías de Pruebas Incorporadas:
1. `BoardGameEventServiceTests`:
   - `GetUpcomingEventsAsync_ReturnsFutureAndOngoingEvents_OrderedByStartDate`: Garantiza que solo se retornen eventos futuros o vigentes ordenados cronológicamente.
   - `GetPastEventsAsync_ReturnsOnlyPastEvents_OrderedByEndDateDescending`: Garantiza el filtrado correcto del histórico por fecha de finalización descendente.
   - `CreateEventAsync_ValidRequest_PersistsAndReturnsDto`: Valida el alta de ferias con metadatos completos y persistencia.
   - `UpdateEventAsync_ExistingEvent_UpdatesDetailsSuccessfully`: Valida la mutación controlada de ferias y eventos.
   - `UpdateEventAsync_NonExistingId_ThrowsKeyNotFoundException`: Control de errores ante identidades inexistentes.
   - `DeleteEventAsync_RemovesEventFromRepository`: Comprueba el borrado físico del evento.
2. `GiveawayPromotionTests`:
   - `SetPromotedAsync_UpdatesStateAndPersists`: Verifica que la conmutación de `IsPromoted` altera el estado y persiste en base de datos.
   - `SetPromotedAsync_NonExistingId_ThrowsKeyNotFoundException`: Validación ante ID inexistente.
   - `GetGiveawaysAsync_OrdersPromotedFirst_ThenByDeadline`: Demuestra que los sorteos promocionados se sitúan en cabecera de carril y lista, seguidos de los no promocionados.
3. `WeeklyReleaseCreationTests`:
   - `CreateReleaseAsync_ValidRequest_AddsToRepositoryAndReturnsDto`: Alta manual de lanzamientos con cálculo de DTO.
   - `CreateReleaseAsync_NullRequest_ThrowsArgumentNullException`: Control de invariantes.
   - `CreateReleaseAsync_InvalidStrings_ThrowsArgumentException`: Rechazo de cadenas vacías en títulos o editoriales.
4. `PhysicalFileImageStorageExtendedTests`:
   - `SaveEventPosterAsync_ValidJpg_SavesInEventsDirectory`: Almacenamiento físico de carteles bajo `wwwroot/images/events/`.
   - `SaveCommunityImageAsync_ValidPng_SavesInSpecifiedSubfolder`: Almacenamiento seguro en subcarpetas de comunidad (`draws`, `releases`).
   - `SaveEventPosterAsync_EmptyStream_FailsGracefully`: Rechazo seguro de flujos vacíos.
   - `SaveEventPosterAsync_DisallowedExtension_FailsGracefully`: Rechazo estricto de extensiones no permitidas (.exe, .sh, etc.).

---

## 3. Matriz de Cumplimiento de Criterios de Aceptación (Gherkin)

| Criterio / Escenario | Estado | Evidencia y Mecanismo de Verificación |
|---|---|---|
| **Segregación de menús en la navegación principal** | ✅ Superado | Retirado el enlace monolítico `/radar` de la barra superior y footer en `MainLayout.razor`. Creados enlaces independientes para `🎁 Sorteos` (`/sorteos`), `📰 Novedades` (`/novedades`) y `🎪 Eventos` (`/eventos`). |
| **Consulta del calendario de eventos lúdicos** | ✅ Superado | `Events.razor` implementado en `@page "/eventos"` con selector de pestañas ("Próximas Citas" e "Histórico"), carteles, badges de tiempo restante ("¡En curso!", "En X días"), ciudad, organizador y botón saliente a la web oficial con `rel="noopener noreferrer"`. |
| **Marcado de sorteo promocionado por un moderador** | ✅ Superado | En `Radar.razor` y `GiveawayCard.razor`, los usuarios con rol `FoundingTeam` o `Moderator` disponen de un botón para alternar el estado promocionado en 1 clic y una casilla en el formulario de alta. Los sorteos promocionados lucen el distintivo "⭐ Promocionado" y se ordenan en cabecera. |
| **Subida de imagen al crear una novedad manual** | ✅ Superado | `News.razor` en `@page "/novedades"` permite a moderadores registrar lanzamientos editoriales con portada (`CoverImageUrl`), PVP estimado, fecha e indicador de novedad/reimpresión. |
| **Panel editorial de eventos** | ✅ Superado | `EventsManagement.razor` en `@page "/admin/eventos"` permite a la moderación dar de alta, editar, eliminar ferias y subir directamente carteles con `InputFile` y `IImageStorageService`. |
| **Retrocompatibilidad de `/radar`** | ✅ Superado | La ruta `/radar` se mantiene operativa como alias que renderiza la vista de sorteos y despliega un aviso informativo invitando a explorar las nuevas secciones `/novedades` y `/eventos`. |

---

## 4. Auditoría de Accesibilidad y Buenas Prácticas
1. **Accesibilidad (WCAG 2.2 AA):** Modales con `role="dialog"` y `aria-modal="true"`, pestañas con `role="tab"` y `aria-selected`, contraste alto en todos los botones y badges temáticos.
2. **Seguridad y Sanitización:** Subida de imágenes validada con extensiones fijas (`.jpg`, `.jpeg`, `.png`, `.webp`), límite de 5 MB y protección de rutas administrativas mediante `AuthorizeView` y comprobación de roles en tiempo de renderizado.
