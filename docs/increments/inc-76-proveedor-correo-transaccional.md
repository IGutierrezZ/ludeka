# INC-76: Proveedor de Correo Transaccional (Resend / Brevo) para Magic Link y Notificaciones

> **Estado:** ⏳ Planificado (Backlog futuro)  
> **Fecha de Creación:** 2026-09-28  
> **Rama Prevista:** `inc/proveedor-correo-transaccional`  
> **Worktree Previsto:** `C:\repos\ludeka-wt\proveedor-correo-transaccional`  
> **Dependencias:** INC-64 (Acceso por correo con Magic Link), INC-47 (Trabajos en segundo plano en Cloud Run), INC-48 (Secretos y persistencia en producción)  
> **Especificación Viva Prevista:** `docs/specs/sistema/40-acceso-por-correo-magic-link.md` (actualización de infraestructura)

---

## 1. Cómo se descubrió

Durante la auditoría del flujo de autenticación y la revisión del acceso por enlace mágico (INC-64):
- Se comprobó que la pantalla `/login` y el servicio `MagicLinkService` están 100% operativos a nivel de dominio y contratos, pero la única implementación inyectada de `IEmailSender` es `DevelopmentEmailSender`.
- En entornos locales esto imprime el correo en consola, pero en producción (`Google Cloud Run`) el usuario final no recibe ningún correo en su buzón al solicitar acceso con enlace mágico.
- El mantenedor solicitó dejar planificado para el futuro la incorporación de un proveedor de email transaccional real para cerrar este ciclo.

---

## 2. El agujero, verificado

1. **Simulación permanente de email:** `LudekaServiceCollectionExtensions.cs` registra incondicionalmente `services.AddScoped<IEmailSender, DevelopmentEmailSender>()`. No existe ningún adaptador real para producción.
2. **Bloqueo de puerto 25 en Google Cloud Run:** Cloud Run restringe el tráfico saliente por el puerto SMTP estándar (25) para prevenir abusos de spam. El envío mediante clientes SMTP tradicionales basados en sockets directos requiere configuración de túneles o puertos alternativos (587/465).
3. **Falta de proveedor API HTTP moderno:** La solución óptima en contenedores serverless es un proveedor transaccional moderno vía REST/HTTP API (como **Resend** o **Brevo**) utilizando `HttpClientFactory`. Esto garantiza:
   - Conexión HTTPS saliente estándar por puerto 443 (100% compatible con Cloud Run).
   - Alta reputación de entrega, firmas DKIM/SPF y métricas de rebote.
   - Coste cero en volumen inicial (tiers gratuitos generosos: Resend ofrece hasta 3.000 emails/mes gratis; Brevo 300 emails/día).
4. **Plantillas visuales editoriales:** El cuerpo HTML actual generado en `MagicLinkService` es básico; se requiere una plantilla responsive con la identidad visual editorial de Ludeka (modo claro/oscuro, botón de acceso claro y advertencia de caducidad de 15 minutos).

---

## 3. Lo que pide el maintainer

1. Incorporar en el futuro un proveedor real de correo transaccional para que el Magic Link funcione de extremo a extremo en producción.
2. Mantener la resiliencia: si no hay credenciales configuradas (o en desarrollo), fallback automático a `DevelopmentEmailSender` sin romper la aplicación.
3. Gestionar la clave de API a través de variables de entorno y Google Secret Manager en Cloud Run.

---

## 4. Alcance y decisiones que hay que tomar

1. **Elección del proveedor:**
   - **Opción recomendada:** **Resend** (API extremadamente limpia, SDK o llamada HTTP trivial en C# con `HttpClient`, excelente entregabilidad y soporte de dominios personalizados como `ludeka.es`).
   - **Alternativa:** **Brevo** (antiguo Sendinblue) o cliente SMTP sobre TLS (puerto 587).
2. **Implementación de `ResendEmailSender`:**
   - Crear `ResendEmailSender : IEmailSender` en `Ludeka.Infrastructure.Services`.
   - Inyección vía `HttpClient` tipado (`services.AddHttpClient<IEmailSender, ResendEmailSender>()`).
   - Opciones tipadas `ResendOptions` (`ApiKey`, `FromEmail` ej. `acceso@ludeka.es` o `notificaciones@ludeka.es`, `FromName` «Ludeka»).
3. **Registro condicional en Dependency Injection:**
   - Si `string.IsNullOrWhiteSpace(options.ApiKey)`, inyectar `DevelopmentEmailSender`.
   - Si está presente, registrar `ResendEmailSender`.
4. **Plantilla HTML Editorial de Magic Link:**
   - Maquetación cuidada con tipografía nítida, caja de 1 solo clic y enlace alternativo en texto plano para clientes de correo restringidos.
5. **Secretos y CI/CD:**
   - Secreto `RESEND_API_KEY` en Google Secret Manager y mapeo en `ci-cd.yml` de Cloud Run.

---

## 5. Fuera de alcance

- Envío de correos masivos / marketing / newsletters comunitarias.
- Configuración DNS del dominio (registros MX/SPF/DKIM en el registrador del dominio `ludeka.es`, que es tarea del operador).

---

## 6. Criterios de aceptación

1. En desarrollo / pruebas unitarias, `DevelopmentEmailSender` sigue activo sin requerir claves de API externas.
2. En producción, con `ResendOptions:ApiKey` configurada, `SendEmailAsync` envía la petición HTTP a la API de Resend y retorna éxito (HTTP 200/201).
3. Si la API del proveedor responde error (ej. 401/429/500), el servicio captura el fallo, lo registra en el log estructurado con severidad `LogError` y lanza una excepción descriptiva (`EmailDeliveryException`) sin exponer claves.
4. Cobertura de pruebas unitarias con `MockHttpMessageHandler` validando serialización, encabezados de autorización Bearer y tratamiento de fallos HTTP.
5. El correo enviado incluye encabezados y cuerpo HTML accesibles con enlace directo `/login/magic-link?token=...`.

---

## 7. Riesgos y mitigaciones

- **Riesgo:** Exceder límites de cuota gratuita o sufrir abuso de peticiones de Magic Link.  
  *Mitigación:* Rate limiting ya implementado en endpoint de login y protección anti-spam.
- **Riesgo:** Caída o indisponibilidad del servicio de terceros durante el acceso de un usuario.  
  *Mitigación:* Logging detallado y mensaje claro al usuario en `/login` solicitando reintentar o usar acceso social (Google/Discord).

---

## 8. Plan de Tareas ODD (Work Units)

- [ ] **Tarea 1 (Opciones y Configuración):**
  - Crear `EmailOptions` y `ResendOptions` en `Ludeka.Infrastructure.Options`.
  - Configuración en `appsettings.json` y `appsettings.Production.json`.
- [ ] **Tarea 2 (Servicio HTTP Resend):**
  - Implementar `ResendEmailSender` con `HttpClientFactory`.
  - Pruebas unitarias con mocks HTTP simulando respuestas 200, 401 y 500.
- [ ] **Tarea 3 (Plantilla Editorial de Magic Link):**
  - Crear generador de plantilla HTML responsive en `EmailTemplateBuilder`.
  - Pruebas unitarias validando la presencia del token y enlace seguro.
- [ ] **Tarea 4 (Inyección y Fallback en DI):**
  - Extender `LudekaServiceCollectionExtensions.cs` con resolución condicional.
  - Pruebas de integración de DI en `tests/Ludeka.UnitTests/Infrastructure/DependencyInjectionTests.cs`.
- [ ] **Tarea 5 (Despliegue y Secretos):**
  - Incorporar variable y secreto `RESEND_API_KEY` en `.github/workflows/ci-cd.yml` para Cloud Run.
  - Documentación de puesta en marcha en `docs/deployment/google-cloud-run.md`.
- [ ] **Tarea 6 (Cierre y Actualización de Especificación):**
  - Actualizar `docs/specs/sistema/40-acceso-por-correo-magic-link.md` con el nuevo flujo real de producción.
