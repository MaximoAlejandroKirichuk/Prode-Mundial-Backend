using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class RegistrationRepository(AppDbContext context) : IRegistrationRepository
{
    public async Task<Registration?> GetRecentPendingByEmailAsync(
        string email,
        TimeSpan within,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - within;

        return await context.Registrations
            .Where(r => r.Email == email)
            .Where(r => r.Status == RegistrationStatus.Pending)
            .Where(r => r.CreatedAt >= cutoff)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        await context.Registrations.AddAsync(registration, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
