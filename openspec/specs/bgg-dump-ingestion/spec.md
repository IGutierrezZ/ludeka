# Especificación: Ingesta de Volcado BGG y Staging (bgg-dump-ingestion)

## 1. Contexto y Requerimientos

El sistema debe ser capaz de procesar el archivo o volcado de clasificación de BoardGameGeek (`bg_ranks` o export CSV equivalente) para construir la base de títulos candidatos sin saturar la tabla operativa `Games`.

### Requerimientos Funcionales
- **RF-01 (Descarga / Lectura del Volcado):** Soporte para consumir el volcado en formato CSV o comprimido GZ/ZIP desde una URL de BGG o archivo local.
- **RF-02 (Filtro de Tracción Comunitaria):** Solo los juegos con `usersrated >= 30` (configurable mediante `BggMassIngestionOptions.MinUsersRated`, por defecto 30) serán seleccionados para entrar en staging. Aquellos con menos de 30 votos se descartan silenciosamente para evitar ruido de juegos no publicados o prototipos sin datos.
- **RF-03 (Inserción Idempotente en Staging):** La tabla `BggCatalogStaging` almacena el `BggId`, título original, año de publicación, rango y número de votos. Si un `BggId` ya existe en `BggCatalogStaging` o en `Games`, se actualizan los metadatos de clasificación sin resetear su estado si ya fue procesado o promovido.
- **RF-04 (Modo Simulación / Fallback):** En entornos de prueba o desarrollo sin conexión, debe existir un dataset simulado con registros representativos de ranks BGG para verificar el flujo de extremo a extremo.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Filtro de relevancia e ingesta en staging

  Escenario: Filtrado de juegos por número de valoraciones
    Dado un volcado de BGG con 3 títulos:
      | BggId  | Title            | UsersRated | YearPublished |
      | 174430 | Gloomhaven       | 62000      | 2017          |
      | 224517 | Brass: Birmingham| 48000      | 2018          |
      | 999999 | Prototipo Fantasma| 12        | 2026          |
    Cuando se ejecuta el proceso de ingesta con MinUsersRated = 30
    Entonces Gloomhaven y Brass: Birmingham se registran en BggCatalogStaging con estado FetchStatus = Pending
    Y Prototipo Fantasma es ignorado

  Escenario: Idempotencia ante re-ejecución del volcado
    Dado que Gloomhaven ya existe en BggCatalogStaging con FetchStatus = Fetched
    Cuando se vuelve a ejecutar la ingesta del volcado
    Entonces el registro existente actualiza su rango o votos
    Y su estado FetchStatus se mantiene en Fetched
```
