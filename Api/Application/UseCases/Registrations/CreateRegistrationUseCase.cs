using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Domain.Exceptions;

namespace Api.Application.UseCases.Registrations;

public sealed record CreateRegistrationResult(
    Guid RegistrationId,
    string PaymentUrl,
    bool IsExisting);

public class CreateRegistrationUseCase(
    IRegistrationRepository registrationRepository,
    ITournamentRepository tournamentRepository,
    IMercadoPagoService mercadoPagoService)
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);

    public virtual async Task<CreateRegistrationResult> ExecuteAsync(
        string name,
        string email,
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs at the application boundary
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (tournamentId == Guid.Empty)
            throw new ArgumentException("TournamentId is required.", nameof(tournamentId));

        // Validate tournament exists and is active
        var tournament = await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken);

        if (tournament is null)
            throw new TournamentNotFoundException(tournamentId);

        if (!tournament.IsActive())
            throw new TournamentNotActiveException(tournamentId);

        // Enforce scoped duplicate policy (email, tournament_id)
        var latest = await registrationRepository.GetLatestByEmailAndTournamentAsync(
            email, tournamentId, cancellationToken);

        if (latest is not null)
        {
            switch (latest.Status)
            {
                // Block: already paid for this tournament
                case RegistrationStatus.Paid:
                case RegistrationStatus.Notified:
                case RegistrationStatus.PaidPendingNotification:
                    throw new DuplicatePaidRegistrationException(email, tournamentId);

                // Reuse: recent pending checkout (< 5 min)
                case RegistrationStatus.Pending
                    when latest.CreatedAt >= DateTimeOffset.UtcNow - RecentWindow
                    && latest.PaymentUrl is not null:
                    return new CreateRegistrationResult(
                        latest.Id,
                        latest.PaymentUrl,
                        IsExisting: true);

                // Retry: rejected, expired, or stale pending → fall through to create new
            }
        }

        // Create registration entity (not yet persisted)
        var registration = new Registration(name, email, tournamentId);

        // Create payment preference via Mercado Pago
        var preference = await mercadoPagoService.CreatePreferenceAsync(
            name, email, tournament.PriceAmount, tournament.Currency, registration.Id.ToString(), cancellationToken);

        // Set payment data on the entity
        registration.SetPaymentPreference(preference.PreferenceId, preference.PaymentUrl);

        // Persist ONLY after successful payment preference creation
        await registrationRepository.AddAsync(registration, cancellationToken);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        return new CreateRegistrationResult(
            registration.Id,
            preference.PaymentUrl,
            IsExisting: false);
    }
}
