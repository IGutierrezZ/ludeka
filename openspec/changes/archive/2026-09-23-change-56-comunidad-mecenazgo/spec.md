# Spec: change-56-comunidad-mecenazgo (INC-56: Comunidad y Mecenazgo)

> Modo híbrido · Español castellano · Fuentes: `docs/increments/inc-56-comunidad-mecenazgo.md`, `odd/tasks/inc-56-comunidad-mecenazgo.md`.

---

## 1. Requisitos de Opciones y Configuración

### Requirement: Canales de apoyo y comunidad en opciones tipadas

`CommunityNotificationOptions` DEBE declarar la propiedad `KofiUrl` con valor por defecto `"https://ko-fi.com/ludeka"`, conviviendo armónicamente con `DiscordInviteUrl` y `TelegramChannelUrl`.

#### Scenario: Opciones por defecto contienen Discord, Telegram y Ko-fi
- GIVEN una instancia de `CommunityNotificationOptions` creada sin configuración externa
- WHEN se consultan sus propiedades
- THEN `DiscordInviteUrl` es `"https://discord.gg/ludeka"`
- AND `TelegramChannelUrl` es `"https://t.me/ludeka"`
- AND `KofiUrl` es `"https://ko-fi.com/ludeka"`.

#### Scenario: URL de Ko-fi configurable por entorno
- GIVEN configuración que inyecta `CommunityNotifications:KofiUrl`
- WHEN el servicio lee `IOptions<CommunityNotificationOptions>`
- THEN la propiedad `KofiUrl` adopta el valor provisto.

---

## 2. Requisitos de Afiliación

### Requirement: Regla de afiliación de Amazon por defecto

`AffiliateOptions.CreateDefaultRules()` DEBE incluir una regla para `"Amazon"` con `ParamName = "tag"`, `AffiliateTag = "ludeka-21"` y `DomainMatch = "amazon.es"`. `AffiliateUrlResolver` DEBE aplicar esta regla sobre enlaces de productos de Amazon.

#### Scenario: Reglas por defecto incluyen Amazon
- GIVEN `AffiliateOptions.CreateDefaultRules()`
- WHEN se busca la clave `"Amazon"`
- THEN la regla existe
- AND su `ParamName` es `"tag"`
- AND su `AffiliateTag` es `"ludeka-21"`
- AND su `DomainMatch` es `"amazon.es"`.

#### Scenario: Inyección del tag de afiliado en URLs de Amazon
- GIVEN una URL de producto `https://www.amazon.es/dp/B07MZT`
- WHEN `AffiliateUrlResolver.ResolveAffiliateUrl` la procesa
- THEN la URL resultante incluye `tag=ludeka-21`.

#### Scenario: Conservación de query params y hash fragments en Amazon
- GIVEN una URL `https://www.amazon.es/dp/B07MZT?ref=old#reviews`
- WHEN `AffiliateUrlResolver.ResolveAffiliateUrl` la procesa
- THEN la URL resultante contiene `tag=ludeka-21`
- AND preserva el hash fragment `#reviews` al final.

---

## 3. Requisitos de Interfaz (UI Blazor)

### Requirement: Botón de mecenazgo en el pie de página global

`MainLayout.razor` DEBE renderizar en su pie de página un botón hacia `KofiUrl` cuando la opción no sea nula ni vacía, junto a los botones de Discord y Telegram, cumpliendo con accesibilidad WCAG 2.2 AA.

#### Scenario: Botón de Ko-fi visible con opción configurada
- GIVEN `CommunityOptions.Value.KofiUrl` no nula
- WHEN se renderiza el pie de página de `MainLayout.razor`
- THEN aparece un enlace `<a>` apuntando a `KofiUrl` con icono `coffee` y texto «Apoyar en Ko-fi»
- AND cuenta con atributos `target="_blank"` y `rel="noopener noreferrer"`.

### Requirement: Página de transparencia con canales reales

`Transparency.razor` DEBE consumir `IOptions<CommunityNotificationOptions>` y renderizar botones hacia `KofiUrl`, `DiscordInviteUrl` y `TelegramChannelUrl`, eliminando URLs genéricas hardcodeadas.

#### Scenario: Enlaces de Transparencia consumen opciones del sistema
- GIVEN `Transparency.razor`
- WHEN se inspecciona su marcado
- THEN los botones enlazan a las propiedades de `CommunityOptions`
- AND incluye acceso a Ko-fi, Discord y Telegram.

---

## 4. Requisitos de Calidad y Pruebas

### Requirement: Suite completa sin regresiones

La suite de pruebas DEBE incluir pruebas de contrato para la UI y pruebas unitarias para el resolvedor de afiliados, manteniendo todos los tests en verde ($\ge 1.639$ unitarias + 10 de integración).

#### Scenario: Suite completa pasando al 100%
- GIVEN el proyecto con los cambios aplicados
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN el código de salida es 0 con 0 pruebas fallidas.
