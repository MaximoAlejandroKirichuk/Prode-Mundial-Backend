using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class RegistrationAnomalyRepository(AppDbContext context) : IRegistrationAnomalyRepository
{
    public async Task AddAsync(RegistrationAnomaly anomaly, CancellationToken cancellationToken = default)
    {
        await context.RegistrationAnomalies.AddAsync(anomaly, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
