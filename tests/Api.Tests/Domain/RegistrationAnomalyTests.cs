using Api.Domain.Entities;

namespace Api.Tests.Domain;

public sealed class RegistrationAnomalyTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithEvidence()
    {
        var registrationId = Guid.NewGuid();
        var type = "OrphanPayment";
        var description = "Payment pay-999 has no matching registration";

        var anomaly = new RegistrationAnomaly(registrationId, type, description);

        Assert.NotEqual(Guid.Empty, anomaly.Id);
        Assert.Equal(registrationId, anomaly.RegistrationId);
        Assert.Equal(type, anomaly.Type);
        Assert.Equal(description, anomaly.Description);
        Assert.Equal(DateTimeOffset.UtcNow, anomaly.DetectedAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ShouldAllowNullRegistrationId_ForOrphanCases()
    {
        var anomaly = new RegistrationAnomaly(null, "OrphanPayment", "Unmatched payment");

        Assert.Null(anomaly.RegistrationId);
        Assert.Equal("OrphanPayment", anomaly.Type);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTypeIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new RegistrationAnomaly(Guid.NewGuid(), "", "description"));

        Assert.Contains("type", ex.Message);
    }
}
