using System;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Se lanza cuando se intenta desvincular la última identidad externa de una cuenta que no
/// conserva ningún otro método de acceso (INC-49, diseño §D5). Deriva de
/// <see cref="InvalidOperationException"/> —el mismo idioma que ya usa el repositorio para las
/// denegaciones controladas— para que sea imposible de ignorar por accidente.
/// </summary>
public sealed class LastAccessMethodException : InvalidOperationException
{
    public LastAccessMethodException(string message) : base(message)
    {
    }
}

/// <summary>
/// Se lanza cuando un correo verificado por el proveedor coincide con una cuenta que YA tiene al
/// menos una identidad externa vinculada (rama 2b de <c>ResolveAsync</c>, INC-49). Sin consumidor
/// hasta el PR #5 (partición 2a/2b de la cascada de acceso): se define aquí porque su tipo forma
/// parte del contrato público de excepciones de este PR, el mismo patrón que <c>AccountConnectionDto</c>
/// (tarea 2.2), un tipo definido antes de su primer uso.
/// </summary>
public sealed class ExternalLoginCollisionException : InvalidOperationException
{
    public ExternalLoginCollisionException(string message) : base(message)
    {
    }
}
