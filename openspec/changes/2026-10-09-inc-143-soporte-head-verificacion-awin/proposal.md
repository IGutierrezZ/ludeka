# Propuesta de Cambio — Soporte HTTP HEAD y Verificación de Dominio Awin (INC-143)

## Motivación y Contexto
Al solicitar la afiliación de `https://ludeka.es` en plataformas como Awin, el validador automático rechaza la URL con *"La URL debe estar accesible públicamente"*.
Esto se debe a que las peticiones `HEAD` no están mapeadas en Blazor SSR (`MapRazorComponents`), devolviendo `405 Method Not Allowed`.

## Solución Técnica
1. Implementar un middleware `UseHeadMethodSupport` que capture peticiones `HEAD`, las convierta internamente a `GET` y dirija el cuerpo de respuesta a `Stream.Null`, devolviendo `200 OK` con cabeceras y 0 bytes de cuerpo.
2. Añadir en `App.razor` la metaetiqueta `<meta name="awin-site-verification" content="awin" />`.
3. Cobertura con tests unitarios xUnit asegurando que peticiones HEAD devuelven 200 OK y body vacío.
