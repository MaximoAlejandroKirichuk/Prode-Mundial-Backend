using Api.Domain.Entities;

namespace Api.Application.Abstractions.Persistence;

public interface IWebhookIdempotencyRepository
{
    Task<bool> IsDuplicateAsync(string paymentId, string status, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
