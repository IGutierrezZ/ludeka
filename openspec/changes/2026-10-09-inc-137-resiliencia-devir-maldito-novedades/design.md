# Diseño: INC-137 — Resiliencia en Extracción de Novedades (Devir y Maldito Games) y Despliegue de Jobs

## 1. Arquitectura de Cabeceras HTTP y Prevención de Detección WAF

Para evitar que Cloudflare WAF en datacenters (como Google Cloud Run `europe-west1`) clasifique las peticiones como tráfico automatizado malicioso, definimos un helper centralizado para peticiones de navegación editorial:

```csharp
private static HttpRequestMessage CreateBrowserNavRequest(HttpMethod method, string url, string? referer = null)
{
    var request = new HttpRequestMessage(method, url);
    request.Headers.Accept.Clear();
    request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
    request.Headers.AcceptLanguage.Clear();
    request.Headers.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
    request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"");
    request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
    request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
    request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
    request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
    request.Headers.TryAddWithoutValidation("sec-fetch-site", referer != null ? "same-origin" : "none");

    if (!string.IsNullOrWhiteSpace(referer))
    {
        request.Headers.Referrer = new Uri(referer);
    }

    return request;
}
```

En `LudekaServiceCollectionExtensions.cs`, la configuración de `HttpClient` para `IDevirReleasesExtractor` y `IMalditoReleasesExtractor` mantendrá el `UserAgent` único a nivel de cliente (`Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36`) sin añadir cabeceras `User-Agent` contradictorias en el `HttpRequestMessage`.

## 2. Reintento Comedido en DevirReleasesExtractor

En `ExtractCatalogPageAsync`:
1. Ejecutar petición inicial con timeout defensivo de 10s.
2. Si el código de respuesta es 403, 429 o >= 500:
   - Esperar 1.500 ms con `Task.Delay(1500, ct)`.
   - Reintentar una única vez con una nueva instancia de `HttpRequestMessage`.
3. Si el reintento también falla:
   - Registrar advertencia (`_logger.LogWarning(...)`).
   - Retornar `new DevirCatalogPageResultDto(Array.Empty<DevirCatalogItemDto>(), HasMore: false, Success: false)`.
4. Si tiene éxito:
   - Retornar los ítems parseados con `Success: true`.

## 3. Tolerancia y Ritmo en DevirImagesBackfillJobRunner

- Throttle: Entre páginas, añadir `await Task.Delay(750, workCt)`.
- Si `pageResult.Success == false`, incrementar contador `consecutiveFailures`. Si `consecutiveFailures >= 2`, abortar bucle de catálogo. Si es solo un fallo aislado, continuar a la siguiente página sin abortar el trabajo entero.
- Si `pageResult.Success == true && pageResult.Items.Count == 0`, se interpreta legítimamente como fin de catálogo (`break`).

## 4. Desacoplo de Tareas en MalditoReleasesExtractor

En lugar de `Task.WhenAll(homeTask, catalogTask)`, desacoplar la ejecución:

```csharp
string? homeHtml = null;
try
{
    homeHtml = await FetchWithRetryAsync(DefaultMalditoHomeUrl, referer: null, ct);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Fallo al descargar la portada de Maldito Games.");
}

string? catalogHtml = null;
try
{
    catalogHtml = await FetchWithRetryAsync(DefaultMalditoCatalogUrl, referer: DefaultMalditoHomeUrl, ct);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Fallo al descargar el catálogo reciente de Maldito Games. Se continuará únicamente con la portada.");
}

if (string.IsNullOrWhiteSpace(homeHtml) && string.IsNullOrWhiteSpace(catalogHtml))
{
    _logger.LogError("No se pudo obtener contenido de ninguna de las fuentes de Maldito Games.");
    return Array.Empty<EditorialReleaseItem>();
}

return ParseHtml(homeHtml ?? string.Empty, catalogHtml);
```

## 5. Actualización de Workflow CI/CD

En `.github/workflows/ci-cd.yml`:
Añadir `devir-images-backfill` al listado de trabajos del bucle `for JOB in ...`:
```yaml
for JOB in nightly-cataloging price-radar social-collector notification-outbox seed-staging drain-staging feed-sync editorial-releases-sync devir-images-backfill; do
```
Esto garantiza el despliegue automático del contenedor y la creación del Cloud Run Job en GCP.
