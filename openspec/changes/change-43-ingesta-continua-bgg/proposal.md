# Propuesta SDD — INC-43: Ingesta Continua y Auto-Descubrimiento de Novedades BGG en el Lote Nocturno

## 1. Contexto y Justificación
El catálogo de Ludeka cuenta actualmente con auto-catalogación bajo demanda comunitaria (`UserImport`), extracción a partir de noticias editoriales (`NewsDiscovery`), relleno del cupo diario mediante el Top histórico de BGG (`TopBggBackfill`) y drenaje progresivo de los 8.000 títulos más votados (`BggMassIngestionService`).

Sin embargo, para consolidar la visión de *"El Letterboxd de los juegos de mesa en español"*, el catálogo no puede limitarse al pasado o a esperar que un usuario descubra un juego y lo importe manualmente. Los lanzamientos anuales (Essen Spiel, Gen Con, campañas de crowdfunding de alto impacto y novedades que se colocan en el *Hotness* global de BoardGameGeek) deben incorporarse de forma proactiva, automatizada y continua en el ciclo diario de catalogación.

## 2. Objetivos del Incremento
1. **Auto-descubrimiento en BGG (`IBggDiscoveryService`)**:
   - Monitorear el endpoint oficial de tendencias de BGG (`/xmlapi2/hot?type=boardgame`).
   - Identificar y filtrar lanzamientos recientes correspondientes al año en curso y año previo (`YearPublished >= CurrentYear - 1`).
   - Deduplicar de manera infalible contra el catálogo existente (`Games`), la cola de espera (`PendingBggImports`) y la tabla de staging (`BggCatalogStaging`).
2. **Ampliación del Modelo de Dominio (`CatalogQueueOrigin`)**:
   - Incorporar los nuevos valores de enumeración:
     - `BggNewReleases = 3`: Títulos descubiertos por ser lanzamientos recientes del año.
     - `BggHotness = 4`: Títulos en tendencia mundial detectados en el Hotness de BGG.
3. **Integración en el Lote Nocturno (`NightlyCatalogingService`)**:
   - Incorporar una nueva fase: **Fase 1.5: Auto-descubrimiento de Novedades BGG**.
   - Registrar métricas de nuevos descubrimientos en `NightlyCatalogingExecutionLog` (`BggDiscoveryCount`).
   - Respetar de forma estricta las cuotas de rate limiting hacia BGG y las cuotas de síntesis IA de Gemini.
4. **Herramientas de Administración y UI Editorial (`CatalogQueueAdmin.razor`)**:
   - Botón de acción: *"Escanear Novedades BGG"* para lanzamiento bajo demanda por administradores.
   - Filtro por origen en la tabla de la cola para inspeccionar `BggNewReleases` y `BggHotness`.
   - Badges visuales editoriales sin emojis, usando componentes accesibles e iconos Lucide (`TrendingUp`, `Sparkles`).
5. **Simulación Determinista y Pruebas Unitarias**:
   - Actualización de `SimulatedBggClient` con dataset de lanzamientos y tendencias recientes.
   - Pruebas unitarias completas con Fakes para el servicio de descubrimiento, deduplicación, orquestador nocturno y componentes Blazor.

## 3. Criterios de Aceptación
- [ ] `IBggDiscoveryService` rastrea juegos del Hotness y lanzamientos recientes de BGG.
- [ ] Títulos ya existentes en catálogo, cola o staging son descartados limpiamente sin duplicación.
- [ ] Los juegos no catalogados se insertan en `PendingBggImports` con origen `BggNewReleases` o `BggHotness`.
- [ ] `NightlyCatalogingService` ejecuta el descubrimiento en su flujo nocturno y reporta los descubrimientos en el resultado y la bitácora.
- [ ] La interfaz de administración permite ejecutar el escaneo bajo demanda y filtrar los elementos por los nuevos orígenes.
- [ ] 100% de la suite de pruebas unitarias en verde sin regresiones.
