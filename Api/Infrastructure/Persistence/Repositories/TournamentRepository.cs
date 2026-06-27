using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class TournamentRepository(AppDbContext context) : ITournamentRepository
{
    public async Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Tournaments
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tournament?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await context.Tournaments
            .FirstOrDefaultAsync(t => t.Active, cancellationToken);
    }

    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Tournaments
            .AnyAsync(t => t.Id == id && t.Active, cancellationToken);
    }
}
