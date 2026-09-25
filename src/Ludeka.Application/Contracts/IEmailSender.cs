using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el envío de correos transaccionales (Magic Link, notificaciones de cuenta).
/// En desarrollo/pruebas utiliza un emisor simulado; en producción delega en proveedor HTTP (Cloud Run).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envía un correo transaccional de forma asíncrona.
    /// </summary>
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
