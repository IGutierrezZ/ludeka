# 49. Módulo de Retención de Datos, Purga de Caducados y Liberación de Imágenes

> **Estado:** Implementado y Verificado (INC-94)  
> **Incrementos SDD:** `inc-94-retencion-caducados-novedades` (INC-94)  
> **Área:** Mantenimiento, Ciclo de Vida de Datos y Liberación de Medios  
> **Tests:** 2.238 pruebas unitarias pasando al 100% (2.248 con integración).

---

## 1. Propósito y Visión del Módulo

A medida que Ludeka opera en producción, las secciones dinámicas dependientes del tiempo acumulan registros obsoletos que consumen capacidad en la base de datos (PostgreSQL en Supabase / SQLite) y almacenamiento de imágenes (disco local y buckets en Cloudflare R2):
1. **Sorteos (`Giveaway`):** Una vez concluida la fecha de participación (`DeadlineAt`), pierden su valor utilitario para la comunidad tras un período prudencial.
2. **Grandes Eventos (`BoardGameEvent`):** Concluida la última jornada del evento (`EndDate`), los eventos deben cesar en la agenda activa y ser retirados tras su margen de consulta post-evento.
3. **Novedades Editoriales (`WeeklyRelease`):** Artículos de actualidad comercial que anuncian futuros lanzamientos o novedades inmediatas. Algunos anuncios de editoriales no tienen fecha fijada ("Próximamente"), debiendo admitir fecha opcional y expirar a los 60 días salvo que su lanzamiento esté programado para el futuro.
4. **Liberación de Almacenamiento:** Antes de borrar físicamente cualquier entidad de la base de datos, deben eliminarse sus archivos multimedia asociados (carátulas, carteles promocionales e imágenes WebP) tanto en disco local como en el bucket de Cloudflare R2.

---

## 2. Reglas de Negocio y Políticas de Retención

Las políticas de retención se configuran centralmente mediante `DataRetentionOptions`:

```csharp
public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";
    public bool Enabled { get; set; } = true;
    public int GiveawayGracePeriodDays { get; set; } = 7;
    public int EventGracePeriodDays { get; set; } = 7;
    public int ReleaseRetentionDays { get; set; } = 60;
    public int ExecutionHourUtc { get; set; } = 3;
}
```

### 2.1 Criterios Específicos por Entidad

| Entidad | Criterio de Purga | Margen / Condición | Tratamiento de Imágenes |
|---|---|---|---|
| **Sorteos (`Giveaway`)** | `DeadlineAt <= nowUtc.AddDays(-GiveawayGracePeriodDays)` | 7 días de gracia tras vencimiento | Se elimina `ThumbnailUrl` si es imagen gestionada |
| **Eventos (`BoardGameEvent`)** | `EndDate <= today.AddDays(-EventGracePeriodDays)` | 7 días de gracia tras último día de celebración | Se elimina `ImageUrl` si es imagen gestionada |
| **Novedades (`WeeklyRelease`)** | `CreatedAt <= nowUtc.AddDays(-ReleaseRetentionDays)` **Y** (`ReleaseDate == null \|\| ReleaseDate.Value < today`) | $\ge 60$ días en Ludeka y (sin fecha o fecha vencida) | Se elimina `CoverImageUrl` si es imagen gestionada |

> [!NOTE]
> **Preservación de Lanzamientos Futuros:** Si una novedad editorial lleva 60 o más días registrada en Ludeka pero su `ReleaseDate` es futura (`ReleaseDate >= today`), **no se purga**, garantizando que los anuncios anticipados permanezcan visibles hasta su lanzamiento en tiendas.

---

## 3. Extractor de Claves y Liberación de Medios (`ManagedImageKeyExtractor`)

Ubicación: `src/Ludeka.Application/Features/Maintenance/ManagedImageKeyExtractor.cs`

Para evitar intentar eliminar imágenes externas o ajenas a la infraestructura de Ludeka, el extractor analiza la URL de la imagen y determina si corresponde a un activo bajo control propio:

- **URLs Relativas Locales:** Rutas como `/images/events/festival.webp` o `images/community/sorteo.jpg` se normalizan extrayendo la clave relativa (`events/festival.webp`, `community/sorteo.jpg`).
- **URLs Absolutas de CDN / R2:** Si apuntan al dominio público de Cloudflare R2 o CDN propio bajo subcarpetas canónicas (`events/`, `community/`, `games/`), se extrae la clave correspondiente.
- **Dominios Ajenos Excluidos:** Se rechazan expresamente URLs de terceros como `geekdo-images.com`, `boardgamegeek.com`, `instagram.com`, `cdninstagram.com`, `fbcdn.net`, `unsplash.com`, `imgur.com` o `cloudinary.com`.
- **Resiliencia ante Fallos de Almacenamiento:** Si la imagen ya fue eliminada o el servicio de almacenamiento arroja un error puntual, este se registra en la bitácora (`LogWarning`) pero no impide la eliminación física del registro en la base de datos.

---

## 4. Arquitectura de Ejecución e Idempotencia

### 4.1 Servicio de Aplicación (`IDataRetentionService`)
Ubicación: `src/Ludeka.Application/Contracts/IDataRetentionService.cs` e implementación en `src/Ludeka.Application/Features/Maintenance/DataRetentionService.cs`.
- Inyecta `IGiveawayRepository`, `IBoardGameEventRepository`, `IWeeklyReleaseRepository`, `IImageStorageService`, `IOptions<DataRetentionOptions>`, `ILogger` y `TimeProvider` determinista.
- Retorna `DataRetentionResult` con contadores de entidades purgadas e imágenes liberadas o fallidas.

### 4.2 Runner en `Ludeka.Jobs` (`DataRetentionJobRunner`)
Ubicación: `src/Ludeka.Jobs/Runners/DataRetentionJobRunner.cs`
- Registrado como runner oficial en `JobNames.DataRetention` (`"data-retention"`).
- Se coordina con `IJobExecutionCoordinator.ExecuteWithWindowLeaseAsync` empleando una clave de ventana diaria UTC (`JobWindowKeyCalculator.DailyUtc(nowUtc)`).
- Apto para ser disparado diariamente vía Cloud Scheduler en Google Cloud Run Jobs:
  ```bash
  dotnet run --project src/Ludeka.Jobs -- data-retention
  ```

### 4.3 Servicio Hosted en Background (`DataRetentionHostedService`)
Ubicación: `src/Ludeka.Infrastructure/Background/DataRetentionHostedService.cs`
- `BackgroundService` en `Ludeka.Infrastructure` para entornos que requieran ejecución autónoma en segundo plano a la hora configurada (`ExecutionHourUtc: 3`).
- Diseñado para no introducir dependencias cautivas ni registros en el host web de producción (en concordancia con las guardas de `WebHostHostedServiceCompositionTests`).

---

## 5. Pruebas y Cobertura

- `tests/Ludeka.UnitTests/Application/DataRetentionServiceTests.cs`:
  - Pruebas unitarias de discriminación en `ManagedImageKeyExtractor` (URLs relativas, CDN propio y rechazo de dominios externos).
  - Verificación de no-operación cuando `Enabled = false`.
  - Purgas controladas de sorteos y eventos respetando el margen de gracia de 7 días.
  - Purgas de novedades editoriales aplicando el umbral de 60 días y la regla de fechas cumplidas vs futuras.
  - Resiliencia y eliminación en base de datos aun con fallos en la capa de storage.
- `tests/Ludeka.UnitTests/Jobs/DataRetentionJobRunnerTests.cs`:
  - Validación del identificador `JobNames.DataRetention` y la concesión de ventana diaria.
- `tests/Ludeka.UnitTests/Jobs/LudekaJobsCompositionTests.cs`:
  - Validación de los 10 runners registrados en `Ludeka.Jobs`.
