# INC-117: Ficha de Juego Editorial (Secciones 01-05, Cifras Clave, Herramientas Staff y Banner de Juego Padre)

## Estado
✅ Verificado (Fase 5 del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/ficha-editorial-bloques-staff-veredicto`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
La ficha de juego (`GameDetail.razor`) es el núcleo del producto. La dirección «Revista Lúdica» organiza la información en bloques secuenciales numerados con gran impacto visual:
1. **Cabecera partida:**
   - Izquierda: Eyebrow en acento, título gigante en clamp apretado, créditos con enlaces a directorios de editorial y creadores, franja de 3 cifras clave (Rating BGG, Consenso Ludeka en terracota, Jugadores ideales en verde) entre regla de 3 px y línea fina.
   - Botonera de ludoteca en píldoras: *«Añadir a mi ludoteca»*, *«Lo quiero»*, *«Jugado»* y *«+ Registrar partida»*.
   - Derecha: Bloque terracota con la carátula rotada a $3^\circ$, enlace canónico a BGG y pegatina circular mostaza girada a $-12^\circ$ con el veredicto fundador.
2. **Cinta inversa de mercado:** mejor precio hoy, mínimo histórico y recuento de medios.
3. **Cuerpo editorial en dos columnas:**
   - `01 ¿A cuántos se disfruta?`: 7 columnas con barras de escalabilidad y frase resumen.
   - `02 ADN lúdico`: cuadrícula compacta con clave mayúscula y valor negrita.
   - `03 El veredicto`: cita en cursiva destacada con selector de origen (Mesa Fundadora / Síntesis con IA).
   - `04 Vídeos, fundas y reglas`: pestañas con cuadrícula de vídeos, tabla de fundas y acordeón de FAQs.
   - `05 Expansiones`: lista con etiquetas de necesidad pastel y enlace al juego base cuando aplique (`ParentGameBanner`).
   - Lateral: tarjeta verde de *«Dónde comprar»* con precios y afiliados, y caja de *«Tu valoración»* con 5 estrellas.
4. **Herramientas de Mesa y Moderación integradas:**
   - Bloque staff en el veredicto para *«Generar síntesis con IA»* (con vista previa en borrador), *«Editar veredicto»*, *«Editar ficha»* y *«Reportar problema»*.
5. **Móvil:** Barra inferior flotante con el mejor precio y el botón principal de colección siempre al alcance del pulgar.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Pages/GameDetail.razor`**:
   - Reestructuración integral según la maqueta de la Opción C.
   - Integración de modales existentes (`RecordPlayModal`, `LoanModal`, `FoundingVerdictModal`, `GameEditorModal`, `GameReportModal`).
   - Mantenimiento del banner `ParentGameBanner.razor` para expansiones.
2. **Componentes Relacionados**:
   - Actualización de `ScalabilityTrafficLight.razor` al formato de 7 columnas con barra de 10 px.
   - `StoreOffersCard.razor` estilizada como tarjeta verde redondeada (24 px).
3. **Pruebas de Contrato**:
   - `GameDetailContractTests.cs` y tests de permisos staff en la ficha.

## Criterios de Aceptación
- Visualización perfecta en escritorio y en móvil con barra de compra inferior fija.
- Veredicto interactivo con generación y aprobación de resúmenes de IA para staff.
- Cero pérdida de metadatos o enlaces a expansiones.
