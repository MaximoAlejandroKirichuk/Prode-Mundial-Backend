using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class RegistrationRepository(AppDbContext context) : IRegistrationRepository
{
    public async Task<Registration?> GetRecentPendingByEmailAndTournamentAsync(
        string email,
        Guid tournamentId,
        TimeSpan within,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - within;

        return await context.Registrations
            .Where(r => r.Email == email)
            .Where(r => r.TournamentId == tournamentId)
            .Where(r => r.Status == RegistrationStatus.Pending)
            .Where(r => r.CreatedAt >= cutoff)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Registration?> GetByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(externalReference, out var registrationId))
            return null;

        return await context.Registrations
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);
    }

    public async Task<Registration?> GetLatestByEmailAndTournamentAsync(
        string email,
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        return await context.Registrations
            .Where(r => r.Email == email)
            .Where(r => r.TournamentId == tournamentId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        await context.Registrations.AddAsync(registration, cancellationToken);
    }

    public Task UpdateStatusAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        // Registration is already tracked by EF Core; just mark it as modified.
        // The caller is responsible for calling SaveChangesAsync.
        context.Registrations.Update(registration);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
