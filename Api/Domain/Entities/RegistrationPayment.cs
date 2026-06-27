namespace Api.Domain.Entities;

public sealed class RegistrationPayment
{
    public Guid Id { get; private set; }
    public Guid RegistrationId { get; private set; }
    public string PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618
    private RegistrationPayment() { } // EF Core
#pragma warning restore CS8618

    public RegistrationPayment(
        Guid registrationId,
        string paymentId,
        decimal amount,
        string currency,
        string status)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("PaymentId is required.", nameof(paymentId));

        Id = Guid.NewGuid();
        RegistrationId = registrationId;
        PaymentId = paymentId;
        Amount = amount;
        Currency = currency;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
