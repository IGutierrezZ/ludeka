# Documento Vivo ODD — INC-56: Comunidad, Mecenazgo y Enlaces de Apoyo

> **Feature:** `comunidad-mecenazgo`  
> **Fichero:** `odd/tasks/inc-56-comunidad-mecenazgo.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-56-comunidad-mecenazgo/`  
> **Rama:** `inc/comunidad-mecenazgo`  
> **Worktree:** `C:\repos\ludeka-wt\comunidad-mecenazgo`  
> **Creado:** 2026-09-23 · **Ruta:** rama `inc/comunidad-mecenazgo` → PR a `main`  

---

## 1. Objetivo

Hacer visibles, honestos y plenamente operativos los canales de comunidad (Discord, Telegram) y las vías de apoyo y mecenazgo voluntario (Ko-fi) en la interfaz viva de Ludeka (pie de página general y página de transparencia), respaldados por opciones de configuración tipadas y variables de entorno. Asimismo, integrar formalmente la regla de afiliación de Amazon en el motor de afiliados de producción (`AffiliateOptions` y `appsettings.json`), sustituyendo la referencia aislada que existía solo en tests.

---

## 2. Problema y Diagnóstico

1. **Cero visibilidad de Ko-fi en la interfaz global:**
   El pie de página general en [`MainLayout.razor`](file:///C:/repos/ludeka-wt/comunidad-mecenazgo/src/Ludeka.Web/Components/Layout/MainLayout.razor) cuenta con botones condicionales para Discord y Telegram, pero no dispone de ningún botón ni mención para el mecenazgo en Ko-fi.
2. **URLs genéricas / placeholders en Transparencia:**
   En [`Transparency.razor`](file:///C:/repos/ludeka-wt/comunidad-mecenazgo/src/Ludeka.Web/Components/Pages/Transparency.razor), los enlaces de apoyo apuntan a `https://ko-fi.com` y `https://discord.com` genéricos y hardcodeados, sin leer las URLs configuradas en el sistema, y el canal oficial de Telegram no aparece listado en este bloque.
3. **Ausencia de Amazon en la configuración de afiliados:**
   El manifiesto de transparencia cita explícitamente: *«Nuestras únicas vías de ingreso provienen de enlaces de compra contextuales (afiliación con tiendas de confianza y Amazon)»*, pero `AffiliateOptions.CreateDefaultRules()` y `appsettings.json` solo registran Zacatrus, Mathom, DungeonMarvels, CuartoDeJuegos y Tablerum. El tag `ludeka-21` de Amazon solo figuraba como cadena literal en una prueba unitaria, sin soporte nativo en el resolvedor de compras.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **`CommunityNotificationOptions.cs`:** Incorporar la propiedad `KofiUrl` con valor por defecto `"https://ko-fi.com/ludeka"`.
- **`AffiliateOptions.cs`:** Registrar la regla oficial para `"Amazon"` (`ParamName = "tag"`, `AffiliateTag = "ludeka-21"`, `DomainMatch = "amazon.es"`).
- **`appsettings.json`:** Reflejar `KofiUrl` bajo `CommunityNotifications` y el nodo `Amazon` bajo `Affiliates.Stores`.
- **`docker-compose.prod.yml`:** Proveer variables de entorno `CommunityNotifications__KofiUrl` y `Affiliates__Stores__Amazon__AffiliateTag`.
- **`MainLayout.razor`:** Renderizar botón accesible para Ko-fi en el pie de página junto a Discord y Telegram si `KofiUrl` está configurado.
- **`Transparency.razor`:** Inyectar `IOptions<CommunityNotificationOptions>` y utilizar `@CommunityOptions.Value.KofiUrl`, `@CommunityOptions.Value.DiscordInviteUrl` y `@CommunityOptions.Value.TelegramChannelUrl`.
- **Pruebas automáticas:**
  - Pruebas en `AffiliateUrlResolverTests.cs` verificando la resolución de enlaces de Amazon.
  - Nueva batería de pruebas de contrato en `CommunityAndSupportLinksContractTests.cs`.
- **Módulos del Sistema y Roadmap:** Actualización de módulos 08 y 25 de la especificación viva y avance del registro central de incrementos.

### Fuera de Alcance:
- Pasarela interna de pago con tarjeta en Ludeka (el mecenazgo se delega externamente en Ko-fi).
- Sistema automatizado de asignación de roles mediante webhooks de Ko-fi (se posterga a los incrementos de perfil y gamificación).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Opción `KofiUrl` en `CommunityNotificationOptions` | Agrupa de forma cohesiva los canales comunitarios y de soporte (`DiscordInviteUrl`, `TelegramChannelUrl`, `KofiUrl`) bajo el namespace ya consumido por los layouts. |
| **D-02** | Regla de Amazon en `AffiliateOptions` | Estandariza la inyección del parámetro `tag=ludeka-21` para dominios `amazon.es` y preserva fragmentos/rutas según la heurística probada de `AffiliateUrlResolver`. |
| **D-03** | Accesibilidad WCAG 2.2 AA en botones de apoyo | Iconografía Lucide (`coffee`, `message-circle`, `send`), foco visible con anillos de contraste (`focus-visible:ring-2`), `target="_blank"`, `rel="noopener noreferrer"` y etiquetas descriptivas legibles. |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Opciones y Motor de Afiliación (Backend)**
  - [x] 1.1 Añadir `KofiUrl` a `src/Ludeka.Application/Options/CommunityNotificationOptions.cs`.
  - [x] 1.2 Añadir regla por defecto para Amazon en `src/Ludeka.Application/Options/AffiliateOptions.cs`.
  - [x] 1.3 Actualizar `src/Ludeka.Web/appsettings.json` y `docker-compose.prod.yml` con `KofiUrl` y `Amazon`.
  - [x] 1.4 Añadir pruebas para Amazon en `tests/Ludeka.UnitTests/Application/AffiliateUrlResolverTests.cs`.
- [x] **ODD-2 — Interfaz Web y Transparencia (Frontend)**
  - [x] 2.1 Añadir botón de Ko-fi en el footer de `src/Ludeka.Web/Components/Layout/MainLayout.razor`.
  - [x] 2.2 Inyectar `CommunityOptions` y usar URLs reales para Ko-fi, Discord y Telegram en `src/Ludeka.Web/Components/Pages/Transparency.razor`.
- [x] **ODD-3 — Pruebas de Contrato y Verificación**
  - [x] 3.1 Crear `tests/Ludeka.UnitTests/Web/CommunityAndSupportLinksContractTests.cs`.
  - [x] 3.2 Ejecutar suite completa `dotnet test Ludeka.sln` y asegurar verde total (1.646 unitarias + 10 de integración, 1.656 total).
- [x] **ODD-4 — Especificación Viva, SDD y PR**
  - [x] 4.1 Actualizar módulos 08 y 25 en `docs/specs/sistema/` y total de pruebas en `README.md`.
  - [x] 4.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 4.3 Generar artefactos SDD en `openspec/changes/change-56-comunidad-mecenazgo/` y archivar.
  - [ ] 4.4 Ejecutar `scripts/sdd-worktree.ps1 pr comunidad-mecenazgo`.

---

## 6. Verificación Final y Resultados

- **Pruebas Unitarias:** 1.646 superadas (0 fallos).
- **Pruebas de Integración:** 10 superadas (0 fallos).
- **Total:** 1.656 pruebas automatizadas en verde.
- **Compilación de Ludeka.sln:** 0 errores, 0 advertencias nuevas.

