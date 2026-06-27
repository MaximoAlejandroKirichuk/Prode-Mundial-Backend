using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class Registration
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public RegistrationStatus Status { get; private set; }
    public string? PaymentPreferenceId { get; private set; }
    public string? PaymentUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618
    private Registration() { } // EF Core
#pragma warning restore CS8618

    public Registration(string name, string email)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        Status = RegistrationStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPaymentPreference(string preferenceId, string paymentUrl)
    {
        PaymentPreferenceId = preferenceId;
        PaymentUrl = paymentUrl;
    }
}
