using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Servicio para solicitud, emisión y consumo atómico de enlaces de acceso por correo (Magic Link).
/// </summary>
public class MagicLinkService : IMagicLinkService
{
    private readonly IMagicLinkTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly MagicLinkOptions _options;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(
        IMagicLinkTokenRepository tokenRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IOptions<MagicLinkOptions> options,
        ILogger<MagicLinkService> logger)
    {
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _options = options?.Value ?? new MagicLinkOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<MagicLinkRequestResult> RequestMagicLinkAsync(
        string email,
        string? returnUrl = null,
        string? targetUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.IndexOf('@') == 0 || email.IndexOf('@') == email.Length - 1)
        {
            return new MagicLinkRequestResult(false, "Por favor, introduce una dirección de correo válida.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 1. Generar token criptográfico de 32 bytes (64 caracteres hexadecimales en minúscula)
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashToken(rawToken);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.TokenLifetimeMinutes);

        var magicLinkEntity = new MagicLinkToken(normalizedEmail, tokenHash, now, expiresAt, targetUserId);
        await _tokenRepository.AddAsync(magicLinkEntity, cancellationToken);

        // 2. Construir enlace de acceso
        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "";
        var link = $"{baseUrl}/login/magic-link?token={Uri.EscapeDataString(rawToken)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            link += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var subject = "Tu enlace de acceso a Ludeka";
        var htmlBody = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #2d3748;">
                <h2 style="color: #e65100; margin-top: 0;">Bienvenido a tu mesa en Ludeka</h2>
                <p style="font-size: 16px; line-height: 1.5;">Has solicitado iniciar sesión con tu correo electrónico. Pincha en el siguiente botón para entrar a Ludeka sin contraseñas:</p>
                <p style="margin: 28px 0; text-align: center;">
                    <a href="{link}" style="background-color: #e65100; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 12px; font-weight: bold; font-size: 16px; display: inline-block;">
                        Entrar a Ludeka
                    </a>
                </p>
                <p style="font-size: 13px; color: #718096; line-height: 1.4; border-top: 1px solid #e2e8f0; padding-top: 16px; margin-top: 24px;">
                    Este enlace caduca en {_options.TokenLifetimeMinutes} minutos y solo se puede utilizar una vez.<br/>
                    Si no has solicitado este acceso, puedes ignorar este mensaje con total seguridad.
                </p>
            </div>
            """;

        await _emailSender.SendEmailAsync(normalizedEmail, subject, htmlBody, cancellationToken);

        return new MagicLinkRequestResult(
            true,
            "Te hemos enviado un enlace de acceso a tu correo. Revisa tu bandeja de entrada.",
            DevTokenLink: link);
    }

    /// <inheritdoc />
    public async Task<MagicLinkVerifyResult> VerifyAndConsumeAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new MagicLinkVerifyResult(false, ErrorMessage: "El enlace de acceso es inválido.");
        }

        var tokenHash = HashToken(rawToken.Trim());
        var now = DateTimeOffset.UtcNow;

        var token = await _tokenRepository.GetValidByTokenHashAsync(tokenHash, now, cancellationToken);
        if (token is null)
        {
            return new MagicLinkVerifyResult(false, ErrorMessage: "El enlace de acceso es inválido o ha caducado. Por favor, solicita uno nuevo.");
        }

        // Consumo atómico del token
        token.Consume(now);
        await _tokenRepository.UpdateAsync(token, cancellationToken);

        // Resolución de la cuenta vinculada o coincidente por correo verificado
        AppUser? user = null;
        if (!string.IsNullOrWhiteSpace(token.TargetUserId))
        {
            user = await _userRepository.GetByIdAsync(token.TargetUserId, cancellationToken);
        }

        if (user is null)
        {
            user = await _userRepository.GetByEmailAsync(token.Email, cancellationToken);
        }

        if (user is null)
        {
            // Alta comunitaria nueva garantizada con correo verificado (nunca FoundingTeam por defecto)
            var userId = "user_" + Guid.NewGuid().ToString("N")[..12];
            var userName = token.Email.Split('@')[0];
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "Jugador";
            }

            user = new AppUser(
                userId,
                userName,
                token.Email,
                UserRole.CommunityUser,
                ModeratorPermission.None,
                UserStatus.Active,
                now);

            await _userRepository.AddAsync(user, cancellationToken);
            _logger.LogInformation("Creada nueva cuenta de usuario '{UserId}' ({Email}) mediante Magic Link.", user.Id, user.Email);
        }

        return new MagicLinkVerifyResult(true, User: user);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
