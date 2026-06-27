using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface IRegistrationRepository
{
    Task<Registration?> GetRecentPendingByEmailAsync(string email, TimeSpan within, CancellationToken cancellationToken = default);
    Task AddAsync(Registration registration, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
