namespace Api.Domain.Entities;

public sealed class WebhookDelivery
{
    public Guid Id { get; private set; }
    public string PaymentId { get; private set; }
    public string Topic { get; private set; }
    public string Status { get; private set; }
    public bool Processed { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

#pragma warning disable CS8618
    private WebhookDelivery() { } // EF Core
#pragma warning restore CS8618

    public WebhookDelivery(string paymentId, string topic, string status)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("PaymentId is required.", nameof(paymentId));

        Id = Guid.NewGuid();
        PaymentId = paymentId;
        Topic = topic;
        Status = status;
        Processed = false;
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessed()
    {
        Processed = true;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
