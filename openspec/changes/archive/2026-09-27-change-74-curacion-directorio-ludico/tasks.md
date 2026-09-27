# Checklist de Tareas: INC-73 — Curación y Saneamiento Integral del Directorio Lúdico Español

## Fase 1: Curación de Editoriales (46 entidades)
- [x] 1.1 Contrastar y corregir los dominios web oficiales de las editoriales en `seed-directory.json` (fijar `www.masqueoca.com`, `es.asmodee.com`, sanea sellos con dominios expirados o redirigidos).
- [x] 1.2 Validar y fijar los canales oficiales de YouTube (`@handle`) para las editoriales con presencia audiovisual activa.
- [x] 1.3 Purgar cuentas de redes sociales inactivas o inexistentes (404) en perfiles editoriales.

## Fase 2: Curación de Tiendas Especializadas (37 entidades)
- [x] 2.1 Corregir dominios web de tiendas (fijar `https://nostromocomics.com`, revisar tiendas con cambios de URL comercial y tiendas inactivas).
- [x] 2.2 Verificar y asegurar la integridad de los metadatos comerciales (`affiliateCode`, `hasLoyaltyProgram`, `shippingCountries`).
- [x] 2.3 Purgar enlaces rotos en redes sociales de tiendas.

## Fase 3: Curación de Creadores de Contenido (35 divulgadores)
- [x] 3.1 Auditar y fijar con precisión los canales canónicos de YouTube (`@handle`) de cada creador.
- [x] 3.2 Corregir las webs oficiales de divulgación (asignar `https://doctormeeple.es`, `https://www.elclubdante.es`).
- [x] 3.3 Retirar cuentas de Twitter/X y redes inactivas que devuelvan 404.

## Fase 4: Normalización Estructural del JSON
- [x] 4.1 Retirar sistemáticamente la redundancia de `{ "platform": "Website", ... }` en `socialLinks` cuando ya existe `websiteUrl` en la entidad.
- [x] 4.2 Verificar que todos los `slug` se conserven intactos.

## Fase 5: Pruebas Unitarias y Validación
- [x] 5.1 Añadir en `DirectorySeederTests.cs` pruebas de integridad sobre `seed-directory.json` (ausencia de webs duplicadas, formatos https y casos específicos clave).
- [x] 5.2 Ejecutar la suite completa de pruebas unitarias verificando que el 100% pase en verde (1.948 pruebas).
- [x] 5.3 Ejecutar auditoría de red de contraste y comprobar la reducción drástica de fallos.
