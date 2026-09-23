# 22. Portada Minimalista y Directorio de Creadores de Contenido

> **Estado del Módulo:** ✅ Implementado y Verificado — **parcialmente superado por INC-35** (ver nota)  
> **Incremento Asociado:** [INC-31 — cambio SDD `portada-minimalista-creadores` (archivado)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-08-portada-minimalista-creadores/proposal.md)  
> **Pruebas Automatizadas:** 18 pruebas nuevas dedicadas (suite total **723/723** en verde; verificación SDD PASS con 15/15 requerimientos y 22/22 escenarios COMPLIANT)  
> **⚠️ Superación parcial (INC-35):** el hero minimalista de este módulo (§2.1, §2.2 parcialmente, decisión D3 y la fila `/` de §7) fue **reemplazado por el hero editorial con titular visible** del [módulo 23](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md). Siguen vigentes aquí: el directorio de creadores, el alias `/radar` silencioso, el bugfix del enlace de novedades y el reetiquetado Autores → Creadores.  

---

## 1. Propósito y Filosofía

Este módulo corrige de rumbo el producto sobre las bases de INC-19 (directorio) e INC-21 (dashboard de portada). La portada era un dashboard editorial saturado —con un titular que anunciaba una marca ajena— y el directorio "Autores" estaba sesgado a **diseñadores de juegos**, cuando la visión real de Ludeka es un directorio de **creadores de contenido** con redes sociales.

Se implementa bajo los siguientes pilares:

1. **Hero minimalista:** la portada se reduce a párrafo + buscador rápido + 4 píldoras de acceso. Sin badge decorativo ni titular visible; el único `h1` del documento es un `sr-only` con texto neutro propio (WCAG 2.2 AA).
2. **Cero URLs rotas:** el diseñador de juego deja de enlazar al directorio (evita enlaces rotos sistemáticos) y los alias `/autores` y `/radar` se conservan como rutas silenciosas.
3. **Directorio honesto:** `/creadores` lista exclusivamente creadores de contenido reales (padrón estático de canales), con fichas que exponen redes sociales y sin la sección "Obras" heredada de la era de diseñadores.
4. **Fuente única de datos:** el padrón de creadores vive una sola vez (`ChannelFocusProvider`) y el seeder lo consume; los nombres no se duplican en el sembrado.
5. **Idempotencia quirúrgica:** la purga de diseñadores retirados usa una lista cerrada de slugs de semilla y jamás toca creators creados manualmente por moderadores.

---

## 2. Alcance Funcional

### 2.1 Hero Minimalista (`HomeDashboard.razor`)
- Se eliminan el badge decorativo ("PORTADA EDITORIAL") y el titular visible que anunciaba una marca ajena de terceros.
- El documento de portada conserva un único `h1`, visualmente oculto (`sr-only`), con el texto exacto `Ludeka — Juegos de mesa en español` (em dash U+2014 y ñ U+00F1 verificados por code points en render real).
- El párrafo introductorio y el buscador rápido (`form` → `HandleQuickSearch` → `/catalogo?q={término}`; con término vacío redirige a `/catalogo`) quedan intactos.

### 2.2 Píldoras de Acceso Finales (D4)
Exactamente 4 píldoras, en orden fijo, con destinos exactos:

| Píldora | Destino |
|---|---|
| 🎲 Catálogo Completo | `/catalogo` |
| 🎁 Sorteos (re-etiquetada desde "Radar & Sorteos") | `/sorteos` |
| 📰 Novedades | `/novedades` |
| 🎪 Eventos | `/eventos` |

Quedan fuera del hero las píldoras de Autores y Editoriales (siguen accesibles desde la navegación principal).

### 2.3 Bugfix: Enlace "Ver todas las novedades"
El enlace inferior de la portada "Ver todas las novedades" apuntaba erróneamente a `/radar` (Sorteos); ahora apunta a `/novedades` (página de novedades).

### 2.4 Página de Sorteos sin Banner Legacy (`Radar.razor`)
- Eliminado el banner "¡Radar renovado!" y su condición `IsLegacyRoute` (junto con el `@inject NavigationManager` que solo lo alimentaba).
- `/radar` continúa resolviendo la página de sorteos como **alias silencioso** de `/sorteos`: mismo contenido, sin página de error, sin avisos de migración.

### 2.5 Directorio de Creadores de Contenido
- **Listado público** (`/creadores`): 12 creadores de contenido del padrón (incluido Análisis Parálisis con su red de YouTube); 0 diseñadores.
- **Ficha** (`/creadores/{slug}`): nombre, bio, avatar (fallback de iniciales) y componente `SocialLinksList` con las redes sociales. Ya no existe la sección "Obras de X".
- **Alta y edición** con permiso `CanManageCreators` (modal `CreatorEditModal` con editor de redes); el servicio audita con la etiqueta "Creador de Contenido".
- **Sembrado con purga (D1):** los 6 diseñadores de la semilla antigua se purgan y se re-siembra desde el padrón estático (ver §5).
- **Diseñador texto plano en fichas de juego:** en `GameDetail.razor`, "Diseñado por {Designer}" se muestra como `<span>` plano (fallback "Desconocido"), sin enlace al directorio. El bloque de editoriales ("Publicado en España por…") permanece intacto.

### 2.6 Reetiquetado Transversal Autores → Creadores
Navegación (`MainLayout`), fichas, modal de edición, etiquetas de auditoría (`AuditService`, `AuditLogViewer`) y permisos (`UserPermissionsModal`: "Gestionar Creadores de Contenido") usan la nueva terminología. Fuera de alcance por decisión: `LibraryStatsDashboard.razor` ("Top Autores/Editoriales" es estadística de catálogo derivada de `Game.Designer`, que persiste en el modelo de juego) y el `<PageTitle>` de la portada.

---

## 3. Decisiones de Diseño (D1–D5)

| # | Decisión | Implementación |
|---|---|---|
| **D1** | Purga + re-siembra solo de creadores de contenido | Lista cerrada `RetiredSeedCreatorSlugs` (6 slugs exactos) + siembra aditiva desde el padrón (AD-1/AD-2) |
| **D2** | Alias `/autores` mantenido (directorios y fichas) | `@page "/autores"` y `@page "/autores/{Slug}"` intactos; cero bookmarks rotos |
| **D3** | Hero = subtítulo + buscador + h1 `sr-only` | Único `h1` del documento con texto fijo; buscador y párrafo sin cambios |
| **D4** | Píldoras Catálogo/Sorteos/Novedades/Eventos | 4 píldoras exactas con destinos exactos (ver §2.2) |
| **D5** | Scrapeo social nocturno (Instagram/TikTok) fuera de alcance | Trabajo futuro (candidato INC-32+); el pipeline YouTube ya consume las redes vía `ChannelDirectoryProvider` |

Decisiones de arquitectura asociadas (AD-1 a AD-7) y su trazabilidad completa: [design.md del cambio archivado](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-08-portada-minimalista-creadores/design.md).

---

## 4. Arquitectura Tocada (por capa)

| Capa | Componentes | Cambio |
|---|---|---|
| **Infrastructure** | `YouTube/ChannelFocusProvider.cs` | Nuevo accesor estático `GetStaticCreators()` (padrón fuente única) |
| **Infrastructure** | `Seeding/DirectorySeeder.cs` | `RetiredSeedCreatorSlugs` + purga idempotente + re-siembra aditiva desde el padrón |
| **Application** | `Features/Directory/CreatorService.cs` | Desacople total de `Game.Designer`: constructor sin `IGameRepository`, sin conteos ni listado de obras; textos de auditoría "creador de contenido" |
| **Application** | `DTOs/DirectoryDtos.cs` | `CreatorDto` sin `GamesCount`; `CreatorDetailDto` sin `Games` |
| **Application** | `Features/Admin/AuditService.cs` | Etiqueta de auditoría "Creador de Contenido" |
| **Web** | `Pages/HomeDashboard.razor` | Hero minimalista, píldoras D4, fix del enlace de novedades |
| **Web** | `Pages/Radar.razor` | Banner legacy, `IsLegacyRoute` e inject fuera; alias silencioso conservado |
| **Web** | `Pages/GameDetail.razor` | Diseñador como texto plano |
| **Web** | `Pages/CreatorsDirectory.razor`, `Pages/CreatorDetail.razor` | Reetiquetado; sin badge "obras"; ficha sin sección "Obras" |
| **Web** | `Shared/CreatorEditModal.razor`, `Shared/UserPermissionsModal.razor`, `Layout/MainLayout.razor`, `Pages/AuditLogViewer.razor` | Reetiquetado transversal |
| **Core** | — | Sin cambios: entidad `Creator` + `SocialNetworkLink` + `SocialPlatform` ya modeladas en INC-19; sin migraciones EF |

---

## 5. Flujo de Sembrado y Purga

```
Arranque (Program.cs → seeder de directorio)
   └─> DirectorySeeder.SeedCreatorsAsync(db)
         ├─ 1. PURGA:  db.Creators WHERE Slug ∈ RetiredSeedCreatorSlugs → DELETE   (idempotente)
         └─ 2. SIEMBRA: ChannelFocusProvider.GetStaticCreators()                    (fuente única)
               └─ por cada entrada: slug = GenerateSlug(ChannelName)
                    ├─ slug existe (p. ej. analisis-paralisis) → SKIP (sin update)
                    └─ slug nuevo → INSERT Creator + SocialLink YouTube
   ─── lectores ───
   /creadores            → CreatorService.GetAllAsync   (sin Game.Designer)
   /creadores/{slug}     → CreatorService.GetBySlugAsync (solo SocialLinks)
   fichas de juego       → GameDetail: Designer como TEXTO PLANO (sin cruce)
   Pipeline YouTube      → ChannelDirectoryProvider lee SocialLinks (INTACTO)
```

Garantías de la purga:
- **Solo semillas retiradas:** solo se borran filas cuyo slug pertenece a la lista cerrada de 6 slugs (`elizabeth-hargrave`, `klaus-teuber`, `uwe-rosenberg`, `bruno-cathala`, `jacob-fryxelius`, `jamey-stegmaier`). Los creators creados manualmente por moderadores sobreviven siempre (test T3).
- **Aditiva y sin updates:** si el slug ya existe (p. ej. el creator manual de Análisis Parálisis) se omite sin sobrescribir, respetando ediciones de usuarios.
- **Idempotente:** re-ejecuciones sucesivas no crean duplicados (test T2). Tras el primer arranque la purga es no-op.
- **Reversible por diseño:** `git revert` del seeder re-siembra los diseñadores; los creators de contenido quedan como filas válidas.

---

## 6. Interfaces y Contratos

```csharp
// ChannelFocusProvider (nuevo, Infrastructure)
public static IReadOnlyList<ChannelFocusEntry> GetStaticCreators()
    => Channels.Where(c => c.Category == ChannelCategory.Creator).ToList();

// DirectorySeeder (Infrastructure) — lista cerrada de semillas retiradas
private static readonly string[] RetiredSeedCreatorSlugs =
    ["elizabeth-hargrave", "klaus-teuber", "uwe-rosenberg", "bruno-cathala", "jacob-fryxelius", "jamey-stegmaier"];

// DTOs (Application) — contratos recortados
public record CreatorDto(Guid Id, string Name, string Slug, string? Nationality, string? Bio,
    string? AvatarUrl, int? BggPersonId, string? WebsiteUrl,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks, DateTimeOffset CreatedAt);           // sin GamesCount
public record CreatorDetailDto(Guid Id, string Name, string Slug, string? Nationality, string? Bio,
    string? AvatarUrl, int? BggPersonId, string? WebsiteUrl,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);                                                            // sin Games

// CreatorService — constructor final (DI sin cambios: resuelve el ctor único)
public CreatorService(ICreatorRepository creatorRepository,
    ICurrentUserService? currentUserService = null, IAuditService? auditService = null);

// ICreatorService — SIN cambios (firmas públicas intactas)
```

---

## 7. Componentes y Rutas

| Ruta | Componente | Comportamiento |
|---|---|---|
| `/` | `HomeDashboard.razor` | *(Superado por INC-35: hero editorial con titular visible — ver [módulo 23](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md). El buscador y las 4 píldoras D4 y el enlace de novedades se conservan)* |
| `/sorteos` | `Radar.razor` | Ruta canónica de sorteos, sin banner legacy |
| `/radar` | `Radar.razor` | Alias silencioso de `/sorteos` (segunda directiva `@page`) |
| `/creadores` | `CreatorsDirectory.razor` | Listado de creadores de contenido (alias `/autores` conservado) |
| `/creadores/{slug}` | `CreatorDetail.razor` | Ficha con redes sociales (alias `/autores/{Slug}` conservado) |
| `/juegos/{slug}` | `GameDetail.razor` | "Diseñado por" como texto plano; bloque editoriales intacto |

---

## 8. Estrategia de Pruebas

Suite final **723/723** (baseline 705 de INC-30 + 16 del incremento + 2 de remediación de cobertura). Distribución de las 18 pruebas nuevas:

| Capa | Pruebas | Archivos | Herramienta |
|---|---|---|---|
| Integración de repositorio (SQLite en memoria) | T1-T3 (purga, idempotencia, conservación) | `DirectorySeederTests.cs` | xUnit + Microsoft.Data.Sqlite |
| Contrato de fuente | T4 (padrón 12 creators) + T6 (12 contratos de markup) | `ChannelFocusProviderTests.cs`, `WebMarkupContractTests.cs` | xUnit (lectura de fuentes) |
| Unit (Application) | T5 (CRUD sin cruce, permisos, ficha sin redes) | `DirectoryServicesTests.cs`, `GranularPermissionsTests.cs` | xUnit 2.9.3 |
| Unit (Web, handler sin bUnit) | Buscador rápido (navegación con query y fallback) | `HomeDashboardQuickSearchTests.cs` | xUnit + `NavigationManager` fake |

Decisiones probatorias: el contrato de fuente (lectura de `.razor`) sustituye a bUnit por presupuesto y determinismo; el render real se confirma en `sdd-verify` con `dotnet run` (portada, `/sorteos`, `/radar`, `/creadores`, `/autores`, ficha de creador: todos HTTP 200 con invariantes del spec).

---

## 9. Seguimiento Posterior (Follow-ups del Cierre)

1. **BUG pre-existente (INC-30), candidato INC-32:** `/juegos/{slug}` responde HTTP 500 por `ORDER BY` sobre `DateTimeOffset` no soportado por SQLite en `SqliteGamePlayLogRepository.GetByUserAndGameAsync` (vía `GameDetail.LoadUserDataAsync`). Fix aprobado por el mantenedor como remediación separada inmediata.
2. **Test flaky ajeno:** `StoreStockServiceTests.GetStockAsync_WhenClientTimesOut_ShouldGracefullyDegradeToUnknown` (timing); falló 1/3 ejecuciones en una verificación anterior; estable en las 2 corridas del cierre.
3. **Cosméticos fuera de contrato:** CTA "Ver obras diseñadas" en `CreatorsDirectory.razor` (renombrar a "Ver ficha") y la palabra "autores" en la microcopy de `AuditLogViewer.razor`; ambos no violan ningún requerimiento del spec.
