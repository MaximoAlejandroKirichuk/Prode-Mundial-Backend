using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface IRegistrationPaymentRepository
{
    Task AddAsync(RegistrationPayment payment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
