using Api.Domain.Entities;

namespace Api.Tests.Domain;

public sealed class WebhookDeliveryTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithPaymentId()
    {
        var paymentId = "pay-abc-123";
        var topic = "payment";

        var delivery = new WebhookDelivery(paymentId, topic, "approved");

        Assert.NotEqual(Guid.Empty, delivery.Id);
        Assert.Equal(paymentId, delivery.PaymentId);
        Assert.Equal(topic, delivery.Topic);
        Assert.Equal("approved", delivery.Status);
        Assert.False(delivery.Processed);
        Assert.Equal(DateTimeOffset.UtcNow, delivery.ReceivedAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkProcessed_ShouldSetProcessedFlag()
    {
        var delivery = new WebhookDelivery("pay-xyz", "payment", "pending");

        delivery.MarkProcessed();

        Assert.True(delivery.Processed);
        Assert.NotNull(delivery.ProcessedAt);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPaymentIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new WebhookDelivery("", "payment", "approved"));

        Assert.Contains("paymentId", ex.Message);
    }
}
