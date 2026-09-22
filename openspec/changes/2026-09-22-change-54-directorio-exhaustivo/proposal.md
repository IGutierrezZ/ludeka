# Propuesta de Cambio — INC-54: Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico Español

## 1. Motivación y Contexto
El directorio de Ludeka (Editoriales en `/editoriales`, Tiendas en `/tiendas` y Creadores de contenido en `/creadores`) contaba hasta la fecha con una semilla mínima y testimonial (7 editoriales, 6 tiendas y 12 creadores), sembrada de forma cableada en código C# dentro de `DirectorySeeder.cs`.

Para que Ludeka actúe verdaderamente como *"El Letterboxd de los juegos de mesa en español"*, el directorio debe ofrecer desde el primer momento una cobertura exhaustiva del ecosistema hispanohablante:
1. **Prácticamente todas las editoriales de juegos de mesa de España** (más de 45 sellos activos, cubriendo gigantes, editoriales medianas, sellos independientes de autor, microeditoriales y editoriales infantiles/educativas).
2. **Top 35 de tiendas especializadas en España** (híbridas de referencia, tiendas físicas históricas de las principales capitales y tiendas online con programa de fidelidad y envíos nacionales).
3. **Top 35 de creadores de contenido y divulgadores referentes** (canales de YouTube consolidados, divulgadores con foco en reseñas, tutoriales, partidas y actualidad lúdica).
4. **Enriquecimiento de datos riguroso**: nombre, slug canónico, descripción editorial redactada en castellano, URL de imagen o logotipo oficial, sitio web canónico y red de enlaces sociales completos (YouTube con su handle `@`, Instagram, Twitter/X, Twitch, etc.).
5. **Mecanismo de carga y sincronización desatendido**: cero entrada manual de datos; ejecutable tanto vía CLI (`Ludeka.Jobs` con `dotnet run --project src/Ludeka.Jobs -- seed-directory`) como mediante un botón administrativo en la consola web con feedback visual reactivo, además de la siembra automática al arrancar en desarrollo.

## 2. Alcance Propuesto
- **Asset de Datos Canónico (`seed-directory.json`):** Creación de un JSON embebido en `Ludeka.Infrastructure` (con fallback al disco para desarrollo) que reúne el censo íntegro de las tres entidades.
- **Evolución del Seeder (`DirectorySeeder`):** Reemplazar las listas fijas en código por un cargador y reconciliador inteligente que lee el JSON, previene duplicados por slug, actualiza datos vacíos y respeta la regla de purga de diseñadores retirados de INC-31 y los creadores añadidos por moderadores.
- **Runner de Trabajo Autónomo (`SeedDirectoryJobRunner`):** Incorporación del trabajo `seed-directory` en `Ludeka.Jobs` para ejecución directa por terminal.
- **Acción Administrativa en Web:** Botón en `/admin/cola-catalogacion` (o similar consola de moderación) para disparar la sincronización del padrón con feedback interactivo.
- **Sincronización Automática con Foco Multimedia:** Dado que `ChannelDirectoryProvider` ya lee de la base de datos las entidades con YouTube, las nuevas editoriales, tiendas y creadores quedan automáticamente integrados en el radar de monitorización y prioridad de vídeos de Ludeka.

## 3. Criterios de Aceptación
1. **Editoriales:** Al menos 45 editoriales de España sembradas, con enlaces web, logos y redes verificadas.
2. **Tiendas:** Al menos 35 tiendas especializadas de España (híbridas/online) sembradas, con dirección, tipo de tienda y enlaces.
3. **Creadores:** Al menos 35 creadores de contenido lúdico en español sembrados, con sus enlaces y canales de YouTube/Instagram.
4. **Mecanismo de Carga CLI:** `dotnet run --project src/Ludeka.Jobs -- seed-directory` puebla o actualiza el directorio sin errores.
5. **Mecanismo Web:** Botón interactivo en el panel de administración que sincroniza el padrón y reporta el recuento al operador.
6. **Idempotencia:** Múltiples ejecuciones consecutivas del seeder no duplican registros ni borran creadores dados de alta manualmente.
7. **Pruebas:** 100% de la suite de pruebas unitarias en verde, incluyendo nuevas pruebas de deserialización, seeder e idempotencia.
