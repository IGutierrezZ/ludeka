# Tasks: INC-56 — Comunidad, Mecenazgo y Enlaces de Apoyo

> **Cambio:** `change-56-comunidad-mecenazgo` · modo ODD · rama `inc/comunidad-mecenazgo` · worktree `C:\repos\ludeka-wt\comunidad-mecenazgo`  
> **Línea base:** 1.639 unitarias + 10 de integración

---

## Fase 1 — Opciones de Configuración y Motor de Afiliación (ODD-1)

- [x] **1.1** Modificar `src/Ludeka.Application/Options/CommunityNotificationOptions.cs`: Añadir `public string KofiUrl { get; set; } = "https://ko-fi.com/ludeka";`.
- [x] **1.2** Modificar `src/Ludeka.Application/Options/AffiliateOptions.cs`: Añadir regla para `"Amazon"` en `CreateDefaultRules()` con `ParamName = "tag"`, `AffiliateTag = "ludeka-21"`, `DomainMatch = "amazon.es"`.
- [x] **1.3** Modificar `src/Ludeka.Web/appsettings.json` y `docker-compose.prod.yml`: registrar `KofiUrl` bajo `CommunityNotifications` y el nodo `Amazon` bajo `Affiliates.Stores`.
- [x] **1.4** Modificar `tests/Ludeka.UnitTests/Application/AffiliateUrlResolverTests.cs`: Añadir pruebas para resolución de enlaces de Amazon (`ResolveAffiliateUrl_WhenAmazonUrl_AppendsTagParam`).

## Fase 2 — Componentes de Interfaz Blazor (ODD-2)

- [x] **2.1** Modificar `src/Ludeka.Web/Components/Layout/MainLayout.razor`: en el footer, renderizar botón para Ko-fi condicionado a `!string.IsNullOrWhiteSpace(CommunityOptions?.Value?.KofiUrl)`.
- [x] **2.2** Modificar `src/Ludeka.Web/Components/Pages/Transparency.razor`: inyectar `IOptions<CommunityNotificationOptions>` y usar `@CommunityOptions.Value.KofiUrl`, `@CommunityOptions.Value.DiscordInviteUrl` y `@CommunityOptions.Value.TelegramChannelUrl`.

## Fase 3 — Pruebas de Contrato y Verificación (ODD-3)

- [x] **3.1** Crear `tests/Ludeka.UnitTests/Web/CommunityAndSupportLinksContractTests.cs`: contratos de fuente para `MainLayout.razor` y `Transparency.razor`, validando la presencia de los enlaces, atributos de seguridad (`rel="noopener noreferrer"`) y opciones tipadas.
- [x] **3.2** Ejecutar `dotnet test Ludeka.sln` y certificar que la suite completa pasa en verde (1.646 unitarias + 10 integración, 1.656 total).

## Fase 4 — Documentación Viva, SDD y PR (ODD-4)

- [x] **4.1** Actualizar módulos `08-notificaciones-y-webhooks.md` y `25-motor-afiliados-y-atribucion-comunitaria.md` en `docs/specs/sistema/` y total de pruebas en `README.md`.
- [x] **4.2** Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
- [x] **4.3** Generar `verify-report.md`, archivar el incremento a `docs/increments/archive/inc-56-comunidad-mecenazgo.md` y mover el cambio a `openspec/changes/archive/`.
- [ ] **4.4** Ejecutar `scripts/sdd-worktree.ps1 pr comunidad-mecenazgo` para abrir el Pull Request formal.
