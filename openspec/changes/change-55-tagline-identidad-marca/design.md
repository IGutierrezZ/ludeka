# Diseño Técnico — INC-55: Retirada del Tagline de Marca y Unificación de la Identidad

## 1. Decisiones de Diseño y Reemplazos Concretos

### 1.1 Frontend (Blazor Web / PWA)

| Fichero | Texto Original | Texto Diseñado / Acción |
|---|---|---|
| `HomeDashboard.razor:8` | `<PageTitle>Ludeka — El Letterboxd de los juegos de mesa en español</PageTitle>` | `<PageTitle>Ludeka — Juegos, sorteos, eventos y opiniones de verdad</PageTitle>` |
| `MainLayout.razor:241` | `<p class="text-xs text-[var(--text-secondary)]">El Letterboxd de los juegos de mesa en español.</p>` | Sustituir por: `<p class="text-xs text-[var(--text-secondary)]">Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.</p>` |
| `Login.razor:14` | `<p class="text-sm text-[var(--text-secondary)] mb-6 text-center">El Letterboxd de los juegos de mesa en español. Entra con tu cuenta social:</p>` | `<p class="text-sm text-[var(--text-secondary)] mb-6 text-center">Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa. Entra con tu cuenta social:</p>` |
| `InstagramModeration.razor:198` | `<span class="text-[0.65rem] text-[var(--text-muted)]">El Letterboxd de los juegos de mesa</span>` | `<span class="text-[0.65rem] text-[var(--text-muted)]">Bienvenido a tu mesa</span>` |
| `MyLibrary.razor:382` | `Terracota & Pizarra. La estética insignia de Ludeka estilo Letterboxd. Sobriedad, elegancia y acento naranja cálido.` | `Terracota & Pizarra. La estética insignia de Ludeka. Sobriedad, elegancia y acento naranja cálido.` |
| `input.css:7` | `/* Tema Original: Carbón Oscuro & Naranja Terracota (Estilo Letterboxd) */` | `/* Tema Original: Carbón Oscuro & Naranja Terracota (Estilo Insignia) */` |
| `manifest.webmanifest:11` | `"description": "El Letterboxd de los juegos de mesa en español. Consulta tu colección, préstamos y estadísticas sin conexión.",` | `"description": "Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa. Consulta tu colección, préstamos y estadísticas sin conexión.",` |

### 1.2 Backend e Infraestructura

| Fichero | Línea | Original | Diseñado |
|---|---|---|---|
| `GeminiGameSummaryService.cs` | 366 | `sb.AppendLine("Eres un crítico y analista experto de juegos de mesa para Ludeka (\"El Letterboxd de los juegos de mesa en español\").");` | `sb.AppendLine("Eres un crítico y analista experto de juegos de mesa para Ludeka, la plataforma comunitaria y editorial de juegos de mesa en español.");` |
| `GeminiGameSummaryService.cs` | 517 | `sb.AppendLine("Eres un crítico y analista experto de juegos de mesa para Ludeka (\"El Letterboxd de los juegos de mesa en español\").");` | `sb.AppendLine("Eres un crítico y analista experto de juegos de mesa para Ludeka, la plataforma comunitaria y editorial de juegos de mesa en español.");` |
| `GeekDoImagesClient.cs` | 60 | `request.Headers.Add("User-Agent", "Ludeka/1.0 (El Letterboxd de los juegos de mesa; contacto@ludeka.com)");` | `request.Headers.Add("User-Agent", "Ludeka/1.0 (Comunidad de juegos de mesa en español; contacto@ludeka.com)");` |

### 1.3 Documentación y Configuración Viva

- `AGENTS.md` / `GEMINI.md`:
  Línea 3: `> **Proyecto:** Ludist / Ludeka ("El Letterboxd de los juegos de mesa en español")`
  Cambio a: `> **Proyecto:** Ludist / Ludeka (Plataforma y comunidad de juegos de mesa en español)`
- `axiom.yaml` / `openspec/config.yaml`:
  Cambio del subtítulo a `"Plataforma y comunidad de juegos de mesa en español"`.
- `openspec/specs/social-card-generator/spec.md`:
  Línea 18: Cambiar pie de marca a: `*"Ludeka • Juegos de mesa en español"*`.
- `docs/specs/LUDIST_SPEC_FUNCIONAL_MVP.md`:
  Línea 13: Ajustar descripción fundacional sin el paréntesis de la marca ajena.
- `docs/specs/sistema/15-dashboard-inicio-editorial.md` & `22-portada-y-directorio-creadores.md`:
  Armonizar menciones contextuales para clarificar que Ludeka tiene identidad visual y editorial propia.

## 2. Invariantes y Estrategia de Verificación
- Cero alteraciones en contratos de API, clases o DTOs.
- Cero impacto en serialización o migraciones de base de datos.
- Toda la suite de 1.622 pruebas unitarias debe continuar en verde.
- Auditoría automatizada con ripgrep/grep para validar que no queden referencias vivas.
