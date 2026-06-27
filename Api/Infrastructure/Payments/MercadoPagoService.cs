using Api.Application.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Payments;

public sealed class MercadoPagoService : IMercadoPagoService
{
    private readonly IMercadoPagoApi _api;

    /// <summary>
    /// Production constructor — wires the real Mercado Pago SDK.
    /// </summary>
    public MercadoPagoService(IOptions<MercadoPagoOptions> options)
        : this(new MercadoPagoApi(options))
    {
    }

    /// <summary>
    /// Internal constructor for unit testing with a mocked <see cref="IMercadoPagoApi"/>.
    /// </summary>
    internal MercadoPagoService(IMercadoPagoApi api)
    {
        _api = api;
    }

    public async Task<CreatePaymentPreferenceResult> CreatePreferenceAsync(
        string name,
        string email,
        decimal amount,
        string currency,
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        var tournamentRegistrationTitle = $"Tournament registration — {name}";

        var preference = await _api.CreatePreferenceAsync(
            tournamentRegistrationTitle,
            unitPrice: amount,
            currency: currency,
            externalReference,
            cancellationToken);

        return new CreatePaymentPreferenceResult(
            PreferenceId: preference.PreferenceId,
            PaymentUrl: preference.InitPoint);
    }

    public async Task<MercadoPagoPayment> GetPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _api.GetPaymentAsync(paymentId, cancellationToken);

        return new MercadoPagoPayment(
            response.PaymentId,
            response.Status,
            response.Amount,
            response.Currency,
            response.ExternalReference,
            response.CreatedAt);
    }
}
