# Incremento 125: Rediseño Editorial de Clasificación, Directorios (Editoriales, Creadores, Tiendas) y Transparencia

- **ID del Incremento:** `INC-125`
- **Slug:** `clasificacion-directorios-transparencia`
- **Rama:** `inc/clasificacion-directorios-transparencia`
- **Fecha:** 2026-10-07
- **Estado:** ✅ Verificado (Listo para PR)
- **Épica / Contexto:** Revista Lúdica (Alineación estética y funcional con el prototipo de Claude Design `Ludeka Final.dc.html` y `Ludeka Final Movil.dc.html`).

---

## 1. Descripción del Problema y Objetivos
A partir del análisis del prototipo interactivo de referencia de Claude (`Ludeka Final.dc.html` y su renderizado móvil `Ludeka Final Movil.dc.html`), se alinearon estéticamente y funcionalmente cinco pantallas clave de la aplicación con la identidad visual "Revista Lúdica":

1. **Clasificación (`/clasificaciones` → `Leaderboards.razor`):**
   - Hero ciruela (`var(--plum)`), tipografía monumental `Clasificación mensual` con cursiva en mostaza (`var(--mustard)`).
   - Selector de mes en píldora con navegación `‹` y `›`.
   - Banner reactivo de opt-in y estado de participación del usuario.
   - Podio Top 3 con números monumentales `01`, `02`, `03` en tonos de medalla sobre tarjetas limpias.
   - Tabla completa accesible con fila destacada del usuario actual.
   - Bloque inferior de tres reglas de mesa en columnas monumentales.
2. **Directorio y Ficha Editorial (`/editoriales` → `PublishersDirectory.razor`, `PublisherDetail.razor`):**
   - Cabecera display con eyebrow temático `Ecosistema lúdico · Casas de edición` y título monumental.
   - Buscador en píldora con borde `--rule` y fondo `--card`.
   - Tarjetas editoriales con borde superior `3px solid var(--rule)`, avatares en fondos pasteles con rotaciones sutiles, localización geográfica, catálogo de títulos y botón de me gusta.
   - Ficha editorial con estructura hero de directorio, avatar rotado -4°, enlaces oficiales y catálogo de títulos mediante `GameCard`.
3. **Directorio y Ficha de Creadores (`/creadores` → `CreatorsDirectory.razor`, `CreatorDetail.razor`):**
   - Cabecera editorial con eyebrow reetiquetado `Creadores de Contenido • Divulgadores del Hobby`.
   - Buscador reactivo, tarjetas de perfil con avatares pasteles rotados, nacionalidad con icono `globe`, biografía, redes sociales y botón de me gusta.
   - Ficha de creador con hero de directorio rotado, enlaces a web oficial, perfil BGG y canales de divulgación.
4. **Directorio y Ficha de Tiendas (`/tiendas` → `StoresDirectory.razor`, `StoreDetail.razor`):**
   - Buscador en píldora, selector de país y filtros por tipo (*Todas*, *Híbrida*, *Física*, *Online*).
   - Tarjetas con borde superior `3px solid var(--rule)`, avatar pastel rotado, distintivo de Club de fidelidad, cobertura de envíos, dirección y enlace a ofertas activas.
   - Ficha de tienda con hero editorial, enlaces directos a tienda online y catálogo de juegos en oferta con precios y stock en tiempo real.
5. **Transparencia (`/transparencia` → `Transparency.razor`):**
   - Cabecera editorial `Transparencia y afiliados` con tipografía de revista y cursiva en terracota.
   - Cuatro bloques numerados monumentales (`01` a `04`) con bordes marcados `--rule`, iconos Lucide temáticos (`zap`, `dices`, `camera`, `gift`) y explicaciones honestas sobre el modelo de sostenibilidad.
   - Módulo de apoyo comunitario con botones en píldora a Ko-fi, Discord oficial y Telegram.

---

## 2. Verificación de Contratos y Calidad
- **Pruebas de Marcado Contractual (`WebMarkupContractTests`):** 111/111 pruebas superadas en verde (0 fallos).
- **Pruebas de Clasificación (`LeaderboardsPageContractTests`):** 16/16 pruebas superadas en verde.
- **Pruebas de Transparencia y Comunidad (`CommunityAndSupportLinksContractTests`):** 6/6 pruebas superadas en verde.
- **Pruebas de Tiendas y Ofertas (`StoreOffersCardTests`, `StoreStockDomainTests`):** 78/78 pruebas superadas en verde.
- **Suite Completa del Monorepo:** 2.619 de 2.619 pruebas unitarias y de integración superadas al 100% en verde.
- **Accesibilidad y Semántica:** Cumplimiento de WCAG 2.2 AA (roles, estados de accesibilidad, contrastes, foco visible `focus-visible:ring-2` y ausencia de emojis directos en marcado).
- **Commits atómicos por unidad de trabajo:** Sin atribución de IA ("Co-Authored-By"), en formato Conventional Commits.
