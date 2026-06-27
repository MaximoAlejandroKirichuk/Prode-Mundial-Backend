namespace Api.Application.Abstractions.Payments;

public sealed record CreatePaymentPreferenceResult(string PreferenceId, string PaymentUrl);

public interface IMercadoPagoService
{
    Task<CreatePaymentPreferenceResult> CreatePreferenceAsync(
        string name,
        string email,
        string externalReference,
        CancellationToken cancellationToken = default);
}
