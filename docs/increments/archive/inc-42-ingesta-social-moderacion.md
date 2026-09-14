# Incremento 42: Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés + Directorio de Cuentas Monitorizadas)

- **Identificador SDD:** `change-42-ingesta-social-moderacion`
- **Estado:** ✅ **Archivado (Completado y Verificado)**
- **Pruebas Automatizadas:** 960 pruebas unitarias en verde (100% superado)
- **Rama:** `inc/ingesta-social-moderacion` (worktree en `C:\repos\ludeka-wt\ingesta-social-moderacion`)
- **Módulo del Sistema:** `docs/specs/sistema/28-hub-ingesta-social-moderacion.md`
- **Objetivo Principal:** Crear un ecosistema unificado y de coste cero de infraestructura para capturar, procesar, asistir mediante IA y moderar contenidos externos del mundo de los juegos de mesa (sorteos en Instagram, novedades editoriales, eventos lúdicos y vídeos de tutoriales/partidas/reseñas en YouTube e Instagram) con un flujo de alta exprés por URL ("copiar, pegar y listo"), modo manual avanzado para vídeos sin descripción, bandeja de moderación 100% editable antes de la publicación definitiva y directorio centralizado de canales y cuentas monitorizadas sincronizado con las editoriales, creadores y tiendas de Ludeka.

---

## 1. Alcance Funcional y Técnico

1. **Alta Exprés ("Copiar, Pegar y Listo") + Modo Manual Asistido:**
   - Modo Rápido: Introducción de una URL pública (Instagram, YouTube o web). Extracción de metadatos mediante oEmbed y OpenGraph sin APIs de pago (descartando Apify). Análisis del texto con Google Gemini Flash (o generador heurístico local) para identificar automáticamente si se trata de un Sorteo, Novedad, Evento o Vídeo/Medio, y extraer organizador, fechas, juego vinculado y requisitos.
   - Modo Manual Avanzado: Para vídeos, reels o publicaciones sin pie de foto descriptivo, el moderador especifica la URL, el juego del catálogo y la tipología (ej. "Ark Nova" + "Tutorial/Partida" u "Opinión"), y el sistema resuelve la miniatura/carátula optimizándola a WebP en R2.
2. **Bandeja de Moderación 100% Editable (`/admin/ingesta-social`):**
   - Todos los envíos se conservan inicialmente en estado `PendingReview`.
   - El moderador puede modificar cualquier campo extraído antes de aprobar (título, fechas límite o de evento, organizador/editorial, juego vinculado del catálogo con buscador reactivo, tipo de publicación).
   - Acción "Aprobar y Publicar": Crea la entidad definitiva (`Giveaway` para `/sorteos`, `WeeklyRelease` para `/novedades`, `BoardGameEvent` para `/eventos`, o `MediaItem` para `/multimedia` y fichas de juego) de manera atómica, marcando el ítem de la bandeja como `Approved`.
   - Acción "Descartar": Marca el ítem como `Rejected` con motivo opcional.
3. **Pipeline Gráfico con Cloudflare R2 y SkiaSharp:**
   - Las imágenes y miniaturas capturadas se procesan en memoria a formato WebP (máximo 1000px ancho, calidad 82%) y se suben al bucket R2 vía `IImageStorageService` bajo `social-inbox/{id}/thumbnail.webp`.
4. **Directorio de Cuentas y Canales Monitorizados (`/admin/canales-monitorizados`):**
   - Catálogo de fuentes comunitarias (Instagram, YouTube, webs) de editoriales, creadores y tiendas.
   - Acción "Sincronizar desde Directorio": Importa automáticamente las redes sociales registradas previamente en las entidades `Publisher`, `Creator` y `Store`.
   - Acceso rápido para visitar el perfil y lanzar el modal de alta exprés con el organizador preconfigurado en 1 clic.
