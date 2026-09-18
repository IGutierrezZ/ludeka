using System;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Decide el mensaje de diagnóstico accionable cuando el contenedor de PostgreSQL real no pudo
/// arrancar (INC-47, diseño §D6). Función pura, deliberadamente separada de <c>PostgresFixture</c>
/// y de la colección <c>postgres-real</c>, para poder verificar la lógica de decisión en cualquier
/// máquina, incluida una que sí tiene Docker (especificación <c>postgres-integration-testing</c>,
/// línea 29).
/// </summary>
public static class ContainerCapabilityGuard
{
    private const string NoDockerMessage =
        "No se pudo arrancar un contenedor PostgreSQL real (Testcontainers) para ejecutar las " +
        "pruebas de integración. Verifica que el demonio Docker esté instalado, en ejecución y " +
        "accesible desde esta máquina, o instala Docker Desktop / el motor de contenedores equivalente.";

    private const string CiInfrastructureMessage =
        "El entorno de CI garantiza un demonio Docker operativo en este mismo trabajo, pero el " +
        "contenedor de PostgreSQL real no pudo arrancar. Esto es un fallo de infraestructura del " +
        "runner de CI, no una ausencia esperada de capacidad de contenedores.";

    private const string NoCapturedCauseMessage =
        "No se pudo arrancar un contenedor PostgreSQL real para las pruebas de integración y no " +
        "se capturó ninguna causa concreta del fallo de arranque.";

    /// <summary>
    /// Devuelve SIEMPRE una excepción descriptiva y nunca <see langword="null"/>, con un mensaje
    /// distinto según si la ejecución ocurre en CI (fallo de infraestructura del runner) o en una
    /// máquina de desarrollo (falta de capacidad de contenedores local).
    /// </summary>
    public static ContainerCapabilityException Evaluate(Exception? startupFailure, bool runningOnCi)
    {
        if (startupFailure is null)
        {
            return new ContainerCapabilityException(NoCapturedCauseMessage);
        }

        var message = runningOnCi ? CiInfrastructureMessage : NoDockerMessage;
        return new ContainerCapabilityException(message, startupFailure);
    }
}
