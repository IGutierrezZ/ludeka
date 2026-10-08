# Informe de Verificación: INC-137 Sincronización BGG desde Ficha de Juego

**Fecha:** 2026-10-09  
**Incremento:** INC-137 (`sincronizacion-bgg-ficha`)  
**Autor:** Antigravity (Principal Systems Architect)  
**Estado:** ✅ APROBADO

---

## 1. Resumen Ejecutivo

El incremento INC-137 implementa la sincronización forzada en tiempo real con BoardGameGeek desde la sección de herramientas de gestión de la ficha editorial (`/juegos/{slug}`). Permite a los miembros del equipo de moderación y la mesa fundadora resolver discrepancias de catálogo (títulos en inglés, editoriales ausentes, códigos EAN-13 desactualizados o imágenes faltantes) descargando directamente el snapshot crudo de BGG con sus versiones físicas (`&versions=1`), persistiendo el payload en `BggRawSnapshotRepository` y actualizando de forma atómica y auditada la entidad `Game`.

---

## 2. Cobertura de Pruebas Unitarias

Se ejecutó la suite completa de pruebas unitarias del proyecto:

```
Comando: dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj
Resultado: Correctas! - Con error: 0, Superado: 2742, Omitido: 0, Total: 2742
Duración: 25 s
```

### Casos de Prueba Específicos para `ForceSyncFromBggAsync`:
1. `ForceSyncFromBggAsync_WhenUnauthorized_ThrowsUnauthorizedAccessException`: Verifica que usuarios anónimos o sin permisos `CanEditGames` sean rechazados deterministamente.
2. `ForceSyncFromBggAsync_WhenGameNotFound_ThrowsKeyNotFoundException`: Verifica la excepción adecuada ante identificadores inexistentes.
3. `ForceSyncFromBggAsync_WhenBggIdZeroOrNegative_ThrowsInvalidOperationException`: Rechaza juegos sin identificador BGG asignado.
4. `ForceSyncFromBggAsync_WhenBggReturnsValidXmlWithSpanishVersion_UpdatesGameAndSnapshotAndAudit`: Simula la respuesta completa de BGG con subárbol de versiones, extrayendo título español saneado, editorial española (`Lúdilo`), código EAN-13 normalizado (`8436017220100`) y registrando la auditoría correspondiente en `GameEditLog` e invalidando la caché de catálogo.
5. `ForceSyncFromBggAsync_WhenAlreadyUpToDate_ReturnsSuccessWithoutRedundantEntityChanges`: Comprueba la idempotencia del comando cuando la ficha ya cuenta con los datos más recientes de BGG, evitando escrituras redundantes o logs espurios.

---

## 3. Verificación de UI y Accesibilidad

- Pruebas de contrato de marcado web (`WebMarkupContractTests`): 111 de 111 pruebas superadas en verde.
- Integración en `GameStaffToolsPanel.razor`: Botón de acción rápida con spinner de carga interactivo cuando `IsSyncingBgg` está activo.
- Integración en `GameDetail.razor`: Botón en la botonera de staff de la pestaña veredicto y botón contextual en el pie del marco fotográfico polaroid junto al enlace exterior de BGG.
- Feedback reactivo: Notificación de éxito o advertencia mediante el componente contextual de avisos editoriales.

---

## 4. Conclusión

El incremento cumple rigurosamente con los contratos de Clean Architecture, los principios de Spec-Driven Development (SDD) y el protocolo Organic Driven Development (ODD). Queda listo para su promoción a Pull Request y despliegue a producción.
