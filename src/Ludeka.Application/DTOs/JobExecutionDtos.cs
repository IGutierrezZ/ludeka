using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Resultado de <see cref="Ludeka.Application.Contracts.IJobExecutionLeaseRepository.TryAcquireAsync"/>
/// (INC-47, R5, diseño §7.3/§7.4). Nombre de fichero y forma exacta decididos por
/// <c>sdd-apply</c> (tasks.md 9.10): el diseño solo fija el nombre del tipo <c>LeaseAcquisition</c>,
/// no su ubicación ni sus miembros; se agrupa aquí siguiendo el mismo patrón de agrupación por
/// funcionalidad que <c>OutboxDtos.cs</c> ya usa en el proyecto.
/// </summary>
public enum LeaseAcquisitionOutcome
{
    /// <summary>La concesión es mía: o bien el <c>INSERT</c> tuvo éxito, o bien tomé el control
    /// por intercambio condicional de una fila huérfana o retomable.</summary>
    Acquired,

    /// <summary>La fila existente ya está en estado <c>Completed</c>: nada que hacer.</summary>
    AlreadyCompleted,

    /// <summary>Otra ejecución tiene la ventana: viva (latido reciente), o se adelantó en la
    /// toma de control de una concesión huérfana o retomable.</summary>
    HeldByOther
}

/// <summary>Desenlace de un intento de adquisición, con el identificador de la fila implicada
/// cuando se conoce (siempre que <see cref="Outcome"/> no sea <see cref="LeaseAcquisitionOutcome.HeldByOther"/>
/// tras una fila desaparecida entre el <c>INSERT</c> fallido y su lectura posterior).</summary>
public sealed record LeaseAcquisition(LeaseAcquisitionOutcome Outcome, Guid? LeaseId);
