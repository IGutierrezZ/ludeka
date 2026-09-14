# Especificación: Orquestador Nocturno y Drenaje de Staging (nightly-orchestrator-staging)

## 1. Contexto y Requerimientos

El servicio en segundo plano `NightlyCatalogingHostedService` procesaba anteriormente una cuota fija de 20 juegos del listado Hot de BGG. Con la ingesta masiva, debe convertirse en un orquestador que gestione de manera balanceada y no bloqueante múltiples fuentes de entrada.

### Requerimientos Funcionales
- **RF-01 (Prioridad 1: Novedades Editoriales):** Detección y extracción automática de títulos a partir de noticias o publicaciones semanales (`INewsGameExtractor`).
- **RF-02 (Prioridad 2: Cola de Usuarios Manuales):** Procesar solicitudes pendientes registradas por usuarios (`IPendingBggImportRepository`). Cada juego completado promueve las listas comunitarias de los usuarios.
- **RF-03 (Prioridad 3: Drenaje Progresivo de Staging Masivo):**
  1. **Detalle Thing BGG:** Consultar en lotes de hasta 20 IDs a `/xmlapi2/thing?id={ids}&stats=1` para rellenar descripción, diseñador, editorial, escalabilidad y mecánicas en staging.
  2. **Imágenes GeekDo + R2:** Para los juegos con Thing completado, buscar las 3 fotos comunitarias, subirlas a R2 y registrar URLs.
  3. **Síntesis IA en Lotes:** Procesar lotes de 5 a 10 juegos con Gemini Flash mientras la cuota del día lo permita.
  4. **Promoción a Catálogo (`Games`):** Convertir los registros de staging completamente listos en entidades `Game` definitivas e insertarlas en la base de datos de manera atómica, marcando `PromotionStatus = Promoted`.
- **RF-04 (Prioridad 4: Descubrimiento de Novedades BGG):** Verificación periódica de nuevos títulos añadidos a BGG para encolar en staging los lanzamientos recientes.
- **RF-05 (Bitácora Consolidada):** Registro del número de juegos procesados por cada fase, indicando tiempos, fallos y motivo de pausas (ej. cuota de Gemini alcanzada).

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Orquestación nocturna integral

  Escenario: Ejecución nocturna con drenaje balanceado
    Dadas 2 peticiones de usuario en cola prioritaria y 15 juegos en staging listos para procesar
    Cuando el servicio nocturno se ejecuta con un límite diario de 20 juegos
    Entonces procesa primero las 2 peticiones de usuario
    Y a continuación drena los juegos de staging hasta agotar el cupo o pausar por cuota
    Y genera un registro de bitácora con el desglose exacto
```
