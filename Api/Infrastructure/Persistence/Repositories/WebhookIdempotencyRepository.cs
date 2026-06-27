using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence.Repositories;

public sealed class WebhookIdempotencyRepository(AppDbContext context) : IWebhookIdempotencyRepository
{
    public async Task<bool> IsDuplicateAsync(string paymentId, string status, CancellationToken cancellationToken = default)
    {
        return await context.WebhookDeliveries
            .AnyAsync(d => d.PaymentId == paymentId && d.Status == status, cancellationToken);
    }

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await context.WebhookDeliveries.AddAsync(delivery, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
