# Reporte de Verificación: INC-73 — Ingesta Masiva BGG (>100 opiniones), Anti-Duplicados, Enriquecimiento Integral y Localización Multipaís

**Fecha de Ejecución:** 27 de Septiembre de 2026  
**Rama:** `inc/ingesta-enriquecimiento-catalogo`  
**Directorio de Trabajo:** `C:\repos\ludeka-wt\ingesta-enriquecimiento-catalogo`  
**Resultado Global:** ✅ **100% SUPERADO (1.966 / 1.966 pruebas unitarias en verde)**

---

## 1. Resumen Ejecutivo

El Incremento 73 aborda la ampliación masiva de catálogo BGG y la elevación cualitativa de los datos de mesa, junto con la localización territorial de editoriales y títulos comerciales en todos los países hispanohablantes soportados por Ludeka (España, México, Argentina, Chile, Colombia, Perú y Uruguay):

1. **Ampliación de Umbral de Ingesta Masiva BGG (`minUsersRated = 100`):**
   - Reducción del filtro de corte de 1.000 a 100 valoraciones comunitarias en `BggDumpParser`, `BggMassIngestionOptions` y orquestación.
   - Permite absorber decenas de miles de títulos preservando la fiabilidad comunitaria sin dejar fuera juegos de nicho o ediciones hispanas.
2. **Blindaje Anti-Duplicados y Promoción Aditiva no Destructiva:**
   - La promoción por lotes (`PromoteReadyToCatalogBatchAsync`) comprueba la existencia previa por `BggId`.
   - Si el título ya existe en `Games`, descarta la inserción para prevenir violaciones de índice único y ejecuta una actualización aditiva sobre escalabilidad, fundas, duraciones, huella en mesa y editoriales regionales.
3. **Enriquecimiento Integral de Metadatos de Mesa:**
   - **Escalabilidad por Jugadores:** Extracción comunitaria de BGG (`poll name="suggested_numplayers"`) con algoritmo de contingencia determinista en `[minplayers .. maxplayers]` (`MustPlay` si `min == max`, `Recommended` si `min < max`) para erradicar fichas sin datos.
   - **Fundas de Cartas (`Sleeves`):** Ingesta de dimensiones y recuentos de fundas desde BGG persistidas en el agregado de juego.
   - **Huella en Mesa (`TableFootprint`):** Inferencia analítica basada en categorías (juegos de cartas -> `SmallTable`, wargames y miniaturas -> `TableMonster`, tableros estándar -> `StandardTable`).
   - **Tiempos Reales de Juego:** Captura de `MinPlayTimeMinutes`, `MaxPlayTimeMinutes` y cálculo proporcional y realista de `EstimatedPerPlayerMinutes`.
4. **Enriquecimiento Retroactivo (Backfill):**
   - Métodos `BackfillCatalogQualityBatchAsync` y `RunScheduledBackfillCatalogQualityBatchAsync` en `BggMassIngestionService` con estrategia *Staging-First*.
5. **Localización Territorial Multipaís:**
   - Padrón ampliado en `seed-directory.json` a 57 editoriales clave cubriendo España, México, Argentina, Chile, Colombia, Perú y Uruguay.
   - Mapeador `RegionalPublisherMatcher` para detectar licencias y sellos territoriales en enlaces BGG.
   - Persistencia en `Game` de `SpanishPublisher`, `RegionalPublishers` y `LocalizedTitles`.
   - Resolución contextual en frontend (`GameDetail.razor`, `GameCard.razor`) adaptada al país preferido del usuario vía `IUserPreferenceService`.
   - Navegación bidireccional en el directorio de editoriales (`PublisherDetail.razor`, `PublisherService.cs`).

---

## 2. Resultados de la Suite de Pruebas Automatizadas

Comando ejecutado:
```powershell
dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj
```

```text
Serie de pruebas para Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error: 0, Superado: 1966, Omitido: 0, Total: 1966, Duración: 21 s
```

### Pruebas Específicas del Incremento 73 (+28 pruebas directas / 1.966 acumuladas):
- **Infraestructura (`tests/Ludeka.UnitTests/Infrastructure/`):**
  - `RegionalPublisherMatcherTests`: 7 pruebas (resolución de editoriales de España como Devir o Maldito, filiales en México, Chile, Colombia, Perú, Argentina, Uruguay, y tolerancia a nombres desconocidos).
  - `DirectorySeederTests`: Actualizada con el nuevo padrón de 57 editoriales verificando siembra aditiva e idempotente.
- **BGG y Parsers (`tests/Ludeka.UnitTests/Bgg/`):**
  - `BggXmlParserQualityTests`: 7 pruebas (extracción de duraciones reales `minplaytime`/`maxplaytime`, cálculo de `EstimatedPerPlayerMinutes`, fallback determinista de escalabilidad sin votos, inferencia analítica de huella de mesa, detección de fundas e identificación de editoriales multipaís).
- **Aplicación (`tests/Ludeka.UnitTests/Application/`):**
  - `BggMassIngestionBackfillTests`: 3 pruebas (promoción de nuevo juego con metadatos completos, actualización aditiva idempotente de juego existente sin colisión de `BggId`, y ejecución de backfill retroactivo con estrategia staging-first).
  - `BggMassIngestionAutonomousDownloadTests`: 10 pruebas actualizadas para validar la opción por defecto `MinUsersRated = 100`.
- **Contratos de Esquema y Migración (`SqliteSchemaMigrator` & EF Core):**
  - Reconciliación de columnas `SpanishPublisher`, `RegionalPublishers` y `LocalizedTitles` en SQLite y validación de migración EF Core `20260927023336_AddBggQualityAndLocalizationFields`.

---

## 3. Conclusión y Veredicto

El incremento INC-73 cumple rigurosamente con todos los requerimientos funcionales, arquitectónicos y de accesibilidad establecidos, entregando un catálogo más amplio, con datos de mesa confiables y contextualizado para la comunidad lúdica hispanohablante.

**Veredicto Final:** ✅ **APROBADO PARA ARCHIVADO Y PULL REQUEST**
