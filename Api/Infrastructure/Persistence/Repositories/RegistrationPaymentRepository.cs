using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class RegistrationPaymentRepository(AppDbContext context) : IRegistrationPaymentRepository
{
    public async Task AddAsync(RegistrationPayment payment, CancellationToken cancellationToken = default)
    {
        await context.RegistrationPayments.AddAsync(payment, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
