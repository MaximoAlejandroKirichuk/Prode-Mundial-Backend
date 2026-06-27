using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class Registration
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public Guid TournamentId { get; private set; }
    public RegistrationStatus Status { get; private set; }
    public string? PaymentPreferenceId { get; private set; }
    public string? PaymentUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Payment lifecycle timestamps
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? NotifiedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }

    // Anomaly / manual review
    public string? AnomalyNote { get; private set; }

    // Navigation properties — configured via EF Core in AppDbContext
    public Tournament Tournament { get; private set; } = null!;
    public ICollection<RegistrationPayment> Payments { get; private set; } = [];
    public ICollection<RegistrationAnomaly> Anomalies { get; private set; } = [];

#pragma warning disable CS8618
    private Registration() { } // EF Core
#pragma warning restore CS8618

    public Registration(string name, string email, Guid tournamentId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        TournamentId = tournamentId;
        Status = RegistrationStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPaymentPreference(string preferenceId, string paymentUrl)
    {
        PaymentPreferenceId = preferenceId;
        PaymentUrl = paymentUrl;
    }

    // ---- State transitions ----

    public void MarkAsPaid(DateTimeOffset paidAt)
    {
        if (Status == RegistrationStatus.Paid
            || Status == RegistrationStatus.Notified
            || Status == RegistrationStatus.PaidPendingNotification)
        {
            throw new InvalidOperationException(
                $"Cannot mark as Paid: registration is already {Status}.");
        }

        Status = RegistrationStatus.Paid;
        PaidAt = paidAt;
    }

    public void MarkAsNotified(DateTimeOffset notifiedAt)
    {
        if (Status != RegistrationStatus.Paid
            && Status != RegistrationStatus.PaidPendingNotification)
        {
            throw new InvalidOperationException(
                $"Cannot mark as Notified: registration must be Paid or PaidPendingNotification, but is {Status}.");
        }

        Status = RegistrationStatus.Notified;
        NotifiedAt = notifiedAt;
    }

    public void MarkAsPaymentNotificationFailed()
    {
        if (Status != RegistrationStatus.Paid)
        {
            throw new InvalidOperationException(
                $"Cannot mark notification as failed: registration must be Paid, but is {Status}.");
        }

        Status = RegistrationStatus.PaidPendingNotification;
    }

    public void MarkAsRejected(DateTimeOffset rejectedAt, string? reason = null)
    {
        if (Status == RegistrationStatus.Paid
            || Status == RegistrationStatus.Notified
            || Status == RegistrationStatus.PaidPendingNotification)
        {
            throw new InvalidOperationException(
                $"Cannot mark as Rejected: registration is already in a paid terminal state ({Status}).");
        }

        Status = RegistrationStatus.Rejected;
        RejectedAt = rejectedAt;
        if (!string.IsNullOrWhiteSpace(reason))
            AnomalyNote = reason;
    }

    public void MarkAsExpired()
    {
        if (Status != RegistrationStatus.Pending
            && Status != RegistrationStatus.Rejected)
        {
            throw new InvalidOperationException(
                $"Cannot mark as Expired: registration must be Pending or Rejected, but is {Status}.");
        }

        Status = RegistrationStatus.Expired;
    }

    public void MarkAsManualReview(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required for manual review.", nameof(reason));

        Status = RegistrationStatus.ManualReview;
        AnomalyNote = reason;
    }
}
