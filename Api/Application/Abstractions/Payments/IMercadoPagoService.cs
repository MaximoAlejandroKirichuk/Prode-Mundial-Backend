namespace Api.Application.Abstractions.Payments;

public sealed record CreatePaymentPreferenceResult(string PreferenceId, string PaymentUrl);

public sealed record MercadoPagoPayment(
    long PaymentId,
    string Status,
    decimal Amount,
    string Currency,
    string? ExternalReference,
    DateTimeOffset CreatedAt);

public interface IMercadoPagoService
{
    Task<CreatePaymentPreferenceResult> CreatePreferenceAsync(
        string name,
        string email,
        decimal amount,
        string currency,
        string externalReference,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPayment> GetPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken = default);
}
