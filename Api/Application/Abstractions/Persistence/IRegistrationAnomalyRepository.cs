using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface IRegistrationAnomalyRepository
{
    Task AddAsync(RegistrationAnomaly anomaly, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
