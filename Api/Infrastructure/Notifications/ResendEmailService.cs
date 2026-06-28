using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Notifications;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;

    public ResendEmailService(HttpClient httpClient, IOptions<ResendOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        ConfigureClient();
    }

    /// <summary>
    /// Internal constructor for testing with pre-configured HttpClient.
    /// </summary>
    internal ResendEmailService(HttpClient httpClient, ResendOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        ConfigureClient();
    }

    /// <summary>
    /// Parameterless constructor for DI with ResendOptions only.
    /// The HttpClient is created internally with the configured timeout.
    /// </summary>
    public ResendEmailService(IOptions<ResendOptions> options)
    {
        _options = options.Value;

        var handler = new HttpClientHandler();
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com"),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        ConfigureClient();
    }

    private void ConfigureClient()
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public virtual async Task<EmailSendResult> SendAccessEmailAsync(
        string toEmail,
        string toName,
        string tournamentName,
        DateTimeOffset approvedAt,
        string accessLink,
        CancellationToken cancellationToken = default)
    {
        var htmlBody = BuildAccessEmailHtml(toName, tournamentName, approvedAt, accessLink, toEmail);

        var payload = new
        {
            from = _options.FromEmail,
            to = toEmail,
            subject = $"¡Acceso confirmado — {tournamentName}!",
            html = htmlBody
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/emails", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new EmailSendResult(true);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new EmailSendResult(false,
                    $"Resend API returned {(int)response.StatusCode}: {errorBody}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new EmailSendResult(false, ex.Message);
        }
    }

    private static string BuildAccessEmailHtml(
        string toName,
        string tournamentName,
        DateTimeOffset approvedAt,
        string accessLink,
        string toEmail)
    {
        var approvalDate = approvedAt.ToLocalTime().ToString("f", new CultureInfo("es-AR"));

        return $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8" /></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
                <h2>¡Pago aprobado!</h2>
                <p>Hola {toName},</p>
                <p>Tu pago para el torneo <strong>{tournamentName}</strong> fue aprobado el {approvalDate}.</p>
                <p>Ya tenés acceso a la plataforma. Ingresá con el siguiente enlace:</p>
                <p style="text-align: center; margin: 24px 0;">
                    <a href="{accessLink}" style="background-color: #4CAF50; color: white; padding: 12px 24px;
                       text-decoration: none; border-radius: 4px; font-size: 16px;">
                        Ingresar al Prode
                    </a>
                </p>
                <p><strong>Importante:</strong> debés ingresar con el mismo correo electrónico con el que te registraste
                   (<em>{toEmail}</em>). Si usás otro correo, no vas a poder acceder.</p>
                <hr style="border: none; border-top: 1px solid #eee; margin: 24px 0;" />
                <p style="color: #666; font-size: 14px;">
                    ¿Problemas para ingresar? Verificá que estés usando <strong>{toEmail}</strong> como correo
                    de acceso. Si el problema persiste, contactá a nuestro equipo de soporte respondiendo este mensaje.
                </p>
                <p style="color: #666; font-size: 14px;">
                    ¿Necesitás ayuda? <a href="mailto:oficialprodelito@gmail.com">oficialprodelito@gmail.com</a>
                </p>
            </body>
            </html>
            """;
    }
}
