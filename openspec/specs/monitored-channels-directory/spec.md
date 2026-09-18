# Especificación: monitored-channels-directory

Directorio central de cuentas y canales monitorizados de Instagram, YouTube y webs de la comunidad lúdica sin dependencias de servicios externos de scraping de pago (Apify).

---

## 1. Requerimientos Funcionales

### R3.1: Directorio Central de Perfiles y Canales
- El sistema dispondrá de un panel de administración en `/admin/canales-monitorizados`.
- Muestra el listado de fuentes oficiales y divulgativas monitorizadas:
  - Nombre de la entidad (ej. "Devir Iberia", "Asmodee España", "Meepletopia", "Zacatrus").
  - Plataforma: `Instagram`, `YouTube`, `TwitterX`, `TikTok`, `Web`.
  - Tipo de Cuenta: `Publisher` (Editorial), `Creator` (Divulgador/Autor), `Store` (Tienda), `Community` (Asociación/Comunidad).
  - Handle o identificador (`@deviriberia`, ID de canal YouTube).
  - URL directa al perfil.
  - Estado: Activo / Inactivo.
  - Última fecha de comprobación o captura.

### R3.2: Alta y Edición Rápida de Canales
- Formulario modal accesible para dar de alta nuevas cuentas o canales:
  - Nombre, plataforma, handle o URL, tipología y notas internas.
  - Validación de unicidad para evitar canales duplicados por plataforma.
  - Conmutador para activar o desactivar la monitorización en 1 clic.

### R3.3: Sincronización Automática con el Directorio Existente
- Ludeka ya cuenta con entidades de `Publisher`, `Creator` y `Store` con enlaces sociales (`SocialNetworkLink`).
- El panel debe incluir una acción "Sincronizar desde Directorio":
  - Recorre las editoriales, creadores y tiendas de la base de datos que posean enlaces a Instagram o YouTube.
  - Genera automáticamente las entradas correspondientes en el directorio de cuentas monitorizadas si aún no existían.
  - Asigna la categoría y el nombre correctos de forma consistente.

### R3.4: Acceso Rápido a Captura Exprés desde el Canal
- Cada fila o tarjeta del directorio debe disponer de:
  - Un enlace directo para abrir el perfil del canal en una pestaña nueva ("Visitar Perfil").
  - Un botón de acción rápida "⚡ Añadir Publicación": abre el modal de Alta Exprés preconfigurando el organizador con el nombre de la cuenta para acelerar el flujo del moderador al máximo.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Alta manual de una cuenta de Instagram en el directorio
  Dado un moderador en el panel "/admin/canales-monitorizados"
  Cuando pulsa "Añadir Cuenta" e introduce el nombre "GDM Games", plataforma "Instagram" y handle "@gdmgames"
  Entonces se crea la cuenta monitorizada con estado Activo
  Y aparece en el listado de editoriales

Escenario: Sincronización desde entidades del directorio
  Dado que existen 10 creadores en la base de datos con canales de YouTube configurados
  Cuando el moderador ejecuta "Sincronizar desde Directorio"
  Entonces se insertan 10 nuevas cuentas de tipo "Creator" y plataforma "YouTube" en el directorio
  Y no se duplican registros si se pulsa de nuevo el botón de sincronización

Escenario: Lanzar captura exprés desde una tarjeta de canal
  Dado el canal "La Mazmorra de Pacheco" en el directorio
  Cuando el moderador pulsa "⚡ Añadir Publicación"
  Entonces se abre el modal de Alta Exprés con el organizador prepoblado como "La Mazmorra de Pacheco"
```
