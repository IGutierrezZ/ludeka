# Investigación — Mapeo de dominio propio en Cloud Run

> **Fase:** `sdd-research` · **Fecha:** 2026-09-20
> **Pregunta:** cómo servir un dominio propio en Cloud Run antes del primer despliegue, para poder registrar las URL de retorno de OAuth.
> **Región del proyecto:** `europe-west1` (`.github/workflows/ci-cd.yml`).
> **Decisión del maintainer tras este informe:** **mapeo directo**, asumiendo a sabiendas la advertencia de Google (§2).

---

## 1. La sospecha de partida era falsa

El orquestador sospechaba que los *domain mappings* de Cloud Run no estaban disponibles en `europe-west1` y que eso obligaría a un balanceador. **Es falso.**

Regiones soportadas, **verificadas por el orquestador con lectura directa** de `https://docs.cloud.google.com/run/docs/mapping-custom-domains`:

`asia-east1`, `asia-northeast1`, `asia-southeast1`, `europe-north1`, **`europe-west1`**, `europe-west4`, `us-central1`, `us-east1`, `us-east4`, `us-west1`.

Fuera de esa lista, la documentación remite a las otras opciones de mapeo.

---

## 2. Lo que sí hay que saber: Google desaconseja su propio mecanismo

Citas literales de la misma página, **verificadas por el orquestador**:

> «Due to latency issues, they are not production-ready and are not supported at General Availability.»
> «At the moment, this option is not recommended for production services.»

**No es una restricción regional:** aplica a las diez regiones. La funcionalidad está en fase *preview*.

**Decisión tomada:** el maintainer opta por el mapeo directo de todas formas. El razonamiento, que debe quedar por escrito en la guía de despliegue:

- El proyecto **no tiene tráfico todavía**, así que el riesgo de latencia es bajo y sus consecuencias, reversibles.
- El balanceador global cobra **exista o no tráfico**, lo que rompería el «coste cero sin tráfico» con el que está dimensionado todo el despliegue (512 MB, 1 vCPU, `min-instances: 0`).
- La migración a balanceador **sigue abierta** cuando el tráfico la justifique.

---

## 3. Procedimiento

### 3.1. Verificar la propiedad del dominio

```
gcloud domains verify DOMINIO_BASE
```

- Abre un flujo en el navegador que verifica contra **Google Search Console**.
- Es obligatorio salvo que el dominio se comprara a través de Google.
- Para un subdominio (`app.tudominio.com`) se verifica el **dominio padre** (`tudominio.com`).
- **Es un paso de cuenta, independiente del servicio: se puede hacer antes del primer despliegue.**

### 3.2. Crear el mapeo

⚠️ **La orden GA `gcloud run domain-mappings create` es para Cloud Run *for Anthos*/GKE, no para Cloud Run gestionado.** Para el caso de Ludeka hay que usar la variante `beta`, **verificado por el orquestador en la página oficial**:

```
gcloud beta run domain-mappings create --service SERVICE --domain DOMAIN --region europe-west1
```

La referencia marca esta orden como *Beta*, con la nota «This command is currently in beta and might change without notice». `--region` es opcional en la referencia, pero conviene pasarlo siempre porque la funcionalidad está restringida por región.

Para consultar los registros DNS que Google exige para tu dominio concreto:

```
gcloud beta run domain-mappings describe --domain DOMAIN --region europe-west1
```

> **Confianza media**, no verificado literalmente: la variante `gcloud beta run deploy --domain ...`, que crearía servicio y mapeo en una sola orden. Confírmala con `gcloud beta run deploy --help` antes de usarla.

### 3.3. Registros DNS

| Caso | Tipo de registro |
|---|---|
| Dominio raíz o ápex (`tudominio.com`, nombre `@`) | `A` y `AAAA` |
| Subdominio (`www.` o `app.`) | `CNAME` |

**Los valores concretos no son constantes públicas:** Google los entrega al crear el mapeo, y se consultan con el `describe` de arriba. No los inventes ni los copies de ningún blog.

> **No confirmado contra fuente primaria:** el número exacto de registros `A` y `AAAA` para el ápex (se citan cuatro y cuatro en fuentes secundarias).

### 3.4. Trampa del registro CAA

Cita literal de la documentación:

> «If you use Certification Authority Authorization (CAA) DNS records for your custom domain, authorize both `pki.goog` and `letsencrypt.org`.»

**Si tu dominio ya tiene registros CAA restrictivos y no incluyes esas dos entidades, la emisión del certificado falla en silencio.** Es exactamente el tipo de fallo sin síntoma que este proyecto lleva dos incrementos persiguiendo.

### 3.5. Certificado TLS

- Google emite y renueva un certificado gestionado automáticamente en cuanto el DNS resuelve.
- Plazo habitual: unos 15 minutos; hasta 24 horas.
- No admite certificados comodín, no permite subir uno propio en esta modalidad y no se puede desactivar TLS 1.0/1.1.

> **Hueco declarado:** la página **no dice** si el servicio queda inaccesible por HTTPS mientras se aprovisiona el certificado. Lo esperable es un error de TLS contra el dominio nuevo, mientras la URL `*.run.app` sigue respondiendo, pero **no está documentado**. Recomendación: no dirigir tráfico real al dominio hasta confirmar el certificado activo.

---

## 4. Implicación directa para `UseForwardedHeaders`

**Este es el hallazgo que más afecta al diseño de INC-52, y es un hueco de evidencia real.**

La página *Container runtime contract* de Cloud Run **no menciona `X-Forwarded-Proto`, `X-Forwarded-For`, `Via` ni `Forwarded`**. Verificado por ausencia.

La única confirmación literal procede de **Cloud Run functions**, que es un producto hermano sobre la misma infraestructura, **no el mismo producto**:

> «X-Forwarded-For: A comma-delimited list of IP addresses through which the client request has been routed.»
> «X-Forwarded-Proto: Shows http or https based on the protocol the client used to connect to your application.»

**Consecuencias que el diseño debe asumir explícitamente:**

1. Que Cloud Run envía `X-Forwarded-Proto` es **casi seguro pero no está documentado para este producto**. El diseño no puede citarlo como hecho oficial.
2. `ForwardLimit = 1` es un punto de partida razonable con mapeo directo, que introduce **un solo salto** de proxy. **Si algún día se migra a balanceador global, serían dos**, y el valor tendría que subir.
3. **No fijes el valor a ciegas.** Lo correcto es comprobar empíricamente las cabeceras crudas que llegan al contenedor antes de darlo por bueno en producción.

La partición propuesta ya contempla una **prueba negativa** —que sin vaciar las listas de confianza el esquema no cambia—, que es la forma correcta de convertir esta suposición en algo demostrado en vez de citado.

---

## 5. Alternativas descartadas, y por qué

| Vía | Coste | Motivo del descarte |
|---|---|---|
| **Balanceador de aplicación externo global** | Del orden de 18 USD/mes en reglas de reenvío **(no confirmado con lectura directa)** más IP estática **(cifras contradictorias entre fuentes)** | Es la recomendación de Google para producción, pero cobra exista o no tráfico y rompe el coste cero del proyecto. Queda como vía de migración futura |
| **Firebase Hosting** con *rewrites* a Cloud Run | Gratis en plan Spark (10 GB de almacenamiento, 360 MB/día de transferencia) | Conserva el coste cero, pero añade dependencia de un producto con **términos de servicio distintos**, y no se pudo confirmar si soporta dominio ápex ni su plazo de TLS |

---

## 6. Huecos de evidencia declarados

1. Coste exacto de la IP estática: fuentes contradictorias, lectura directa fallida dos veces.
2. Coste del *forwarding rule*: procede de un resumen de búsqueda, no de lectura literal de la página de precios.
3. Si el servicio queda inaccesible durante el aprovisionamiento del certificado.
4. Número exacto de registros `A`/`AAAA` para el ápex.
5. Sintaxis de `--domain` en `gcloud beta run deploy`.
6. Comportamiento comparado de `X-Forwarded-*` entre mapeo directo y balanceador.
7. Soporte de dominio ápex en Firebase Hosting.

**Ninguno bloquea la decisión tomada.** Los puntos 1 y 2 solo importarían si se eligiera el balanceador; el 3 y el 5 se resuelven al provisionar; el 6 lo resuelve la prueba negativa del diseño.

---

## 7. Verificación del orquestador

El investigador no tenía acceso al repositorio y trabajó solo con fuentes web. El orquestador releyó por su cuenta `https://docs.cloud.google.com/run/docs/mapping-custom-domains` y confirmó literalmente las tres afirmaciones pivote: la lista de regiones con `europe-west1` incluida, las dos citas sobre el estado *preview* y no recomendado para producción, y que la orden lleva `beta`.

El resto de afirmaciones conserva la calificación de confianza que les dio el investigador, que fue honesto al distinguir lo verificado de lo inferido.
