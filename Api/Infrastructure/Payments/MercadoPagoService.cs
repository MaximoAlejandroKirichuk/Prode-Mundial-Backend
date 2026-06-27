using Api.Application.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Payments;

/// <summary>
/// Placeholder Mercado Pago service ready for SDK replacement.
/// Throws <see cref="NotSupportedException"/> until a real access token is configured.
/// Provides the shape expected by the application layer so DI and tests work immediately.
/// </summary>
public sealed class MercadoPagoService(IOptions<MercadoPagoOptions> options) : IMercadoPagoService
{
    private readonly MercadoPagoOptions _options = options.Value;

    public Task<CreatePaymentPreferenceResult> CreatePreferenceAsync(
        string name,
        string email,
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new NotSupportedException(
                "Mercado Pago is not configured. Set MercadoPago:AccessToken in configuration to enable real payment integration.");
        }

        throw new NotImplementedException(
            "MercadoPagoService SDK integration is not yet implemented. Replace this placeholder with the MercadoPago SDK.");
    }
}
