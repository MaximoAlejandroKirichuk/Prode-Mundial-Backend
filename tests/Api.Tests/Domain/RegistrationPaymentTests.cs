using Api.Domain.Entities;

namespace Api.Tests.Domain;

public sealed class RegistrationPaymentTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithPaymentDetails()
    {
        var registrationId = Guid.NewGuid();
        var paymentId = "pay-123456789";
        var amount = 5000m;
        var currency = "ARS";
        var status = "approved";

        var payment = new RegistrationPayment(registrationId, paymentId, amount, currency, status);

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(registrationId, payment.RegistrationId);
        Assert.Equal(paymentId, payment.PaymentId);
        Assert.Equal(amount, payment.Amount);
        Assert.Equal(currency, payment.Currency);
        Assert.Equal(status, payment.Status);
        Assert.Equal(DateTimeOffset.UtcNow, payment.CreatedAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPaymentIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new RegistrationPayment(Guid.NewGuid(), "", 5000m, "ARS", "approved"));

        Assert.Contains("paymentId", ex.Message);
    }
}
