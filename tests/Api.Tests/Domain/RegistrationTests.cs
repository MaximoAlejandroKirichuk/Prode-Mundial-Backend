using Api.Domain.Entities;
using Api.Domain.Enums;

namespace Api.Tests.Domain;

public sealed class RegistrationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithTournamentId()
    {
        var tournamentId = Guid.NewGuid();

        var registration = new Registration("Juan Perez", "juan@example.com", tournamentId);

        Assert.NotEqual(Guid.Empty, registration.Id);
        Assert.Equal("Juan Perez", registration.Name);
        Assert.Equal("juan@example.com", registration.Email);
        Assert.Equal(tournamentId, registration.TournamentId);
        Assert.Equal(RegistrationStatus.Pending, registration.Status);
        Assert.Null(registration.PaidAt);
        Assert.Null(registration.NotifiedAt);
    }

    [Fact]
    public void MarkAsPaid_ShouldTransitionToPaid_WhenPending()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());
        var paidAt = DateTimeOffset.UtcNow;

        registration.MarkAsPaid(paidAt);

        Assert.Equal(RegistrationStatus.Paid, registration.Status);
        Assert.Equal(paidAt, registration.PaidAt);
    }

    [Fact]
    public void MarkAsPaid_ShouldThrow_WhenAlreadyPaid()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());
        registration.MarkAsPaid(DateTimeOffset.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(
            () => registration.MarkAsPaid(DateTimeOffset.UtcNow));

        Assert.Contains("already Paid", ex.Message);
    }

    [Fact]
    public void MarkAsNotified_ShouldTransitionToNotified_WhenPaid()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());
        registration.MarkAsPaid(DateTimeOffset.UtcNow);
        var notifiedAt = DateTimeOffset.UtcNow;

        registration.MarkAsNotified(notifiedAt);

        Assert.Equal(RegistrationStatus.Notified, registration.Status);
        Assert.Equal(notifiedAt, registration.NotifiedAt);
    }

    [Fact]
    public void MarkAsNotified_ShouldThrow_WhenNotPaid()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());

        var ex = Assert.Throws<InvalidOperationException>(
            () => registration.MarkAsNotified(DateTimeOffset.UtcNow));

        Assert.Contains("must be Paid", ex.Message);
    }

    [Fact]
    public void MarkAsPaymentNotificationFailed_ShouldTransitionToPaidPendingNotification_WhenPaid()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());
        registration.MarkAsPaid(DateTimeOffset.UtcNow);

        registration.MarkAsPaymentNotificationFailed();

        Assert.Equal(RegistrationStatus.PaidPendingNotification, registration.Status);
        Assert.NotNull(registration.PaidAt);
    }

    [Fact]
    public void MarkAsRejected_ShouldTransitionToRejected_WhenPending()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());
        var rejectedAt = DateTimeOffset.UtcNow;

        registration.MarkAsRejected(rejectedAt);

        Assert.Equal(RegistrationStatus.Rejected, registration.Status);
        Assert.Equal(rejectedAt, registration.RejectedAt);
    }

    [Fact]
    public void MarkAsExpired_ShouldTransitionToExpired_WhenPending()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());

        registration.MarkAsExpired();

        Assert.Equal(RegistrationStatus.Expired, registration.Status);
    }

    [Fact]
    public void MarkAsManualReview_ShouldSetStatusAndNote()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());

        registration.MarkAsManualReview("Amount mismatch detected");

        Assert.Equal(RegistrationStatus.ManualReview, registration.Status);
        Assert.Equal("Amount mismatch detected", registration.AnomalyNote);
    }

    [Fact]
    public void SetPaymentPreference_ShouldSetPreferenceAndUrl()
    {
        var registration = new Registration("Juan", "juan@example.com", Guid.NewGuid());

        registration.SetPaymentPreference("pref-abc", "https://mp.com/pay/abc");

        Assert.Equal("pref-abc", registration.PaymentPreferenceId);
        Assert.Equal("https://mp.com/pay/abc", registration.PaymentUrl);
    }
}
