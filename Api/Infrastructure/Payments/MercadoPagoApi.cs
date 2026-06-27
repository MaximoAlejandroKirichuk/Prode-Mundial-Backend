using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Payment;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Payments;

internal sealed class MercadoPagoApi : IMercadoPagoApi
{
    // The Mercado Pago SDK uses a static AccessToken property for configuration.
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly MercadoPagoOptions _options;

    public MercadoPagoApi(IOptions<MercadoPagoOptions> options)
    {
        _options = options.Value;
        MercadoPagoConfig.AccessToken = _options.AccessToken;
    }

    public async Task<CreatePreferenceResponse> CreatePreferenceAsync(
        string title,
        decimal unitPrice,
        string currency,
        string externalReference,
        CancellationToken cancellationToken)
    {
        var request = new PreferenceRequest
        {
            Items =
            [
                new PreferenceItemRequest
                {
                    Title = title,
                    Quantity = 1,
                    CurrencyId = currency,
                    UnitPrice = unitPrice
                }
            ],
            ExternalReference = externalReference,
            NotificationUrl = null // configure via MercadoPago dashboard or env
        };

        var client = new PreferenceClient();
        Preference preference = await client.CreateAsync(request, cancellationToken: cancellationToken);

        return new CreatePreferenceResponse(preference.Id!, preference.InitPoint!);
    }

    public async Task<MercadoPagoPaymentResponse> GetPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken)
    {
        var client = new PaymentClient();
        Payment payment = await client.GetAsync(paymentId, cancellationToken: cancellationToken);

        return new MercadoPagoPaymentResponse(
            payment.Id!.Value,
            payment.Status ?? "unknown",
            payment.TransactionAmount ?? 0m,
            payment.CurrencyId ?? "ARS",
            payment.ExternalReference,
            payment.DateCreated ?? DateTimeOffset.UtcNow);
    }
}
