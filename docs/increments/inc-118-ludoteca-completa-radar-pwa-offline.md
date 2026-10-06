# INC-118: Mi Ludoteca Completa (Cola Comunitaria, ADN Lúdico, Radar de Compra) y Modo Offline PWA

## Estado
⏳ Planificado (Fase 6 del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/ludoteca-completa-radar-pwa-offline`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
La gestión de la colección personal y el área de cuenta (`MyLibrary.razor`, `/cuenta/*`) deben asumir el lenguaje de píldoras, contadores y tipografía display de la Revista Lúdica, resolviendo la totalidad de las herramientas personales:
1. **Pestañas canónicas en píldoras:**
   - *En mi ludoteca* (con contadores e imágenes giradas).
   - *Jugados*.
   - *Comprar* (con **Radar de compra inteligente activo**, bajadas de precio, mínimos históricos y alertas).
   - *Diario de partidas* (estadísticas del mes, lugar favorito y formulario para registrar partida).
   - *Préstamos* (seguimiento de juegos prestados, formularios y devoluciones).
   - *Cola comunitaria* (seguimiento de peticiones de importación a BGG y votos de la comunidad).
   - *ADN y estadísticas* (distribución por categoría, dureza y comensales, con botón para ver y copiar perfil público).
2. **Acciones superiores:** *«Importar de BGG»*, *«+ Añadir por BGG»* (búsqueda asistida en catálogo BGG) y *«Explorar catálogo»*.
3. **Área de cuenta unificada (`/cuenta`):**
   - Agrupación en una única página accesible con anclas: `01 Apariencia y tema`, `02 Ubicación y país`, `03 Privacidad` (switches estilo píldora de 56x32 px) y `04 Conexiones OAuth`.
4. **Soporte PWA y Modo Fuera de Cobertura:**
   - Detección de pérdida de red, banner contextual y pantalla amigable «Estás fuera de cobertura» (`offline.html`) para rutas no cacheadas, permitiendo siempre consultar la ludoteca local.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Pages/MyLibrary.razor`**:
   - Rediseño integral de cabecera con 4 cifras clave, barra horizontal de pestañas y paneles correspondientes.
   - Integración de modales `RecordPlayModal`, `LoanModal`, `BggImportModal` y `BggSearchModal`.
2. **`src/Ludeka.Web/Components/Pages/Account.razor` y subsecciones**:
   - Reestructuración de la página de cuenta con secciones numeradas del 01 al 04 y switches reactivos.
3. **PWA y Offline (`wwwroot/js/ludeka-offline.js`, `offline.html`)**:
   - Adaptación de estilos para sincronización de cola outbox y alerta visual en la cabecera.
4. **Pruebas de Contrato**:
   - `MyLibraryContractTests.cs`, `AccountPrivacyContractTests.cs` y tests de comportamiento offline.

## Criterios de Aceptación
- Las 7 pestañas de la ludoteca son operativas y respetan el estado offline en caso de caída de red.
- El radar de compra muestra correctamente los mínimos históricos y opciones de alerta.
- La página de cuenta unificada persiste las preferencias de apariencia, país y privacidad en la base de datos.
