# INC-56: Comunidad, Mecenazgo y Enlaces de Apoyo

> **Estado:** ✅ Archivado (completado y verificado)  
> **Fecha de Inicio:** 2026-09-23 · **Fecha de Cierre:** 2026-09-23  
> **Rama de Trabajo:** `inc/comunidad-mecenazgo`  
> **Worktree:** `C:\repos\ludeka-wt\comunidad-mecenazgo`  
> **Dependencias:** —  
> **Especificación Viva:** [08. Notificaciones y Webhooks](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md) · [25. Motor de Afiliados, Atribución BGG, Comunidad y Modo Producción de APIs](file:///c:/repos/Ludeka/docs/specs/sistema/25-motor-afiliados-y-atribucion-comunitaria.md)  


---

## 1. Cómo se descubrió

El manifiesto de transparencia y el modelo de negocio (mecenazgo Ko-fi + afiliación) existen en la especificación, pero al revisar la UI viva no aparece ninguna puerta de entrada a la comunidad ni al apoyo económico.

## 2. El agujero, verificado

- **Cero enlaces públicos** de Ko-fi, Discord o Telegram renderizados en la UI viva (barrido sin resultados).
- `docker-compose.prod.yml:30,33` define `CommunityNotifications__DiscordInviteUrl` y `CommunityNotifications__TelegramChannelUrl` con defaults `https://discord.gg/ludeka` y `https://t.me/ludeka`, pero **no está verificado** que ninguna vista los renderice (hueco de evidencia: puede que solo alimenten notificaciones).
- `Transparency.razor:28` menciona la afiliación Amazon/Ko-fi en texto, pero sin enlaces de mecenazgo accionables.
- Motor de afiliados previo ya existe: `GamePurchaseLink`, `AffiliateUrlResolver` (INC-37, archivado).
- `GamePurchaseLinkTests.cs:103` usa `amazon.es/dp/B07MZT?tag=ludeka-21`: el tag de afiliado **solo vive en un test**, no se ha verificado en producción.
- `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md:~289` define el rol de «Mecenas Ko-fi» sin implementación de puerta de apoyo.

## 3. Lo que pide el maintainer

Hacer visible la comunidad (Discord, Telegram) y el mecenazgo (Ko-fi) de forma honesta, sin romper el manifiesto de transparencia, y cerrar el hueco entre el motor de afiliados documentado y su configuración real.

## 4. Alcance y decisiones que hay que tomar

1. **Decisión abierta:** renderizar o no las invites por defecto de `docker-compose.prod.yml` mientras no se confirmen URLs reales.
2. Puerta de comunidad/mecenazgo en UI (pie o zona de transparencia) con enlaces configurables.
3. Verificar y fijar la configuración real del tag de afiliado fuera de los tests.
4. Alinear `Transparency.razor` con los enlaces efectivos (afiliación y Ko-fi).

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Roles o insignias de mecenas, pasarela de pagos propia y cambios en el pipeline de notificaciones de INC-09.

## 6. Criterios de aceptación

1. La UI muestra enlaces de comunidad y mecenazgo que resuelven a URLs configuradas (no a placeholders muertos).
2. El tag de afiliado está declarado en configuración y consumido por `AffiliateUrlResolver`, no hardcodeado solo en tests.
3. `Transparency.razor` describe exactamente los mecanismos de apoyo activos.
4. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos
- Enlaces rotos o inventados si se renderizan los defaults sin verificar (falsedad documental) — *Mitigado mediante inyección de `CommunityNotificationOptions` con valores configurables por entorno y pruebas de contrato.*
- Requisitos legales de disclosure de afiliación mal cubiertos — *Mitigado con avisos explícitos en pie de página y página de transparencia `/transparencia` con atributos `rel="noopener noreferrer sponsored"` y `rel="noopener noreferrer"`.*
- Colisión de claves de configuración con el sistema de notificaciones de comunidad — *Mitigado unificando las URLs bajo `CommunityNotificationOptions` (`DiscordInviteUrl`, `TelegramChannelUrl`, `KofiUrl`).*

---

## 8. Resultados de la Verificación

- **Pruebas Unitarias:** 1.646 pasadas (0 errores).
- **Pruebas de Integración:** 10 pasadas (0 errores).
- **Total Verificado:** 1.656 pruebas automatizadas en verde.
- **Archivos Modificados:**
  - `src/Ludeka.Application/Options/CommunityNotificationOptions.cs`
  - `src/Ludeka.Application/Options/AffiliateOptions.cs`
  - `src/Ludeka.Web/appsettings.json`
  - `docker-compose.prod.yml`
  - `src/Ludeka.Web/Components/Layout/MainLayout.razor`
  - `src/Ludeka.Web/Components/Pages/Transparency.razor`
  - `tests/Ludeka.UnitTests/Application/AffiliateUrlResolverTests.cs`
  - `tests/Ludeka.UnitTests/Web/CommunityAndSupportLinksContractTests.cs`
- **Módulos de la Especificación Viva Actualizados:**
  - `docs/specs/sistema/08-notificaciones-y-webhooks.md`
  - `docs/specs/sistema/25-motor-afiliados-y-atribucion-comunitaria.md`
  - `docs/specs/sistema/README.md`

