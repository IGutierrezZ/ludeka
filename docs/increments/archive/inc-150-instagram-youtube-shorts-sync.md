# Incremento 150: Sincronización Automática de Reels de Instagram a YouTube Shorts con Adaptación Híbrida IA y Directorio

## 📌 Identificador ODD / SDD
`inc-150-instagram-youtube-shorts-sync`

## 🎯 Estado
✅ Archivado (Verificado: 2.916 pruebas unitarias + 10 pruebas de integración = 2.926 pruebas totales)

---

## 1. Contexto y Objetivos
1. **Descubrimiento de Reels propios desde Instagram Graph API (`InstagramApiClient`)**:
   - Consulta de los Reels publicados en la cuenta oficial de Instagram de Ludeka (`GET /{InstagramAccountId}/media`), obteniendo su `id`, `caption`, `media_url`, `permalink`, `timestamp` y `video_duration`.
2. **Motor Híbrido de Adaptación de Texto y Menciones (`InstagramToYouTubeCaptionAdapter`)**:
   - **Fase 1 (Directorio Ludeka):** Cruza las menciones `@handle` de Instagram contra las entidades de Ludeka (`Publisher`, `Store`, `Creator` y `MonitoredSocialAccount`). Si tienen canal de YouTube registrado en `SocialLinks`, resuelve su `@handle`/canal de YouTube; si no, resuelve su nombre oficial y enlace a su ficha en `ludeka.es`.
   - **Fase 2 (Google Gemini Flash + Fallback Determinista):** Genera un título atractivo ($\le 90$ caracteres + `#Shorts`) y adapta el cuerpo de la descripción para YouTube utilizando las equivalencias del directorio y convirtiendo cualquier `@cuenta` externa no registrada en su nombre comercial limpio.
   - **Fase 3 (Cierre determinista):** Añade un bloque final editorial con el enlace a `https://ludeka.es`, enlaces a las fichas de Ludeka detectadas y hashtags de Shorts.
3. **Subida directa a YouTube Shorts vía OAuth 2.0 (`YouTubeShortsUploadClient`)**:
   - Intercambio de `RefreshToken` por `AccessToken` y subida en *streaming* del vídeo MP4 a YouTube Data API v3 (`videos.insert`), respetando un límite máximo de 5 vídeos por ejecución para operar 100% dentro de la cuota gratuita diaria de Google Cloud.
4. **Job Desatendido e Idempotencia (`InstagramYouTubeSyncJobRunner` / `instagram-youtube-sync`)**:
   - Tabla `InstagramReelSyncItems` (SQLite + PostgreSQL) para control de estado (`Pending`, `Uploaded`, `Failed`, `Skipped`), carga inicial cronológica de los Reels históricos y ejecución diaria automática, más visualización y disparo manual desde `/admin/instagram`.
