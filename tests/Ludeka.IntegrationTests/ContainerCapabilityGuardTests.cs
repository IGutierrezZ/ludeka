using System;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Prueba la lógica de decisión de <see cref="ContainerCapabilityGuard"/> de forma aislada, sin
/// depender de que esta máquina tenga o no capacidad de contenedores (especificación
/// <c>postgres-integration-testing</c>, línea 29). Vive fuera de la colección <c>postgres-real</c>
/// a propósito: debe ejecutarse y pasar con o sin Docker disponible.
/// </summary>
public class ContainerCapabilityGuardTests
{
    [Fact]
    public void Evaluate_ConFalloDeArranqueYFueraDeCi_DebeDevolverExcepcionConGuiaLocalDeDocker()
    {
        // Arrange
        var startupFailure = new InvalidOperationException("El demonio Docker no responde");

        // Act
        var result = ContainerCapabilityGuard.Evaluate(startupFailure, runningOnCi: false);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Docker", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(startupFailure, result.InnerException);
    }

    [Fact]
    public void Evaluate_ConFalloDeArranqueYEnCi_DebeDevolverUnMensajeDistintoDeFueraDeCi()
    {
        // Arrange
        var startupFailure = new InvalidOperationException("El demonio Docker no responde");

        // Act
        var localMessage = ContainerCapabilityGuard.Evaluate(startupFailure, runningOnCi: false).Message;
        var ciMessage = ContainerCapabilityGuard.Evaluate(startupFailure, runningOnCi: true).Message;

        // Assert
        Assert.NotEqual(localMessage, ciMessage);
        Assert.Contains("infraestructura", ciMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_SinFalloDeArranqueCapturado_NuncaDebeDevolverNull()
    {
        // Act
        var result = ContainerCapabilityGuard.Evaluate(startupFailure: null, runningOnCi: false);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.InnerException);
    }
}
