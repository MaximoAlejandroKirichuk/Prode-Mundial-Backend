using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface ITournamentRepository
{
    Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);
}
