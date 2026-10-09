# INC-143: Soporte de Peticiones HTTP HEAD y Verificación de Dominio Awin

## 1. Contexto y Problema Detectado

Al intentar dar de alta el dominio `https://ludeka.es` en plataformas de marketing de afiliación (específicamente Awin), el validador automático rechaza la URL con el error:
> *"La URL debe estar accesible públicamente."*

### Causa Raíz Técnica:
1. **Peticiones HTTP HEAD rechazadas con 405:**
   - La inmensa mayoría de bots y validadores de URLs (incluyendo Awin) realizan peticiones `HEAD` para comprobar la disponibilidad de un sitio sin descargar el cuerpo HTML completo.
   - En ASP.NET Core Blazor Web App (`MapRazorComponents<App>()`), las rutas SSR mapeadas por defecto solo asocian los métodos `GET` y `POST`.
   - Cuando el servidor recibe una petición `HEAD /` o `HEAD /catalogo`, Kestrel/EndpointRouting responde inmediatamente con `405 Method Not Allowed` y cabecera `allow: GET, POST`, haciendo que cualquier validador asuma que la página no está viva o no es accesible públicamente.
2. **Acreditación de Propiedad de Dominio:**
   - Awin recomienda la presencia de una metaetiqueta o token de verificación en el código fuente de la página de inicio para confirmar que el solicitante es el legítimo propietario de la web.

---

## 2. Objetivos del Incremento

1. **Middleware de Soporte de Método HEAD (`Ludeka.Web.Extensions.HeadMethodExtensions`):**
   - Interceptar peticiones con método HTTP `HEAD` tempranamente en el pipeline.
   - Reescribir internamente la petición a `GET` para que los endpoints de Blazor SSR, páginas estáticas y APIs coincidan en el enrutamiento.
   - Desviar el flujo de salida (`Response.Body`) a `Stream.Null` para descartar los bytes del cuerpo, garantizando que el cliente reciba `200 OK`, cabeceras HTTP completas (`Content-Type`, `Date`, etc.) y 0 bytes de cuerpo según la especificación HTTP.
   - Restaurar de forma segura el método original `HEAD` y el stream de salida en el bloque `finally`.

2. **Verificación de Dominio Awin en `App.razor`:**
   - Incorporar en el `<head>` de `App.razor` la metaetiqueta canónica de verificación: `<meta name="awin-site-verification" content="awin" />`.

3. **Pruebas de Regresión y Contrato (TDD Estricto):**
   - Pruebas unitarias de integración en `tests/Ludeka.UnitTests/Web/HeadMethodSupportTests.cs` verificando:
     - Una petición `HEAD` a un endpoint devuelve `200 OK`.
     - Las cabeceras se preservan pero el cuerpo está completamente vacío (0 bytes).
     - Las peticiones `GET` y `POST` habituales siguen funcionando con normalidad sin alteración.
     - Presencia de la metaetiqueta en el HTML renderizado.
   - Suite completa de pruebas en verde (0 regresiones).

---

## 3. Plan de Tareas (ODD)

- [ ] **Tarea 1 (TDD - Fase Roja):** Diseñar las pruebas de contrato en `tests/Ludeka.UnitTests/Web/HeadMethodSupportTests.cs`.
- [ ] **Tarea 2 (Implementación del Middleware):** Crear `src/Ludeka.Web/Extensions/HeadMethodExtensions.cs` y registrarlo en `Program.cs`.
- [ ] **Tarea 3 (Metaetiqueta Awin):** Añadir la etiqueta de verificación en `src/Ludeka.Web/Components/App.razor`.
- [ ] **Tarea 4 (Verificación Local):** Ejecutar la suite de pruebas unitarias y verificar fase verde.
- [ ] **Tarea 5 (Cierre y Despliegue):** Abrir Pull Request con `scripts/sdd-worktree.ps1 pr soporte-peticiones-head`, custodiar CI, squash merge a `main` y verificar despliegue en Google Cloud Run.
