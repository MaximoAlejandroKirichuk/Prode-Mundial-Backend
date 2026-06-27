using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface IRegistrationRepository
{
    Task<Registration?> GetRecentPendingByEmailAndTournamentAsync(
        string email,
        Guid tournamentId,
        TimeSpan within,
        CancellationToken cancellationToken = default);

    Task<Registration?> GetByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken = default);

    Task<Registration?> GetLatestByEmailAndTournamentAsync(
        string email,
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Registration registration, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Registration registration,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
