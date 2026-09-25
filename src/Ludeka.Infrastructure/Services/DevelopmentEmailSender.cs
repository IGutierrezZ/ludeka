using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Emisor de correos simulado para desarrollo y pruebas.
/// Registra el cuerpo del mensaje y enlaces en el logger para inspección en tiempo real.
/// </summary>
public class DevelopmentEmailSender : IEmailSender
{
    private readonly ILogger<DevelopmentEmailSender> _logger;
    private readonly ConcurrentBag<SentEmailRecord> _sentEmails = new();

    public DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _sentEmails.Add(new SentEmailRecord(toEmail, subject, htmlBody));

        _logger.LogInformation(
            "=== [CORREO TRANSACCIONAL SIMULADO] ===\nDestinatario: {ToEmail}\nAsunto: {Subject}\nCuerpo:\n{HtmlBody}\n========================================",
            toEmail, subject, htmlBody);

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<SentEmailRecord> SentEmails => _sentEmails.ToArray();
}

public sealed record SentEmailRecord(string ToEmail, string Subject, string HtmlBody);
