# Especificación: social-card-generator (Motor Gráfico Omnicanal y Plantillas de Redes)

## 1. Propósito y Contexto
Permite generar plantillas gráficas vectoriales (1:1 cuadrada para Instagram) y textos optimizados con hashtags a partir de los datos de cualquier juego del catálogo o sorteo. Facilita la difusión orgánica y el marketing de guerrilla a coste 0 €.

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-SCG-01: Generación de Imagen SVG de Marca (1:1)
- El sistema **DEBE** proporcionar un servicio (`ISocialCardService`) capaz de generar el código SVG completo de una tarjeta cuadrada (1080x1080 px).
- La tarjeta gráfica **DEBE** incluir:
  - Fondo editorial oscuro elegante (`#0B0F17` a `#1E293B`) con acentos en naranja corporativo (`#F97316`).
  - Carátula oficial del juego o imagen del sorteo renderizada en alta fidelidad.
  - Título principal en español y año de publicación.
  - Píldora de ADN Lúdico / Dinámica (ej. "⚔️ Eurogame Competitivo").
  - Semáforo de escalabilidad o Sello Fundador si aplica.
  - Pie de marca inferior: *"Ludeka • Bienvenido a tu mesa"*.

### REQ-SCG-02: Generación de Copy y Hashtags para Instagram
- El servicio **DEBE** generar un texto listo para pegar que incluya:
  - Titular llamativo con emojis.
  - Micro-análisis o veredicto de la casa.
  - Datos clave (minutos por jugador, edad real comunitaria y escalabilidad recomendada).
  - Bloque de hashtags curados en español (ej. `#juegosdemesa #ludeka #boardgames #devir #malditogames #juegosdemesaespaña`).

### REQ-SCG-03: Interfaz Táctil y Descarga en 1 Clic
- La vista de detalle del juego **DEBE** incluir un botón destacado `[ 🎨 Generar Cartel para Redes ]`.
- Al pulsarlo, se **DEBE** abrir un modal interactivo con la previsualización del SVG renderizado en vivo y botones:
  - `[ 📋 Copiar Texto del Post ]`: Copia el copy formateado con hashtags al portapapeles.
  - `[ 📥 Descargar Imagen SVG ]`: Descarga el archivo gráfico en el navegador.

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Generación de SVG de un juego del catálogo
```gherkin
Given un juego con título "Terraforming Mars" y carátula oficial
When se invoca la generación de la tarjeta social
Then el SVG devuelto contiene el título "Terraforming Mars", las dimensiones 1080x1080 y la marca "Ludeka"
And el texto para Instagram incluye los hashtags relevantes
```
