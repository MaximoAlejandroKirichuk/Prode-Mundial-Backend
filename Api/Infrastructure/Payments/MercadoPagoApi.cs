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
    private readonly IPreferenceClient _preferenceClient;

    /// <summary>
    /// Production constructor — wires the real Mercado Pago SDK.
    /// </summary>
    public MercadoPagoApi(IOptions<MercadoPagoOptions> options)
        : this(options, new SdkPreferenceClient())
    {
    }

    /// <summary>
    /// Internal constructor for unit testing with a mocked <see cref="IPreferenceClient"/>.
    /// </summary>
    internal MercadoPagoApi(IOptions<MercadoPagoOptions> options, IPreferenceClient preferenceClient)
    {
        _options = options.Value;
        _preferenceClient = preferenceClient;
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
            NotificationUrl = null, // configure via MercadoPago dashboard or env
            BackUrls = BuildBackUrls(),
            AutoReturn = _options.AutoReturn
        };

        Preference preference = await _preferenceClient.CreateAsync(request, cancellationToken: cancellationToken);

        return new CreatePreferenceResponse(preference.Id!, preference.InitPoint!);
    }

    private PreferenceBackUrlsRequest? BuildBackUrls()
    {
        var backUrls = _options.BackUrls;
        if (backUrls is null)
            return null;

        // Return null if none of the URLs are configured — MP SDK treats null as "no back URLs"
        if (backUrls.Success is null && backUrls.Failure is null && backUrls.Pending is null)
            return null;

        return new PreferenceBackUrlsRequest
        {
            Success = backUrls.Success,
            Failure = backUrls.Failure,
            Pending = backUrls.Pending
        };
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
