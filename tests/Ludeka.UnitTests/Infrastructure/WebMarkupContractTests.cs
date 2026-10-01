using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Contrato de markup por archivo fuente (INC-31): lee los .razor/.cs desde la raíz del repo
/// y afirma invariantes estructurales contiene/no-contiene. El render real se verifica en
/// sdd-verify con `dotnet run` (limitación documentada en el diseño: no prueba el DOM).
/// </summary>
public class WebMarkupContractTests
{
    // Emojis prohibidos en la portada (Decisión 6): cada uno tiene su icono Lucide en el catálogo
    private static readonly string[] EmojisDePortada =
    { "🔍", "🎲", "🎁", "📰", "🎪", "🏆", "⭐", "⏱", "🚀", "🔄", "🆕", "🗓", "📅", "📍", "🌐", "🧩" };

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
    }

    public static TheoryData<string, string, string[], string[]> MarkupContracts => new()
    {
        // GameDetail: diseñador como texto plano, sin <a> al directorio de creadores
        { "GameDetail (diseñador texto plano)", "src/Ludeka.Web/Components/Pages/GameDetail.razor",
          new[] { "Diseñado por", "<span class=\"text-[var(--text-primary)] font-bold\">@Game.Designer</span>" },
          new[] { "creadores/", "Ver ficha del autor" } },

        // CreatorDetail: sin "Obras de", reetiquetado a creador de contenido, alias /autores/{slug} conservado
        { "CreatorDetail (ficha reetiquetada)", "src/Ludeka.Web/Components/Pages/CreatorDetail.razor",
          new[] { "@page \"/autores/{Slug}\"", "Ficha del Creador de Contenido", "Cargando ficha del creador...", "Creador de contenido" },
          new[] { "Obras de", "Autor / Diseñador", "Obras y Ficha de Autor", "autor o creador registrado" } },

        // CreatorsDirectory: PageTitle/badge/h1/párrafo nuevos, sin badge de obras, alias /autores conservado
        { "CreatorsDirectory (directorio reetiquetado)", "src/Ludeka.Web/Components/Pages/CreatorsDirectory.razor",
          new[] { "@page \"/autores\"", "Directorio de Creadores de Contenido — Ludeka", "Creadores de Contenido &bull; Divulgadores del Hobby", "hobby hispanohablante" },
          new[] { "Creadores y Autores", "Autoría Lúdica", "GamesCount", "diseñadores, ilustradores y divulgadores referentes" } },

        // MainLayout: navegación y pie reetiquetados a Creadores; el pie solo mantiene
        // Transparencia y Discord (el resto de accesos vive en la nav superior, INC-35)
        { "MainLayout (nav y pie)", "src/Ludeka.Web/Components/Layout/MainLayout.razor",
          new[] { "Creadores", "href=\"/transparencia\"", "Discord" },
          new[] { "Autores" } },

        // CreatorEditModal: formulario reetiquetado
        { "CreatorEditModal (formulario)", "src/Ludeka.Web/Components/Shared/CreatorEditModal.razor",
          new[] { "Nuevo Creador de Contenido", "Nombre del Creador de Contenido", "Ej. Análisis Parálisis, Meepletopía...", "Trayectoria, canales y estilo de divulgación..." },
          new[] { "Creador / Autor", "Nombre Completo del Creador", "Elizabeth Hargrave, Klaus Teuber", "estilo de diseño" } },

        // AuditService: etiqueta de auditoría
        { "AuditService (etiqueta)", "src/Ludeka.Application/Features/Admin/AuditService.cs",
          new[] { "\"Creador de Contenido\"" },
          new[] { "Autor/Creador" } },

        // AuditLogViewer: filtro de auditoría sin "Autor / Diseñador"
        { "AuditLogViewer (filtro)", "src/Ludeka.Web/Components/Pages/AuditLogViewer.razor",
          new[] { "Creador de Contenido" },
          new[] { "Autor / Diseñador" } },

        // UserPermissionsModal: permiso reetiquetado
        { "UserPermissionsModal (permiso)", "src/Ludeka.Web/Components/Shared/UserPermissionsModal.razor",
          new[] { "Gestionar Creadores de Contenido" },
          new[] { "Autores y Diseñadores" } },

        // HomeDashboard: orquestador editorial (Decisión 3) — hero y carriles por componentes,
        // sin markup de card inline, sin emojis, sin h1 propio (vive en el hero) y sin la
        // clase inválida sm:w-68
        { "HomeDashboard (orquestador editorial)", "src/Ludeka.Web/Components/Pages/HomeDashboard.razor",
          new[] { "<HeroEditorial", "Background=\"HeroBackgroundVariant.Ilustracion\"", "<RailHeader", "<HomeGameCard", "<HomeGiveawayCard", "<HomeReleaseCard", "<HomeEventCard", "Name=\"dices\"" },
          new[] { "PORTADA EDITORIAL", "href=\"/radar\"", "sm:w-68", "BggRating", "RemainingTimeText", "<h1", "sr-only" } },

        // Radar: sin banner legacy, alias silencioso con ambas rutas @page
        { "Radar (sin banner legacy)", "src/Ludeka.Web/Components/Pages/Radar.razor",
          new[] { "@page \"/sorteos\"", "@page \"/radar\"" },
          new[] { "¡Radar renovado!", "IsLegacyRoute" } },

        // Icon: SVG Lucide inline, currentColor, aria-hidden por defecto; sin <img> ni peticiones de red.
        // INC-46 F3: acepta atributos adicionales (class, aria-*) aplicados al SVG; sin ese paso
        // directo, las llamadas que los pasaban lanzaban InvalidOperationException al renderizar.
        { "Icon (SVG Lucide inline)", "src/Ludeka.Web/Components/Shared/Icon.razor",
          new[] { "viewBox=\"0 0 24 24\"", "stroke=\"currentColor\"", "aria-hidden=\"true\"", "[Parameter(CaptureUnmatchedValues = true)]", "@attributes=\"AdditionalAttributes\"" },
          new[] { "<img", "http" } },

        // DefaultImage: SVG inline temable por variables de tema; sin <img> roto
        { "DefaultImage (SVG inline temable)", "src/Ludeka.Web/Components/Shared/DefaultImage.razor",
          new[] { "viewBox=\"0 0 400 225\"", "var(--brand-", "var(--bg-", "aria-hidden" },
          new[] { "<img" } },

        // Assets por defecto servibles (variante estática para onerror), uno por dominio
        { "Asset default de eventos", "src/Ludeka.Web/wwwroot/images/defaults/evento-default.svg",
          new[] { "EVENTO LUDEKA", "viewBox=\"0 0 400 225\"" },
          new[] { "game-placeholder" } },
        { "Asset default de sorteos", "src/Ludeka.Web/wwwroot/images/defaults/sorteo-default.svg",
          new[] { "SORTEO LUDEKA", "viewBox=\"0 0 400 225\"" },
          new[] { "game-placeholder" } },
        { "Asset default de novedades", "src/Ludeka.Web/wwwroot/images/defaults/novedad-default.svg",
          new[] { "NOVEDAD", "viewBox=\"0 0 400 225\"" },
          new[] { "game-placeholder" } },
        { "Asset default genérico", "src/Ludeka.Web/wwwroot/images/defaults/generico-default.svg",
          new[] { "LUDEKA", "viewBox=\"0 0 400 225\"" },
          new[] { "game-placeholder" } },

        // Fundación CSS de microinteracciones (Decisión 4): tokens compartidos + .rail-card
        // + tipografía display del hero y de los títulos de carril (Decisiones 7 y 10).
        // INC-36 (PR-1, DD-01..DD-04/DD-06): añade --on-brand y los 4 tokens semánticos de
        // estado por tema, el modelo de altura responsiva del hero (aspect + cap clamp, sin
        // min-height fijo de bloque), los focos autoriales hero-focal--* con object-position
        // y la clase .page-header-title de las cabeceras compartidas.
        { "Fundación CSS (tokens, rail-card y hero)", "src/Ludeka.Web/Styles/input.css",
          new[] { "--ease-out-expo", "--ease-out-quad", "--dur-fast", "--dur-base", "--dur-slow", "--rail-lift", "--rail-zoom", "--font-display: 'Fraunces'",
                  ".rail-card:hover, .rail-card:focus-visible", ".rail-cover--square", ".rail-cover--wide", ".rail-cover--banner",
                  ".scrollbar-none", "prefers-reduced-motion: reduce", ".rail-title", ".hero-actions",
                  "--on-brand", "--state-error", "--state-warning", "--state-info", "--state-highlight",
                  "aspect-ratio: 16 / 9", "max-height: clamp(200px, 36vw, 460px)", ".page-header-title",
                  "hero-focal--eurogame", "hero-focal--mesa-amigos", "hero-focal--primer-plano", "hero-focal--ilustracion",
                  "object-position" },
          new[] { "min-height: 360px", "min-height: 460px" } },

        // PageHeaderEditorial (INC-36 PR-1, DD-06): cabecera compartida de las 4 páginas de
        // listado — badge en píldora, h1 único en serif display y zona de acción opcional;
        // el título es RenderFragment para conservar composición rica (punto terracota)
        { "PageHeaderEditorial (cabecera compartida)", "src/Ludeka.Web/Components/Shared/PageHeaderEditorial.razor",
          new[] { "page-header-title", "<h1", "badge-pill", "Actions" },
          Array.Empty<string>() },

        // EditorialModal (INC-36 PR-1, DD-07): shell compartido de los modales de página —
        // overlay fijo, tarjeta centrada max-w-lg, cierre identificado y diálogo etiquetado;
        // el cuerpo vive como ChildContent, el pie en <Footer> y el foco via ludekaModal
        { "EditorialModal (shell compartido)", "src/Ludeka.Web/Components/Shared/EditorialModal.razor",
          new[] { "role=\"dialog\"", "aria-modal=\"true\"", "aria-label=\"@Title\"", "OnClose", "ChildContent", "Footer", "ludekaModal.open" },
          Array.Empty<string>() },

        // HeroEditorial: hero narrativo con <picture> AVIF/WebP/JPG priorizado (patrón INC-07),
        // escena CSS bajo la foto y altura derivada del ancho con foco autoral por variante
        // (Decisiones 1, 2 y 10 + INC-36 DD-01/DD-02/DD-03: sin min-h fijo, la clase de foco
        // hero-focal--{clave} viaja en la <section> y el botón Buscar usa --on-brand sobre
        // la marca; el chip de pruebas usa un par autocontenido sobre su fondo literal)
        { "HeroEditorial (picture, prioridad y escena CSS)", "src/Ludeka.Web/Components/Home/HeroEditorial.razor",
          new[] { "<picture", "<source type=\"image/avif\"", "<source type=\"image/webp\"",
                  "fetchpriority=\"high\"", "width=\"1600\"", "height=\"900\"",
                  "alt=\"@HeroBackgroundAssets.AltText(Background)\"", "@switch (Background)",
                  "hero-actions",
                  "<h1 class=\"sr-only\">La mesa está servida</h1>",
                  "hero-focal--", "text-[var(--on-brand)]" },
          new[] { "PORTADA EDITORIAL", "alt=\"\"", "hero-text-chip", "hero-scrim", "hero-panel", "hero-title", "Catálogo Completo",
                  "min-h-[360px]", "sm:min-h-[460px]", "text-white" } },

        // RailHeader: cabecera de carril reutilizable con título en serif display, icono Lucide
        // y enlace "Ver todos…" solo cuando hay destino (Decisiones 3 y 7)
        { "RailHeader (cabecera de carril)", "src/Ludeka.Web/Components/Home/RailHeader.razor",
          new[] { "rail-title", "Icon", "href=\"@Href\"" },
          EmojisDePortada },

        // HomeGameCard: carril Top 20 con lenguaje .rail-card y fallback vigente de carátulas
        { "HomeGameCard (Top 20)", "src/Ludeka.Web/Components/Home/HomeGameCard.razor",
          new[] { "rail-card", "rail-cover--square", "game-placeholder.svg", "expansion-placeholder.svg", "loading=\"lazy\"", "onerror" },
          EmojisDePortada },

        // HomeGiveawayCard (INC-79): carril Sorteos; formato vertical catálogo con rail-cover--square
        { "HomeGiveawayCard (Sorteos)", "src/Ludeka.Web/Components/Home/HomeGiveawayCard.razor",
          new[] { "rail-card", "rail-cover--square", "sorteo-default.svg", "DefaultImage", "loading=\"lazy\"", "onerror" },
          EmojisDePortada },

        // HomeReleaseCard (INC-87): carril Novedades; formato vertical catálogo con rail-cover--square
        { "HomeReleaseCard (Novedades)", "src/Ludeka.Web/Components/Home/HomeReleaseCard.razor",
          new[] { "rail-card", "rail-cover--square", "novedad-default.svg", "DefaultImage", "loading=\"lazy\"", "onerror" },
          EmojisDePortada },

        // HomeEventCard (INC-87): carril Eventos; formato vertical catálogo con rail-cover--square
        { "HomeEventCard (Eventos)", "src/Ludeka.Web/Components/Home/HomeEventCard.razor",
          new[] { "rail-card", "rail-cover--square", "evento-default.svg", "DefaultImage", "loading=\"lazy\"", "onerror", "width=", "height=" },
          EmojisDePortada },

        // ===== INC-35 PR-3: migración global de emojis a Icon (Fase 2 — layout, catálogo y fichas) =====

        // MainLayout: nav, utilidades, menú de gestión y pie con iconos Lucide (sin emojis).
        // El pie reducido (Transparencia + Discord) ya no usa pen-line (Creadores solo en nav).
        // Tras INC-62, el acceso a biblioteca pasa al menú desplegable de cuenta.
        { "MainLayout (iconografia Lucide)", "src/Ludeka.Web/Components/Layout/MainLayout.razor",
          new[] { "<Icon Name=\"gift\"", "<Icon Name=\"newspaper\"", "<Icon Name=\"tent\"", "<Icon Name=\"shield\"", "<Icon Name=\"bell\"", "<Icon Name=\"menu\"" },
          new[] { "🎁", "📰", "🎪", "🌍", "📚", "🛡", "👤", "⚙", "🚩", "🌙", "🎬", "📸", "🔔", "👥", "📜", "🏢", "✍", "🛒", "💬", "🗙" } },

        // AccountMenu: menú de cuenta con iconos Lucide (INC-61/INC-62)
        { "AccountMenu (iconografia Lucide)", "src/Ludeka.Web/Components/Shared/AccountMenu.razor",
          new[] { "<Icon Name=\"user\"", "<Icon Name=\"library\"", "<Icon Name=\"palette\"", "<Icon Name=\"globe\"", "<Icon Name=\"shield\"", "<Icon Name=\"link\"", "<Icon Name=\"log-out\"" },
          new[] { "👤", "📚", "🎨", "🌍", "🛡", "🔗", "🚪" } },

        // MainLayout: menú móvil desplegable (details/summary, SSR puro) con los mismos
        // 7 destinos de la nav superior; la nav principal se oculta bajo lg (1024px).
        { "MainLayout (menú móvil)", "src/Ludeka.Web/Components/Layout/MainLayout.razor",
          new[] { "<details", "aria-label=\"Menú de navegación\"", "href=\"/catalogo\"", "href=\"/editoriales\"", "href=\"/creadores\"", "href=\"/tiendas\"", "href=\"/sorteos\"", "href=\"/novedades\"", "href=\"/eventos\"" },
          new string[] { } },

        // GameCard: badges de estilo y público con iconos Lucide (sin emoji, modo solitario en filtro avanzado en INC-90)
        { "GameCard (badges sin emojis)", "src/Ludeka.Web/Components/Shared/GameCard.razor",
          new[] { "<Icon Name=\"puzzle\"", "<Icon Name=\"settings\"", "<Icon Name=\"party-popper\"", "<Icon Name=\"book-open\"", "<Icon Name=\"baby\"" },
          new[] { "🧩", "⚙", "🎉", "📖", "👤", "👶" } },

        // GameDetail: ficha editorial tokenizada (INC-36 PR-3, DD-03/DD-04/DD-08): back-bar
        // envolvente con la moderación agrupada en subcontenedor, botón de marca con --on-brand,
        // estado «no encontrado» legible en los 5 temas, sin errata «Ludeca» y sin hardcodes
        // de estado (familias rose/amber/indigo/purple/slate/orange)
        { "GameDetail (ficha sin emojis)", "src/Ludeka.Web/Components/Pages/GameDetail.razor",
          new[] { "<Icon Name=\"globe\"", "<Icon Name=\"flag\"", "<Icon Name=\"palette\"", "<Icon Name=\"shopping-cart\"", "<Icon Name=\"bot\"", "<Icon Name=\"package\"", "<Icon Name=\"trophy\"",
                  "flex-wrap", "text-[var(--on-brand)]" },
          new[] { "🎲", "🕵", "🌐", "🚩", "🎨", "🛒", "✏", "⚙", "🤖", "🛡", "📚", "🧩", "🏆", "📦", "⚡",
                  "Ludeca", "text-white", "text-slate-400", "bg-amber-500", "bg-indigo-500", "bg-purple-950", "bg-rose-500", "text-orange-400" } },

        // CreatorsDirectory: buscador, spinner, vacío y acciones por iconos Lucide
        { "CreatorsDirectory (sin emojis)", "src/Ludeka.Web/Components/Pages/CreatorsDirectory.razor",
          new[] { "<Icon Name=\"pen-line\"", "<Icon Name=\"plus\"", "<Icon Name=\"search\"", "<Icon Name=\"user\"", "<Icon Name=\"globe\"" },
          new[] { "✍", "➕", "🔍", "🎲", "👤", "🌍", "✏" } },

        // CreatorDetail: spinner, vacío, badge de canal y enlaces externos por iconos Lucide
        { "CreatorDetail (sin emojis)", "src/Ludeka.Web/Components/Pages/CreatorDetail.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"search\"", "<Icon Name=\"pen-line\"", "<Icon Name=\"clapperboard\"", "<Icon Name=\"globe\"" },
          new[] { "🎲", "🔍", "✏", "🎬", "🌍", "🌐" } },

        // Home (/catalogo): elevación visual sin cabecera redundante (INC-90), buscador en bloque propio e iconos Lucide en filtros avanzados
        { "Home catalogo (sin emojis)", "src/Ludeka.Web/Components/Pages/Home.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"puzzle\"", "<Icon Name=\"swords\"", "<Icon Name=\"users\"", "<Icon Name=\"timer\"" },
          new[] { "🎲", "🧩", "⚔", "👨", "👤", "⏱", "<h1", "no-scrollbar" } },

        // StoreOffersCard: cabecera, envíos, recomprobación y nota de afiliación por iconos Lucide
        { "StoreOffersCard (sin emojis)", "src/Ludeka.Web/Components/Shared/StoreOffersCard.razor",
          new[] { "<Icon Name=\"shopping-cart\"", "<Icon Name=\"shield\"", "<Icon Name=\"plane\"", "<Icon Name=\"refresh-cw\"", "<Icon Name=\"package\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"settings\"" },
          new[] { "🛒", "⚙", "🛡", "✈", "🔄", "📦", "💡" } },

        // ===== INC-35 PR-3: migración global de emojis a Icon (Fase 3 — ludoteca y préstamos) =====

        // MyLibrary: pestañas, spinners, vacíos, temas y acciones por iconos Lucide
        { "MyLibrary (sin emojis)", "src/Ludeka.Web/Components/Pages/MyLibrary.razor",
          new[] { "<Icon Name=\"library\"", "<Icon Name=\"dices\"", "<Icon Name=\"package\"", "<Icon Name=\"dna\"", "<Icon Name=\"handshake\"", "<Icon Name=\"undo-2\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"wifi-off\"" },
          new[] { "📥", "🎲", "🔍", "📡", "🔄", "📚", "🛒", "📝", "📦", "⏳", "🎨", "🌍", "🧬", "📖", "🪵", "🌌", "🌑", "⚠", "📍", "📋", "👁", "🤝", "👤", "↩", "🔔", "🧩", "⏱", "👥", "📅" } },

        // CollectionActionBar: estados de colección y préstamos por iconos Lucide
        { "CollectionActionBar (sin emojis)", "src/Ludeka.Web/Components/Shared/CollectionActionBar.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"plus\"", "<Icon Name=\"package\"", "<Icon Name=\"handshake\"", "<Icon Name=\"library\"", "<Icon Name=\"shopping-cart\"", "<Icon Name=\"bell\"" },
          new[] { "🎲", "➕", "📦", "🤝", "📚", "🛒", "🔔" } },

        // LoanModal: cabecera y devolución por iconos Lucide
        { "LoanModal (sin emojis)", "src/Ludeka.Web/Components/Shared/LoanModal.razor",
          new[] { "<Icon Name=\"package\"", "<Icon Name=\"undo-2\"" },
          new[] { "📦", "↩" } },

        // RecordPlayModal: cabecera, error y spinner por iconos Lucide
        { "RecordPlayModal (sin emojis)", "src/Ludeka.Web/Components/Shared/RecordPlayModal.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"settings\"" },
          new[] { "🎲", "⚠", "⚙" } },

        // ReviewBottomSheet: cabecera y contexto familiar por iconos Lucide; semáforo por puntos CSS
        { "ReviewBottomSheet (sin emojis)", "src/Ludeka.Web/Components/Shared/ReviewBottomSheet.razor",
          new[] { "<Icon Name=\"zap\"", "<Icon Name=\"baby\"", "rounded-full bg-emerald-500" },
          new[] { "⚡", "🟢", "🟡", "🔴", "👶", "🏠", "🛡", "👥", "☕", "💻" } },

        // LibraryStatsDashboard: métricas y secciones por iconos Lucide (el badge IconEmoji
        // proviene de datos del backend y queda documentado como excepción de este contrato)
        { "LibraryStatsDashboard (sin emojis)", "src/Ludeka.Web/Components/Features/Library/LibraryStatsDashboard.razor",
          new[] { "<Icon Name=\"hourglass\"", "<Icon Name=\"users\"", "<Icon Name=\"shield\"", "<Icon Name=\"dna\"", "<Icon Name=\"handshake\"", "<Icon Name=\"trophy\"", "<Icon Name=\"building-2\"" },
          new[] { "⏳", "👥", "🛡", "🧬", "🤝", "🐺", "🏆", "🖋", "🏢" } },

        // ===== INC-35 PR-3: migración global de emojis a Icon (Fase 4 — eventos, sorteos y novedades) =====

        // Events: pestañas, spinner, vacío, metadatos y pie por iconos Lucide.
        // INC-36 (PR-4, DD-03/DD-04/DD-06/DD-09): cabecera compartida PageHeaderEditorial,
        // pestañas con relación accesible tabpanel/aria-controls, tarjetas .rail-card y
        // tokens de marca/estado. El text-white queda ACOTADO al patrón de botón de marca
        // (dos fragmentos que cubren las 5 variantes de botón); el par overlay
        // bg-black/60 + text-white de los badges sobre fotografía es la excepción
        // contratada de DD-04 y el mustNotContain se formula sin capturarlo — igual que
        // el chip del hero en PR-1 (text-white/80 y los badges de estado tampoco los
        // contienen). `dark:` es guarda profiláctica (hoy el archivo no lo usa).
        { "Events (sin emojis)", "src/Ludeka.Web/Components/Pages/Events.razor",
          new[] { "<Icon Name=\"tent\"", "<Icon Name=\"calendar-days\"", "<Icon Name=\"map-pin\"", "<Icon Name=\"globe\"", "<Icon Name=\"scroll\"", "<Icon Name=\"settings\"", "rounded-full bg-emerald-500",
                  "<PageHeaderEditorial", "text-[var(--on-brand)]", "rail-card", "role=\"tabpanel\"", "aria-controls=\"panel-" },
          new[] { "🎪", "⚙", "🟢", "📜", "🌍", "⭐", "➕", "🗓", "📍", "🌐",
                  "hover:scale-105", "bg-rose-500/90", "bg-amber-500/90", "text-zinc-300", "dark:",
                  "hover:opacity-90 text-white", ")] text-white" } },

        // EventsManagement: cabecera, acciones, tabla y modal de edición por iconos Lucide
        { "EventsManagement (sin emojis)", "src/Ludeka.Web/Components/Pages/EventsManagement.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"eye\"", "<Icon Name=\"plus\"", "<Icon Name=\"tent\"", "<Icon Name=\"map-pin\"", "<Icon Name=\"pen-line\"", "<Icon Name=\"trash-2\"", "<Icon Name=\"folder\"" },
          new[] { "🛡", "👁", "➕", "🎪", "📍", "⭐", "✏", "🗑", "📁", "🇪🇸", "🌎" } },

        // GiveawayCard: tarjeta editorial con fallback de imagen por dominio, tokens de
        // estado y badges de plataforma/promoción por iconos Lucide (el switch devuelve el
        // nombre del icono en el catálogo, no un emoji).
        { "GiveawayCard (sin emojis)", "src/Ludeka.Web/Components/Shared/GiveawayCard.razor",
          new[] { "<Icon Name=\"@GetPlatformIcon(Giveaway.Platform)\"", "<Icon Name=\"star\"", "<Icon Name=\"gift\"", "<Icon Name=\"dices\"", "<Icon Name=\"camera\"", "GiveawayPlatform.Instagram => \"camera\"",
                  "rail-card", "rail-cover", "DefaultImage", "DefaultImageDomain.Sorteo", "sorteo-default.svg", "onerror", "this.onerror=null", "width=", "height=", "text-[var(--on-brand)]" },
          new[] { "⭐", "🎁", "🎲", "📸", "🔗", "🎬", "dark:", "hover:scale-105", "text-amber-500", "text-pink-500", "text-sky-500", "text-rose-600" } },

        // Radar: cabecera compartida, modal editorial, acciones, spinner y vacío por
        // iconos Lucide; los estados se expresan con tokens temáticos. Remediación F4.2:
        // el shell del modal permanece montado (sin @if) para restaurar el foco al cerrar.
        { "Radar (sin emojis)", "src/Ludeka.Web/Components/Pages/Radar.razor",
          new[] { "BadgeIcon=\"gift\"", "<Icon Name=\"plus\"", "<Icon Name=\"dices\"", "<Icon Name=\"radar\"", "<Icon Name=\"star\"", "<PageHeaderEditorial", "<EditorialModal" },
          new[] { "🎁", "➕", "🌍", "⭐", "🎲", "📡", "🌎", "dark:", "text-amber-500", "text-rose-600", "fixed inset-0 z-50", "@if (_isCreateModalOpen)" } },

        // News: cabecera compartida, modal editorial, imagen con fallback por dominio,
        // buscador, spinner, vacío y badges por iconos Lucide. Remediación F4.2:
        // el shell del modal permanece montado (sin @if) para restaurar el foco al cerrar.
        { "News (sin emojis)", "src/Ludeka.Web/Components/Pages/News.razor",
          new[] { "BadgeIcon=\"newspaper\"", "<Icon Name=\"plus\"", "<Icon Name=\"search\"", "<Icon Name=\"package\"", "<Icon Name=\"refresh-cw\"", "<Icon Name=\"camera\"", "<Icon Name=\"calendar-days\"",
                  "<PageHeaderEditorial", "<EditorialModal", "DefaultImage", "DefaultImageDomain.Novedad", "novedad-default.svg", "onerror", "this.onerror=null", "width=", "height=", "rail-card" },
          new[] { "📰", "➕", "🔍", "📦", "🔄", "🆕", "🗓", "📸", "dark:", "text-pink-500", "text-rose-600", "hover:scale-105", "fixed inset-0 z-50", "@if (_isCreateModalOpen)" } },

        // NotFound: spinner/hero del 404 por icono Lucide
        { "NotFound (sin emojis)", "src/Ludeka.Web/Components/Pages/NotFound.razor",
          new[] { "<Icon Name=\"dices\"" },
          new[] { "🎲" } },

        // OfflineIndicator: visible en la cabecera de todas las páginas (portada incluida)
        { "OfflineIndicator (sin emojis)", "src/Ludeka.Web/Components/Shared/OfflineIndicator.razor",
          new[] { "<Icon Name=\"wifi-off\"", "rounded-full bg-emerald-500" },
          new[] { "📡", "🟢" } },

        // ScalabilityTrafficLight: píldora del ideal por icono Lucide
        { "ScalabilityTrafficLight (sin emojis)", "src/Ludeka.Web/Components/Shared/ScalabilityTrafficLight.razor",
          new[] { "<Icon Name=\"sparkles\"" },
          new[] { "✨" } },

        // ===== INC-35 PR-3b: migración global de emojis a Icon (Fase 5 — admin y modales) =====

        // AdminNotifications: cabecera, canales, disparadores y badges de evento por iconos Lucide;
        // los estados de canal DryRun/Activo/SinConfigurar son puntos CSS (patrón PR-3a)
        { "AdminNotifications (sin emojis)", "src/Ludeka.Web/Components/Pages/AdminNotifications.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"refresh-cw\"", "<Icon Name=\"triangle-alert\"",
                  "<Icon Name=\"message-circle\"", "<Icon Name=\"plane\"",
                  "<Icon Name=\"bell\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"shopping-bag\"", "<Icon Name=\"megaphone\"",
                  "<Icon Name=\"inbox\"", "rounded-full bg-emerald-400", "GetEventIcon",
                  "@(_isFeedbackError ? \"circle-x\" : \"circle-check\")" },
          new[] { "🛡", "🔄", "⚠", "👤", "❌", "✅", "✕", "💬", "🟡", "🟢", "⚪", "🔔", "✈", "⏳", "🛍", "📢", "📭", "💡" } },

        // AuditLogViewer: cabeceras, filtros de entidad/acción, spinner y tabla por iconos Lucide
        // (el switch de tipo de entidad devuelve el nombre del icono, no un emoji; ▲▼ de
        // despliegue se conservan como glifos tipográficos, decisión del PR-3a)
        { "AuditLogViewer (sin emojis)", "src/Ludeka.Web/Components/Pages/AuditLogViewer.razor",
          new[] { "<Icon Name=\"scroll\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"users\"", "<Icon Name=\"triangle-alert\"",
                  "<Icon Name=\"search\"", "<Icon Name=\"eraser\"", "<Icon Name=\"hourglass\"",
                  "GetEntityTypeIcon", "AuditEntityType.Game => \"dices\"", "AuditEntityType.Publisher => \"building-2\"",
                  "AuditEntityType.Creator => \"pen-line\"", "AuditEntityType.Store => \"store\"", "AuditEntityType.Media => \"clapperboard\"",
                  "AuditEntityType.Report => \"flag\"", "AuditEntityType.User => \"users\"", "_ => \"file-text\"" },
          new[] { "📜", "💡", "👥", "⚠", "🎲", "🏢", "✍", "🏪", "🎬", "🚩", "➕", "✏", "🗑", "🔄", "👑", "🛡", "🔍", "🧹", "⏳", "📄" } },

        // CatalogQueueAdmin: cabecera, métricas, pestañas y estados de cola por iconos Lucide (↗ conservado)
        { "CatalogQueueAdmin (sin emojis)", "src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"moon\"", "<Icon Name=\"settings\"", "<Icon Name=\"zap\"",
                  "<Icon Name=\"refresh-cw\"", "<Icon Name=\"target\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"newspaper\"",
                  "<Icon Name=\"scroll\"", "<Icon Name=\"circle-check\"", "<Icon Name=\"circle-x\"",
                  "<Icon Name=\"party-popper\"", "<Icon Name=\"user\"", "<Icon Name=\"trophy\"",
                  "@(_lastResult.FailedCount == 0 ? \"circle-check\" : \"triangle-alert\")" },
          new[] { "🛡", "🌙", "⚙", "⚡", "🔄", "🎯", "⏳", "📰", "📜", "✅", "🎉", "👤", "🏆", "❌", "⚠" } },

        // GameReportsModeration: cabeceras, pestañas, acciones y tipos de incidencia por iconos
        // Lucide (el switch del tipo devuelve el nombre del icono, no un emoji)
        { "GameReportsModeration (sin emojis)", "src/Ludeka.Web/Components/Pages/GameReportsModeration.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"flag\"", "<Icon Name=\"moon\"",
                  "<Icon Name=\"clapperboard\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"search\"",
                  "<Icon Name=\"circle-check\"", "<Icon Name=\"folder\"", "<Icon Name=\"x\"", "<Icon Name=\"dices\"",
                  "<Icon Name=\"sparkles\"", "<Icon Name=\"pencil\"", "<Icon Name=\"refresh-cw\"", "GetIssueIcon",
                  "GameIssueType.BrokenImage => \"ban\"", "GameIssueType.WrongImage => \"image\"", "GameIssueType.IncorrectAge => \"cake\"",
                  "GameIssueType.IncorrectPlayerCount => \"users\"", "GameIssueType.ErroneousMetadata => \"notebook-pen\"",
                  "GameIssueType.BrokenPurchaseLink => \"shopping-cart\"", "GameIssueType.Other => \"message-circle\"" },
          new[] { "🛡", "💡", "🚩", "🌙", "🎬", "⚠", "⏳", "🔍", "✅", "📁", "✕", "🎲", "✨", "🖼", "🚫", "👥", "⏱", "🎂", "📝", "🛒", "💬", "✏", "🔄" } },

        // InstagramModeration: cabeceras, previsualización del feed, acciones y badges por iconos
        // Lucide; la barra social del mock usa heart/message-circle/bookmark y ↗ plano (✓ conservado)
        { "InstagramModeration (sin emojis)", "src/Ludeka.Web/Components/Pages/InstagramModeration.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"camera\"", "<Icon Name=\"flask-conical\"", "<Icon Name=\"gift\"",
                  "<Icon Name=\"newspaper\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"smartphone\"", "<Icon Name=\"heart\"",
                  "<Icon Name=\"message-circle\"", "<Icon Name=\"bookmark\"", "<Icon Name=\"party-popper\"", "<Icon Name=\"link\"",
                  "<Icon Name=\"trash-2\"", "<Icon Name=\"palette\"", "<Icon Name=\"moon\"", "<Icon Name=\"sun\"",
                  "<Icon Name=\"hourglass\"", "<Icon Name=\"save\"", "<Icon Name=\"clipboard-copy\"", "<Icon Name=\"rocket\"",
                  "<Icon Name=\"image\"", "GetSourceIcon",
                  "InstagramPostSourceType.Game => \"dices\"" },
          new[] { "🛡", "📸", "🧪", "🎁", "📰", "⚠", "📱", "🖼", "❤", "🤍", "💬", "🔖", "🎉", "🔗", "🗑", "🎨", "🌙", "☀", "⏳", "💾", "📋", "🚀", "🎲", "📝", "↗️" } },

        // MediaModeration: cabeceras, pestañas, acciones y metadatos por iconos Lucide
        { "MediaModeration (sin emojis)", "src/Ludeka.Web/Components/Pages/MediaModeration.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"clapperboard\"", "<Icon Name=\"flag\"",
                  "<Icon Name=\"tv\"", "<Icon Name=\"search\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"circle-check\"",
                  "<Icon Name=\"circle-x\"", "<Icon Name=\"x\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"package\"",
                  "<Icon Name=\"dices\"", "<Icon Name=\"sparkles\"", "<Icon Name=\"users\"", "<Icon Name=\"eye\"",
                  "<Icon Name=\"link\"" },
          new[] { "🛡", "💡", "🎬", "🚩", "📺", "🔍", "⚠", "✅", "✕", "⏳", "📦", "🎲", "✨", "👥", "👁", "🔗", "❌", "⚡", "💬" } },

        // UserManagement: cabeceras, pestañas de rol, buscador y acciones por iconos Lucide
        // (el switch de rol devuelve el nombre del icono, no un emoji; reactivación por punto CSS).
        // INC-46 F3: el botón «Simular» de la identidad simulada se retira con su icono drama.
        { "UserManagement (sin emojis)", "src/Ludeka.Web/Components/Pages/UserManagement.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"users\"", "<Icon Name=\"scroll\"",
                  "<Icon Name=\"flag\"", "<Icon Name=\"search\"", "<Icon Name=\"crown\"",
                  "<Icon Name=\"user\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"sparkles\"", "<Icon Name=\"settings\"",
                  "<Icon Name=\"ban\"", "rounded-full bg-emerald-400", "GetRoleIcon" },
          new[] { "🛡", "💡", "👥", "📜", "🚩", "🎭", "🔍", "👑", "👤", "⏳", "✨", "⚙", "🚫", "🟢" } },

        // ===== INC-35 PR-3b: migración global de emojis a Icon (Fase 5b — modales de gestión) =====

        // UserPermissionsModal: rol de destino y conmutadores granulares por iconos Lucide
        { "UserPermissionsModal (sin emojis)", "src/Ludeka.Web/Components/Shared/UserPermissionsModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"crown\"", "<Icon Name=\"user\"", "<Icon Name=\"sparkles\"",
                  "<Icon Name=\"dices\"", "<Icon Name=\"image\"", "<Icon Name=\"building-2\"", "<Icon Name=\"pen-line\"",
                  "<Icon Name=\"clapperboard\"", "<Icon Name=\"flag\"", "<Icon Name=\"store\"", "<Icon Name=\"camera\"",
                  "<Icon Name=\"hourglass\"", "<Icon Name=\"save\"", "<Icon Name=\"info\"" },
          new[] { "🛡", "👑", "👤", "✨", "🎲", "🖼", "🏢", "✍", "🎬", "🚩", "🏪", "📸", "⏳", "💾", "ℹ" } },

        // CreatorEditModal: cabecera, plataformas (options sin emoji, el select no admite SVG) y acciones;
        // el título «Nuevo/Editar Creador» es texto plano, sin icono
        { "CreatorEditModal (sin emojis)", "src/Ludeka.Web/Components/Shared/CreatorEditModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"plus\"", "<Icon Name=\"trash-2\"",
                  "<Icon Name=\"settings\"", "<Icon Name=\"save\"" },
          new[] { "🛡", "➕", "✏", "🐦", "📷", "🎲", "🌐", "💬", "▶", "🗑", "⚙", "💾" } },

        // PublisherEditModal: cabecera, plataformas y acciones por iconos Lucide
        { "PublisherEditModal (sin emojis)", "src/Ludeka.Web/Components/Shared/PublisherEditModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"plus\"", "<Icon Name=\"trash-2\"",
                  "<Icon Name=\"settings\"", "<Icon Name=\"save\"" },
          new[] { "🛡", "➕", "✏", "🐦", "📷", "🌐", "💬", "▶", "📘", "🗑", "⚙", "💾" } },

        // StoreEditModal: cabecera, tipos de tienda y plataformas por iconos Lucide
        { "StoreEditModal (sin emojis)", "src/Ludeka.Web/Components/Shared/StoreEditModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"plus\"", "<Icon Name=\"trash-2\"",
                  "<Icon Name=\"settings\"", "<Icon Name=\"save\"" },
          new[] { "🛡", "➕", "✏", "🏪", "🏢", "🌐", "🐦", "📷", "▶", "📘", "🗑", "⚙", "💾" } },

        // GameEditorModal: pestañas del editor, controles de carátula, fundas y guardado por iconos Lucide
        { "GameEditorModal (sin emojis)", "src/Ludeka.Web/Components/Shared/GameEditorModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"flag\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"notebook-pen\"",
                  "<Icon Name=\"dices\"", "<Icon Name=\"file-text\"", "<Icon Name=\"image\"", "<Icon Name=\"folder\"",
                  "<Icon Name=\"settings\"", "<Icon Name=\"link\"", "<Icon Name=\"club\"", "<Icon Name=\"trash-2\"",
                  "<Icon Name=\"plus\"", "<Icon Name=\"save\"" },
          new[] { "🛡", "🚩", "⚠", "📝", "🎲", "📄", "🖼", "🪑", "🍽", "🏰", "📁", "⚙", "🔗", "🃏", "🗑", "➕", "💾", "🟢", "🟡", "🔴", "✅" } },

        // BggImportModal: fases de importación y resultados por iconos Lucide
        { "BggImportModal (sin emojis)", "src/Ludeka.Web/Components/Shared/BggImportModal.razor",
          new[] { "<Icon Name=\"inbox\"", "<Icon Name=\"hourglass\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"timer\"",
                  "<Icon Name=\"package\"", "<Icon Name=\"dices\"", "<Icon Name=\"party-popper\"",
                  "<Icon Name=\"circle-check\"", "<Icon Name=\"user\"", "<Icon Name=\"triangle-alert\"" },
          new[] { "📥", "⏳", "💡", "⏱", "📦", "🎲", "🎉", "🟢", "👤", "⚠" } },

        // BggSearchModal: buscador, spinner, error y resultados por iconos Lucide (✓ conservado)
        { "BggSearchModal (sin emojis)", "src/Ludeka.Web/Components/Shared/BggSearchModal.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"search\"", "<Icon Name=\"settings\"", "<Icon Name=\"triangle-alert\"",
                  "<Icon Name=\"search-x\"", "<Icon Name=\"zap\"" },
          new[] { "🎲", "🔍", "⚙", "⚠", "🤷", "⚡" } },

        // YouTubeSearchModal: cabecera, foco, pestañas, resultados y acciones por iconos Lucide
        // (los mensajes de resultado en C# quedan como texto plano)
        { "YouTubeSearchModal (sin emojis)", "src/Ludeka.Web/Components/Shared/YouTubeSearchModal.razor",
          new[] { "<Icon Name=\"tv\"", "<Icon Name=\"x\"", "<Icon Name=\"target\"", "<Icon Name=\"dices\"",
                  "<Icon Name=\"search\"", "<Icon Name=\"zap\"", "<Icon Name=\"clapperboard\"", "<Icon Name=\"sparkles\"",
                  "<Icon Name=\"users\"", "<Icon Name=\"play\"", "<Icon Name=\"inbox\"", "<Icon Name=\"circle-check\"" },
          new[] { "📺", "✕", "🎯", "🎲", "🔍", "⚡", "🎬", "✨", "👥", "▶", "📥", "✅" } },

        // GameReportModal: cabecera, selector de tipología e identificación por iconos Lucide
        // (la tupla estática de incidencias almacena el nombre del icono, no un emoji)
        { "GameReportModal (sin emojis)", "src/Ludeka.Web/Components/Shared/GameReportModal.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"flag\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"settings\"",
                  "CurrentUserService.IsFoundingTeam ? \"shield\" : \"user\"",
                  "(GameIssueType.WrongImage, \"Imagen incorrecta o de otra edición\", \"image\")",
                  "(GameIssueType.Other, \"Otro problema o sugerencia libre\", \"message-circle\")" },
          new[] { "🎲", "🚩", "⚠", "🛡", "👤", "⚙", "🖼", "🚫", "👥", "⏱", "🎂", "📝", "🛒", "💬" } },

        // LocationSelectorModal: cabecera, aviso y autodetección por iconos Lucide;
        // CountryCatalog.FlagEmoji queda como excepción data-driven documentada (datos, no markup)
        { "LocationSelectorModal (sin emojis)", "src/Ludeka.Web/Components/Shared/LocationSelectorModal.razor",
          new[] { "<Icon Name=\"earth\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"map-pin\"" },
          new[] { "🌍", "⚠", "📍" } },

        // MediaEmbedModal: cabecera por tipo, metadatos y cierre por iconos Lucide (↗ conservado)
        { "MediaEmbedModal (sin emojis)", "src/Ludeka.Web/Components/Shared/MediaEmbedModal.razor",
          new[] { "<Icon Name=\"clapperboard\"", "<Icon Name=\"dices\"", "<Icon Name=\"camera\"", "<Icon Name=\"smartphone\"",
                  "<Icon Name=\"x\"", "<Icon Name=\"users\"", "<Icon Name=\"timer\"", "<Icon Name=\"heart\"" },
          new[] { "🎬", "🎲", "📸", "📱", "✕", "👥", "⏱", "❤" } },

        // CatalogQueuePanel: acciones, resultados y tarjetas de cola por iconos Lucide
        { "CatalogQueuePanel (sin emojis)", "src/Ludeka.Web/Components/Shared/CatalogQueuePanel.razor",
          new[] { "<Icon Name=\"refresh-cw\"", "<Icon Name=\"settings\"", "<Icon Name=\"moon\"", "<Icon Name=\"zap\"",
                  "<Icon Name=\"circle-check\"", "<Icon Name=\"circle-x\"", "<Icon Name=\"triangle-alert\"",
                  "<Icon Name=\"hourglass\"", "<Icon Name=\"sparkles\"", "<Icon Name=\"flame\"" },
          new[] { "🔄", "⚙", "🌙", "⚡", "✅", "⚠", "❌", "⏳", "✨", "🔥" } },

        // ===== INC-35 PR-3b: migración global de emojis a Icon (Fase 5c — fichas y directorios) =====

        // PublicProfile: cabecera, acciones, estantería y badges por iconos Lucide; el punto
        // ● es CSS; Stats.Badge.IconEmoji queda como excepción data-driven documentada
        { "PublicProfile (sin emojis)", "src/Ludeka.Web/Components/Pages/PublicProfile.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"search\"", "<Icon Name=\"library\"", "<Icon Name=\"clipboard-copy\"",
                  "<Icon Name=\"settings\"", "<Icon Name=\"puzzle\"", "<Icon Name=\"package\"", "rounded-full bg-emerald-400" },
          new[] { "🎲", "🔍", "📚", "●", "📋", "⚙", "🧩", "📦" } },

        // PublisherDetail: cabeceras, badges y enlaces de la ficha editorial por iconos Lucide
        { "PublisherDetail (sin emojis)", "src/Ludeka.Web/Components/Pages/PublisherDetail.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"search\"", "<Icon Name=\"pencil\"", "<Icon Name=\"building-2\"",
                  "<Icon Name=\"map-pin\"", "<Icon Name=\"globe\"" },
          new[] { "🎲", "🔍", "✏", "🏢", "📍", "🌐" } },

        // PublishersDirectory: cabecera, buscador, spinner, vacío y tarjetas por iconos Lucide
        { "PublishersDirectory (sin emojis)", "src/Ludeka.Web/Components/Pages/PublishersDirectory.razor",
          new[] { "<Icon Name=\"building-2\"", "<Icon Name=\"plus\"", "<Icon Name=\"search\"", "<Icon Name=\"dices\"",
                  "<Icon Name=\"package\"", "<Icon Name=\"map-pin\"", "<Icon Name=\"pencil\"" },
          new[] { "🏢", "➕", "🔍", "🎲", "📦", "📍", "✏" } },

        // StoreDetail: ficha de tienda, badges, envíos y ofertas por iconos Lucide
        // (el switch del tipo de tienda devuelve texto plano, sin emoji incrustado)
        { "StoreDetail (sin emojis)", "src/Ludeka.Web/Components/Pages/StoreDetail.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"search\"", "<Icon Name=\"pencil\"", "<Icon Name=\"map-pin\"",
                  "<Icon Name=\"star\"", "<Icon Name=\"truck\"", "<Icon Name=\"building-2\"", "<Icon Name=\"globe\"",
                  "<Icon Name=\"shopping-cart\"" },
          new[] { "🎲", "🔍", "✏", "📍", "⭐", "🚚", "🏢", "🌐", "🛒", "🏪" } },

        // StoresDirectory: cabecera, filtros, tarjetas y acciones por iconos Lucide
        // (los selects con FlagEmoji/GetFlag quedan como excepción data-driven documentada)
        { "StoresDirectory (sin emojis)", "src/Ludeka.Web/Components/Pages/StoresDirectory.razor",
          new[] { "<Icon Name=\"shopping-cart\"", "<Icon Name=\"plus\"", "<Icon Name=\"search\"", "<Icon Name=\"dices\"",
                  "<Icon Name=\"store\"", "<Icon Name=\"building-2\"", "<Icon Name=\"star\"", "<Icon Name=\"truck\"",
                  "<Icon Name=\"map-pin\"", "<Icon Name=\"pencil\"" },
          new[] { "🛒", "➕", "🔍", "🌍", "🏪", "🏢", "🌐", "🎲", "⭐", "🚚", "📍", "✏" } },

        // Transparency: manifiesto editorial por iconos Lucide
        { "Transparency (sin emojis)", "src/Ludeka.Web/Components/Pages/Transparency.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"dices\"", "<Icon Name=\"zap\"", "<Icon Name=\"camera\"",
                  "<Icon Name=\"gift\"", "<Icon Name=\"coffee\"", "<Icon Name=\"message-circle\"" },
          new[] { "🛡", "🎲", "⚡", "📸", "🎁", "☕", "💬" } },

        // QuickBadges: badges del ADN lúdico por iconos Lucide
        { "QuickBadges (sin emojis)", "src/Ludeka.Web/Components/Shared/QuickBadges.razor",
          new[] { "<Icon Name=\"handshake\"", "<Icon Name=\"swords\"", "<Icon Name=\"users\"", "<Icon Name=\"settings\"",
                  "<Icon Name=\"dices\"", "<Icon Name=\"party-popper\"", "<Icon Name=\"puzzle\"", "<Icon Name=\"book-open\"",
                  "<Icon Name=\"user\"", "<Icon Name=\"baby\"", "<Icon Name=\"cake\"", "<Icon Name=\"globe\"",
                  "<Icon Name=\"timer\"", "<Icon Name=\"armchair\"", "<Icon Name=\"castle\"", "<Icon Name=\"utensils\"" },
          new[] { "🤝", "🗡", "👥", "⚔", "⚙", "🎲", "🎉", "🧩", "📖", "👤", "👶", "🎂", "🌐", "⏱", "🪑", "🏰", "🍽" } },

        // AiSummaryCard: píldoras de la síntesis IA por iconos Lucide (sin badge de modelo técnico en INC-90)
        { "AiSummaryCard (sin emojis)", "src/Ludeka.Web/Components/Shared/AiSummaryCard.razor",
          new[] { "<Icon Name=\"bot\"", "<Icon Name=\"shield\"", "<Icon Name=\"users\"",
                  "<Icon Name=\"baby\"", "<Icon Name=\"shapes\"" },
          new[] { "🤖", "⚡", "🛡", "👥", "🧒", "📐", "⏳" } },

        // FoundingVerdictCard: veredicto editorial, pestañas y galería por iconos Lucide
        { "FoundingVerdictCard (sin emojis)", "src/Ludeka.Web/Components/Shared/FoundingVerdictCard.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"pen-line\"", "<Icon Name=\"users\"", "<Icon Name=\"baby\"",
                  "<Icon Name=\"scale\"", "<Icon Name=\"puzzle\"", "<Icon Name=\"camera\"", "<Icon Name=\"search\"",
                  "<Icon Name=\"x\"" },
          new[] { "🛡", "✍", "👥", "🧒", "⚖", "🧩", "📸", "🔍", "✕" } },

        // FoundingVerdictModal: editor del veredicto y sellos por iconos Lucide
        { "FoundingVerdictModal (sin emojis)", "src/Ludeka.Web/Components/Shared/FoundingVerdictModal.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"x\"", "<Icon Name=\"triangle-alert\"", "<Icon Name=\"trophy\"",
                  "<Icon Name=\"tag\"", "<Icon Name=\"package\"", "<Icon Name=\"save\"" },
          new[] { "🛡", "✕", "⚠", "🏆", "🏷", "📦", "💾" } },

        // ExpansionAporteCard: necesidad, chips de impacto y métricas por iconos Lucide
        // (el switch del impacto devuelve el nombre del icono y la necesidad un punto CSS)
        { "ExpansionAporteCard (sin emojis)", "src/Ludeka.Web/Components/Shared/ExpansionAporteCard.razor",
          new[] { "<Icon Name=\"users\"", "<Icon Name=\"timer\"", "GetTagIcon", "<Icon Name=\"@GetTagIcon(tag)\"",
                  "GetNecessityDotClass", "rounded-full @GetNecessityDotClass(Necessity.Value)",
                  "ExpansionImpactTag.ModularContent => \"puzzle\"",
                  "ExpansionImpactTag.FixesBalance => \"scale\"", "ExpansionImpactTag.NewMapOrFactions => \"map\"" },
          new[] { "👥", "⏱", "🟢", "🟡", "🔵", "⚪", "⚔", "⚖", "🎭", "🧩", "🗺", "⚡", "✨" } },

        // ExpansionEcosystemSection: pestañas, mezclador y diagnóstico por iconos Lucide (★ conservada)
        { "ExpansionEcosystemSection (sin emojis)", "src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"puzzle\"", "<Icon Name=\"flask-conical\"", "<Icon Name=\"lightbulb\"",
                  "<Icon Name=\"triangle-alert\"", "<Icon Name=\"sparkles\"", "<Icon Name=\"target\"",
                  "isSelected ? \"circle-check\" : \"square\"" },
          new[] { "🎲", "🧩", "🔬", "💡", "✅", "⬜", "⚠", "✨", "🎯" } },

        // ExpansionSisterList: badge de expansión hermana por icono Lucide (★ conservada)
        { "ExpansionSisterList (sin emojis)", "src/Ludeka.Web/Components/Shared/ExpansionSisterList.razor",
          new[] { "<Icon Name=\"puzzle\"", "rounded-full bg-slate-400" },
          new[] { "🧩", "⚪" } },

        // UserReviewCard: valoración personal, contexto de partida y acciones por iconos Lucide
        // (el switch del contexto devuelve texto plano, patrón ReviewBottomSheet del PR-3a)
        { "UserReviewCard (sin emojis)", "src/Ludeka.Web/Components/Shared/UserReviewCard.razor",
          new[] { "<Icon Name=\"baby\"", "<Icon Name=\"lock\"", "Review != null ? \"pencil\" : \"star\"",
                  "PlayContextType.Owned => \"En propiedad\"", "PlayContextType.Bga => \"Digital (BGA)\"" },
          new[] { "👶", "🔒", "✏", "⭐", "🏠", "🛡", "👥", "☕", "💻" } },

        // MultimediaHub: pestañas, tarjetas, badges y moderación por iconos Lucide
        { "MultimediaHub (sin emojis)", "src/Ludeka.Web/Components/Shared/MultimediaHub.razor",
          new[] { "<Icon Name=\"tv\"", "<Icon Name=\"zap\"", "<Icon Name=\"clapperboard\"", "<Icon Name=\"dices\"",
                  "<Icon Name=\"message-circle\"", "<Icon Name=\"x\"", "<Icon Name=\"settings\"", "<Icon Name=\"play\"",
                  "<Icon Name=\"timer\"", "<Icon Name=\"users\"", "<Icon Name=\"heart\"", "<Icon Name=\"tag\"",
                  "<Icon Name=\"repeat\"", "<Icon Name=\"smartphone\"" },
          new[] { "📺", "⚡", "🎬", "🎲", "💬", "✕", "⚙", "▶", "⏱", "👥", "❤", "🏷", "🔁", "📱", "📸" } },

        // ParentGameBanner: banner de juego base por iconos Lucide (★ conservada)
        { "ParentGameBanner (sin emojis)", "src/Ludeka.Web/Components/Shared/ParentGameBanner.razor",
          new[] { "<Icon Name=\"dices\"", "<Icon Name=\"puzzle\"" },
          new[] { "🎲", "🧩" } },

        // RuleQuestionsSection: consultorio de reglas por iconos Lucide (▲ y ✓ conservados)
        { "RuleQuestionsSection (sin emojis)", "src/Ludeka.Web/Components/Shared/RuleQuestionsSection.razor",
          new[] { "<Icon Name=\"plus\"", "<Icon Name=\"dices\"", "<Icon Name=\"book-open\"", "<Icon Name=\"message-circle\"" },
          new[] { "➕", "🎲", "📖", "💬" } },

        // SleeveGuideCard: guía de fundas, micras y compras por iconos Lucide
        { "SleeveGuideCard (sin emojis)", "src/Ludeka.Web/Components/Shared/SleeveGuideCard.razor",
          new[] { "<Icon Name=\"shield\"", "<Icon Name=\"lightbulb\"", "<Icon Name=\"ruler\"", "<Icon Name=\"shopping-cart\"",
                  "<Icon Name=\"party-popper\"" },
          new[] { "🛡", "💡", "📏", "🛒", "🎉" } },

        // SocialCardModal: generador de cartel social por iconos Lucide (✓ conservado)
        { "SocialCardModal (sin emojis)", "src/Ludeka.Web/Components/Shared/SocialCardModal.razor",
          new[] { "<Icon Name=\"palette\"", "<Icon Name=\"clipboard-copy\"", "<Icon Name=\"inbox\"" },
          new[] { "🎨", "📋", "📥" } },

        // ===== INC-45: Radar de Precios, Mínimos Históricos y Alertas 'Quiero comprar' =====
        { "StoreOffersCard (mínimos históricos e integración radar)", "src/Ludeka.Web/Components/Shared/StoreOffersCard.razor",
          new[] { "Mínimo histórico", "PriceRadarService", "GetGamePriceMetricsAsync", "RecordPriceObservationAsync" },
          Array.Empty<string>() },

        { "MyLibrary (alertas de compra y radar de precios)", "src/Ludeka.Web/Components/Pages/MyLibrary.razor",
          new[] { "PriceRadarService", "GetUserWantToBuyAlertsAsync", "Radar de Compra Inteligente" },
          Array.Empty<string>() },

        { "Radar (pestaña ofertas y mínimos históricos)", "src/Ludeka.Web/Components/Pages/Radar.razor",
          new[] { "PriceRadarService", "GetTopDiscountsAsync", "panel-deals", "Ofertas &amp; Mínimos Históricos" },
          Array.Empty<string>() },

        // ReconnectModal: textos en castellano y script con retardo de cortesía
        { "ReconnectModal (textos en castellano)", "src/Ludeka.Web/Components/Layout/ReconnectModal.razor",
          new[] { "Reconectando con el servidor...", "Reconexión fallida...", "No se pudo reconectar.", "Reintentar", "La sesión ha sido pausada", "Reanudar" },
          new[] { "Rejoining the server...", "Rejoin failed...", "Failed to rejoin", "Retry", "The session has been paused", "Resume" } },

        { "ReconnectModal.js (retardo de cortesía)", "src/Ludeka.Web/Components/Layout/ReconnectModal.razor.js",
          new[] { "RECONNECT_GRACE_PERIOD_MS", "showModalTimer", "clearShowTimer" },
          Array.Empty<string>() },
    };

    [Theory]
    [MemberData(nameof(MarkupContracts))]
    public void Source_FulfillsMarkupContract(string description, string relativePath, string[] mustContain, string[] mustNotContain)
    {
        var source = ReadSource(relativePath);

        foreach (var fragment in mustContain)
        {
            Assert.True(source.Contains(fragment, StringComparison.Ordinal),
                $"{description}: el archivo {relativePath} debe contener '{fragment}'.");
        }

        foreach (var fragment in mustNotContain)
        {
            Assert.False(source.Contains(fragment, StringComparison.Ordinal),
                $"{description}: el archivo {relativePath} no debe contener '{fragment}'.");
        }
    }

    [Fact]
    public void RestoDeLaWeb_SinEmojisDeLaListaSpecEnNingunComponenteRazor()
    {
        // Cierre del escenario «Resto de la web sin emojis» (spec iconography-lucide):
        // ningún .razor de la web contiene ya los 16 emojis migrados de la Decisión 6.
        // Los glifos tipográficos (★ ✓ ↗ → ▲ ▼) y los datos emitidos desde Application
        // (CountryCatalog.FlagEmoji/GetFlag, Stats.Badge.IconEmoji) quedan fuera de este
        // barrido por decisión documentada del incremento.
        var componentsDir = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components");
        var razors = Directory.EnumerateFiles(componentsDir, "*.razor", SearchOption.AllDirectories).ToList();
        Assert.True(razors.Count > 20, "No se encontraron componentes Razor que barrer.");

        var offenders = new List<string>();
        foreach (var file in razors)
        {
            var source = File.ReadAllText(file);
            foreach (var emoji in EmojisDePortada)
            {
                if (source.Contains(emoji, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)} contiene '{emoji}'.");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "La web aún contiene emojis de la lista spec: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void HeroEditorial_NoRenderizaPildorasRedundantes()
    {
        // Revisión del maintainer (2026-09-10): las píldoras D4 se eliminan — duplicaban
        // la navegación superior y los carriles de portada. Los destinos viven en la nav
        // superior (lg+) y en el menú móvil desplegable de MainLayout (< lg).
        var source = ReadSource("src/Ludeka.Web/Components/Home/HeroEditorial.razor");
        Assert.DoesNotContain("Catálogo Completo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/sorteos\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/novedades\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/eventos\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeDashboard_CarrilNovedades_EnlaceVerTodasApuntaANovedades()
    {
        // Paridad del enlace "Ver todas las novedades" tras la extracción: el destino lo
        // fija el Href del orquestador en la misma llamada a RailHeader (Decisión 3).
        var source = ReadSource("src/Ludeka.Web/Components/Pages/HomeDashboard.razor");
        var anchorPos = source.IndexOf("Ver todas las novedades", StringComparison.Ordinal);
        Assert.True(anchorPos >= 0, "No se encontró el enlace 'Ver todas las novedades'.");

        var hrefPos = source.LastIndexOf("Href=\"", anchorPos, StringComparison.Ordinal);
        Assert.True(hrefPos >= 0, "El carril de Novedades no declara Href.");
        Assert.StartsWith("Href=\"/novedades\"", source[hrefPos..]);
    }

    [Fact]
    public void RailHeader_EnlaceVerTodos_SeRenderizaSoloConDestinoYRespetaElHref()
    {
        // Retarget tras la extracción (Decisión 3): el enlace "Ver todas las novedades"
        // pasa a vivir en RailHeader.razor y su destino lo fija el Href del orquestador
        // (paridad protegida en la entrada del orquestador del C3).
        var source = ReadSource("src/Ludeka.Web/Components/Home/RailHeader.razor");

        // El enlace se emite con el Href recibido y solo cuando hay destino (Eventos no tiene)
        Assert.Contains("<a href=\"@Href\"", source);
        Assert.Contains("@if (Href is not null)", source);
    }

    [Fact]
    public void App_razor_CargaFrauncesEnLaMismaPeticionDeFuentesSinNuevoEnlace()
    {
        // Decisión 7 (INC-35): la serif display Fraunces entra en la URL ya existente de
        // Google Fonts (misma petición css2, display=swap intacto, sin preload).
        var source = ReadSource("src/Ludeka.Web/Components/App.razor");

        Assert.Contains("family=Fraunces:opsz,wght@9..144,600..700", source);
        Assert.Contains("display=swap", source);

        // No se añade ningún <link> de fuente nuevo: una única URL css2 y las mismas
        // tres hojas de estilo que había antes del incremento.
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(source, @"fonts\.googleapis\.com/css2").Count);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(source, "rel=\"stylesheet\"").Count);
    }

    // ===== INC-35 PR-2: hero editorial narrativo (Decisiones 1, 2 y 10) =====

    private static Type? GetHeroBackgroundVariantType() =>
        typeof(Ludeka.Web.Components.Pages.HomeDashboard).Assembly
            .GetType("Ludeka.Web.Components.Home.HeroBackgroundVariant", throwOnError: false);

    private static Type? GetHeroBackgroundAssetsType() =>
        typeof(Ludeka.Web.Components.Pages.HomeDashboard).Assembly
            .GetType("Ludeka.Web.Components.Home.HeroBackgroundAssets", throwOnError: false);

    [Fact]
    public void HeroBackgroundVariant_ExponeLasCincoVariantesDelDiseno()
    {
        // Decisión 2: 3 fotos de ambiente, escena CSS de serie e ilustración futura.
        var variantType = GetHeroBackgroundVariantType();
        Assert.NotNull(variantType);
        Assert.Equal(
            new[] { "FotoEurogame", "FotoMesaAmigos", "FotoPrimerPlano", "CssScene", "Ilustracion" },
            Enum.GetNames(variantType!));
    }

    [Theory]
    [InlineData("FotoEurogame", "hero-ambiente-eurogame")]
    [InlineData("FotoMesaAmigos", "hero-ambiente-mesa-amigos")]
    [InlineData("FotoPrimerPlano", "hero-ambiente-primer-plano")]
    [InlineData("Ilustracion", "hero-ilustracion")]
    public void HeroBackgroundAssets_MapeaCadaVarianteFotoASusTresFormatos(string variantName, string baseName)
    {
        var variantType = GetHeroBackgroundVariantType();
        Assert.NotNull(variantType);
        var assetsType = GetHeroBackgroundAssetsType();
        Assert.NotNull(assetsType);

        var variant = Enum.Parse(variantType!, variantName);
        foreach (var (methodName, extension) in new[] { ("Avif", ".avif"), ("Webp", ".webp"), ("Jpg", ".jpg") })
        {
            var method = assetsType!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            var resolved = method!.Invoke(null, new[] { variant }) as string;
            Assert.Equal($"/images/home/{baseName}{extension}", resolved);
        }
    }

    [Fact]
    public void HeroBackgroundAssets_AltTextosDeFotoEnCastellanoNoVacios()
    {
        // Decisión 10: alt descriptivo en castellano por variante con imagen; la escena
        // CSS no renderiza <img>, así que su alt queda vacío por diseño.
        var variantType = GetHeroBackgroundVariantType();
        Assert.NotNull(variantType);
        var assetsType = GetHeroBackgroundAssetsType();
        Assert.NotNull(assetsType);
        var altTextMethod = assetsType!.GetMethod("AltText", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(altTextMethod);

        var expectedByVariant = new (string VariantName, string ExpectedAlt)[]
        {
            ("FotoEurogame", "Mesa de juego con un eurogame en marcha sobre el tapete y una estantería lúdica al fondo"),
            ("FotoMesaAmigos", "Grupo de amigos riendo alrededor de una mesa de madera con juegos de mesa"),
            ("FotoPrimerPlano", "Primer plano de manos colocando piezas sobre el tablero de un juego de mesa"),
            ("Ilustracion", "Ilustración editorial de una sala de juegos con estantería, mesa con eurogame y el logotipo de Ludeka"),
        };

        foreach (var (variantName, expectedAlt) in expectedByVariant)
        {
            var variant = Enum.Parse(variantType!, variantName);
            var alt = altTextMethod!.Invoke(null, new[] { variant }) as string;
            Assert.False(string.IsNullOrWhiteSpace(alt), $"El alt de {variantName} debe ser descriptivo.");
            Assert.Equal(expectedAlt, alt);
        }

        var cssScene = Enum.Parse(variantType!, "CssScene");
        Assert.Equal(string.Empty, altTextMethod!.Invoke(null, new[] { cssScene }));
    }

    [Fact]
    public void HeroEditorial_RamaCssScene_NoRenderizaPicture()
    {
        // Escenario «Escena CSS sin peticiones de imagen»: en la rama del @switch
        // correspondiente a CssScene el <picture> no existe (troceado de fuente).
        var source = ReadSource("src/Ludeka.Web/Components/Home/HeroEditorial.razor");
        var casePos = source.IndexOf("case HeroBackgroundVariant.CssScene", StringComparison.Ordinal);
        Assert.True(casePos >= 0, "El hero debe conmutar el fondo con @switch sobre Background (rama CssScene).");
        var breakPos = source.IndexOf("break;", casePos, StringComparison.Ordinal);
        Assert.True(breakPos > casePos, "La rama CssScene del @switch debe terminar en break;");
        var cssSceneBranch = source[casePos..breakPos];
        Assert.DoesNotContain("<picture", cssSceneBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void HeroEditorial_SelectorPruebas_SoloApareceConQuerystringHero()
    {
        // Selector de variantes del hero para pruebas: lee el parámetro ?hero= de la
        // querystring (SSR, sin JavaScript), construye los enlaces con
        // GetUriWithQueryParameter y cubre las 5 variantes. La tira de enlaces SOLO
        // se renderiza cuando el parámetro está presente: los visitantes normales
        // de producción jamás la ven (autoeliminatoria).
        var source = ReadSource("src/Ludeka.Web/Components/Home/HeroEditorial.razor");
        Assert.Contains("QueryHelpers.ParseQuery", source, StringComparison.Ordinal);
        Assert.Contains("_showSwitcher", source, StringComparison.Ordinal);
        Assert.Contains("GetUriWithQueryParameter", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Selector de variante del hero (pruebas)\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-current", source, StringComparison.Ordinal);
        foreach (var key in new[] { "eurogame", "amigos", "primer-plano", "ilustracion", "css" })
        {
            Assert.Contains(key, source, StringComparison.Ordinal);
        }
    }

    // ===== INC-35 PR-3: fix D5 — fallback de imagen en las páginas de eventos =====

    [Theory]
    [InlineData("src/Ludeka.Web/Components/Pages/Events.razor")]
    [InlineData("src/Ludeka.Web/Components/Pages/EventsManagement.razor")]
    public void PaginasEventos_TodaImagenDeEventoTieneFallbackPorDominio(string relativePath)
    {
        // Escenario «Página de eventos con evento sin imagen» (spec default-image-fallbacks):
        // sin URL el cartel es DefaultImage inline por dominio; con URL externa, onerror cae
        // en el asset estático de eventos. Nadie queda con un <img> roto ni sin dimensiones.
        var source = ReadSource(relativePath);
        Assert.Contains("DefaultImageDomain.Evento", source);
        Assert.Contains("/images/defaults/evento-default.svg", source);

        var imgTags = System.Text.RegularExpressions.Regex.Matches(
            source, @"<img\b[^>]*>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(imgTags.Count > 0, $"{relativePath}: debe existir al menos una imagen de cartel de evento.");

        foreach (System.Text.RegularExpressions.Match img in imgTags)
        {
            Assert.True(img.Value.Contains("onerror=", StringComparison.Ordinal),
                $"{relativePath}: toda <img> de evento debe declarar onerror con el default de Ludeka.");
            Assert.True(img.Value.Contains("width=", StringComparison.Ordinal)
                && img.Value.Contains("height=", StringComparison.Ordinal),
                $"{relativePath}: toda <img> de evento debe fijar width/height para CLS 0.");
        }
    }

    // ===== INC-36 PR-1: fundación editorial (tokens on-brand y de estado por tema) =====

    // Los tokens de marca y de estado viven DENTRO de cada bloque [data-theme=…] de
    // input.css: el patrón TheoryData no sabe acotar por bloque, así que estos Fact
    // parsean los 5 bloques y afirman token a token (DD-03 y DD-04).
    private static readonly string[] TemasEditoriales =
    { "charcoal", "editorial", "tabletop", "midnight", "wood" };

    private static string BloqueTema(string fuenteCss, string tema)
    {
        var marcador = $"[data-theme=\"{tema}\"]";
        var inicio = fuenteCss.IndexOf(marcador, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"input.css no declara el bloque del tema '{tema}'.");

        var siguiente = fuenteCss.IndexOf("[data-theme=", inicio + marcador.Length, StringComparison.Ordinal);
        return siguiente < 0 ? fuenteCss[inicio..] : fuenteCss[inicio..siguiente];
    }

    [Fact]
    public void FundacionCss_OnBrand_DeclaradoEnLosCincoDataTheme()
    {
        // INC-36, DD-03 (D3): el token de contraste sobre la marca --on-brand se define
        // en los 5 data-theme (tinta #14181C en charcoal/tabletop/midnight, blanco en
        // editorial/wood), de modo que todo botón sobre --brand-primary pueda dejar de
        // usar text-white y alcanzar AA (≥ 4,5:1) en cada tema.
        var fuenteCss = ReadSource("src/Ludeka.Web/Styles/input.css");

        foreach (var tema in TemasEditoriales)
        {
            var bloque = BloqueTema(fuenteCss, tema);
            Assert.True(bloque.Contains("--on-brand:", StringComparison.Ordinal),
                $"El tema '{tema}' no declara el token de contraste --on-brand.");
        }
    }

    [Fact]
    public void FundacionCss_TokensEstado_DeclaradosEnLosCincoDataTheme()
    {
        // INC-36, DD-04 (D8/D9): los 4 tokens semánticos base de estado (error, warning,
        // info, highlight — con sus sufijos -bg/-border en cada tema) se declaran en los
        // 5 data-theme. El colon después del nombre evita que las variantes -bg/-border
        // satisfagan la aserción del token base.
        var fuenteCss = ReadSource("src/Ludeka.Web/Styles/input.css");

        foreach (var tema in TemasEditoriales)
        {
            var bloque = BloqueTema(fuenteCss, tema);
            foreach (var token in new[] { "--state-error:", "--state-warning:", "--state-info:", "--state-highlight:" })
            {
                Assert.True(bloque.Contains(token, StringComparison.Ordinal),
                    $"El tema '{tema}' no declara el token de estado '{token}'.");
            }
        }
    }

    [Fact]
    public void HeroEditorial_AnchoDelContenedorDeclaradoEnElBloqueCss()
    {
        // Remediación DD-01 (INC-36): el bloque .hero-editorial debe fijar width: 100%
        // para que el ancho lo determine el contenedor y el alto solo se derive del ratio
        // clampado (medido en navegador: 409,58 y 793,58 px transferidos desde el alto).
        // La aserción se acota al bloque porque input.css ya contiene 'width: 100%' en
        // otros bloques: un fragmento global sería tautológico y no detectaría la regresión.
        var fuenteCss = ReadSource("src/Ludeka.Web/Styles/input.css");

        var inicio = fuenteCss.IndexOf(".hero-editorial {", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "input.css no declara el bloque .hero-editorial.");

        var fin = fuenteCss.IndexOf('}', inicio);
        Assert.True(fin > inicio, "El bloque .hero-editorial no cierra con '}'.");

        var bloque = fuenteCss[inicio..fin];
        Assert.True(bloque.Contains("width: 100%", StringComparison.Ordinal),
            $"El bloque .hero-editorial debe fijar 'width: 100%'; contenido actual: {bloque}");
    }
}
