namespace Ludeka.Core.Enums;

/// <summary>
/// Estado del mensaje lógico del outbox de notificaciones (INC-47, diseño §5.2). Distinto de
/// <see cref="NotificationStatus"/>, que describe el resultado de una entrega por un canal
/// concreto: este enum describe el mensaje completo, antes de que el despachador derive el
/// conjunto de canales y cree sus sub-entregas.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 0,
    Completed = 1,
    Dead = 2
}
