using System;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Se lanza cuando el entorno de ejecución carece de la capacidad de contenedores necesaria para
/// ejercitar las pruebas de integración contra PostgreSQL real (INC-47, diseño §D6). La produce
/// <see cref="ContainerCapabilityGuard.Evaluate"/> a partir del fallo de arranque capturado por
/// <c>PostgresFixture</c>, para que la suite falle en rojo de forma explícita en vez de omitir en
/// silencio las pruebas de concurrencia (especificación <c>postgres-integration-testing</c>).
/// </summary>
public sealed class ContainerCapabilityException : InvalidOperationException
{
    public ContainerCapabilityException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
