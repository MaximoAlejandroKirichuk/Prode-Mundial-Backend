using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;

namespace Api.Application.UseCases.Registrations;

public sealed record CreateRegistrationResult(
    Guid RegistrationId,
    string PaymentUrl,
    bool IsExisting);

public sealed class CreateRegistrationUseCase(
    IRegistrationRepository repository,
    IMercadoPagoService mercadoPagoService)
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);

    public async Task<CreateRegistrationResult> ExecuteAsync(
        string name,
        string email,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs at the application boundary
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        // Check for recent pending registration with same email (double-click guard)
        var existing = await repository.GetRecentPendingByEmailAsync(
            email, RecentWindow, cancellationToken);

        if (existing is not null && existing.PaymentUrl is not null)
        {
            return new CreateRegistrationResult(
                existing.Id,
                existing.PaymentUrl,
                IsExisting: true);
        }

        // Create registration entity (not yet persisted)
        var registration = new Registration(name, email);

        // Create payment preference via Mercado Pago
        var preference = await mercadoPagoService.CreatePreferenceAsync(
            name, email, registration.Id.ToString(), cancellationToken);

        // Set payment data on the entity
        registration.SetPaymentPreference(preference.PreferenceId, preference.PaymentUrl);

        // Persist ONLY after successful payment preference creation
        await repository.AddAsync(registration, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new CreateRegistrationResult(
            registration.Id,
            preference.PaymentUrl,
            IsExisting: false);
    }
}
