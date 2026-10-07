# INC-122: Reparación de Calidad y Completitud de Imágenes BGG (Portadas, Contraportadas y En Mesa)

- **ID:** INC-122
- **Slug:** `reparacion-imagenes-bgg`
- **Estado:** ✅ Archivado
- **Fecha de inicio:** 2026-10-07
- **Fecha de finalización:** 2026-10-07
- **Módulo impactado:** 53 (Portadas en Español y Medios Comunitarios Top 3.000)
- **Pruebas unitarias:** 2.604 verificadas al 100%

---

## 1. Motivación y Problema
En el despliegue del sincronizador de medios BGG (INC-120), se manifestaron fallos severos de calidad visual y datos incompletos en producción:
1. Las imágenes de galería comunitaria usaban miniaturas micro de 64×64 píxeles (`imageurl` en lugar de `imageurl_lg`), provocando pixelado extremo en el carrusel.
2. Al no emplear filtrado por `tag=BoxBack` ni ordenación `sort=hot`, la contraportada nunca se encontraba entre las 10 fotos recientes de usuario, dejando el carrusel con solo 2 imágenes.
3. La portada oficial era sobreescrita con fotos caseras de usuarios si la versión española carecía de carátula propia adjunta, obviando la portada raíz canónica de BGG.
4. El carrusel forzaba una relación de aspecto `aspect-[4/3]` que comprimía las carátulas verticales o cuadradas.

---

## 2. Objetivos Técnicos Cumplidos
- Corregido el mapeo DTO y el cliente `GeekDoImagesClient` para consumir `imageurl_lg` (1024×1024), descartar `__micro` y consultar de forma dirigida `tag=BoxBack&sort=hot`.
- Blindada la jerarquía de portadas en `BggImagesSyncService` para salvaguardar la carátula oficial de raíz antes de admitir fotos comunitarias.
- Detectadas y sustituidas URLs corruptas con `__micro` en el barrido de catálogo.
- Ajustado `GameImageCarousel.razor` con proporción equilibrada `aspect-square sm:aspect-[4/3] max-h-[460px]`, fondo oscuro editorial (`bg-neutral-900/90`) y preservación de atributos anti-CLS.
- Suite global verificada con 2.604 pruebas unitarias al 100% en verde (cero regresiones).
