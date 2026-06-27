namespace Api.Infrastructure.Payments;

/// <summary>
/// Internal abstraction over the Mercado Pago SDK to enable unit testing of <see cref="MercadoPagoService"/>.
/// </summary>
internal interface IMercadoPagoApi
{
    Task<CreatePreferenceResponse> CreatePreferenceAsync(
        string title,
        decimal unitPrice,
        string currency,
        string externalReference,
        CancellationToken cancellationToken);

    Task<MercadoPagoPaymentResponse> GetPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken);
}

internal sealed record CreatePreferenceResponse(string PreferenceId, string InitPoint);

internal sealed record MercadoPagoPaymentResponse(
    long PaymentId,
    string Status,
    decimal Amount,
    string Currency,
    string? ExternalReference,
    DateTimeOffset CreatedAt);
