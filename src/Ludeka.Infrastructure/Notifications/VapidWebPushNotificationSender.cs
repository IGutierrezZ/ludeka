using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Notifications;

/// <summary>
/// Despachador de avisos Web Push (W3C Push API + VAPID RFC 8292) para PWAs instaladas en móvil y navegadores de escritorio (INC-149).
/// </summary>
public class VapidWebPushNotificationSender : IWebPushNotificationSender
{
    private readonly HttpClient _httpClient;
    private readonly WebPushOptions _options;
    private readonly ILogger<VapidWebPushNotificationSender> _logger;

    public VapidWebPushNotificationSender(
        HttpClient httpClient,
        IOptions<WebPushOptions> options,
        ILogger<VapidWebPushNotificationSender> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new WebPushOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string GetVapidPublicKey() => _options.PublicKey;

    public async Task SendNotificationAsync(
        UserPushSubscription subscription,
        string title,
        string message,
        string targetUrl,
        string? imageUrl = null,
        CancellationToken ct = default)
    {
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.Endpoint))
            return;

        try
        {
            if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpointUri))
                return;

            var audience = $"{endpointUri.Scheme}://{endpointUri.Authority}";
            var jwt = CreateVapidJwt(audience, _options.Subject, _options.PublicKey, _options.PrivateKey);

            var payloadJson = JsonSerializer.Serialize(new
            {
                title,
                body = message,
                url = targetUrl,
                icon = "/favicon.svg",
                image = imageUrl,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri);
            request.Headers.TryAddWithoutValidation("TTL", "86400");
            request.Headers.TryAddWithoutValidation("Urgency", "normal");

            if (!string.IsNullOrWhiteSpace(jwt))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("vapid", $"t={jwt}, k={_options.PublicKey}");
            }

            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "El servidor Push devolvió {StatusCode} para el endpoint {Endpoint}.",
                    (int)response.StatusCode,
                    subscription.Endpoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo completar el envío Web Push al endpoint {Endpoint}.", subscription.Endpoint);
        }
    }

    public async Task SendNotificationsBatchAsync(
        IReadOnlyCollection<UserPushSubscription> subscriptions,
        string title,
        string message,
        string targetUrl,
        string? imageUrl = null,
        CancellationToken ct = default)
    {
        if (subscriptions == null || subscriptions.Count == 0)
            return;

        foreach (var sub in subscriptions)
        {
            if (ct.IsCancellationRequested)
                break;

            await SendNotificationAsync(sub, title, message, targetUrl, imageUrl, ct);
        }
    }

    internal static string CreateVapidJwt(string audience, string subject, string publicKeyBase64Url, string privateKeyBase64Url)
    {
        try
        {
            var headerJson = JsonSerializer.Serialize(new { typ = "JWT", alg = "ES256" });
            var exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds();
            var payloadJson = JsonSerializer.Serialize(new { aud = audience, exp, sub = subject });

            var unsignedToken = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson))}";

            var pubBytes = Base64UrlDecode(publicKeyBase64Url);
            var privBytes = Base64UrlDecode(privateKeyBase64Url);

            if (pubBytes.Length != 65 || pubBytes[0] != 0x04 || privBytes.Length != 32)
                return string.Empty;

            var x = new byte[32];
            var y = new byte[32];
            Buffer.BlockCopy(pubBytes, 1, x, 0, 32);
            Buffer.BlockCopy(pubBytes, 33, y, 0, 32);

            var ecParams = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = privBytes,
                Q = new ECPoint { X = x, Y = y }
            };

            using var ecdsa = ECDsa.Create(ecParams);
            var signature = ecdsa.SignData(
                Encoding.ASCII.GetBytes(unsignedToken),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            return $"{unsignedToken}.{Base64UrlEncode(signature)}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
